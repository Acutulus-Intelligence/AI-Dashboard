import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2, LayoutDashboard } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import {
  getDashboard,
  saveWidgets,
  WIDGET_TYPE,
  type DashboardResponse,
  type WidgetItem,
} from '../../lib/api/dashboards';
import { ROUTES } from '../routes';

interface AddToDashboardDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  savedChartId: string;
}

function toWidgetItems(dashboard: DashboardResponse): WidgetItem[] {
  return dashboard.widgets.map((w) => {
    const base = {
      id: w.id,
      positionX: w.positionX,
      positionY: w.positionY,
      width: w.width,
      height: w.height,
    };
    if (w.widgetType === WIDGET_TYPE.Text) {
      return {
        ...base,
        widgetType: WIDGET_TYPE.Text,
        textContent: w.textContent,
        textVariant: w.textVariant,
        textHorizontalAlign: w.textHorizontalAlign,
        textVerticalAlign: w.textVerticalAlign,
      };
    }
    return {
      ...base,
      widgetType: WIDGET_TYPE.Chart,
      savedChartId: w.savedChartId,
    };
  });
}

export default function AddToDashboardDialog({
  open,
  onOpenChange,
  savedChartId,
}: AddToDashboardDialogProps) {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [adding, setAdding] = useState(false);
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setDashboard(null);
    setSelectedId(null);
    getDashboard()
      .then((d) => {
        if (cancelled) return;
        setDashboard(d);
        setSelectedId(d.id);
      })
      .catch(() => {
        if (!cancelled) toast.error('Could not load dashboards.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open]);

  async function handleAdd() {
    if (!dashboard || !selectedId) return;
    setAdding(true);
    try {
      const current = await getDashboard();
      const existing = toWidgetItems(current);
      const maxBottom = existing.reduce(
        (max, w) => Math.max(max, w.positionY + w.height),
        0,
      );
      const next: WidgetItem = {
        widgetType: WIDGET_TYPE.Chart,
        savedChartId,
        positionX: 0,
        positionY: maxBottom,
        width: 6,
        height: 6,
      };
      await saveWidgets([...existing, next]);
      toast.success(`Added to “${current.name}”.`, {
        action: {
          label: 'Open dashboard',
          onClick: () => navigate(ROUTES.DASHBOARD),
        },
      });
      onOpenChange(false);
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Could not add chart to dashboard.');
    } finally {
      setAdding(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Add to dashboard</DialogTitle>
          <DialogDescription>Choose which dashboard should show this chart.</DialogDescription>
        </DialogHeader>

        <div className="grid gap-2 py-1">
          {loading && (
            <p className="text-muted-foreground flex items-center gap-2 text-sm">
              <Loader2 className="size-4 animate-spin" />
              Loading dashboards…
            </p>
          )}
          {!loading && dashboard && (
            <button
              type="button"
              onClick={() => setSelectedId(dashboard.id)}
              className={cn(
                'flex w-full cursor-pointer items-center gap-3 rounded-lg border px-3 py-3 text-left transition-colors',
                selectedId === dashboard.id
                  ? 'border-primary bg-primary/5'
                  : 'border-border hover:bg-muted',
              )}
            >
              <div className="bg-primary/10 text-primary flex size-9 items-center justify-center rounded-lg">
                <LayoutDashboard className="size-4" />
              </div>
              <div className="min-w-0">
                <p className="truncate font-medium">{dashboard.name}</p>
                <p className="text-muted-foreground text-xs">
                  {dashboard.widgets.length} widget
                  {dashboard.widgets.length === 1 ? '' : 's'}
                </p>
              </div>
            </button>
          )}
          {!loading && !dashboard && (
            <p className="text-muted-foreground text-sm">No dashboards available.</p>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={adding}>
            Cancel
          </Button>
          <Button
            type="button"
            onClick={() => void handleAdd()}
            disabled={adding || loading || !selectedId}
          >
            {adding && <Loader2 className="animate-spin" />}
            Add
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
