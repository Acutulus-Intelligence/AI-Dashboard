import type { ReactNode } from 'react';
import { Table2 } from 'lucide-react';
import { Chart } from './Chart';

export class TableChartType extends Chart {
  readonly id = 'table';
  readonly label = 'Table';
  readonly icon = Table2;
  readonly defaultSize = { w: 8, h: 5 };

  render(data?: unknown): ReactNode {
    const d = data as { queryResult?: Record<string, unknown>[]; labels: string[]; datasets: { label: string; values: number[] }[] } | undefined;
    if (!d) return this.placeholder();

    if (d.queryResult?.length) {
      const columns = Object.keys(d.queryResult[0]);
      return (
        <div className="h-full w-full overflow-auto">
          <table className="w-full border-collapse text-body-sm">
            <thead>
              <tr className="border-b border-outline-variant bg-surface-container">
                {columns.map((col) => (
                  <th
                    key={col}
                    className="sticky top-0 whitespace-nowrap bg-surface-container px-3 py-2 text-left text-body-xs font-semibold uppercase tracking-wider text-on-surface-variant"
                  >
                    {col}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {d.queryResult.map((row, i) => (
                <tr
                  key={i}
                  className="border-b border-outline-variant/50 transition-colors hover:bg-surface-container"
                >
                  {columns.map((col) => (
                    <td key={col} className="whitespace-nowrap px-3 py-2 text-on-surface-variant">
                      {row[col] == null ? '' : String(row[col])}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
    }

    if (!d.labels?.length) return this.placeholder();

    const rows = d.labels.map((label, i) => {
      const row: Record<string, string | number> = {};
      row[''] = label;
      d.datasets.forEach((ds) => { row[ds.label] = ds.values[i] ?? ''; });
      return row;
    });

    return (
      <div className="h-full w-full overflow-auto">
        <table className="w-full border-collapse text-body-sm">
          <thead>
            <tr className="border-b border-outline-variant bg-surface-container">
              <th className="sticky top-0 whitespace-nowrap bg-surface-container px-3 py-2 text-left text-body-xs font-semibold uppercase tracking-wider text-on-surface-variant" />
              {d.datasets.map((ds) => (
                <th
                  key={ds.label}
                  className="sticky top-0 whitespace-nowrap bg-surface-container px-3 py-2 text-left text-body-xs font-semibold uppercase tracking-wider text-on-surface-variant"
                >
                  {ds.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr
                key={i}
                className="border-b border-outline-variant/50 transition-colors hover:bg-surface-container"
              >
                <td className="whitespace-nowrap px-3 py-2 font-medium text-on-background">
                  {row['']}
                </td>
                {d.datasets.map((ds) => (
                  <td key={ds.label} className="whitespace-nowrap px-3 py-2 text-on-surface-variant">
                    {row[ds.label]}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  private placeholder() {
    return (
      <div className="flex h-full items-center justify-center text-on-surface-variant">
        <div className="text-center">
          <Table2 size={48} className="mx-auto mb-2 text-secondary/40" />
          <p className="text-body-sm font-medium">Table</p>
        </div>
      </div>
    );
  }
}
