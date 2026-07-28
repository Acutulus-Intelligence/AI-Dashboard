import { BarChart3 } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, LabelList, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
} from '@/components/ui/chart';
import { tickFormatter } from '../format';
import { cssColorVar } from '../colorKey';
import { toChartConfig, toRows } from '../transform';
import { param, type ChartDescriptor } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  CARTESIAN_MARGIN,
  cornerRadius,
  hasSeries,
  showGrid,
  showLabels,
  showLegend,
  showTooltip,
  showXAxis,
  showYAxis,
} from './params';

export const barChart: ChartDescriptor = {
  id: 'bar',
  label: 'Bar',
  description: 'Compares a value across categories.',
  icon: BarChart3,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Grouped', description: 'Series side by side.' },
    { id: 'stacked', label: 'Stacked', description: 'Series stacked into one bar.' },
    { id: 'horizontal', label: 'Horizontal', description: 'Bars run left to right.' },
  ],
  params: [
    cornerRadius,
    showGrid,
    showXAxis,
    showYAxis,
    showTooltip,
    showLegend,
    showLabels,
  ],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={BarChart3} label="Bar chart" />;

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const horizontal = style.variant === 'horizontal';
    const stacked = style.variant === 'stacked';
    const radius = param(style, 'cornerRadius', 4);

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <BarChart
          accessibilityLayer
          data={rows}
          layout={horizontal ? 'vertical' : 'horizontal'}
          margin={CARTESIAN_MARGIN}
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
                width={80}
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
              />
              <YAxis
                hide={!param(style, 'showYAxis', true)}
                tickLine={false}
                axisLine={false}
                width={48}
                tickFormatter={tickFormatter(style)}
              />
            </>
          )}

          {param(style, 'showTooltip', true) && (
            <ChartTooltip cursor={false} content={<StyledChartTooltip style={style} />} />
          )}
          {param(style, 'showLegend', false) && <ChartLegend content={<ChartLegendContent />} />}

          {data.datasets.map((ds) => (
            <Bar
              key={ds.label}
              dataKey={ds.label}
              fill={cssColorVar(ds.label)}
              stackId={stacked ? 'a' : undefined}
              radius={radius}
            >
              {param(style, 'showLabels', false) && (
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
