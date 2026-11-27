import { formatStyledValue } from '../format';
import type { ResolvedStyle } from '../types';

export default function HeatmapLegend({
  label,
  color,
  min,
  max,
  style,
}: {
  label: string;
  color: string;
  min: number;
  max: number;
  style: ResolvedStyle;
}) {
  const diverging = min < 0;
  const gradient = diverging
    ? `linear-gradient(to right, var(--destructive), transparent, ${color})`
    : `linear-gradient(to right, transparent, ${color})`;

  return (
    <div className="text-muted-foreground flex flex-wrap items-center justify-between gap-2 text-[11px]">
      <span className="truncate">{label}</span>
      <span className="flex items-center gap-1">
        <span>{diverging ? formatStyledValue(min, style) : 'Low'}</span>
        <span className="bg-muted relative h-2 w-16 overflow-hidden rounded-sm">
          <span className="absolute inset-0" style={{ background: gradient }} />
        </span>
        <span>{formatStyledValue(max, style)}</span>
      </span>
    </div>
  );
}
