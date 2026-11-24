export interface HistogramBin {
  from: number;
  to: number;
  label: string;
  count: number;
}

/**
 * Splits numeric values into equal-width bins. Nulls and non-finite values are
 * ignored so a gap in the data does not create a fake zero-height bin.
 */
export function buildHistogram(values: (number | null)[], binCount: number): HistogramBin[] {
  const numbers: number[] = [];
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;

  for (const value of values) {
    if (value == null || !Number.isFinite(value)) continue;
    numbers.push(value);
    if (value < min) min = value;
    if (value > max) max = value;
  }

  if (numbers.length === 0) return [];

  const bins = Math.max(1, Math.min(60, Math.trunc(binCount) || 12));

  if (min === max) {
    return [{ from: min, to: max, label: formatBoundary(min), count: numbers.length }];
  }

  const width = (max - min) / bins;
  const counts = new Array<number>(bins).fill(0);

  for (const value of numbers) {
    const index = Math.min(bins - 1, Math.floor((value - min) / width));
    counts[index] += 1;
  }

  return counts.map((count, index) => {
    const from = min + index * width;
    const to = index === bins - 1 ? max : from + width;
    return { from, to, label: `${formatBoundary(from)}–${formatBoundary(to)}`, count };
  });
}

function formatBoundary(value: number): string {
  if (Number.isInteger(value)) return value.toLocaleString();
  return value.toLocaleString(undefined, { maximumFractionDigits: 2 });
}
