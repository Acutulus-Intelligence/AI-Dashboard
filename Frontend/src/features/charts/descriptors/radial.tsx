import { Gauge } from 'lucide-react';
import { LabelList, PolarAngleAxis, PolarGrid, RadialBar, RadialBarChart } from 'recharts';
import { ChartContainer, ChartTooltip } from '@/components/ui/chart';
import type { ChartConfig } from '@/components/ui/chart';
import { chartColorKey } from '../colorKey';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import { hasSeries, showGrid, showLabels, showTooltip } from './params';

const startAngle: ParamSpec = {
  kind: 'number',
  key: 'startAngle',
  label: 'Start angle',
  default: 90,
  min: -180,
  max: 360,
  step: 15,
};

const endAngle: ParamSpec = {
  kind: 'number',
  key: 'endAngle',
  label: 'End angle',
  default: -270,
  min: -360,
  max: 360,
  step: 15,
};

const barSize: ParamSpec = {
  kind: 'number',
  key: 'barSize',
  label: 'Ring thickness',
  default: 14,
  min: 4,
  max: 40,
  step: 2,
};

export const radialChart: ChartDescriptor = {
  id: 'radial',
  label: 'Radial',
  description: 'Draws each category as an arc, good for progress-style values.',
  icon: Gauge,
  defaultSize: { w: 4, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Rings', description: 'One ring per category.' },
    { id: 'labelled', label: 'With labels', description: 'Category names drawn on the rings.' },
    { id: 'stacked', label: 'Stacked', description: 'Categories share one ring.' },
  ],
  params: [barSize, startAngle, endAngle, showGrid, showLabels, showTooltip],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={Gauge} label="Radial chart" />;

    const series = data.datasets[0];
    const stacked = style.variant === 'stacked';
    const labelled = style.variant === 'labelled' || param(style, 'showLabels', false);

    const keys = data.labels.map((label, i) => chartColorKey(i, label));

    const config: ChartConfig = {};
    data.labels.forEach((label, i) => {
      config[keys[i]] = { label, color: style.colors[i % style.colors.length] };
    });

    const stackTotal = series.values.reduce((sum, v) => sum + v, 0);

    const rows = stacked
      ? [
          data.labels.reduce<Record<string, string | number>>(
            (row, _label, i) => ({ ...row, [keys[i]]: series.values[i] ?? 0 }),
            { name: series.label },
          ),
        ]
      : data.labels.map((label, i) => ({
          key: keys[i],
          name: label,
          value: series.values[i] ?? 0,
          fill: `var(--color-${keys[i]})`,
        }));

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <RadialBarChart
          data={rows}
          innerRadius={stacked ? '60%' : '25%'}
          outerRadius="95%"
          startAngle={param(style, 'startAngle', 90)}
          endAngle={param(style, 'endAngle', -270)}
        >
          {param(style, 'showTooltip', true) && (
            <ChartTooltip
              cursor={false}
              content={<StyledChartTooltip style={style} nameKey={stacked ? undefined : 'key'} />}
            />
          )}
          {param(style, 'showGrid', true) && (
            <PolarGrid gridType="circle" radialLines={false} stroke="none" />
          )}

          {stacked ? (
            <>
              <PolarAngleAxis type="number" domain={[0, stackTotal]} tick={false} />
              {keys.map((key) => (
                <RadialBar
                  key={key}
                  dataKey={key}
                  stackId="a"
                  fill={`var(--color-${key})`}
                  cornerRadius={4}
                />
              ))}
            </>
          ) : (
            <RadialBar dataKey="value" background barSize={param(style, 'barSize', 14)} cornerRadius={4}>
              {labelled && (
                <LabelList
                  position="insideStart"
                  dataKey="name"
                  className="fill-background capitalize mix-blend-luminosity"
                  fontSize={11}
                />
              )}
            </RadialBar>
          )}
        </RadialBarChart>
      </ChartContainer>
    );
  },
};
