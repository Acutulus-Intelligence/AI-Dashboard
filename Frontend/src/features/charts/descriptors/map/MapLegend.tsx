interface MapLegendProps {
  low: string;
  high: string;
  minLabel: string;
  maxLabel: string;
  measure: string;
}

export default function MapLegend({ low, high, minLabel, maxLabel, measure }: MapLegendProps) {
  return (
    <div className="flex shrink-0 items-center gap-2">
      <span className="text-muted-foreground text-[10px] tabular-nums">{minLabel}</span>
      <div
        className="h-2 min-w-8 flex-1 rounded-full"
        style={{ background: `linear-gradient(to right, ${low}, ${high})` }}
      />
      <span className="text-muted-foreground text-[10px] tabular-nums">{maxLabel}</span>
      {measure && (
        <span className="text-muted-foreground max-w-[40%] truncate text-[10px]">{measure}</span>
      )}
    </div>
  );
}
