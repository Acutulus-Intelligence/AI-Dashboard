import { LayoutGrid } from 'lucide-react';
import { Treemap, type TreemapNode } from 'recharts';
import { ChartContainer } from '@/components/ui/chart';
import { formatStyledValue } from '../format';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import { topN } from './params';

const showLabels: ParamSpec = {
  kind: 'boolean',
  key: 'showLabels',
  label: 'Labels',
  default: true,
};

const showTooltip: ParamSpec = {
  kind: 'boolean',
  key: 'showTooltip',
  label: 'Tooltip',
  default: true,
};

function truncate(value: string, max: number): string {
  if (max <= 1) return '';
  return value.length <= max ? value : `${value.slice(0, max - 1)}…`;
}

export const treemapChart: ChartDescriptor = {
  id: 'treemap',
  label: 'Treemap',
  description: 'Rectangles sized by one measure, good for comparing many categories.',
  icon: LayoutGrid,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  colorAxis: 'category',
  colorSlotFilter: (data, index) => (data.datasets[0]?.values[index] ?? 0) > 0,
  variants: [
    { id: 'default', label: 'Treemap', description: 'Rectangles sized by the measure.' },
  ],
  params: [showLabels, showTooltip, topN],
  render({ data, style, colorSlots }) {
    const series = data.datasets[0];
    if (!series) return <ChartPlaceholder icon={LayoutGrid} label="Treemap" />;

    const slots =
      colorSlots ??
      data.labels.map((label, index) => ({
        index,
        key: '',
        label: String(label ?? ''),
      }));

    const items = slots.flatMap((slot, colorIndex) => {
      const value = series.values[slot.index];
      if (value == null || value <= 0) return [];
      return [
        {
          name: slot.label || series.label,
          size: value,
          color: style.colors[colorIndex % style.colors.length],
        },
      ];
    });

    if (items.length === 0) return <ChartPlaceholder icon={LayoutGrid} label="Treemap" />;

    const labels = param(style, 'showLabels', true);
    const tooltips = param(style, 'showTooltip', true);

    const renderCell = (node: TreemapNode) => {
      // Recharts also renders the synthetic root node; skip its overlay/total.
      if (node.depth === 0) return <g />;

      const width = Number(node.width ?? 0);
      const height = Number(node.height ?? 0);
      const value = Number(node.value ?? 0);
      const name = String(node.name ?? '');
      const fill =
        (node.color as string | undefined) ??
        style.colors[Number(node.index ?? 0) % style.colors.length];

      return (
        <g>
          <rect
            x={node.x}
            y={node.y}
            width={width}
            height={height}
            rx={3}
            fill={fill}
            fillOpacity={0.82}
            stroke="var(--background)"
            strokeWidth={2}
          >
            {tooltips && (
              <title>{`${name}: ${formatStyledValue(value, style)}`}</title>
            )}
          </rect>
          {labels && width > 48 && height > 26 && (
            <text
              x={node.x + 6}
              y={node.y + 17}
              fontSize={11}
              className="fill-foreground pointer-events-none"
            >
              {truncate(name, Math.floor(width / 7))}
            </text>
          )}
          {labels && width > 48 && height > 42 && (
            <text
              x={node.x + 6}
              y={node.y + 31}
              fontSize={10}
              className="fill-muted-foreground pointer-events-none"
            >
              {formatStyledValue(value, style)}
            </text>
          )}
        </g>
      );
    };

    return (
      <ChartContainer
        config={{ size: { label: series.label, color: style.colors[0] } }}
        className="h-full w-full aspect-auto"
      >
        <Treemap
          data={items}
          dataKey="size"
          nameKey="name"
          aspectRatio={4 / 3}
          isAnimationActive={false}
          content={renderCell}
        />
      </ChartContainer>
    );
  },
};
