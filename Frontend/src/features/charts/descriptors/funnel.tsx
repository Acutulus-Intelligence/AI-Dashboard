import { Filter } from 'lucide-react';
import { Funnel, FunnelChart, LabelList } from 'recharts';
import { ChartContainer } from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { formatStyledValue } from '../format';
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
  variants: [{ id: 'default', label: 'Funnel', description: 'Stages narrow towards the bottom.' }],
  params: [showLabels, showPercent, showTooltip],
  render({ data, style }) {
    const series = data.datasets[0];
    if (!series) return <ChartPlaceholder icon={Filter} label="Funnel" />;

    const stages = data.labels.flatMap((label, index) => {
      const value = series.values[index];
      return value == null || value < 0
        ? []
        : [{ name: label || series.label, value }];
    });

    if (stages.length === 0) return <ChartPlaceholder icon={Filter} label="Funnel" />;

    const labels = param(style, 'showLabels', true);
    const percent = param(style, 'showPercent', true);
    const first = stages[0].value;

    const rows = stages.map((stage, index) => ({
      ...stage,
      fill: style.colors[index % style.colors.length],
      display:
        labels && percent && first > 0
          ? `${stage.name} · ${Math.round((stage.value / first) * 100)}%`
          : labels
            ? `${stage.name} · ${formatStyledValue(stage.value, style)}`
            : '',
    }));

    return (
      <ChartContainer
        config={{ value: { label: series.label, color: style.colors[0] } }}
        className="h-full w-full aspect-auto"
      >
        <FunnelChart>
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
