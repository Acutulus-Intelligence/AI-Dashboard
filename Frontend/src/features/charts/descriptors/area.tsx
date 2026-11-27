import { AreaChart as AreaIcon } from 'lucide-react';
import { Area, AreaChart, Brush, CartesianGrid, ReferenceLine, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { timeLabelFormatter, xAxisConfig, yAxisWidth } from '../axis';
import { tickFormatter } from '../format';
import { cssColorVar, toCssColorIdent } from '../colorKey';
import { toChartConfig, toRows } from '../transform';
import { param, type ChartDescriptor } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  CARTESIAN_MARGIN,
  curve,
  fillOpacity,
  hasSeries,
  referenceValue,
  showBrush,
  showGrid,
  showLegend,
  showReference,
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
  params: [
    curve,
    strokeWidth,
    fillOpacity,
    showGrid,
    showXAxis,
    showYAxis,
    showTooltip,
    showLegend,
    showBrush,
    showReference,
    referenceValue,
  ],
  render({ data, style, layout, onCategoryClick }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={AreaIcon} label="Area chart" />;

    const handleCategoryClick = (entry: unknown) => {
      if (!onCategoryClick) return;
      const record = entry as { name?: unknown; payload?: { name?: unknown } } | null;
      const name = record?.payload?.name ?? record?.name;
      if (name != null) onCategoryClick(String(name));
    };

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const opacity = param(style, 'fillOpacity', 0.25);
    const gradient = style.variant === 'gradient';
    const stacked = style.variant === 'stacked';
    const compact = layout?.compact ?? false;
    const xAxis = xAxisConfig(data, compact);
    const yWidth = yAxisWidth(data, style, compact);
    const gradientId = (label: string, index: number) =>
      `fill-${toCssColorIdent(label)}-${index}-${toCssColorIdent(style.colors[index] ?? '')}`;

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <AreaChart accessibilityLayer data={rows} margin={CARTESIAN_MARGIN}>
          {gradient && (
            <defs>
              {data.datasets.map((ds, index) => (
                <linearGradient
                  key={ds.label}
                  id={gradientId(ds.label, index)}
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop offset="5%" stopColor={cssColorVar(ds.label)} stopOpacity={opacity} />
                  <stop offset="95%" stopColor={cssColorVar(ds.label)} stopOpacity={0} />
                </linearGradient>
              ))}
            </defs>
          )}

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
              content={<StyledChartTooltip style={style} showTotal={stacked} />}
              labelFormatter={timeLabelFormatter(data)}
            />
          )}
          {param(style, 'showLegend', false) && !compact && (
            <ChartLegend content={<ChartLegendContent />} />
          )}

          {data.datasets.map((ds, index) => (
            <Area
              key={ds.label}
              dataKey={ds.label}
              type={param(style, 'curve', 'monotone') as 'monotone' | 'linear' | 'step'}
              stroke={cssColorVar(ds.label)}
              strokeWidth={param(style, 'strokeWidth', 2)}
              fill={gradient ? `url(#${gradientId(ds.label, index)})` : cssColorVar(ds.label)}
              fillOpacity={gradient ? 1 : opacity}
              stackId={stacked ? 'a' : undefined}
              onClick={handleCategoryClick}
            />
          ))}

          {param(style, 'showBrush', false) && !compact && (
            <Brush dataKey={xAxis.dataKey} height={24} travellerWidth={8} />
          )}
        </AreaChart>
      </ChartContainer>
    );
  },
};
