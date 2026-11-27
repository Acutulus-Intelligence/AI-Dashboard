import { formatStyledValue } from '../format';
import type { ResolvedStyle } from '../types';
import type { WaterfallRow } from '../waterfall';

function truncate(value: string, max = 12): string {
  return value.length <= max ? value : `${value.slice(0, max - 1)}…`;
}

export default function WaterfallSvg({
  rows,
  style,
  width,
  height,
  showGridLines,
  showChangeLabels,
  showTooltips,
}: {
  rows: WaterfallRow[];
  style: ResolvedStyle;
  width: number;
  height: number;
  showGridLines: boolean;
  showChangeLabels: boolean;
  showTooltips: boolean;
}) {
  const values = rows.flatMap((row) => [row.start, row.end, 0]);
  // Avoid Math.min/Math.max spread: a large dataset would exceed the argument
  // limit. Zero is included so the baseline is always in range.
  let min = 0;
  let max = 0;
  for (const value of values) {
    if (value < min) min = value;
    if (value > max) max = value;
  }
  const range = max - min || 1;

  const ticks = Array.from({ length: 5 }, (_, index) => min + (range * index) / 4);
  const longestTick = ticks.reduce(
    (longest, tick) => Math.max(longest, formatStyledValue(tick, style).length),
    0,
  );

  const margin = {
    top: 16,
    right: 12,
    bottom: 38,
    left: Math.min(84, Math.max(44, longestTick * 6.2 + 12)),
  };
  const plotWidth = Math.max(40, width - margin.left - margin.right);
  const plotHeight = Math.max(40, height - margin.top - margin.bottom);

  const y = (value: number) => margin.top + ((max - value) / range) * plotHeight;
  const slot = plotWidth / Math.max(rows.length, 1);
  const barWidth = Math.min(72, Math.max(6, slot * 0.62));
  const rotateLabels = rows.length > 6 || rows.some((row) => row.name.length > 12);
  const labelMax = Math.max(6, Math.floor(slot / 6.5));

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      className="h-full w-full"
      role="img"
      aria-label="Waterfall chart"
    >
      {showGridLines &&
        ticks.map((tick) => (
          <g key={tick}>
            <line
              x1={margin.left}
              x2={width - margin.right}
              y1={y(tick)}
              y2={y(tick)}
              className="stroke-border"
              strokeWidth={1}
              strokeDasharray={tick === 0 ? undefined : '3 4'}
            />
            <text
              x={margin.left - 6}
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
          x1={margin.left}
          x2={width - margin.right}
          y1={y(0)}
          y2={y(0)}
          className="stroke-border"
          strokeWidth={1}
        />
      )}

      {min < 0 && max > 0 && (
        <line
          x1={margin.left}
          x2={width - margin.right}
          y1={y(0)}
          y2={y(0)}
          className="stroke-muted-foreground/60"
          strokeWidth={1}
        />
      )}

      {rows.map((row, index) => {
        const center = margin.left + slot * index + slot / 2;
        const x = center - barWidth / 2;
        const top = Math.min(y(row.start), y(row.end));
        const barHeight = Math.max(1, Math.abs(y(row.end) - y(row.start)));
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

            <rect x={x} y={top} width={barWidth} height={barHeight} rx={3} fill={color}>
              {showTooltips && (
                <title>
                  {`${row.name}: ${formatStyledValue(row.delta, style)} → ${formatStyledValue(
                    row.total,
                    style,
                  )}`}
                </title>
              )}
            </rect>

            {showChangeLabels && barWidth >= 22 && (
              <text
                x={center}
                y={row.delta >= 0 ? top - 4 : top + barHeight + 11}
                textAnchor="middle"
                fontSize={10}
                className="fill-foreground"
              >
                {formatStyledValue(row.delta, style)}
              </text>
            )}

            <text
              x={center}
              y={height - margin.bottom + 14}
              textAnchor={rotateLabels ? 'end' : 'middle'}
              fontSize={10}
              className="fill-muted-foreground"
              transform={
                rotateLabels
                  ? `rotate(-28 ${center} ${height - margin.bottom + 14})`
                  : undefined
              }
            >
              {truncate(row.name, labelMax)}
            </text>
          </g>
        );
      })}
    </svg>
  );
}
