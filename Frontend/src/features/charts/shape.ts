import { param, type ChartData, type ResolvedStyle } from './types';

export const OTHER_LABEL = 'Other';

function measureSum(data: ChartData, index: number): number {
  return data.datasets.reduce((sum, dataset) => {
    const value = dataset.values[index];
    return sum + (typeof value === 'number' && Number.isFinite(value) ? value : 0);
  }, 0);
}

function isOther(label: string): boolean {
  return label.trim().toLowerCase() === OTHER_LABEL.toLowerCase();
}

/** Aggregates the hidden category values the same way the SQL did. */
function aggregateTail(
  dataset: ChartData['datasets'][number],
  indexes: number[],
  aggregation: string,
): number | null {
  let sum = 0;
  let count = 0;
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;

  for (const index of indexes) {
    const value = dataset.values[index];
    if (typeof value !== 'number' || !Number.isFinite(value)) continue;
    sum += value;
    count += 1;
    min = Math.min(min, value);
    max = Math.max(max, value);
  }

  if (count === 0) return null;

  switch (aggregation.toLowerCase()) {
    case 'avg':
      return sum / count;
    case 'min':
      return min;
    case 'max':
      return max;
    default:
      return sum;
  }
}

/**
 * Keeps the N largest categories and aggregates the rest into "Other". Shared by
 * the renderer and the exporter so downloads show exactly what the chart shows.
 */
export function shapeForTopN(
  data: ChartData,
  topN: number,
  aggregation?: string,
): ChartData {
  const limit = typeof topN === 'number' && Number.isFinite(topN) ? Math.trunc(topN) : 0;
  if (limit <= 0 || data.labels.length <= limit) return data;

  // A time axis is ordered by timestamp; grouping categories into "Other" would
  // break the scale, so presentation top-N never applies to time-series charts.
  if (data.xTime) return data;

  const otherIndexes = data.labels
    .map((label, index) => (isOther(label) ? index : -1))
    .filter((index) => index >= 0);

  // Aggregating the tail is only meaningful for additive measures.
  const aggregationName = (aggregation ?? data.aggregation ?? 'sum').toLowerCase();
  if (aggregationName !== 'sum' && aggregationName !== 'count') return data;

  const order = data.labels
    .map((label, index) => (isOther(label) ? -1 : index))
    .filter((index) => index >= 0)
    .sort((a, b) => measureSum(data, b) - measureSum(data, a));

  const kept = order.slice(0, limit);
  const rest = [...order.slice(limit), ...otherIndexes];
  const labels = kept.map((index) => data.labels[index]);

  const datasets = data.datasets.map((dataset) => ({
    ...dataset,
    values: kept.map((index) => dataset.values[index] ?? null),
  }));

  if (rest.length > 0) {
    labels.push(OTHER_LABEL);
    datasets.forEach((dataset, datasetIndex) => {
      dataset.values.push(
        aggregateTail(data.datasets[datasetIndex], rest, aggregationName),
      );
    });
  }

  return { ...data, labels, datasets };
}

/**
 * Applies the Top N presentation setting on the client so the slider updates
 * immediately.
 */
export function shapeForDisplay(data: ChartData, style: ResolvedStyle): ChartData {
  return shapeForTopN(data, param(style, 'topN', 0), data.aggregation);
}
