import { ArrowDownRight, ArrowUpRight, Sigma } from 'lucide-react';
import { Area, AreaChart } from 'recharts';
import { ChartContainer } from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { cssColorVar, toCssColorIdent } from '../colorKey';
import { formatStyledValue } from '../format';
import { toChartConfig, toRows } from '../transform';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import { hasSeries } from './params';

const sparklineHeight: ParamSpec = {
  kind: 'number',
  key: 'sparklineHeight',
  label: 'Sparkline height',
  default: 40,
  min: 24,
  max: 96,
  step: 4,
};

/** Index of the last non-null value, or -1 when the series is empty. */
function lastValueIndex(values: (number | null)[]): number {
  for (let i = values.length - 1; i >= 0; i--) {
    if (values[i] != null) return i;
  }
  return -1;
}

/** A KPI label is the row label when meaningful, otherwise the measure name. */
function kpiLabel(raw: string | undefined, seriesLabel: string): string {
  const trimmed = raw?.trim() ?? '';
  if (!trimmed || trimmed === seriesLabel) return seriesLabel;
  return /^-?\d+(\.\d+)?$/.test(trimmed) ? seriesLabel : trimmed;
}

export const kpiChart: ChartDescriptor = {
  id: 'kpi',
  label: 'KPI',
  description: 'A headline number for a single measure, optionally with a trend and sparkline.',
  icon: Sigma,
  defaultSize: { w: 3, h: 2 },
  minSize: { w: 2, h: 2 },
  variants: [
    { id: 'value', label: 'Value', description: 'A large number and its label.' },
    { id: 'sparkline', label: 'Sparkline', description: 'A large number with a small trend line.' },
    {
      id: 'trend',
      label: 'Trend',
      description: 'A large number, the change versus the previous row, and a sparkline.',
    },
  ],
  params: [sparklineHeight],
  render({ data, style, layout }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={Sigma} label="KPI" />;

    const series = data.datasets[0];
    const index = lastValueIndex(series.values);
    const value = index >= 0 ? series.values[index] : null;
    const label = kpiLabel(
      index >= 0 ? data.labels[index] : data.labels[data.labels.length - 1],
      series.label,
    );
    const previous = index > 0 ? series.values[index - 1] : null;
    const delta = value != null && previous != null ? value - previous : null;
    const percent = delta != null && previous ? (delta / Math.abs(previous)) * 100 : null;

    const showSparkline = style.variant !== 'value' && series.values.some((v) => v != null);
    const showDelta = style.variant === 'trend' && delta != null;
    const gradientId = `kpi-fill-${toCssColorIdent(series.label)}`;
    const compact = layout?.compact ?? false;
    const valueText = value == null ? '—' : formatStyledValue(value, style);
    const availableWidth = layout?.width && layout.width > 0 ? layout.width : 220;
    const fontSize = Math.max(
      16,
      Math.min(compact ? 24 : 30, Math.round(availableWidth / Math.max(valueText.length * 0.62, 5))),
    );

    return (
      <div className="flex h-full min-h-0 flex-col justify-center gap-2 px-1">
        <span className="text-muted-foreground truncate text-xs">{label}</span>
        <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1">
          <span
            className="min-w-0 font-semibold tracking-tight tabular-nums"
            style={{ fontSize }}
          >
            {valueText}
          </span>
          {showDelta && (
            <span
              className={`inline-flex items-center gap-1 text-xs font-medium ${
                delta >= 0
                  ? 'text-emerald-600 dark:text-emerald-400'
                  : 'text-rose-600 dark:text-rose-400'
              }`}
            >
              {delta >= 0 ? (
                <ArrowUpRight className="size-3.5" />
              ) : (
                <ArrowDownRight className="size-3.5" />
              )}
              {formatStyledValue(delta, style)}
              {percent != null && ` · ${percent >= 0 ? '+' : ''}${percent.toFixed(1)}%`}
            </span>
          )}
        </div>

        {showSparkline && (
          <div className="w-full" style={{ height: param(style, 'sparklineHeight', 40) }}>
            <ChartContainer
              config={toChartConfig(data, style)}
              className="h-full w-full aspect-auto"
            >
              <AreaChart data={toRows(data)} margin={{ top: 4, right: 0, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor={cssColorVar(series.label)} stopOpacity={0.35} />
                    <stop offset="95%" stopColor={cssColorVar(series.label)} stopOpacity={0} />
                  </linearGradient>
                </defs>
                <Area
                  dataKey={series.label}
                  type="monotone"
                  stroke={cssColorVar(series.label)}
                  strokeWidth={2}
                  fill={`url(#${gradientId})`}
                  fillOpacity={1}
                  dot={false}
                  isAnimationActive={false}
                />
                <ChartHoverTooltip
                  cursor={false}
                  content={<StyledChartTooltip style={style} />}
                />
              </AreaChart>
            </ChartContainer>
          </div>
        )}
      </div>
    );
  },
};
