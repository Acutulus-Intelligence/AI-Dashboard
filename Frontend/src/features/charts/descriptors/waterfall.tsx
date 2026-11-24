import { BarChart3 } from 'lucide-react';
import { formatStyledValue } from '../format';
import { param, type ChartDescriptor, type ParamSpec, type ResolvedStyle } from '../types';
import { buildWaterfallRows, type WaterfallRow } from '../waterfall';
import ChartPlaceholder from './ChartPlaceholder';

const showTotals: ParamSpec = {
  kind: 'boolean',
  key: 'showTotals',
  label: 'Total bar',
  default: true,
};

const showLabels: ParamSpec = {
  kind: 'boolean',
  key: 'showLabels',
  label: 'Change labels',
  default: false,
};

const showGrid: ParamSpec = {
  kind: 'boolean',
  key: 'showGrid',
  label: 'Grid lines',
  default: true,
};

const showTooltip: ParamSpec = {
  kind: 'boolean',
  key: 'showTooltip',
  label: 'Tooltip',
  default: true,
};

const WIDTH = 640;
const HEIGHT = 320;
const MARGIN = { top: 16, right: 12, bottom: 48, left: 56 };
const PLOT_WIDTH = WIDTH - MARGIN.left - MARGIN.right;
const PLOT_HEIGHT = HEIGHT - MARGIN.top - MARGIN.bottom;

function truncate(value: string, max = 12): string {
  return value.length <= max ? value : `${value.slice(0, max - 1)}…`;
}

function WaterfallSvg({
  rows,
  style,
  showGridLines,
  showChangeLabels,
  showTooltips,
}: {
  rows: WaterfallRow[];
  style: ResolvedStyle;
  showGridLines: boolean;
  showChangeLabels: boolean;
  showTooltips: boolean;
}) {
  const values = rows.flatMap((row) => [row.start, row.end, 0]);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;

  const y = (value: number) => MARGIN.top + ((max - value) / range) * PLOT_HEIGHT;
  const slot = PLOT_WIDTH / Math.max(rows.length, 1);
  const barWidth = Math.min(72, slot * 0.62);
  const rotateLabels = rows.length > 6;

  const ticks = Array.from({ length: 5 }, (_, index) => min + (range * index) / 4);

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      className="h-full w-full"
      role="img"
      aria-label="Waterfall chart"
    >
      {showGridLines &&
        ticks.map((tick) => (
          <g key={tick}>
            <line
              x1={MARGIN.left}
              x2={WIDTH - MARGIN.right}
              y1={y(tick)}
              y2={y(tick)}
              className="stroke-border"
              strokeWidth={1}
              strokeDasharray={tick === 0 ? undefined : '3 4'}
            />
            <text
              x={MARGIN.left - 6}
              y={y(tick) + 3}
              textAnchor="end"
              fontSize={10}
              className="fill-muted-foreground"
            >
              {formatStyledValue(tick, style)}
            </text>
          </g>
        ))}

      {!showGridLines && (
        <line
          x1={MARGIN.left}
          x2={WIDTH - MARGIN.right}
          y1={y(0)}
          y2={y(0)}
          className="stroke-border"
          strokeWidth={1}
        />
      )}

      {min < 0 && max > 0 && (
        <line
          x1={MARGIN.left}
          x2={WIDTH - MARGIN.right}
          y1={y(0)}
          y2={y(0)}
          className="stroke-muted-foreground/60"
          strokeWidth={1}
        />
      )}

      {rows.map((row, index) => {
        const center = MARGIN.left + slot * index + slot / 2;
        const x = center - barWidth / 2;
        const top = Math.min(y(row.start), y(row.end));
        const height = Math.max(1, Math.abs(y(row.end) - y(row.start)));
        const color =
          row.kind === 'total'
            ? 'var(--muted-foreground)'
            : row.delta >= 0
              ? style.colors[0]
              : 'var(--destructive)';

        return (
          <g key={`${row.name}-${index}`}>
            {index > 0 && (
              <line
                x1={center - slot}
                x2={center - barWidth / 2}
                y1={y(row.start)}
                y2={y(row.start)}
                className="stroke-muted-foreground/40"
                strokeWidth={1}
                strokeDasharray="2 3"
              />
            )}

            <rect x={x} y={top} width={barWidth} height={height} rx={3} fill={color}>
              {showTooltips && (
                <title>
                  {`${row.name}: ${formatStyledValue(row.delta, style)} → ${formatStyledValue(
                    row.total,
                    style,
                  )}`}
                </title>
              )}
            </rect>

            {showChangeLabels && (
              <text
                x={center}
                y={row.delta >= 0 ? top - 4 : top + height + 11}
                textAnchor="middle"
                fontSize={10}
                className="fill-foreground"
              >
                {formatStyledValue(row.delta, style)}
              </text>
            )}

            <text
              x={center}
              y={HEIGHT - MARGIN.bottom + 14}
              textAnchor={rotateLabels ? 'end' : 'middle'}
              fontSize={10}
              className="fill-muted-foreground"
              transform={rotateLabels ? `rotate(-28 ${center} ${HEIGHT - MARGIN.bottom + 14})` : undefined}
            >
              {truncate(row.name)}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

export const waterfallChart: ChartDescriptor = {
  id: 'waterfall',
  label: 'Waterfall',
  description: 'Shows how signed changes build up to a running total.',
  icon: BarChart3,
  defaultSize: { w: 7, h: 4 },
  minSize: { w: 4, h: 3 },
  variants: [
    { id: 'default', label: 'Waterfall', description: 'Bars show each change and the running total.' },
  ],
  params: [showTotals, showLabels, showGrid, showTooltip],
  render({ data, style }) {
    const series = data.datasets[0];
    if (!series) return <ChartPlaceholder icon={BarChart3} label="Waterfall" />;

    const rows = buildWaterfallRows(
      data.labels,
      series.values,
      param(style, 'showTotals', true),
    );

    if (rows.length === 0) return <ChartPlaceholder icon={BarChart3} label="Waterfall" />;

    return (
      <WaterfallSvg
        rows={rows}
        style={style}
        showGridLines={param(style, 'showGrid', true)}
        showChangeLabels={param(style, 'showLabels', false)}
        showTooltips={param(style, 'showTooltip', true)}
      />
    );
  },
};
