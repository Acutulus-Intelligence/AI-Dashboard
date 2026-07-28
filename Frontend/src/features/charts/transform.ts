import type { ChartConfigResponse } from '../../lib/api/charts';
import type { ChartConfig } from '@/components/ui/chart';
import type { ChartData, ResolvedStyle } from './types';

/** Turns an API chart response into the normalised shape the renderers consume. */
export function transformResult(res: ChartConfigResponse): ChartData {
  if (!res.queryResult?.length) return { labels: [], datasets: [], queryResult: [] };

  const labels = res.queryResult.map((row) => String(row[res.xAxis] ?? ''));
  const datasets = res.yAxis.map((axis) => ({
    label: axis,
    values: res.queryResult.map((row) => {
      const v = row[axis];
      const num = typeof v === 'number' ? v : Number(v);
      return Number.isNaN(num) ? 0 : num;
    }),
  }));

  return { labels, datasets, queryResult: res.queryResult };
}

/** Recharts wants one row per label with a key per series. */
export function toRows(data: ChartData): Record<string, string | number>[] {
  return data.labels.map((label, i) => {
    const row: Record<string, string | number> = { name: label };
    for (const ds of data.datasets) {
      row[ds.label] = ds.values[i] ?? 0;
    }
    return row;
  });
}

/** Maps each series onto a colour token via ChartContainer. */
export function toChartConfig(data: ChartData, style: ResolvedStyle): ChartConfig {
  const config: ChartConfig = {};
  data.datasets.forEach((ds, i) => {
    config[ds.label] = { label: ds.label, color: style.colors[i % style.colors.length] };
  });
  return config;
}
