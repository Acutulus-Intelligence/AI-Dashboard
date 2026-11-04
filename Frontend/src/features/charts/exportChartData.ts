import type { ChartData, ChartStyleConfig } from './types';
import { slugifyTitle } from './exportChartImage';

type SheetUtils = typeof import('xlsx')['utils'];

export type ExportCell = string | number | boolean;

export interface ExportTable {
  header: string[];
  rows: ExportCell[][];
}

const DEFAULT_TABLE_ROWS = 100;
const MAX_COLUMN_WIDTH = 40;

/** Leading characters spreadsheets treat as the start of a formula. */
const FORMULA_PREFIXES = ['=', '+', '-', '@', '\t', '\r'];

/** Coerces an arbitrary query value into something a spreadsheet cell accepts. */
function normalizeCell(value: unknown): ExportCell {
  if (value === null || value === undefined) return '';
  if (typeof value === 'number') return Number.isFinite(value) ? value : '';
  if (typeof value === 'boolean') return value;
  if (typeof value === 'bigint') return value.toString();
  if (value instanceof Date) return value.toISOString();
  if (typeof value === 'string') return value;
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

/**
 * Prefixes risky leading characters so CSV cells are not executed as formulas.
 * Legitimate numeric-looking strings (e.g. "-2", "+3.5") are left untouched.
 */
function escapeFormula(value: ExportCell): ExportCell {
  if (typeof value !== 'string' || value.length === 0 || !FORMULA_PREFIXES.includes(value[0])) {
    return value;
  }
  return Number.isFinite(Number(value)) ? value : `'${value}`;
}

function resolveMaxRows(styleConfig?: ChartStyleConfig): number {
  const value = styleConfig?.params?.maxRows;
  return typeof value === 'number' && Number.isFinite(value) && value > 0
    ? Math.floor(value)
    : DEFAULT_TABLE_ROWS;
}

/** Raw table variant: the query-result columns the table displays, limited to shown rows. */
function buildTableData(data: ChartData, styleConfig?: ChartStyleConfig): ExportTable {
  const rows = (data.queryResult ?? []).slice(0, resolveMaxRows(styleConfig));
  if (!rows.length) return { header: [], rows: [] };

  const header: string[] = [];
  const seen = new Set<string>();
  for (const row of rows) {
    for (const key of Object.keys(row)) {
      if (!seen.has(key)) {
        seen.add(key);
        header.push(key);
      }
    }
  }

  return {
    header,
    rows: rows.map((row) => header.map((key) => normalizeCell(row[key]))),
  };
}

/** Series variant: one row per label with a column per dataset. */
function buildSeriesData(data: ChartData, includeAllDatasets: boolean): ExportTable {
  const datasets = includeAllDatasets ? data.datasets : data.datasets.slice(0, 1);
  if (!data.labels.length || !datasets.length) return { header: [], rows: [] };

  const header = [data.xAxis?.trim() || 'Label', ...datasets.map((ds) => ds.label)];
  const rows = data.labels.map((label, i) => [
    normalizeCell(label),
    ...datasets.map((ds) => normalizeCell(ds.values[i])),
  ]);

  return { header, rows };
}

/**
 * Builds the export table for the data the chart actually shows. Raw SQL is
 * never exported: only the chart's axes/series, or the table's visible columns.
 */
export function buildExportTable(
  data: ChartData,
  chartId: string,
  styleConfig?: ChartStyleConfig,
): ExportTable {
  if (chartId === 'table' && styleConfig?.variant !== 'summary') {
    return buildTableData(data, styleConfig);
  }
  const includeAllDatasets = chartId !== 'pie' && chartId !== 'radial';
  return buildSeriesData(data, includeAllDatasets);
}

/** Cheap check for whether an export would produce rows, without building them. */
export function hasExportData(
  data: ChartData,
  chartId: string,
  styleConfig?: ChartStyleConfig,
): boolean {
  if (chartId === 'table' && styleConfig?.variant !== 'summary') {
    return (data.queryResult ?? []).some((row) => row && Object.keys(row).length > 0);
  }
  return data.labels.length > 0 && data.datasets.length > 0;
}

function toWorksheet(table: ExportTable, escapeFormulas: boolean, utils: SheetUtils) {
  const map = (value: ExportCell) => (escapeFormulas ? escapeFormula(value) : value);
  const aoa: ExportCell[][] = [table.header.map(map), ...table.rows.map((row) => row.map(map))];
  const sheet = utils.aoa_to_sheet(aoa);
  sheet['!cols'] = table.header.map((name) => ({
    wch: Math.min(Math.max(String(name).length + 2, 10), MAX_COLUMN_WIDTH),
  }));
  return sheet;
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

export async function exportChartDataAsCsv(
  data: ChartData,
  chartId: string,
  title: string,
  styleConfig?: ChartStyleConfig,
): Promise<void> {
  const table = buildExportTable(data, chartId, styleConfig);
  if (!table.rows.length) throw new Error('There is no data to export.');

  const { utils } = await import('xlsx');
  const csv = utils.sheet_to_csv(toWorksheet(table, true, utils), { FS: ';', RS: '\r\n' });
  // BOM keeps Excel from misreading non-ASCII (e.g. Swedish names).
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8' });
  downloadBlob(blob, `${slugifyTitle(title) || 'chart'}.csv`);
}

export async function exportChartDataAsExcel(
  data: ChartData,
  chartId: string,
  title: string,
  styleConfig?: ChartStyleConfig,
): Promise<void> {
  const table = buildExportTable(data, chartId, styleConfig);
  if (!table.rows.length) throw new Error('There is no data to export.');

  const { utils, writeFile } = await import('xlsx');
  const book = utils.book_new();
  utils.book_append_sheet(book, toWorksheet(table, false, utils), 'Data');
  writeFile(book, `${slugifyTitle(title) || 'chart'}.xlsx`);
}
