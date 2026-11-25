import type { ReactNode } from 'react';
import { useChart } from '@/components/ui/chart';
import { formatStyledValue } from '../format';
import type { ResolvedStyle } from '../types';

interface TooltipItem {
  name?: string | number;
  value?: string | number;
  dataKey?: string | number;
  color?: string;
  type?: string;
  payload?: Record<string, unknown>;
}

interface StyledChartTooltipProps {
  style: ResolvedStyle;
  active?: boolean;
  payload?: TooltipItem[];
  label?: string | number;
  /** Payload key holding the display name (pie/radial slices). */
  nameKey?: string;
  /** Show a total row (stacked bars/areas). */
  showTotal?: boolean;
  /** Show each value as a share of the payload total (pie/radial). */
  showShare?: boolean;
}

function resolveLabel(
  item: TooltipItem,
  nameKey: string | undefined,
  config: Record<string, { label?: React.ReactNode }>,
): ReactNode {
  const key = nameKey && item.payload?.[nameKey] != null
    ? String(item.payload[nameKey])
    : String(item.name ?? item.dataKey ?? '');

  return config[key]?.label ?? item.name ?? key;
}

export default function StyledChartTooltip({
  style,
  active,
  payload,
  label,
  nameKey,
  showTotal,
  showShare,
}: StyledChartTooltipProps) {
  const { config } = useChart();

  if (!active || !payload?.length) return null;

  const items = payload.filter(
    (item) => item.type !== 'none' && item.value !== undefined && item.value !== null,
  );
  if (!items.length) return null;

  const total = items.reduce((sum, item) => {
    const value = typeof item.value === 'number' ? item.value : Number(item.value);
    return sum + (Number.isFinite(value) ? value : 0);
  }, 0);

  return (
    <div className="border-border/50 bg-background grid min-w-32 gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs shadow-xl">
      {label != null && label !== '' && (
        <div className="font-medium">{String(label)}</div>
      )}
      <div className="grid gap-1.5">
        {items.map((item, index) => {
          const color = (item.payload?.fill as string | undefined) ?? item.color;
          return (
            <div key={index} className="flex items-center justify-between gap-4">
              <span className="flex min-w-0 items-center gap-2">
                <span
                  className="size-2.5 shrink-0 rounded-[2px]"
                  style={{ background: color ?? 'var(--chart-1)' }}
                  aria-hidden
                />
                <span className="text-muted-foreground truncate">
                  {resolveLabel(item, nameKey, config)}
                </span>
              </span>
              <span className="text-foreground shrink-0 font-mono font-medium tabular-nums">
                {formatStyledValue(item.value, style)}
                {showShare && total > 0 && (
                  <span className="text-muted-foreground ml-1 font-normal">
                    {Math.round((Number(item.value) / total) * 100)}%
                  </span>
                )}
              </span>
            </div>
          );
        })}
        {showTotal && items.length > 1 && (
          <div className="border-border/60 mt-0.5 flex items-center justify-between gap-4 border-t pt-1.5">
            <span className="font-medium">Total</span>
            <span className="font-mono font-semibold tabular-nums">
              {formatStyledValue(total, style)}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
