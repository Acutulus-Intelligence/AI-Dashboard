import { param, type ChartData, type ResolvedStyle } from './types';

export const OTHER_LABEL = 'Other';

function measureSum(data: ChartData, index: number): number {
  return data.datasets.reduce((sum, dataset) => {
    const value = dataset.values[index];
    return sum + (typeof value === 'number' && Number.isFinite(value) ? value : 0);
  }, 0);
}

/**
 * Applies the Top N presentation setting on the client so the slider updates
 * immediately. Remaining categories are summed into a single "Other" row.
 */
export function shapeForDisplay(data: ChartData, style: ResolvedStyle): ChartData {
  const topN = Math.trunc(param(style, 'topN', 0));
  if (topN <= 0 || data.labels.length <= topN) return data;

  const otherIndexes = data.labels
    .map((label, index) => (label === OTHER_LABEL ? index : -1))
    .filter((index) => index >= 0);
  const order = data.labels
    .map((label, index) => (label === OTHER_LABEL ? -1 : index))
    .filter((index) => index >= 0)
    .sort((a, b) => measureSum(data, b) - measureSum(data, a));

  const kept = order.slice(0, topN);
  const rest = [...order.slice(topN), ...otherIndexes];
  const labels = kept.map((index) => data.labels[index]);

  const datasets = data.datasets.map((dataset) => ({
    ...dataset,
    values: kept.map((index) => dataset.values[index] ?? null),
  }));

  if (rest.length > 0) {
    labels.push(OTHER_LABEL);
    datasets.forEach((dataset, datasetIndex) => {
      const source = data.datasets[datasetIndex];
      const total = rest.reduce((sum, index) => {
        const value = source.values[index];
        return sum + (typeof value === 'number' && Number.isFinite(value) ? value : 0);
      }, 0);
      dataset.values.push(total);
    });
  }

  return { ...data, labels, datasets };
}
