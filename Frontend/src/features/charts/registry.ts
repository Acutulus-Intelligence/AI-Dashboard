import { areaChart } from './descriptors/area';
import { barChart } from './descriptors/bar';
import { comboChart } from './descriptors/combo';
import { funnelChart } from './descriptors/funnel';
import { heatmapChart } from './descriptors/heatmap';
import { histogramChart } from './descriptors/histogram';
import { kpiChart } from './descriptors/kpi';
import { lineChart } from './descriptors/line';
import { mapChart } from './descriptors/map';
import { pieChart } from './descriptors/pie';
import { pivotChart } from './descriptors/pivot';
import { radarChart } from './descriptors/radar';
import { radialChart } from './descriptors/radial';
import { scatterChart } from './descriptors/scatter';
import { tableChart } from './descriptors/table';
import { waterfallChart } from './descriptors/waterfall';
import type { ChartDescriptor } from './types';

const DESCRIPTORS: ChartDescriptor[] = [
  barChart,
  comboChart,
  histogramChart,
  lineChart,
  areaChart,
  pieChart,
  heatmapChart,
  pivotChart,
  funnelChart,
  waterfallChart,
  radarChart,
  radialChart,
  kpiChart,
  scatterChart,
  mapChart,
  tableChart,
];

const byId = new Map(DESCRIPTORS.map((d) => [d.id.toLowerCase(), d]));

export function get(id: string): ChartDescriptor | undefined {
  return byId.get(id.trim().toLowerCase());
}

export function getAll(): ChartDescriptor[] {
  return DESCRIPTORS;
}

export function isKnownChartType(id: string): boolean {
  return byId.has(id.trim().toLowerCase());
}
