import type { ChartData, ParamSpec } from '../types';

export const showGrid: ParamSpec = {
  kind: 'boolean',
  key: 'showGrid',
  label: 'Grid lines',
  default: true,
};

export const showLegend: ParamSpec = {
  kind: 'boolean',
  key: 'showLegend',
  label: 'Legend',
  default: false,
};

export const showTooltip: ParamSpec = {
  kind: 'boolean',
  key: 'showTooltip',
  label: 'Tooltip',
  default: true,
};

export const showXAxis: ParamSpec = {
  kind: 'boolean',
  key: 'showXAxis',
  label: 'X axis',
  default: true,
};

export const showYAxis: ParamSpec = {
  kind: 'boolean',
  key: 'showYAxis',
  label: 'Y axis',
  default: true,
};

export const showLabels: ParamSpec = {
  kind: 'boolean',
  key: 'showLabels',
  label: 'Value labels',
  default: false,
};

export const strokeWidth: ParamSpec = {
  kind: 'number',
  key: 'strokeWidth',
  label: 'Line thickness',
  default: 2,
  min: 1,
  max: 6,
  step: 1,
};

export const curve: ParamSpec = {
  kind: 'select',
  key: 'curve',
  label: 'Curve',
  default: 'monotone',
  options: [
    { value: 'monotone', label: 'Smooth' },
    { value: 'linear', label: 'Straight' },
    { value: 'step', label: 'Stepped' },
  ],
};

export const fillOpacity: ParamSpec = {
  kind: 'number',
  key: 'fillOpacity',
  label: 'Fill opacity',
  default: 0.25,
  min: 0,
  max: 1,
  step: 0.05,
};

export const cornerRadius: ParamSpec = {
  kind: 'number',
  key: 'cornerRadius',
  label: 'Corner radius',
  default: 4,
  min: 0,
  max: 16,
  step: 1,
};

export const CARTESIAN_MARGIN = { top: 12, right: 12, left: 4, bottom: 4 };

export function hasSeries(data: ChartData) {
  return data.labels.length > 0 && data.datasets.length > 0;
}
