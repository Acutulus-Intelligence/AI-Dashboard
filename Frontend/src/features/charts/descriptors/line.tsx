import { LineChart as LineIcon } from 'lucide-react';
import { Brush, CartesianGrid, LabelList, Line, LineChart, ReferenceLine, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { canShowPointLabels, resolveCartesianMargins, timeLabelFormatter, xAxisConfig, yAxisWidth } from '../axis';
import { tickFormatter } from '../format';
import { cssColorVar } from '../colorKey';
import { toChartConfig, toRows } from '../transform';
import { linearTrend, movingAverage } from '../transform';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  curve,
  hasSeries,
  referenceValue,
  showBrush,
  showGrid,
  showLabels,
  showLegend,
  showReference,
  showTooltip,
  showXAxis,
  showYAxis,
  strokeWidth,
} from './params';

const dotSize: ParamSpec = {
  kind: 'number',
  key: 'dotSize',
  label: 'Point size',
  default: 0,
  min: 0,
  max: 8,
  step: 1,
  help: 'Zero hides the points.',
};

const movingAverageSpec: ParamSpec = {
  kind: 'number',
  key: 'movingAverage',
  label: 'Moving average',
  default: 0,
  min: 0,
  max: 30,
  step: 1,
  help: '0 disables smoothing.',
};

const showTrendline: ParamSpec = {
  kind: 'boolean',
  key: 'showTrendline',
  label: 'Trendline',
  default: false,
  help: 'Adds a dashed least-squares line per series.',
};

export const lineChart: ChartDescriptor = {
  id: 'line',
  label: 'Line',
  description: 'Shows how values change over a sequence.',
  icon: LineIcon,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Line', description: 'A plain line per series.' },
    { id: 'dashed', label: 'Dashed', description: 'Dashed strokes.' },
    { id: 'step', label: 'Step', description: 'Values hold until the next point.' },
  ],
  params: [
    curve,
    strokeWidth,
    dotSize,
    showGrid,
    showXAxis,
    showYAxis,
    showTooltip,
    showLegend,
    showLabels,
    showBrush,
    showReference,
    referenceValue,
    movingAverageSpec,
    showTrendline,
  ],
  render({ data, style, layout, onCategoryClick }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={LineIcon} label="Line chart" />;

    const handleCategoryClick = (entry: unknown) => {
      if (!onCategoryClick) return;
      const record = entry as { name?: unknown; payload?: { name?: unknown } } | null;
      const name = record?.payload?.name ?? record?.name;
      if (name != null) onCategoryClick(String(name));
    };

    const rows = toRows(data);
    const averageWindow = param(style, 'movingAverage', 0);
    const displayData =
      averageWindow > 1
        ? {
            ...data,
            datasets: data.datasets.map((dataset) => ({
              ...dataset,
              values: movingAverage(dataset.values, averageWindow),
            })),
          }
        : data;
    const displayRows = rows.map((row, index) => {
      const next = { ...row };
      if (averageWindow > 1) {
        displayData.datasets.forEach((dataset) => {
          next[dataset.label] = dataset.values[index] ?? null;
        });
      }
      return next;
    });
    const trendLines = param(style, 'showTrendline', false)
      ? displayData.datasets.map((dataset) => ({
          key: `__trend_${dataset.label}`,
          colorKey: dataset.label,
          values: linearTrend(dataset.values),
        }))
      : [];
    for (const trend of trendLines) {
      displayRows.forEach((row, index) => {
        row[trend.key] = trend.values[index] ?? null;
      });
    }
    const width = param(style, 'strokeWidth', 2);
    const config = toChartConfig(displayData, style);
    const dot = param(style, 'dotSize', 0);
    const type = style.variant === 'step' ? 'step' : param(style, 'curve', 'monotone');
    const compact = layout?.compact ?? false;
    const xAxis = xAxisConfig(displayData, compact);
    const yWidth = yAxisWidth(displayData, style, compact);
    const showValues =
      param(style, 'showLabels', false) && canShowPointLabels(displayData, compact);
    const margin = resolveCartesianMargins(displayData, style, { valueLabels: showValues });

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <LineChart accessibilityLayer data={displayRows} margin={margin}>
          {param(style, 'showGrid', true) && <CartesianGrid vertical={false} />}
          <XAxis
            {...xAxis}
            hide={!param(style, 'showXAxis', true)}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
          />
          <YAxis
            hide={!param(style, 'showYAxis', true)}
            tickLine={false}
            axisLine={false}
            width={yWidth}
            tickFormatter={tickFormatter(style)}
          />

          {param(style, 'showReference', false) && (
            <ReferenceLine
              y={param(style, 'referenceValue', 0)}
              stroke="var(--muted-foreground)"
              strokeDasharray="4 4"
            />
          )}

          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip
              cursor={false}
              content={<StyledChartTooltip style={style} />}
              labelFormatter={timeLabelFormatter(data)}
            />
          )}
          {param(style, 'showLegend', false) && !compact && (
            <ChartLegend content={<ChartLegendContent />} />
          )}

          {displayData.datasets.map((ds) => (
            <Line
              key={ds.label}
              dataKey={ds.label}
              type={type as 'monotone' | 'linear' | 'step'}
              stroke={cssColorVar(ds.label)}
              strokeWidth={width}
              strokeDasharray={style.variant === 'dashed' ? '6 4' : undefined}
              dot={dot > 0 ? { r: dot } : false}
              activeDot={{ r: Math.max(dot, 4) }}
              onClick={handleCategoryClick}
            >
              {showValues && (
                <LabelList
                  position="top"
                  offset={10}
                  className="fill-foreground"
                  fontSize={11}
                  formatter={(v) => tickFormatter(style)(v as number)}
                />
              )}
            </Line>
          ))}

          {trendLines.map((trend) => (
            <Line
              key={trend.key}
              dataKey={trend.key}
              type="linear"
              stroke={cssColorVar(trend.colorKey)}
              strokeWidth={Math.max(1, width - 0.5)}
              strokeDasharray="6 4"
              dot={false}
              activeDot={false}
              legendType="none"
              isAnimationActive={false}
            />
          ))}

          {param(style, 'showBrush', false) && !compact && (
            <Brush dataKey={xAxis.dataKey} height={24} travellerWidth={8} />
          )}
        </LineChart>
      </ChartContainer>
    );
  },
};
