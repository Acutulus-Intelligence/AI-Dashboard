import { useEffect, useMemo, useState, useSyncExternalStore } from 'react';
import type { Feature, FeatureCollection, Geometry } from 'geojson';
import { geoJSON, latLngBounds, type Layer, type PathOptions } from 'leaflet';
import { CircleMarker, GeoJSON, MapContainer, TileLayer, Tooltip, useMap } from 'react-leaflet';
import { formatStyledValue } from '../../format';
import { param, type ResolvedStyle } from '../../types';
import MapLegend from './MapLegend';
import { aggregateCountries, mapFootnote, primaryValue, type MapRow } from './places';
import { colorAt, extent, markerRadius, parseRgb } from './scale';
import { CENTERS, WORLD, WORLD_IDS } from './world';
import 'leaflet/dist/leaflet.css';
import './map.css';

interface GeographicMapProps {
  variant: 'choropleth' | 'markers';
  rows: MapRow[];
  style: ResolvedStyle;
  measure: string;
  datasetLabels: string[];
  truncated: boolean;
}

const EMPTY_FEATURES: Feature<Geometry>[] = [];
const EMPTY_POSITIONS: [number, number][] = [];

function subscribeToTheme(onStoreChange: () => void) {
  const observer = new MutationObserver(onStoreChange);
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style'] });
  return () => observer.disconnect();
}

function useDarkMode(): boolean {
  return useSyncExternalStore(
    subscribeToTheme,
    () => document.documentElement.classList.contains('dark'),
    () => false,
  );
}

function readCssColor(input: string): string {
  const probe = document.createElement('span');
  probe.style.color = input;
  probe.style.position = 'absolute';
  probe.style.visibility = 'hidden';
  document.body.appendChild(probe);
  const computed = getComputedStyle(probe).color || input;
  probe.remove();

  // getComputedStyle often returns oklch(), which SVG map fills do not interpolate.
  const canvas = document.createElement('canvas');
  canvas.width = 1;
  canvas.height = 1;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  if (!context) return computed;
  context.fillStyle = '#000000';
  context.fillStyle = computed;
  context.fillRect(0, 0, 1, 1);
  const [red, green, blue, alpha] = context.getImageData(0, 0, 1, 1).data;
  if (alpha === 0) return computed;
  return `rgb(${red}, ${green}, ${blue})`;
}

function useCssColor(input: string): string {
  const [color, setColor] = useState(() => readCssColor(input));
  useEffect(() => {
    const update = () => {
      const next = readCssColor(input);
      setColor((current) => (current === next ? current : next));
    };
    update();
    return subscribeToTheme(update);
  }, [input]);
  return color;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function tooltipHtml(label: string, values: number[], datasetLabels: string[], style: ResolvedStyle): string {
  if (datasetLabels.length <= 1) {
    return `${escapeHtml(label)}: ${escapeHtml(formatStyledValue(values[0] ?? 0, style))}`;
  }
  const lines = [escapeHtml(label)];
  datasetLabels.forEach((name, index) => {
    lines.push(`${escapeHtml(name)}: ${escapeHtml(formatStyledValue(values[index] ?? 0, style))}`);
  });
  return lines.join('<br/>');
}

function KeepSize() {
  const map = useMap();
  useEffect(() => {
    const container = map.getContainer();
    const observer = new ResizeObserver(() => map.invalidateSize());
    observer.observe(container);
    const timer = window.setTimeout(() => map.invalidateSize(), 50);
    return () => {
      observer.disconnect();
      window.clearTimeout(timer);
    };
  }, [map]);
  return null;
}

function FitView({
  features,
  positions,
}: {
  features: Feature<Geometry>[];
  positions: [number, number][];
}) {
  const map = useMap();

  useEffect(() => {
    map.invalidateSize();
    if (features.length > 0) {
      const bounds = geoJSON({
        type: 'FeatureCollection',
        features,
      } as FeatureCollection).getBounds();
      if (bounds.isValid()) {
        map.fitBounds(bounds, { padding: [12, 12], maxZoom: 5, animate: false });
        return;
      }
    }
    if (positions.length === 1) {
      map.setView(positions[0], 4, { animate: false });
      return;
    }
    if (positions.length > 1) {
      const bounds = latLngBounds(positions);
      if (bounds.isValid()) {
        map.fitBounds(bounds, { padding: [16, 16], maxZoom: 8, animate: false });
        return;
      }
    }
    map.setView([20, 0], 1, { animate: false });
  }, [map, features, positions]);

  return null;
}

export default function GeographicMap({
  variant,
  rows,
  style,
  measure,
  datasetLabels,
  truncated,
}: GeographicMapProps) {
  const dark = useDarkMode();
  const high = useCssColor(style.colors[0] || 'var(--chart-1)');
  const low = useCssColor('var(--muted)');
  const border = useCssColor('var(--border)');
  const showLegend = param(style, 'showLegend', true);
  const showLabels = param(style, 'showLabels', false);
  const showTooltip = param(style, 'showTooltip', true);
  const fillOpacity = param(style, 'fillOpacity', 0.85);
  const countries = useMemo(() => aggregateCountries(rows), [rows]);
  const lowRgb = parseRgb(low);
  const highRgb = parseRgb(high);

  const countryValues = useMemo(
    () => [...countries.values()].map((country) => primaryValue(country.values)),
    [countries],
  );
  const countryExtent = extent(countryValues);

  const points = useMemo(() => {
    return rows.flatMap((row) => {
      if (row.lat != null && row.lon != null) return [{ ...row, lat: row.lat, lon: row.lon }];
      if (!row.countryId) return [];
      const center = CENTERS.get(row.countryId);
      if (!center) return [];
      return [{ ...row, lat: center.lat, lon: center.lon }];
    });
  }, [rows]);
  const pointExtent = extent(points.map((point) => primaryValue(point.values)));

  const matchedFeatures = useMemo(
    () => WORLD.features.filter((item) => item.id != null && countries.has(String(item.id))),
    [countries],
  );

  const hiddenLabels = useMemo(() => {
    if (variant === 'choropleth') {
      const unmatched = rows.filter((row) => !row.countryId).map((row) => row.label);
      const missingShape = [...countries.entries()]
        .filter(([id]) => !WORLD_IDS.has(id))
        .map(([, country]) => country.label);
      return [...unmatched, ...missingShape];
    }
    const plotted = new Set(points.map((point) => point.label));
    return rows.filter((row) => !plotted.has(row.label)).map((row) => row.label);
  }, [variant, rows, countries, points]);

  const footnote = mapFootnote(hiddenLabels, truncated);
  const legendExtent = variant === 'choropleth' ? countryExtent : pointExtent;
  const markerPositions = useMemo(
    () => points.map((point) => [point.lat, point.lon] as [number, number]),
    [points],
  );
  const choroplethKey = `${high}|${low}|${border}|${fillOpacity}|${showLabels}|${showTooltip}|${matchedFeatures.map((item) => String(item.id)).join(',')}`;

  function styleFeature(item?: Feature<Geometry, { name?: string }>): PathOptions {
    const id = item?.id == null ? '' : String(item.id);
    const country = countries.get(id);
    if (!country) {
      return { color: border, weight: 0.6, fillColor: low, fillOpacity: 0.35 };
    }
    return {
      color: border,
      weight: 0.8,
      fillColor: colorAt(lowRgb, highRgb, primaryValue(country.values), countryExtent.min, countryExtent.max),
      fillOpacity,
    };
  }

  function onEachFeature(item: Feature<Geometry, { name?: string }>, layer: Layer) {
    const id = item.id == null ? '' : String(item.id);
    const country = countries.get(id);
    const name = country?.label || item.properties?.name || 'Unknown';
    if (country && showLabels) {
      layer.bindTooltip(escapeHtml(name), {
        permanent: true,
        direction: 'center',
        className: 'chart-map-label',
      });
      return;
    }
    if (!showTooltip) return;
    const html = country
      ? tooltipHtml(name, country.values, datasetLabels, style)
      : escapeHtml(name);
    layer.bindTooltip(html, { sticky: true, className: 'chart-map-tip' });
  }

  const tileUrl = dark
    ? 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png'
    : 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png';

  return (
    <div className="chart-map nodrag flex h-full min-h-0 w-full flex-col gap-2">
      <div className="relative min-h-0 flex-1">
        <MapContainer
          center={[20, 0]}
          zoom={1}
          minZoom={1}
          maxZoom={20}
          scrollWheelZoom={false}
          className="absolute inset-0 h-full w-full rounded-md"
        >
          <TileLayer
            key={tileUrl}
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>'
            url={tileUrl}
            subdomains="abcd"
          />
          <KeepSize />
          <FitView
            features={variant === 'choropleth' ? matchedFeatures : EMPTY_FEATURES}
            positions={variant === 'markers' ? markerPositions : EMPTY_POSITIONS}
          />
          {variant === 'choropleth' ? (
            <GeoJSON
              key={choroplethKey}
              data={WORLD as FeatureCollection}
              style={styleFeature}
              onEachFeature={onEachFeature}
            />
          ) : (
            <>
              <GeoJSON
                key={`base-${border}-${low}`}
                data={WORLD as FeatureCollection}
                style={() => ({ color: border, weight: 0.45, fillColor: low, fillOpacity: 0.18 })}
              />
              {points.map((point, index) => (
                <CircleMarker
                  key={`${point.label}-${index}`}
                  center={[point.lat, point.lon]}
                  radius={markerRadius(primaryValue(point.values), pointExtent.min, pointExtent.max)}
                  pathOptions={{
                    color: border,
                    weight: 1,
                    fillColor: colorAt(lowRgb, highRgb, primaryValue(point.values), pointExtent.min, pointExtent.max),
                    fillOpacity,
                  }}
                >
                  {(showTooltip || showLabels) && (
                    <Tooltip permanent={showLabels} direction="top">
                      <span className="text-xs">
                        {point.label}
                        {showTooltip && (
                          <>
                            {datasetLabels.length <= 1 ? (
                              <>: {formatStyledValue(primaryValue(point.values), style)}</>
                            ) : (
                              datasetLabels.map((label, seriesIndex) => (
                                <span key={`${label}-${seriesIndex}`} className="block">
                                  {label}: {formatStyledValue(point.values[seriesIndex] ?? 0, style)}
                                </span>
                              ))
                            )}
                          </>
                        )}
                      </span>
                    </Tooltip>
                  )}
                </CircleMarker>
              ))}
            </>
          )}
        </MapContainer>
      </div>
      {showLegend && (
        <MapLegend
          low={low}
          high={high}
          minLabel={formatStyledValue(legendExtent.min, style)}
          maxLabel={formatStyledValue(legendExtent.max, style)}
          measure={measure}
        />
      )}
      {footnote && (
        <p className="text-muted-foreground shrink-0 truncate text-[11px]" title={footnote}>
          {footnote}
        </p>
      )}
    </div>
  );
}
