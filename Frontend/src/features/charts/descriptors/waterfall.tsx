import { BarChart3 } from 'lucide-react';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import { buildWaterfallRows } from '../waterfall';
import WaterfallSvg from './waterfallSvg';
import ChartPlaceholder from './ChartPlaceholder';

const showTotals: ParamSpec = {
  kind: 'boolean',
  key: 'showTotals',
  label: 'Total bar',
  default: true,
};

const showLabels: ParamSpec = {
  kind: 'boolean',
  key: 'showLabels',
  label: 'Change labels',
  default: false,
};

const showGrid: ParamSpec = {
  kind: 'boolean',
  key: 'showGrid',
  label: 'Grid lines',
  default: true,
};

const showTooltip: ParamSpec = {
  kind: 'boolean',
  key: 'showTooltip',
  label: 'Tooltip',
  default: true,
};

export const waterfallChart: ChartDescriptor = {
  id: 'waterfall',
  label: 'Waterfall',
  description: 'Shows how signed changes build up to a running total.',
  icon: BarChart3,
  defaultSize: { w: 7, h: 4 },
  minSize: { w: 4, h: 3 },
  variants: [
    { id: 'default', label: 'Waterfall', description: 'Bars show each change and the running total.' },
  ],
  params: [showTotals, showLabels, showGrid, showTooltip],
  render({ data, style, layout }) {
    const series = data.datasets[0];
    if (!series) return <ChartPlaceholder icon={BarChart3} label="Waterfall" />;

    const rows = buildWaterfallRows(
      data.labels,
      series.values,
      param(style, 'showTotals', true),
    );

    if (rows.length === 0) return <ChartPlaceholder icon={BarChart3} label="Waterfall" />;

    return (
      <WaterfallSvg
        rows={rows}
        style={style}
        width={Math.max(240, layout?.width || 640)}
        height={Math.max(180, layout?.height || 320)}
        showGridLines={param(style, 'showGrid', true)}
        showChangeLabels={param(style, 'showLabels', false)}
        showTooltips={param(style, 'showTooltip', true)}
      />
    );
  },
};
