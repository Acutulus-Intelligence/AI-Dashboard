import { formatStyledValue } from '../../format';
import { param, type ResolvedStyle } from '../../types';
import MapLegend from './MapLegend';
import { primaryValue, type MapRow } from './places';
import { extent } from './scale';

interface GridMapProps {
  rows: MapRow[];
  style: ResolvedStyle;
  measure: string;
  datasetLabels: string[];
  footnote: string | null;
}

export default function GridMap({ rows, style, measure, datasetLabels, footnote }: GridMapProps) {
  const showLegend = param(style, 'showLegend', true);
  const showLabels = param(style, 'showLabels', false);
  const showTooltip = param(style, 'showTooltip', true);
  const fillOpacity = param(style, 'fillOpacity', 0.85);
  const { min, max } = extent(rows.map((row) => primaryValue(row.values)));
  const high = style.colors[0] || 'var(--chart-1)';

  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-2">
      <div className="grid min-h-0 flex-1 auto-rows-[4.5rem] grid-cols-[repeat(auto-fill,minmax(5.5rem,1fr))] gap-1.5 overflow-auto">
        {rows.map((row, index) => {
          const value = primaryValue(row.values);
          const t = max === min ? 1 : (value - min) / (max - min);
          const mix = Math.round((22 + Math.min(1, Math.max(0, t)) * 78) * fillOpacity);
          const formatted = formatStyledValue(value, style);
          const detail =
            datasetLabels.length <= 1
              ? formatted
              : datasetLabels
                  .map((label, seriesIndex) => `${label}: ${formatStyledValue(row.values[seriesIndex] ?? 0, style)}`)
                  .join(', ');
          return (
            <div
              key={`${row.label}-${index}`}
              className="group relative flex min-h-14 flex-col items-center justify-center gap-0.5 overflow-hidden rounded-md p-1 text-center"
              title={showTooltip ? `${row.label}: ${detail}` : row.label}
            >
              <div
                className="absolute inset-0 rounded-md"
                style={{ backgroundColor: `color-mix(in oklab, ${high} ${mix}%, var(--muted))` }}
              />
              <span className="bg-background/75 relative max-w-full truncate rounded px-1 text-[11px] font-medium">
                {row.label}
              </span>
              {(showLabels || showTooltip) && (
                <span
                  className={
                    'bg-background/75 relative max-w-full truncate rounded px-1 text-[10px] tabular-nums' +
                    (showLabels ? '' : ' opacity-0 group-hover:opacity-100')
                  }
                >
                  {formatted}
                </span>
              )}
            </div>
          );
        })}
      </div>
      {showLegend && (
        <MapLegend
          low={`color-mix(in oklab, ${high} ${Math.round(22 * fillOpacity)}%, var(--muted))`}
          high={`color-mix(in oklab, ${high} ${Math.round(100 * fillOpacity)}%, var(--muted))`}
          minLabel={formatStyledValue(min, style)}
          maxLabel={formatStyledValue(max, style)}
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
