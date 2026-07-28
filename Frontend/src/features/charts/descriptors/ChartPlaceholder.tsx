import type { LucideIcon } from 'lucide-react';

export default function ChartPlaceholder({ icon: Icon, label }: { icon: LucideIcon; label: string }) {
  return (
    <div className="text-muted-foreground flex h-full w-full items-center justify-center">
      <div className="text-center">
        <Icon className="text-muted-foreground/40 mx-auto mb-2 size-10" />
        <p className="text-sm font-medium">{label}</p>
        <p className="text-xs">No data to display</p>
      </div>
    </div>
  );
}
