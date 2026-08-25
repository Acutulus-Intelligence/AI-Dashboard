import { Table2 } from 'lucide-react';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import DataTable from './DataTable';

const striped: ParamSpec = {
  kind: 'boolean',
  key: 'striped',
  label: 'Striped rows',
  default: false,
};

const compact: ParamSpec = {
  kind: 'boolean',
  key: 'compact',
  label: 'Compact rows',
  default: false,
};

const stickyHeader: ParamSpec = {
  kind: 'boolean',
  key: 'stickyHeader',
  label: 'Sticky header',
  default: true,
};

const alignNumbers: ParamSpec = {
  kind: 'boolean',
  key: 'alignNumbers',
  label: 'Right-align numbers',
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

export const tableChart: ChartDescriptor = {
  id: 'table',
  label: 'Table',
  description: 'Shows the query result as rows and columns.',
  icon: Table2,
  defaultSize: { w: 8, h: 5 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'raw', label: 'Query result', description: 'Every column the query returned.' },
    { id: 'summary', label: 'Summary', description: 'Only the chart axes, pivoted by label.' },
  ],
  params: [striped, compact, stickyHeader, alignNumbers, maxRows],
  // Tables have no series colours or $/% value labels — only layout params + variants.
  styleCapabilities: { valueLabels: false, colors: false },
  render({ data, style }) {
    const limit = param(style, 'maxRows', 100);
    const rawRows = style.variant !== 'summary' ? data.queryResult : undefined;

    if (rawRows?.length) {
      const rows = rawRows.slice(0, limit);
      return <DataTable columns={Object.keys(rows[0])} rows={rows} style={style} />;
    }

    if (!data.labels.length) return <ChartPlaceholder icon={Table2} label="Table" />;

    const labelColumn = ' ';
    const columns = [labelColumn, ...data.datasets.map((ds) => ds.label)];
    const rows = data.labels.slice(0, limit).map((label, i) => {
      const row: Record<string, unknown> = { [labelColumn]: label };
      for (const ds of data.datasets) row[ds.label] = ds.values[i] ?? '';
      return row;
    });

    return <DataTable columns={columns} rows={rows} style={style} firstColumnLabel />;
  },
};
