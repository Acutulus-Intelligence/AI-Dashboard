import { BarChart3 } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts';
import { ChartContainer } from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { buildHistogram } from '../histogram';
import { param, type ChartDescriptor, type ParamSpec, type ResolvedStyle } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  CARTESIAN_MARGIN,
  cornerRadius,
  showGrid,
  showTooltip,
  showXAxis,
  showYAxis,
} from './params';

const bins: ParamSpec = {
  kind: 'number',
  key: 'bins',
  label: 'Bins',
  default: 12,
  min: 5,
  max: 60,
  step: 1,
};

export const histogramChart: ChartDescriptor = {
  id: 'histogram',
  label: 'Histogram',
  description: 'Shows how numeric values are distributed across equal-width ranges.',
  icon: BarChart3,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [{ id: 'default', label: 'Histogram', description: 'Bars show how many values fall in each range.' }],
  params: [bins, cornerRadius, showGrid, showXAxis, showYAxis, showTooltip],
  render({ data, style }) {
    if (!data.datasets.length) return <ChartPlaceholder icon={BarChart3} label="Histogram" />;

    const series = data.datasets[0];
    const bucketRows = buildHistogram(series.values, param(style, 'bins', 12));
    if (bucketRows.length === 0) return <ChartPlaceholder icon={BarChart3} label="Histogram" />;

    const rows = bucketRows.map((bucket) => ({ name: bucket.label, count: bucket.count }));
    const config = { count: { label: 'Count', color: style.colors[0] } };
    // Counts are observations, not the measure: never inherit its prefix/suffix.
    const countStyle: ResolvedStyle = {
      ...style,
      valuePrefix: '',
      valueSuffix: '',
      decimals: 0,
      notation: 'auto',
    };

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <BarChart accessibilityLayer data={rows} margin={CARTESIAN_MARGIN}>
          {param(style, 'showGrid', true) && <CartesianGrid vertical={false} />}
          <XAxis
            dataKey="name"
            hide={!param(style, 'showXAxis', true)}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
            interval="preserveStartEnd"
          />
          <YAxis
            hide={!param(style, 'showYAxis', true)}
            tickLine={false}
            axisLine={false}
            width={40}
            allowDecimals={false}
          />

          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip cursor={false} content={<StyledChartTooltip style={countStyle} />} />
          )}

          <Bar
            dataKey="count"
            fill="var(--color-count)"
            radius={param(style, 'cornerRadius', 4)}
          />
        </BarChart>
      </ChartContainer>
    );
  },
};
