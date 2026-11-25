import { Filter } from 'lucide-react';
import { Funnel, FunnelChart, LabelList } from 'recharts';
import { ChartContainer } from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { estimateTextWidth, formatStyledValue } from '../format';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';

const showLabels: ParamSpec = {
  kind: 'boolean',
  key: 'showLabels',
  label: 'Stage labels',
  default: true,
};

const showPercent: ParamSpec = {
  kind: 'boolean',
  key: 'showPercent',
  label: 'Percent of first stage',
  default: true,
};

const showTooltip: ParamSpec = {
  kind: 'boolean',
  key: 'showTooltip',
  label: 'Tooltip',
  default: true,
};

export const funnelChart: ChartDescriptor = {
  id: 'funnel',
  label: 'Funnel',
  description: 'Stages of one process, ordered from widest to narrowest.',
  icon: Filter,
  defaultSize: { w: 5, h: 4 },
  minSize: { w: 3, h: 3 },
  colorAxis: 'category',
  colorSlotFilter: (data, index) => {
    const value = data.datasets[0]?.values[index];
    return value != null && value >= 0;
  },
  variants: [{ id: 'default', label: 'Funnel', description: 'Stages narrow towards the bottom.' }],
  params: [showLabels, showPercent, showTooltip],
  render({ data, style, colorSlots }) {
    const series = data.datasets[0];
    if (!series) return <ChartPlaceholder icon={Filter} label="Funnel" />;

    const slots =
      colorSlots ??
      data.labels.map((label, index) => ({
        index,
        key: '',
        label: String(label ?? ''),
      }));

    const stages = slots.flatMap((slot, colorIndex) => {
      const value = series.values[slot.index];
      if (value == null || value < 0) return [];
      return [
        {
          name: slot.label || series.label,
          value,
          fill: style.colors[colorIndex % style.colors.length],
        },
      ];
    });

    if (stages.length === 0) return <ChartPlaceholder icon={Filter} label="Funnel" />;

    const labels = param(style, 'showLabels', true);
    const percent = param(style, 'showPercent', true);
    const first = stages[0].value;
    const shortName = (name: string) =>
      name.length <= 18 ? name : `${name.slice(0, 17)}…`;

    const rows = stages.map((stage) => ({
      ...stage,
      display:
        labels && percent && first > 0
          ? `${shortName(stage.name)} · ${Math.round((stage.value / first) * 100)}%`
          : labels
            ? `${shortName(stage.name)} · ${formatStyledValue(stage.value, style)}`
            : '',
    }));
    const longest = rows.reduce((max, row) => Math.max(max, row.display.length), 0);
    const rightMargin = labels
      ? Math.min(140, Math.max(24, estimateTextWidth('x'.repeat(longest), 11) + 16))
      : 12;

    return (
      <ChartContainer
        config={{ value: { label: series.label, color: style.colors[0] } }}
        className="h-full w-full aspect-auto"
      >
        <FunnelChart margin={{ top: 8, right: rightMargin, left: 8, bottom: 8 }}>
          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip content={<StyledChartTooltip style={style} />} />
          )}
          <Funnel dataKey="value" data={rows} isAnimationActive={false} stroke="none">
            {labels && (
              <LabelList
                position="right"
                dataKey="display"
                className="fill-foreground"
                fontSize={11}
              />
            )}
          </Funnel>
        </FunnelChart>
      </ChartContainer>
    );
  },
};
