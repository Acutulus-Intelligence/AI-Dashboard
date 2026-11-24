import type { ChartConfigResponse } from '../../lib/api/charts';
import type { ChartConfig } from '@/components/ui/chart';
import type { ChartData, ChartValue, ResolvedStyle } from './types';

/** Keeps missing/blank values as null so charts can show a gap instead of a fake zero. */
export function toChartValue(value: unknown): ChartValue {
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

/** Turns an API chart response into the normalised shape the renderers consume. */
export function transformResult(res: ChartConfigResponse): ChartData {
  if (!res.queryResult?.length) return { labels: [], datasets: [], queryResult: [], xAxis: res.xAxis };

  const labels = res.queryResult.map((row) => String(row[res.xAxis] ?? ''));
  const datasets = res.yAxis.map((axis) => ({
    label: axis,
    values: res.queryResult.map((row) => toChartValue(row[axis])),
  }));

  return { labels, datasets, queryResult: res.queryResult, xAxis: res.xAxis };
}

/** Recharts wants one row per label with a key per series; null keeps gaps. */
export function toRows(data: ChartData): Record<string, string | number | null>[] {
  return data.labels.map((label, i) => {
    const row: Record<string, string | number | null> = { name: label };
    for (const ds of data.datasets) {
      row[ds.label] = ds.values[i] ?? null;
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
