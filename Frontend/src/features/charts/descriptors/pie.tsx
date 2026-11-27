import { PieChart as PieIcon } from 'lucide-react';
import { Cell, Label, Pie, PieChart } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import type { ChartConfig } from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { chartColorKey } from '../colorKey';
import { estimateTextWidth, formatStyledValue } from '../format';
import {
  param,
  type ChartDescriptor,
  type ColorSlot,
  type ParamSpec,
  type ResolvedStyle,
} from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import { hasSeries, showLabels, showLegend, showTooltip, topN } from './params';

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
function sliceConfig(slots: ColorSlot[], style: ResolvedStyle): ChartConfig {
  const config: ChartConfig = {};
  slots.forEach((slot, index) => {
    const key = chartColorKey(index, slot.key || slot.label);
    config[key] = { label: slot.label, color: style.colors[index % style.colors.length] };
  });
  return config;
}

function truncate(value: string, max: number): string {
  return value.length <= max ? value : `${value.slice(0, max - 1)}…`;
}

export const pieChart: ChartDescriptor = {
  id: 'pie',
  label: 'Pie',
  description: 'Shows each category as a share of the whole.',
  icon: PieIcon,
  defaultSize: { w: 4, h: 4 },
  minSize: { w: 3, h: 3 },
  colorAxis: 'category',
  colorSlotFilter: (data, index) => {
    const value = data.datasets[0]?.values[index];
    return value != null && value >= 0;
  },
  variants: [
    { id: 'default', label: 'Pie', description: 'A solid pie.' },
    { id: 'donut', label: 'Donut', description: 'Hollow centre.' },
    { id: 'total', label: 'Donut with total', description: 'Donut showing the sum in the middle.' },
  ],
  params: [innerRadius, padAngle, showLabels, showTooltip, showLegend, topN],
  render({ data, style, layout, colorSlots, onCategoryClick }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={PieIcon} label="Pie chart" />;

    const handleSliceClick = (entry: unknown) => {
      if (!onCategoryClick) return;
      const record = entry as { name?: unknown; payload?: { name?: unknown } } | null;
      const name = record?.payload?.name ?? record?.name;
      if (name != null) onCategoryClick(String(name));
    };

    const series = data.datasets[0];
    const slots =
      colorSlots ??
      data.labels.map((label, index) => ({
        index,
        key: '',
        label: String(label ?? ''),
      }));

    const slices = slots.flatMap((slot, colorIndex) => {
      const value = series.values[slot.index];
      return value == null || value < 0
        ? []
        : [{ key: chartColorKey(colorIndex, slot.key || slot.label), name: slot.label, value }];
    });
    if (!slices.length) return <ChartPlaceholder icon={PieIcon} label="Pie chart" />;
    const total = slices.reduce((sum, s) => sum + s.value, 0);

    const showTotal = style.variant === 'total';
    const hole =
      showTotal || style.variant === 'donut'
        ? Math.max(param(style, 'innerRadius', 0), 45)
        : param(style, 'innerRadius', 0);
    const compact = layout?.compact ?? false;
    const labelsRequested = param(style, 'showLabels', false);
    const showSliceLabels = labelsRequested && slices.length <= (compact ? 4 : 7);
    const maxLabelChars = compact ? 10 : 16;
    const showLegendOn =
      param(style, 'showLegend', false) ||
      (compact && labelsRequested && !showSliceLabels && slices.length <= 8);
    const longestLabel = slices.reduce(
      (longest, slice) => Math.max(longest, truncate(slice.name, maxLabelChars).length),
      0,
    );
    const labelSpace = showSliceLabels
      ? Math.min(72, Math.max(24, Math.ceil(estimateTextWidth('x'.repeat(longestLabel), 11)) + 12))
      : 8;

    return (
      <ChartContainer config={sliceConfig(slots, style)} className="h-full w-full aspect-auto">
        <PieChart accessibilityLayer margin={{ top: 4, right: labelSpace, left: labelSpace, bottom: 4 }}>
          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip
              content={<StyledChartTooltip style={style} nameKey="key" showShare shareTotal={total} />}
            />
          )}

          <Pie
            data={slices}
            dataKey="value"
            nameKey="key"
            innerRadius={`${hole}%`}
            outerRadius={showSliceLabels ? '78%' : '92%'}
            paddingAngle={param(style, 'padAngle', 0)}
            minAngle={0}
            onClick={handleSliceClick}
            label={
              showSliceLabels
                ? ({ name, percent }) => {
                    const slice = slices.find((s) => s.key === name);
                    const title = truncate(slice?.name ?? String(name ?? ''), maxLabelChars);
                    return `${title} ${((percent ?? 0) * 100).toFixed(0)}%`;
                  }
                : undefined
            }
            labelLine={showSliceLabels}
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

          {showLegendOn && (
            <ChartLegend content={<ChartLegendContent nameKey="key" />} />
          )}
        </PieChart>
      </ChartContainer>
    );
  },
};
