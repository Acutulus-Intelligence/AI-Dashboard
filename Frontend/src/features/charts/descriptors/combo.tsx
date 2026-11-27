import { BarChart3 } from 'lucide-react';
import { Bar, Brush, CartesianGrid, ComposedChart, LabelList, Line, ReferenceLine, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { canShowPointLabels, canShowValueLabels, datasetAxisWidth, datasetsAxisWidth, resolveCartesianMargins, timeLabelFormatter, xAxisConfig, yAxisWidth } from '../axis';
import { cssColorVar } from '../colorKey';
import { tickFormatter } from '../format';
import { toChartConfig, toRows } from '../transform';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  cornerRadius,
  hasSeries,
  referenceValue,
  showBrush,
  showGrid,
  showLabels,
  showReference,
  showTooltip,
  showXAxis,
  showYAxis,
  strokeWidth,
} from './params';

const showLegend: ParamSpec = {
  kind: 'boolean',
  key: 'showLegend',
  label: 'Legend',
  default: true,
};

const dotSize: ParamSpec = {
  kind: 'number',
  key: 'dotSize',
  label: 'Point size',
  default: 3,
  min: 0,
  max: 8,
  step: 1,
  help: 'Zero hides the points on the line.',
};

const secondaryAxis: ParamSpec = {
  kind: 'boolean',
  key: 'secondaryAxis',
  label: 'Secondary axis',
  default: true,
  help: 'Draw the line against its own right-hand axis when the scales differ.',
};

export const comboChart: ChartDescriptor = {
  id: 'combo',
  label: 'Combo',
  description: 'Bars for the primary measures with one series drawn as a line.',
  icon: BarChart3,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Bar + line', description: 'The last series is drawn as a line.' },
    {
      id: 'lineFirst',
      label: 'Line first',
      description: 'The first series is drawn as a line; the rest are bars.',
    },
  ],
  params: [
    cornerRadius,
    strokeWidth,
    dotSize,
    showGrid,
    showXAxis,
    showYAxis,
    secondaryAxis,
    showTooltip,
    showLegend,
    showLabels,
    showBrush,
    showReference,
    referenceValue,
  ],
  render({ data, style, layout }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={BarChart3} label="Combo chart" />;

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const lineFirst = style.variant === 'lineFirst';
    const lastIndex = data.datasets.length - 1;
    const lineIndex = lineFirst ? 0 : lastIndex;
    const lineDataset = data.datasets[lineIndex];
    const barDatasets = data.datasets.filter((_, index) => index !== lineIndex);
    const hasSecondaryAxis = param(style, 'secondaryAxis', true) && barDatasets.length > 0;
    const radius = param(style, 'cornerRadius', 4);
    const dot = param(style, 'dotSize', 3);
    const compact = layout?.compact ?? false;
    const xAxis = xAxisConfig(data, compact);
    const leftWidth =
      barDatasets.length > 0
        ? datasetsAxisWidth(barDatasets, style, compact)
        : yAxisWidth(data, style, compact);
    const rightWidth =
      hasSecondaryAxis && lineDataset ? datasetAxisWidth(lineDataset, style, compact) : 0;
    const showY = param(style, 'showYAxis', true);
    const showPointValues =
      param(style, 'showLabels', false) && canShowPointLabels(data, compact);
    const showBarValues =
      param(style, 'showLabels', false) && canShowValueLabels(data, compact);
    const margin = resolveCartesianMargins(data, style, {
      valueLabels: showPointValues || showBarValues,
    });

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <ComposedChart accessibilityLayer data={rows} margin={margin}>
          {param(style, 'showGrid', true) && <CartesianGrid vertical={false} />}
          <XAxis
            {...xAxis}
            hide={!param(style, 'showXAxis', true)}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
          />
          <YAxis
            yAxisId="left"
            hide={!showY}
            tickLine={false}
            axisLine={false}
            width={leftWidth}
            tickFormatter={tickFormatter(style)}
          />
          {hasSecondaryAxis && (
            <YAxis
              yAxisId="right"
              orientation="right"
              hide={!showY}
              tickLine={false}
              axisLine={false}
              width={rightWidth}
              tickFormatter={tickFormatter(style)}
            />
          )}

          {param(style, 'showReference', false) && (
            <ReferenceLine
              yAxisId="left"
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
          {param(style, 'showLegend', true) && !compact && (
            <ChartLegend content={<ChartLegendContent />} />
          )}

          {data.datasets.map((ds, i) => {
            const isLine = lineFirst ? i === 0 : i === lastIndex;
            if (isLine) {
              return (
                <Line
                  key={ds.label}
                  yAxisId={hasSecondaryAxis ? 'right' : 'left'}
                  dataKey={ds.label}
                  type="monotone"
                  stroke={cssColorVar(ds.label)}
                  strokeWidth={param(style, 'strokeWidth', 2)}
                  dot={dot > 0 ? { r: dot } : false}
                  activeDot={{ r: Math.max(dot, 4) }}
                >
                  {showPointValues && (
                    <LabelList
                      position="top"
                      offset={10}
                      className="fill-foreground"
                      fontSize={11}
                      formatter={(v) => tickFormatter(style)(v as number)}
                    />
                  )}
                </Line>
              );
            }

            return (
              <Bar
                key={ds.label}
                yAxisId="left"
                dataKey={ds.label}
                fill={cssColorVar(ds.label)}
                radius={radius}
              >
                {showBarValues && (
                  <LabelList
                    position="top"
                    className="fill-foreground"
                    fontSize={11}
                    formatter={(v) => tickFormatter(style)(v as number)}
                  />
                )}
              </Bar>
            );
          })}

          {param(style, 'showBrush', false) && !compact && (
            <Brush dataKey={xAxis.dataKey} height={24} travellerWidth={8} />
          )}
        </ComposedChart>
      </ChartContainer>
    );
  },
};
