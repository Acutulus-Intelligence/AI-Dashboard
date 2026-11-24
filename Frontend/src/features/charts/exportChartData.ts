import type { ChartData, ChartStyleConfig } from './types';
import { downloadBlob, slugifyTitle } from './download';
import { buildHistogram } from './histogram';

type SheetUtils = typeof import('xlsx')['utils'];
type CellMapper = (value: ExportCell) => ExportCell;

export type ExportCell = string | number | boolean;

export interface ExportTable {
  header: string[];
  rows: ExportCell[][];
}

/** Excel rejects cells longer than 32,767 characters. */
const MAX_CELL_CHARS = 32_767;
const MAX_COLUMN_WIDTH = 40;

/** Leading characters spreadsheets treat as the start of a formula. */
const FORMULA_PREFIXES = ['=', '+', '-', '@', '\t', '\r'];

function clampText(value: string): string {
  return value.length > MAX_CELL_CHARS ? value.slice(0, MAX_CELL_CHARS) : value;
}

/** Coerces an arbitrary query value into something a spreadsheet cell accepts. */
function normalizeCell(value: unknown): ExportCell {
  if (value === null || value === undefined) return '';
  if (typeof value === 'number') return Number.isFinite(value) ? value : '';
  if (typeof value === 'boolean') return value;
  if (typeof value === 'bigint') return clampText(value.toString());
  if (value instanceof Date) return clampText(value.toISOString());
  if (typeof value === 'string') return clampText(value);
  try {
    return clampText(JSON.stringify(value));
  } catch {
    return clampText(String(value));
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

/**
 * Escapes formulas and writes decimals with a comma, matching the `;` field
 * separator European Excel expects. Runs after escaping so `-3,5` is not
 * mistaken for a formula.
 */
function toCsvCell(value: ExportCell): ExportCell {
  const escaped = escapeFormula(value);
  if (typeof escaped === 'number' && !Number.isInteger(escaped)) {
    return String(escaped).replace('.', ',');
  }
  return escaped;
}

/**
 * Raw table variant: every query-result column the table displays. Row limits
 * are a display concern, so exports include all rows.
 */
function buildTableData(data: ChartData): ExportTable {
  const rows = data.queryResult ?? [];
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
    return buildTableData(data);
  }

  // Heatmaps need both dimensions, not just the plotted series.
  if (chartId === 'heatmap') {
    return buildTableData(data);
  }

  if (chartId === 'histogram') {
    const series = data.datasets[0];
    if (!series) return { header: [], rows: [] };

    const rawBins = styleConfig?.params?.bins;
    const bins = buildHistogram(series.values, typeof rawBins === 'number' ? rawBins : 12);

    return {
      header: [data.xAxis?.trim() || 'Value', 'Count'],
      rows: bins.map((bin) => [bin.label, bin.count]),
    };
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

  if (chartId === 'heatmap') {
    return (data.queryResult ?? []).some((row) => row && Object.keys(row).length > 0);
  }

  if (chartId === 'histogram') {
    return data.datasets.some((dataset) => dataset.values.some((value) => value != null));
  }

  return data.labels.length > 0 && data.datasets.length > 0;
}

function toWorksheet(table: ExportTable, mapCell: CellMapper, utils: SheetUtils) {
  const aoa: ExportCell[][] = [
    table.header.map(mapCell),
    ...table.rows.map((row) => row.map(mapCell)),
  ];
  const sheet = utils.aoa_to_sheet(aoa);
  sheet['!cols'] = table.header.map((name) => ({
    wch: Math.min(Math.max(String(name).length + 2, 10), MAX_COLUMN_WIDTH),
  }));
  return sheet;
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
  const csv = utils.sheet_to_csv(toWorksheet(table, toCsvCell, utils), {
    FS: ';',
    RS: '\r\n',
  });
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
  // SheetJS writes plain strings as text cells (t: 's'), so they cannot execute
  // as formulas. Prefixing with an apostrophe would become part of the visible
  // cell value, so only the CSV path needs formula escaping.
  utils.book_append_sheet(book, toWorksheet(table, (value) => value, utils), 'Data');
  writeFile(book, `${slugifyTitle(title) || 'chart'}.xlsx`);
}
