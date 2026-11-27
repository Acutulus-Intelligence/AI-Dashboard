import { Table2 } from 'lucide-react';
import { formatStyledValue } from '../format';
import { buildMatrix, cellKey } from '../matrix';
import { param, type ChartDescriptor, type ParamSpec, type ResolvedStyle } from '../types';
import ChartPlaceholder from './ChartPlaceholder';

const showTotals: ParamSpec = {
  kind: 'boolean',
  key: 'showTotals',
  label: 'Totals',
  default: true,
};

const compact: ParamSpec = {
  kind: 'boolean',
  key: 'compact',
  label: 'Compact cells',
  default: false,
};

const stickyHeader: ParamSpec = {
  kind: 'boolean',
  key: 'stickyHeader',
  label: 'Sticky header',
  default: true,
};

const maxRows: ParamSpec = {
  kind: 'number',
  key: 'maxRows',
  label: 'Row limit',
  default: 100,
  min: 5,
  max: 500,
  step: 5,
};

const sortRows: ParamSpec = {
  kind: 'boolean',
  key: 'sortRows',
  label: 'Sort rows by total',
  default: false,
};

function sum(values: (number | null)[]): number | null {
  let total = 0;
  let seen = false;
  for (const value of values) {
    if (value == null) continue;
    total += value;
    seen = true;
  }
  return seen ? total : null;
}

function formatCell(value: number | null, style: ResolvedStyle): string {
  return value == null ? '–' : formatStyledValue(value, style);
}

export const pivotChart: ChartDescriptor = {
  id: 'pivot',
  label: 'Pivot',
  description: 'A cross-tab table of one measure by two categorical columns.',
  icon: Table2,
  defaultSize: { w: 7, h: 5 },
  minSize: { w: 4, h: 3 },
  variants: [
    { id: 'default', label: 'Pivot table', description: 'Rows and columns with the measure in each cell.' },
  ],
  params: [showTotals, compact, stickyHeader, maxRows, sortRows],
  // Pivots are tables: no series colours, but value formatting still applies.
  styleCapabilities: { colors: false },
  render({ data, style }) {
    const matrix = buildMatrix(data);
    if (!matrix) return <ChartPlaceholder icon={Table2} label="Pivot" />;

    const { columns, rows, cells, xKey, yKey } = matrix;
    const limit = param(style, 'maxRows', 100);
    const rowTotal = (row: string) =>
      columns.reduce((sum, column) => {
        const value = cells.get(cellKey(row, column));
        return sum + (typeof value === 'number' ? value : 0);
      }, 0);
    const orderedRows = param(style, 'sortRows', false)
      ? [...rows].sort((a, b) => rowTotal(b) - rowTotal(a))
      : rows;
    const visibleRows = orderedRows.slice(0, limit);
    const showTotalsRow = param(style, 'showTotals', true);
    const totalsLabel = rows.length > visibleRows.length ? 'Total (shown)' : 'Total';
    const dense = param(style, 'compact', false);
    const sticky = param(style, 'stickyHeader', true);
    const padding = dense ? 'px-1 py-0.5' : 'px-2 py-1';

    const rowTotals = visibleRows.map((row) =>
      sum(columns.map((column) => cells.get(cellKey(row, column)) ?? null)),
    );
    const columnTotals = columns.map((column) =>
      sum(visibleRows.map((row) => cells.get(cellKey(row, column)) ?? null)),
    );
    const grandTotal = sum(rowTotals);

    return (
      <div className="flex h-full min-h-0 flex-col gap-1 overflow-hidden">
        <div className="min-h-0 flex-1 overflow-auto">
          <table className="w-full border-collapse text-xs">
            <thead className={sticky ? 'bg-background sticky top-0 z-10' : undefined}>
              <tr className="border-b">
                <th
                  className={`bg-background text-muted-foreground sticky left-0 z-10 text-left font-medium ${padding}`}
                >
                  <span className="block max-w-[10rem] truncate">{yKey}</span>
                </th>
                {columns.map((column) => (
                  <th
                    key={column}
                    className={`text-muted-foreground text-right font-medium ${padding}`}
                  >
                    <span className="block max-w-[10rem] truncate" title={column}>
                      {column}
                    </span>
                  </th>
                ))}
                {showTotalsRow && (
                  <th className={`text-right font-semibold ${padding}`}>{totalsLabel}</th>
                )}
              </tr>
            </thead>
            <tbody>
              {visibleRows.map((row, rowIndex) => (
                <tr key={row} className="hover:bg-muted/40 border-b last:border-0">
                  <th
                    className={`bg-background sticky left-0 z-10 text-left font-medium ${padding}`}
                  >
                    <span className="block max-w-[10rem] truncate" title={row}>
                      {row}
                    </span>
                  </th>
                  {columns.map((column) => {
                    const value = cells.get(cellKey(row, column)) ?? null;
                    return (
                      <td
                        key={column}
                        className={`text-right tabular-nums ${padding} ${
                          value == null ? 'text-muted-foreground' : ''
                        }`}
                      >
                        {formatCell(value, style)}
                      </td>
                    );
                  })}
                  {showTotalsRow && (
                    <td className={`bg-muted/30 text-right font-semibold tabular-nums ${padding}`}>
                      {formatCell(rowTotals[rowIndex], style)}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
            {showTotalsRow && (
              <tfoot>
                <tr className="border-t">
                  <th className={`bg-background text-left font-semibold ${padding}`}>
                    {totalsLabel}
                  </th>
                  {columnTotals.map((value, index) => (
                    <td
                      key={columns[index]}
                      className={`bg-muted/30 text-right font-semibold tabular-nums ${padding}`}
                    >
                      {formatCell(value, style)}
                    </td>
                  ))}
                  <td className={`bg-muted/50 text-right font-semibold tabular-nums ${padding}`}>
                    {formatCell(grandTotal, style)}
                  </td>
                </tr>
              </tfoot>
            )}
          </table>
        </div>
        {rows.length > visibleRows.length && (
          <p className="text-muted-foreground text-[11px]">
            Showing the first {visibleRows.length} of {rows.length} rows.
          </p>
        )}
        <p className="text-muted-foreground truncate text-[11px]">
          {xKey} × {yKey}
        </p>
      </div>
    );
  },
};
