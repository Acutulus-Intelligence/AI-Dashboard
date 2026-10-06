import { areaChart } from './descriptors/area';
import { barChart } from './descriptors/bar';
import { lineChart } from './descriptors/line';
import { mapChart } from './descriptors/map';
import { pieChart } from './descriptors/pie';
import { radarChart } from './descriptors/radar';
import { radialChart } from './descriptors/radial';
import { scatterChart } from './descriptors/scatter';
import { tableChart } from './descriptors/table';
import type { ChartDescriptor } from './types';

const DESCRIPTORS: ChartDescriptor[] = [
  barChart,
  lineChart,
  areaChart,
  pieChart,
  radarChart,
  radialChart,
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
