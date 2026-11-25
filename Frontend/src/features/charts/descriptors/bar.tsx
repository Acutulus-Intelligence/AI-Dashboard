import { BarChart3 } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Cell, LabelList, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { canShowValueLabels, labelAxisWidth, resolveCartesianMargins, xAxisLayout, yAxisWidth } from '../axis';
import { tickFormatter } from '../format';
import { cssColorVar } from '../colorKey';
import { toChartConfig, toRows } from '../transform';
import { param, type ChartDescriptor } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  colorBy,
  cornerRadius,
  hasSeries,
  showGrid,
  showLabels,
  showLegend,
  showTooltip,
  showXAxis,
  showYAxis,
  topN,
} from './params';

export const barChart: ChartDescriptor = {
  id: 'bar',
  label: 'Bar',
  description: 'Compares a value across categories.',
  icon: BarChart3,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  colorAxisParam: 'colorBy',
  variants: [
    { id: 'default', label: 'Grouped', description: 'Series side by side.' },
    { id: 'stacked', label: 'Stacked', description: 'Series stacked into one bar.' },
    { id: 'horizontal', label: 'Horizontal', description: 'Bars run left to right.' },
  ],
  params: [
    cornerRadius,
    colorBy,
    showGrid,
    showXAxis,
    showYAxis,
    showTooltip,
    showLegend,
    showLabels,
    topN,
  ],
  render({ data, style, layout }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={BarChart3} label="Bar chart" />;

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const horizontal = style.variant === 'horizontal';
    const stacked = style.variant === 'stacked';
    const radius = param(style, 'cornerRadius', 4);
    const compact = layout?.compact ?? false;
    const categoryColors =
      !stacked &&
      data.datasets.length === 1 &&
      param<string>(style, 'colorBy', 'series') === 'category';
    const xAxis = xAxisLayout(data.labels, compact);
    const yWidth = horizontal
      ? labelAxisWidth(data.labels, compact)
      : yAxisWidth(data, style, compact);
    const showValues =
      param(style, 'showLabels', false) &&
      canShowValueLabels(data, compact, { horizontal });
    const margin = resolveCartesianMargins(data, style, {
      valueLabels: showValues,
      rightLabels: horizontal,
    });

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <BarChart
          accessibilityLayer
          data={rows}
          layout={horizontal ? 'vertical' : 'horizontal'}
          margin={margin}
        >
          {param(style, 'showGrid', true) && (
            <CartesianGrid vertical={horizontal} horizontal={!horizontal} />
          )}

          {horizontal ? (
            <>
              <XAxis
                type="number"
                hide={!param(style, 'showYAxis', true)}
                tickLine={false}
                axisLine={false}
                tickFormatter={tickFormatter(style)}
              />
              <YAxis
                type="category"
                dataKey="name"
                hide={!param(style, 'showXAxis', true)}
                tickLine={false}
                axisLine={false}
                width={yWidth}
                tickFormatter={xAxis.tickFormatter}
              />
            </>
          ) : (
            <>
              <XAxis
                dataKey="name"
                hide={!param(style, 'showXAxis', true)}
                tickLine={false}
                axisLine={false}
                tickMargin={8}
                height={xAxis.height}
                interval={xAxis.interval}
                angle={xAxis.angle}
                textAnchor={xAxis.textAnchor}
                tickFormatter={xAxis.tickFormatter}
              />
              <YAxis
                hide={!param(style, 'showYAxis', true)}
                tickLine={false}
                axisLine={false}
                width={yWidth}
                tickFormatter={tickFormatter(style)}
              />
            </>
          )}

          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip
              cursor={false}
              content={<StyledChartTooltip style={style} showTotal={stacked} />}
            />
          )}
          {param(style, 'showLegend', false) && !compact && !categoryColors && (
            <ChartLegend content={<ChartLegendContent />} />
          )}

          {data.datasets.map((ds) => (
            <Bar
              key={ds.label}
              dataKey={ds.label}
              fill={cssColorVar(ds.label)}
              stackId={stacked ? 'a' : undefined}
              radius={radius}
            >
              {categoryColors &&
                data.labels.map((label, index) => (
                  <Cell key={`${label}-${index}`} fill={style.colors[index % style.colors.length]} />
                ))}
              {showValues && (
                <LabelList
                  position={horizontal ? 'right' : 'top'}
                  className="fill-foreground"
                  fontSize={11}
                  formatter={(v) => tickFormatter(style)(v as number)}
                />
              )}
            </Bar>
          ))}
        </BarChart>
      </ChartContainer>
    );
  },
};
