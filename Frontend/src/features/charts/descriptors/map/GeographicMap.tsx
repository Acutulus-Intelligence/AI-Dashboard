import { useEffect, useMemo, useState } from 'react';
import type { Feature, FeatureCollection, Geometry } from 'geojson';
import { geoJSON, latLngBounds, type Layer, type PathOptions } from 'leaflet';
import { CircleMarker, GeoJSON, MapContainer, Rectangle, TileLayer, Tooltip, useMap } from 'react-leaflet';
import { formatStyledValue } from '../../format';
import { param, type ResolvedStyle } from '../../types';
import { binScreenGrid, type GridCell } from './grid';
import MapLegend from './MapLegend';
import { aggregateCountries, mapFootnote, primaryValue, type MapRow } from './places';
import { colorAt, extent, markerRadius, parseRgb } from './scale';
import { CENTERS, WORLD, WORLD_IDS } from './world';
import 'leaflet/dist/leaflet.css';
import './map.css';

/** Esri World Light Gray Canvas. No API key. Attribution is the DeLorme/NAVTEQ line. */
const ESRI_TILES =
  'https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer/tile/{z}/{y}/{x}';
const ESRI_ATTRIBUTION = 'Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ';

interface GeographicMapProps {
  variant: 'choropleth' | 'markers' | 'grid';
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
    const fit = () => {
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
    };

    fit();
    map.on('resize', fit);
    return () => {
      map.off('resize', fit);
    };
  }, [map, features, positions]);

  return null;
}

function IntensityGrid({
  points,
  style,
  datasetLabels,
  showTooltip,
  showLabels,
  fillOpacity,
  lowRgb,
  highRgb,
  border,
  min,
  max,
  onCells,
}: {
  points: { label: string; values: number[]; lat: number; lon: number }[];
  style: ResolvedStyle;
  datasetLabels: string[];
  showTooltip: boolean;
  showLabels: boolean;
  fillOpacity: number;
  lowRgb: [number, number, number];
  highRgb: [number, number, number];
  border: string;
  min: number;
  max: number;
  onCells: (cells: GridCell[]) => void;
}) {
  const map = useMap();
  const [cells, setCells] = useState<GridCell[]>([]);

  useEffect(() => {
    const rebuild = () => {
      const size = map.getSize();
      const next = binScreenGrid(
        points,
        size.x,
        size.y,
        (lat, lon) => {
          const projected = map.latLngToContainerPoint([lat, lon]);
          return { x: projected.x, y: projected.y };
        },
        (x, y) => {
          const latLng = map.containerPointToLatLng([x, y]);
          return { lat: latLng.lat, lon: latLng.lng };
        },
      );
      setCells(next);
      onCells(next);
    };

    rebuild();
    map.on('moveend', rebuild);
    map.on('zoomend', rebuild);
    map.on('resize', rebuild);
    return () => {
      map.off('moveend', rebuild);
      map.off('zoomend', rebuild);
      map.off('resize', rebuild);
    };
  }, [map, points, onCells]);

  return (
    <>
      {cells.map((cell) => {
        const value = primaryValue(cell.values);
        const title = cell.labels.join(', ');
        return (
          <Rectangle
            key={`${cell.south}:${cell.west}:${cell.labels.join('|')}`}
            bounds={[
              [cell.south, cell.west],
              [cell.north, cell.east],
            ]}
            pathOptions={{
              color: border,
              weight: 1,
              fillColor: colorAt(lowRgb, highRgb, value, min, max),
              fillOpacity,
            }}
          >
            {(showTooltip || showLabels) && (
              <Tooltip permanent={showLabels} direction="center">
                <span className="text-xs">
                  {title}
                  {showTooltip && (
                    <>
                      {datasetLabels.length <= 1 ? (
                        <>: {formatStyledValue(value, style)}</>
                      ) : (
                        datasetLabels.map((label, seriesIndex) => (
                          <span key={`${label}-${seriesIndex}`} className="block">
                            {label}: {formatStyledValue(cell.values[seriesIndex] ?? 0, style)}
                          </span>
                        ))
                      )}
                    </>
                  )}
                </span>
              </Tooltip>
            )}
          </Rectangle>
        );
      })}
    </>
  );
}

export default function GeographicMap({
  variant,
  rows,
  style,
  measure,
  datasetLabels,
  truncated,
}: GeographicMapProps) {
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
  const [gridCells, setGridCells] = useState<GridCell[]>([]);
  const gridExtent = extent(
    (gridCells.length > 0 ? gridCells : points.map((point) => ({ values: point.values }))).map((cell) =>
      primaryValue(cell.values),
    ),
  );

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
  const legendExtent =
    variant === 'choropleth' ? countryExtent : variant === 'grid' ? gridExtent : pointExtent;
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

  return (
    <div className="chart-map nodrag flex h-full min-h-0 w-full flex-col gap-2">
      <div className="relative min-h-0 flex-1">
        <MapContainer
          center={[20, 0]}
          zoom={1}
          minZoom={1}
          maxZoom={16}
          scrollWheelZoom={false}
          className="absolute inset-0 h-full w-full rounded-md"
        >
          <TileLayer attribution={ESRI_ATTRIBUTION} url={ESRI_TILES} maxZoom={16} />
          <KeepSize />
          <FitView
            features={variant === 'choropleth' ? matchedFeatures : EMPTY_FEATURES}
            positions={variant === 'choropleth' ? EMPTY_POSITIONS : markerPositions}
          />
          {variant === 'choropleth' ? (
            <GeoJSON
              key={choroplethKey}
              data={WORLD as FeatureCollection}
              style={styleFeature}
              onEachFeature={onEachFeature}
            />
          ) : variant === 'grid' ? (
            <IntensityGrid
              points={points}
              style={style}
              datasetLabels={datasetLabels}
              showTooltip={showTooltip}
              showLabels={showLabels}
              fillOpacity={fillOpacity}
              lowRgb={lowRgb}
              highRgb={highRgb}
              border={border}
              min={gridExtent.min}
              max={gridExtent.max}
              onCells={setGridCells}
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
