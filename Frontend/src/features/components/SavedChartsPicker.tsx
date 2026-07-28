import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from '@/components/ui/command';
import { Skeleton } from '@/components/ui/skeleton';
import { getCharts, type ChartResponse } from '../../lib/api/charts';
import { get } from '../charts/registry';
import { ROUTES } from '../routes';

interface SavedChartsPickerProps {
  open: boolean;
  onClose: () => void;
  onSelect: (savedChartId: string) => void;
}

export default function SavedChartsPicker({ open, onClose, onSelect }: SavedChartsPickerProps) {
  const navigate = useNavigate();
  const [charts, setCharts] = useState<ChartResponse[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) return;
    setLoading(true);
    getCharts()
      .then(setCharts)
      .catch(() => setCharts([]))
      .finally(() => setLoading(false));
  }, [open]);

  return (
    <CommandDialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
      title="Saved charts"
      description="Search and add a chart to the dashboard"
      showCloseButton
    >
      <CommandInput placeholder="Search saved charts…" />
      <CommandList>
        {loading ? (
          <div className="space-y-2 p-3">
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-8 w-3/4" />
            <Skeleton className="h-8 w-5/6" />
          </div>
        ) : (
          <>
            <CommandEmpty>No charts found.</CommandEmpty>

            <CommandGroup heading="Create">
              <CommandItem
                onSelect={() => {
                  onClose();
                  navigate(ROUTES.GRAPHS_NEW);
                }}
              >
                <Plus />
                New chart
              </CommandItem>
            </CommandGroup>

            {charts.length > 0 && (
              <>
                <CommandSeparator />
                <CommandGroup heading="Saved">
                  {charts.map((chart) => {
                    const Icon = get(chart.chartType)?.icon;
                    return (
                      <CommandItem
                        key={chart.id}
                        value={`${chart.title} ${chart.chartType}`}
                        onSelect={() => {
                          onSelect(chart.id);
                          onClose();
                        }}
                      >
                        {Icon ? <Icon /> : null}
                        <div className="min-w-0 flex-1">
                          <p className="truncate">{chart.title}</p>
                          <p className="text-muted-foreground text-xs capitalize">
                            {chart.chartType} · {new Date(chart.createdAt).toLocaleDateString()}
                          </p>
                        </div>
                      </CommandItem>
                    );
                  })}
                </CommandGroup>
              </>
            )}
          </>
        )}
      </CommandList>
    </CommandDialog>
  );
}
