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
  if (!res.queryResult?.length) {
    return {
      labels: [],
      datasets: [],
      queryResult: [],
      xAxis: res.xAxis,
      groupBy: res.groupBy,
      aggregation: res.aggregation,
      xTime: undefined,
    };
  }

  const labels = res.queryResult.map((row) => String(row[res.xAxis] ?? ''));
  const datasets = res.yAxis.map((axis) => ({
    label: axis,
    values: res.queryResult.map((row) => toChartValue(row[axis])),
  }));

  return {
    labels,
    datasets,
    queryResult: res.queryResult,
    xAxis: res.xAxis,
    groupBy: res.groupBy,
    aggregation: res.aggregation,
    xTime: detectTimeAxis(res.queryResult, res.xAxis),
  };
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}(?:[T ].*)?$/;

function toTimestamp(value: unknown): number | null {
  if (value instanceof Date) {
    const time = value.getTime();
    return Number.isFinite(time) ? time : null;
  }

  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!ISO_DATE.test(trimmed)) return null;
    // Some drivers serialize with a space separator; make it ISO before parsing.
    const normalized = trimmed.includes(' ') ? trimmed.replace(' ', 'T') : trimmed;
    const time = Date.parse(normalized);
    return Number.isFinite(time) ? time : null;
  }

  return null;
}

/**
 * Every x value must be an ISO date for the axis to become a time scale. Mixed
 * text/number columns stay categorical so labels are never silently dropped.
 */
function detectTimeAxis(
  rows: Record<string, unknown>[],
  xAxis?: string,
): number[] | undefined {
  if (!xAxis || rows.length < 2) return undefined;

  const timestamps: number[] = [];
  for (const row of rows) {
    const timestamp = toTimestamp(row[xAxis]);
    if (timestamp == null) return undefined;
    timestamps.push(timestamp);
  }

  return timestamps;
}

/** Recharts wants one row per label with a key per series; null keeps gaps. */
export function toRows(data: ChartData): Record<string, string | number | null>[] {
  return data.labels.map((label, i) => {
    const row: Record<string, string | number | null> = { name: label };
    if (data.xTime) row.__x = data.xTime[i] ?? null;
    for (const ds of data.datasets) {
      row[ds.label] = ds.values[i] ?? null;
    }
    return row;
  });
}

/** Trailing moving average; nulls are ignored inside each window. */
export function movingAverage(values: ChartValue[], window: number): ChartValue[] {
  const size = Math.trunc(window);
  if (!Number.isFinite(size) || size <= 1) return values;

  return values.map((_, index) => {
    const start = Math.max(0, index - size + 1);
    let sum = 0;
    let count = 0;
    for (let i = start; i <= index; i++) {
      const value = values[i];
      if (typeof value === 'number' && Number.isFinite(value)) {
        sum += value;
        count += 1;
      }
    }
    return count > 0 ? sum / count : null;
  });
}

/** Least-squares trend over the row index, ignoring null values. */
export function linearTrend(values: ChartValue[]): ChartValue[] {
  let count = 0;
  let sumX = 0;
  let sumY = 0;
  let sumXY = 0;
  let sumXX = 0;

  values.forEach((value, index) => {
    if (typeof value !== 'number' || !Number.isFinite(value)) return;
    count += 1;
    sumX += index;
    sumY += value;
    sumXY += index * value;
    sumXX += index * index;
  });

  if (count < 2) return values.map(() => null);

  const denominator = count * sumXX - sumX * sumX;
  if (denominator === 0) return values.map(() => null);

  const slope = (count * sumXY - sumX * sumY) / denominator;
  const intercept = (sumY - slope * sumX) / count;

  return values.map((_, index) => slope * index + intercept);
}

/** Rebuilds datasets/labels after filtering query rows by a cross-filter value. */
export function filterChartData(data: ChartData, column: string, value: string): ChartData {
  const rows = data.queryResult;
  if (!rows?.length || !data.xAxis) return data;
  const xAxis = data.xAxis;
  if (!rows.some((row) => column in row)) return data;

  const filtered = rows.filter((row) => String(row[column] ?? '') === value);
  if (filtered.length === rows.length) return data;

  const labels = filtered.map((row) => String(row[xAxis] ?? ''));
  const datasets = data.datasets.map((dataset) => ({
    ...dataset,
    values: filtered.map((row) => toChartValue(row[dataset.label])),
  }));

  return {
    ...data,
    labels,
    datasets,
    queryResult: filtered,
    xTime: detectTimeAxis(filtered, xAxis),
  };
}

/** Maps each series onto a colour token via ChartContainer. */
export function toChartConfig(data: ChartData, style: ResolvedStyle): ChartConfig {
  const config: ChartConfig = {};
  data.datasets.forEach((ds, i) => {
    config[ds.label] = { label: ds.label, color: style.colors[i % style.colors.length] };
  });
  return config;
}
