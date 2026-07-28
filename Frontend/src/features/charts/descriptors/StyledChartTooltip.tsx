import type { ComponentProps } from 'react';
import { ChartTooltipContent, useChart } from '@/components/ui/chart';
import { formatStyledValue } from '../format';
import type { ResolvedStyle } from '../types';

type TooltipContentProps = ComponentProps<typeof ChartTooltipContent>;

/**
 * Forwards Recharts tooltip props (active/payload/label) into ChartTooltipContent
 * and applies valuePrefix / valueSuffix from the resolved style.
 */
export default function StyledChartTooltip({
  style,
  ...props
}: { style: ResolvedStyle } & TooltipContentProps) {
  const { config } = useChart();

  return (
    <ChartTooltipContent
      {...props}
      formatter={(value, name) => {
        const key = String(name ?? '');
        const label = config[key]?.label ?? name;
        return (
          <div className="flex flex-1 items-center justify-between gap-4 leading-none">
            <span className="text-muted-foreground">{label}</span>
            <span className="text-foreground font-mono font-medium tabular-nums">
              {formatStyledValue(value, style)}
            </span>
          </div>
        );
      }}
    />
  );
}
