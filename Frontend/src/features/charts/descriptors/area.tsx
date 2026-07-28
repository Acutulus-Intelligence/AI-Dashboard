import { AreaChart as AreaIcon } from 'lucide-react';
import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from 'recharts';
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
  curve,
  fillOpacity,
  hasSeries,
  showGrid,
  showLegend,
  showTooltip,
  showXAxis,
  showYAxis,
  strokeWidth,
} from './params';

export const areaChart: ChartDescriptor = {
  id: 'area',
  label: 'Area',
  description: 'A line chart with the area below it filled.',
  icon: AreaIcon,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Overlapping', description: 'Series drawn on top of each other.' },
    { id: 'stacked', label: 'Stacked', description: 'Series accumulate.' },
    { id: 'gradient', label: 'Gradient', description: 'Fill fades towards the baseline.' },
  ],
  params: [curve, strokeWidth, fillOpacity, showGrid, showXAxis, showYAxis, showTooltip, showLegend],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={AreaIcon} label="Area chart" />;

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const opacity = param(style, 'fillOpacity', 0.25);
    const gradient = style.variant === 'gradient';
    const stacked = style.variant === 'stacked';

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <AreaChart accessibilityLayer data={rows} margin={CARTESIAN_MARGIN}>
          {gradient && (
            <defs>
              {data.datasets.map((ds) => (
                <linearGradient key={ds.label} id={`fill-${ds.label}`} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={cssColorVar(ds.label)} stopOpacity={opacity} />
                  <stop offset="95%" stopColor={cssColorVar(ds.label)} stopOpacity={0} />
                </linearGradient>
              ))}
            </defs>
          )}

          {param(style, 'showGrid', true) && <CartesianGrid vertical={false} />}
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

          {param(style, 'showTooltip', true) && (
            <ChartTooltip cursor={false} content={<StyledChartTooltip style={style} />} />
          )}
          {param(style, 'showLegend', false) && <ChartLegend content={<ChartLegendContent />} />}

          {data.datasets.map((ds) => (
            <Area
              key={ds.label}
              dataKey={ds.label}
              type={param(style, 'curve', 'monotone') as 'monotone' | 'linear' | 'step'}
              stroke={cssColorVar(ds.label)}
              strokeWidth={param(style, 'strokeWidth', 2)}
              fill={gradient ? `url(#fill-${ds.label})` : cssColorVar(ds.label)}
              fillOpacity={gradient ? 1 : opacity}
              stackId={stacked ? 'a' : undefined}
            />
          ))}
        </AreaChart>
      </ChartContainer>
    );
  },
};
