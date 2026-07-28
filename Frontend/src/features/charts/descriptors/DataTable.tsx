import { cn } from '@/lib/utils';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { param, type ResolvedStyle } from '../types';

function isNumeric(value: unknown) {
  return (
    typeof value === 'number' ||
    (typeof value === 'string' && value !== '' && !Number.isNaN(Number(value)))
  );
}

function formatCell(value: unknown) {
  if (value == null) return '';
  if (typeof value === 'number') return value.toLocaleString();
  return String(value);
}

interface DataTableProps {
  columns: string[];
  rows: Record<string, unknown>[];
  style: ResolvedStyle;
  /** Treats the first column as a row header rather than a value. */
  firstColumnLabel?: boolean;
}

export default function DataTable({ columns, rows, style, firstColumnLabel }: DataTableProps) {
  const dense = param(style, 'compact', false);
  const zebra = param(style, 'striped', false);
  const sticky = param(style, 'stickyHeader', true);
  const rightAlign = param(style, 'alignNumbers', true);
  const cellPadding = dense ? 'py-1' : 'py-2';

  return (
    <div className="h-full w-full overflow-auto">
      <Table>
        <TableHeader className={cn(sticky && 'bg-card sticky top-0 z-10')}>
          <TableRow>
            {columns.map((col, i) => {
              const numeric =
                rightAlign && !(firstColumnLabel && i === 0) && rows.some((row) => isNumeric(row[col]));
              return (
                <TableHead key={col} className={cn('whitespace-nowrap', cellPadding, numeric && 'text-right')}>
                  {col}
                </TableHead>
              );
            })}
          </TableRow>
        </TableHeader>

        <TableBody>
          {rows.map((row, i) => (
            <TableRow key={i} className={cn(zebra && i % 2 === 1 && 'bg-muted/40')}>
              {columns.map((col, colIndex) => {
                const isLabel = firstColumnLabel && colIndex === 0;
                const numeric = rightAlign && !isLabel && isNumeric(row[col]);
                return (
                  <TableCell
                    key={col}
                    className={cn(
                      'whitespace-nowrap',
                      cellPadding,
                      isLabel && 'text-foreground font-medium',
                      numeric && 'text-right tabular-nums',
                    )}
                  >
                    {formatCell(row[col])}
                  </TableCell>
                );
              })}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
