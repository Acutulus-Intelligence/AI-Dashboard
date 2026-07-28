import { LineChart as LineIcon } from 'lucide-react';
import { CartesianGrid, LabelList, Line, LineChart, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
} from '@/components/ui/chart';
import { tickFormatter } from '../format';
import { cssColorVar } from '../colorKey';
import { toChartConfig, toRows } from '../transform';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  CARTESIAN_MARGIN,
  curve,
  hasSeries,
  showGrid,
  showLabels,
  showLegend,
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
  ],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={LineIcon} label="Line chart" />;

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const width = param(style, 'strokeWidth', 2);
    const dot = param(style, 'dotSize', 0);
    const type = style.variant === 'step' ? 'step' : param(style, 'curve', 'monotone');

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <LineChart accessibilityLayer data={rows} margin={CARTESIAN_MARGIN}>
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
            <Line
              key={ds.label}
              dataKey={ds.label}
              type={type as 'monotone' | 'linear' | 'step'}
              stroke={cssColorVar(ds.label)}
              strokeWidth={width}
              strokeDasharray={style.variant === 'dashed' ? '6 4' : undefined}
              dot={dot > 0 ? { r: dot } : false}
              activeDot={{ r: Math.max(dot, 4) }}
            >
              {param(style, 'showLabels', false) && (
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
        </LineChart>
      </ChartContainer>
    );
  },
};
