import { toChartValue } from './transform';
import type { ChartData } from './types';

/** Cross-tab of one measure by two categorical columns. */
export interface MatrixData {
  xKey: string;
  yKey: string;
  measure: string;
  columns: string[];
  rows: string[];
  cells: Map<string, number | null>;
  maxAbs: number;
  min: number;
  max: number;
}

export function cellKey(row: string, column: string): string {
  return `${row}\u0000${column}`;
}

function resolveKey(keys: string[], name?: string | null): string | undefined {
  if (!name) return undefined;
  const needle = name.trim().toLowerCase();
  return keys.find((key) => key.trim().toLowerCase() === needle);
}

/**
 * Builds the row/column matrix from the raw query result. `xAxis` is the
 * column dimension, `groupBy` the row dimension and the first dataset label
 * is the measure. Falls back to the first two non-measure keys when the axes
 * were not returned.
 */
export function buildMatrix(data: ChartData): MatrixData | null {
  const sample = data.queryResult?.find((row) => row && Object.keys(row).length > 0);
  if (!sample) return null;

  const keys = Object.keys(sample);
  const measure = data.datasets[0]?.label;
  const xKey = resolveKey(keys, data.xAxis) ?? keys.find((key) => key !== measure);
  const yKey =
    resolveKey(keys, data.groupBy) ?? keys.find((key) => key !== measure && key !== xKey);

  if (!xKey || !yKey || !measure || xKey === yKey) return null;

  const columns: string[] = [];
  const rows: string[] = [];
  const cells = new Map<string, number | null>();
  let maxAbs = 0;
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;

  for (const row of data.queryResult ?? []) {
    const column = String(row[xKey] ?? '');
    const rowLabel = String(row[yKey] ?? '');
    if (!columns.includes(column)) columns.push(column);
    if (!rows.includes(rowLabel)) rows.push(rowLabel);

    const value = toChartValue(row[measure]);
    cells.set(cellKey(rowLabel, column), value);
    if (value != null) {
      maxAbs = Math.max(maxAbs, Math.abs(value));
      min = Math.min(min, value);
      max = Math.max(max, value);
    }
  }

  if (columns.length === 0 || rows.length === 0) return null;

  return {
    xKey,
    yKey,
    measure,
    columns,
    rows,
    cells,
    maxAbs,
    min: Number.isFinite(min) ? min : 0,
    max: Number.isFinite(max) ? max : 0,
  };
}
