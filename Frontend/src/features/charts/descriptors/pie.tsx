import { PieChart as PieIcon } from 'lucide-react';
import { Cell, Label, Pie, PieChart } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
} from '@/components/ui/chart';
import type { ChartConfig } from '@/components/ui/chart';
import { chartColorKey } from '../colorKey';
import { formatStyledValue } from '../format';
import {
  param,
  type ChartData,
  type ChartDescriptor,
  type ParamSpec,
  type ResolvedStyle,
} from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import { hasSeries, showLabels, showLegend, showTooltip } from './params';

const innerRadius: ParamSpec = {
  kind: 'number',
  key: 'innerRadius',
  label: 'Hole size',
  default: 0,
  min: 0,
  max: 80,
  step: 5,
  help: 'Percent of the radius left empty in the middle.',
};

const padAngle: ParamSpec = {
  kind: 'number',
  key: 'padAngle',
  label: 'Slice gap',
  default: 0,
  min: 0,
  max: 8,
  step: 1,
};

/** Pie colours one slice per label; CSS vars must use safe keys (no spaces). */
function sliceConfig(data: ChartData, style: ResolvedStyle): ChartConfig {
  const config: ChartConfig = {};
  data.labels.forEach((label, i) => {
    const key = chartColorKey(i, label);
    config[key] = { label, color: style.colors[i % style.colors.length] };
  });
  return config;
}

export const pieChart: ChartDescriptor = {
  id: 'pie',
  label: 'Pie',
  description: 'Shows each category as a share of the whole.',
  icon: PieIcon,
  defaultSize: { w: 4, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Pie', description: 'A solid pie.' },
    { id: 'donut', label: 'Donut', description: 'Hollow centre.' },
    { id: 'total', label: 'Donut with total', description: 'Donut showing the sum in the middle.' },
  ],
  params: [innerRadius, padAngle, showLabels, showTooltip, showLegend],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={PieIcon} label="Pie chart" />;

    const series = data.datasets[0];
    const slices = data.labels.map((label, i) => ({
      key: chartColorKey(i, label),
      name: label,
      value: series.values[i] ?? 0,
    }));
    const total = slices.reduce((sum, s) => sum + s.value, 0);

    const showTotal = style.variant === 'total';
    const hole =
      showTotal || style.variant === 'donut'
        ? Math.max(param(style, 'innerRadius', 0), 45)
        : param(style, 'innerRadius', 0);

    return (
      <ChartContainer config={sliceConfig(data, style)} className="h-full w-full aspect-auto">
        <PieChart>
          {param(style, 'showTooltip', true) && (
            <ChartTooltip content={<StyledChartTooltip style={style} nameKey="key" />} />
          )}

          <Pie
            data={slices}
            dataKey="value"
            nameKey="key"
            innerRadius={`${hole}%`}
            paddingAngle={param(style, 'padAngle', 0)}
            label={
              param(style, 'showLabels', false)
                ? ({ name, percent }) => {
                    const slice = slices.find((s) => s.key === name);
                    const title = slice?.name ?? String(name ?? '');
                    return `${title} ${((percent ?? 0) * 100).toFixed(0)}%`;
                  }
                : undefined
            }
            labelLine={param(style, 'showLabels', false)}
          >
            {slices.map((slice) => (
              <Cell key={slice.key} fill={`var(--color-${slice.key})`} />
            ))}

            {showTotal && (
              <Label
                content={({ viewBox }) => {
                  if (!viewBox || !('cx' in viewBox)) return null;
                  return (
                    <text x={viewBox.cx} y={viewBox.cy} textAnchor="middle" dominantBaseline="middle">
                      <tspan
                        x={viewBox.cx}
                        y={viewBox.cy}
                        className="fill-foreground text-2xl font-semibold"
                      >
                        {formatStyledValue(total, style)}
                      </tspan>
                      <tspan
                        x={viewBox.cx}
                        y={(viewBox.cy ?? 0) + 20}
                        className="fill-muted-foreground text-xs"
                      >
                        {series.label}
                      </tspan>
                    </text>
                  );
                }}
              />
            )}
          </Pie>

          {param(style, 'showLegend', false) && (
            <ChartLegend content={<ChartLegendContent nameKey="key" />} />
          )}
        </PieChart>
      </ChartContainer>
    );
  },
};
