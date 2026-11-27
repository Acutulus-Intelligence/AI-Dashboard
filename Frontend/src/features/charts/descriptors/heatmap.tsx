import { Fragment } from 'react';
import { LayoutGrid } from 'lucide-react';
import { formatStyledValue } from '../format';
import { buildMatrix, cellKey } from '../matrix';
import {
  param,
  type ChartDescriptor,
  type ParamSpec,
} from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import HeatmapLegend from './heatmapLegend';

const showTooltip: ParamSpec = {
  kind: 'boolean',
  key: 'showTooltip',
  label: 'Tooltip',
  default: true,
};

const showLegend: ParamSpec = {
  kind: 'boolean',
  key: 'showLegend',
  label: 'Legend',
  default: true,
};

const showLabels: ParamSpec = {
  kind: 'boolean',
  key: 'showLabels',
  label: 'Value labels',
  default: false,
};

export const heatmapChart: ChartDescriptor = {
  id: 'heatmap',
  label: 'Heatmap',
  description: 'A matrix of one measure across two categorical columns.',
  icon: LayoutGrid,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 4, h: 3 },
  variants: [{ id: 'default', label: 'Heatmap', description: 'Cells coloured by the measure.' }],
  params: [showTooltip, showLegend, showLabels],
  render({ data, style, layout }) {
    const matrix = buildMatrix(data);
    if (!matrix) return <ChartPlaceholder icon={LayoutGrid} label="Heatmap" />;

    const { xKey, yKey, columns, rows, cells, maxAbs } = matrix;
    const labels = param(style, 'showLabels', false);
    const tooltips = param(style, 'showTooltip', true);
    const compact = layout?.compact ?? false;

    return (
      <div className="flex h-full min-h-0 flex-col gap-2 p-1">
        <div className="min-h-0 flex-1 overflow-auto">
          <div
            className="grid gap-1"
            style={{
              gridTemplateColumns: `minmax(4rem, 7rem) repeat(${Math.max(columns.length, 1)}, minmax(1.5rem, 1fr))`,
            }}
          >
            <div className="bg-background sticky top-0 left-0 z-20" />
            {columns.map((column) => (
              <div
                key={`column-${column}`}
                className="bg-background text-muted-foreground sticky top-0 z-10 truncate text-center text-[11px] font-medium"
                title={column}
              >
                {column}
              </div>
            ))}

            {rows.map((row) => (
              <Fragment key={`row-${row}`}>
                <div
                  className="bg-background text-muted-foreground sticky left-0 z-10 truncate text-[11px]"
                  title={row}
                >
                  {row}
                </div>
                {columns.map((column) => {
                  const value = cells.get(cellKey(row, column)) ?? null;
                  const opacity =
                    value == null || maxAbs === 0
                      ? 0
                      : 0.12 + 0.88 * (Math.abs(value) / maxAbs);
                  const title = tooltips
                    ? `${row} × ${column}: ${
                        value == null ? '–' : formatStyledValue(value, style)
                      }`
                    : undefined;

                  return (
                    <div
                      key={`${row}-${column}`}
                      className="bg-muted/40 relative flex min-h-6 items-center justify-center overflow-hidden rounded-sm"
                      title={title}
                    >
                      {value != null && (
                        <span
                          className="absolute inset-0 rounded-sm"
                          style={{
                            backgroundColor: value < 0 ? 'var(--destructive)' : style.colors[0],
                            opacity,
                          }}
                          aria-hidden
                        />
                      )}
                      {labels && value != null && (
                        <span className="text-foreground relative z-10 px-1 text-[10px] tabular-nums">
                          {formatStyledValue(value, style)}
                        </span>
                      )}
                    </div>
                  );
                })}
              </Fragment>
            ))}
          </div>
        </div>

        {param(style, 'showLegend', true) && !compact && (
          <HeatmapLegend
            label={`${yKey} × ${xKey}`}
            color={style.colors[0]}
            min={matrix.min}
            max={maxAbs}
            style={style}
          />
        )}
      </div>
    );
  },
};
