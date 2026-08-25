import { ScatterChart as ScatterIcon } from 'lucide-react';
import { CartesianGrid, Scatter, ScatterChart, XAxis, YAxis, ZAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { tickFormatter } from '../format';
import { cssColorVar } from '../colorKey';
import { toChartConfig } from '../transform';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  CARTESIAN_MARGIN,
  hasSeries,
  showGrid,
  showLegend,
  showTooltip,
  showXAxis,
  showYAxis,
} from './params';

const pointSize: ParamSpec = {
  kind: 'number',
  key: 'pointSize',
  label: 'Point size',
  default: 60,
  min: 20,
  max: 240,
  step: 10,
};

const pointOpacity: ParamSpec = {
  kind: 'number',
  key: 'pointOpacity',
  label: 'Point opacity',
  default: 0.8,
  min: 0.1,
  max: 1,
  step: 0.05,
};

const pointShape: ParamSpec = {
  kind: 'select',
  key: 'pointShape',
  label: 'Point shape',
  default: 'circle',
  options: [
    { value: 'circle', label: 'Circle' },
    { value: 'square', label: 'Square' },
    { value: 'triangle', label: 'Triangle' },
    { value: 'diamond', label: 'Diamond' },
    { value: 'cross', label: 'Cross' },
    { value: 'star', label: 'Star' },
  ],
};

export const scatterChart: ChartDescriptor = {
  id: 'scatter',
  label: 'Scatter',
  description: 'Plots individual points to reveal spread and outliers.',
  icon: ScatterIcon,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Points', description: 'One point per row.' },
    { id: 'bubble', label: 'Bubble', description: 'Point size scales with the value.' },
  ],
  params: [pointSize, pointOpacity, pointShape, showGrid, showXAxis, showYAxis, showTooltip, showLegend],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={ScatterIcon} label="Scatter chart" />;

    const config = toChartConfig(data, style);
    const bubble = style.variant === 'bubble';
    const size = param(style, 'pointSize', 60);

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <ScatterChart margin={CARTESIAN_MARGIN}>
          {param(style, 'showGrid', true) && <CartesianGrid />}
          <XAxis
            type="category"
            dataKey="x"
            name="Category"
            /* Each series carries its own rows, so without this the categories
               repeat once per series along the axis. */
            allowDuplicatedCategory={false}
            hide={!param(style, 'showXAxis', true)}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
          />
          <YAxis
            type="number"
            dataKey="y"
            name="Value"
            hide={!param(style, 'showYAxis', true)}
            tickLine={false}
            axisLine={false}
            width={48}
            tickFormatter={tickFormatter(style)}
          />
          {bubble && <ZAxis type="number" dataKey="y" range={[size / 2, size * 4]} />}

          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip cursor={{ strokeDasharray: '3 3' }} content={<StyledChartTooltip style={style} />} />
          )}
          {param(style, 'showLegend', false) && <ChartLegend content={<ChartLegendContent />} />}

          {data.datasets.map((ds) => (
            <Scatter
              key={ds.label}
              name={ds.label}
              dataKey={ds.label}
              data={data.labels.map((label, i) => ({ x: label, y: ds.values[i] ?? 0 }))}
              fill={cssColorVar(ds.label)}
              fillOpacity={param(style, 'pointOpacity', 0.8)}
              shape={param(style, 'pointShape', 'circle') as 'circle'}
              legendType={param(style, 'pointShape', 'circle') as 'circle'}
            />
          ))}
        </ScatterChart>
      </ChartContainer>
    );
  },
};
