import { Radar as RadarIcon } from 'lucide-react';
import { PolarAngleAxis, PolarGrid, Radar, RadarChart } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { toChartConfig, toRows } from '../transform';
import { cssColorVar } from '../colorKey';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import { fillOpacity, hasSeries, showGrid, showLegend, showTooltip, strokeWidth } from './params';

const showDots: ParamSpec = {
  kind: 'boolean',
  key: 'showDots',
  label: 'Points',
  default: false,
};

export const radarChart: ChartDescriptor = {
  id: 'radar',
  label: 'Radar',
  description: 'Compares several measures around a circle.',
  icon: RadarIcon,
  defaultSize: { w: 4, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Filled', description: 'Filled area per series.' },
    { id: 'lines', label: 'Outline', description: 'Stroke only, no fill.' },
    { id: 'dots', label: 'Dots', description: 'Filled with a point at each axis.' },
  ],
  params: [strokeWidth, fillOpacity, showGrid, showDots, showTooltip, showLegend],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={RadarIcon} label="Radar chart" />;

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const outline = style.variant === 'lines';
    const dots = style.variant === 'dots' || param(style, 'showDots', false);

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <RadarChart data={rows}>
          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip cursor={false} content={<StyledChartTooltip style={style} />} />
          )}
          {param(style, 'showGrid', true) && <PolarGrid />}
          <PolarAngleAxis dataKey="name" />

          {data.datasets.map((ds) => (
            <Radar
              key={ds.label}
              dataKey={ds.label}
              stroke={cssColorVar(ds.label)}
              strokeWidth={param(style, 'strokeWidth', 2)}
              fill={cssColorVar(ds.label)}
              fillOpacity={outline ? 0 : param(style, 'fillOpacity', 0.25)}
              dot={dots ? { r: 3, fillOpacity: 1 } : false}
            />
          ))}

          {param(style, 'showLegend', false) && <ChartLegend content={<ChartLegendContent />} />}
        </RadarChart>
      </ChartContainer>
    );
  },
};
