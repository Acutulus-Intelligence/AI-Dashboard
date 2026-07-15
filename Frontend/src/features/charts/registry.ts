import { Chart } from './Chart';
import { BarChartType } from './BarChart';
import { LineChartType } from './LineChart';
import { PieChartType } from './PieChart';
import { AreaChartType } from './AreaChart';
import { ScatterChartType } from './ScatterChart';
import { TableChartType } from './TableChart';

const _registry = new Map<string, Chart>();

export function register(chart: Chart): void {
  _registry.set(chart.id, chart);
}

export function get(id: string): Chart | undefined {
  return _registry.get(id);
}

export function getAll(): Chart[] {
  return Array.from(_registry.values());
}

register(new BarChartType());
register(new LineChartType());
register(new PieChartType());
register(new AreaChartType());
register(new ScatterChartType());
register(new TableChartType());
