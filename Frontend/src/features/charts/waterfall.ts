import type { ChartValue } from './types';

export interface WaterfallRow {
  name: string;
  delta: number;
  start: number;
  end: number;
  /** Lower edge of the visible bar (works for negative deltas). */
  base: number;
  /** Visible bar height; always non-negative. */
  rise: number;
  total: number;
  kind: 'delta' | 'total';
}

/**
 * Turns signed per-step changes into cumulative waterfall bars. Each bar spans
 * the running total before and after the step, so negative deltas point down.
 */
export function buildWaterfallRows(
  labels: string[],
  values: ChartValue[],
  includeTotal: boolean,
): WaterfallRow[] {
  const rows: WaterfallRow[] = [];
  let running = 0;

  for (let index = 0; index < labels.length; index++) {
    const delta = values[index];
    if (delta == null || !Number.isFinite(delta)) continue;

    const start = running;
    const end = running + delta;
    rows.push({
      name: labels[index] || `Step ${index + 1}`,
      delta,
      start,
      end,
      base: Math.min(start, end),
      rise: Math.abs(delta),
      total: end,
      kind: 'delta',
    });
    running = end;
  }

  if (includeTotal && rows.length > 0) {
    rows.push({
      name: 'Total',
      delta: running,
      start: 0,
      end: running,
      base: Math.min(0, running),
      rise: Math.abs(running),
      total: running,
      kind: 'total',
    });
  }

  return rows;
}
