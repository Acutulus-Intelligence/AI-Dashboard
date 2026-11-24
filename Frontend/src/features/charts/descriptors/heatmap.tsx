import { Fragment } from 'react';
import { LayoutGrid } from 'lucide-react';
import { formatStyledValue } from '../format';
import { toChartValue } from '../transform';
import {
  param,
  type ChartData,
  type ChartDescriptor,
  type ParamSpec,
  type ResolvedStyle,
} from '../types';
import ChartPlaceholder from './ChartPlaceholder';

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

interface HeatmapMatrix {
  xKey: string;
  yKey: string;
  columns: string[];
  rows: string[];
  cells: Map<string, number | null>;
  maxAbs: number;
}

function resolveKey(keys: string[], name?: string | null): string | undefined {
  if (!name) return undefined;
  const needle = name.trim().toLowerCase();
  return keys.find((key) => key.trim().toLowerCase() === needle);
}

function cellKey(row: string, column: string): string {
  return `${row}\u0000${column}`;
}

function buildMatrix(data: ChartData): HeatmapMatrix | null {
  const sample = data.queryResult?.find((row) => row && Object.keys(row).length > 0);
  if (!sample) return null;

  const keys = Object.keys(sample);
  const measure = data.datasets[0]?.label;
  const xKey = resolveKey(keys, data.xAxis) ?? keys.find((key) => key !== measure);
  const yKey =
    resolveKey(keys, data.groupBy) ?? keys.find((key) => key !== measure && key !== xKey);

  if (!xKey || !yKey || !measure) return null;

  const columns: string[] = [];
  const rows: string[] = [];
  const cells = new Map<string, number | null>();
  let maxAbs = 0;

  for (const row of data.queryResult ?? []) {
    const column = String(row[xKey] ?? '');
    const rowLabel = String(row[yKey] ?? '');
    if (!columns.includes(column)) columns.push(column);
    if (!rows.includes(rowLabel)) rows.push(rowLabel);

    const value = toChartValue(row[measure]);
    cells.set(cellKey(rowLabel, column), value);
    if (value != null) maxAbs = Math.max(maxAbs, Math.abs(value));
  }

  return { xKey, yKey, columns, rows, cells, maxAbs };
}

function HeatmapLegend({
  label,
  color,
  max,
  style,
}: {
  label: string;
  color: string;
  max: number;
  style: ResolvedStyle;
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 text-[11px] text-muted-foreground">
      <span className="truncate">{label}</span>
      <span className="flex items-center gap-1">
        <span>Low</span>
        <span className="bg-muted relative h-2 w-16 overflow-hidden rounded-sm">
          <span
            className="absolute inset-0"
            style={{ background: `linear-gradient(to right, transparent, ${color})` }}
          />
        </span>
        <span>{formatStyledValue(max, style)}</span>
      </span>
    </div>
  );
}

export const heatmapChart: ChartDescriptor = {
  id: 'heatmap',
  label: 'Heatmap',
  description: 'A matrix of one measure across two categorical columns.',
  icon: LayoutGrid,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 4, h: 3 },
  variants: [{ id: 'default', label: 'Heatmap', description: 'Cells coloured by the measure.' }],
  params: [showTooltip, showLegend, showLabels],
  render({ data, style }) {
    if (!data.queryResult?.length) {
      return <ChartPlaceholder icon={LayoutGrid} label="Heatmap" />;
    }

    const matrix = buildMatrix(data);
    if (!matrix) return <ChartPlaceholder icon={LayoutGrid} label="Heatmap" />;

    const { xKey, yKey, columns, rows, cells, maxAbs } = matrix;
    const labels = param(style, 'showLabels', false);
    const tooltips = param(style, 'showTooltip', true);

    return (
      <div className="flex h-full min-h-0 flex-col gap-2 p-1">
        <div className="min-h-0 flex-1 overflow-auto">
          <div
            className="grid gap-1"
            style={{
              gridTemplateColumns: `minmax(4rem, 7rem) repeat(${Math.max(columns.length, 1)}, minmax(1.5rem, 1fr))`,
            }}
          >
            <div />
            {columns.map((column) => (
              <div
                key={`column-${column}`}
                className="text-muted-foreground truncate text-center text-[11px] font-medium"
                title={column}
              >
                {column}
              </div>
            ))}

            {rows.map((row) => (
              <Fragment key={`row-${row}`}>
                <div
                  className="text-muted-foreground truncate text-[11px]"
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
                          style={{ backgroundColor: style.colors[0], opacity }}
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

        {param(style, 'showLegend', true) && (
          <HeatmapLegend
            label={`${yKey} × ${xKey}`}
            color={style.colors[0]}
            max={maxAbs}
            style={style}
          />
        )}
      </div>
    );
  },
};
