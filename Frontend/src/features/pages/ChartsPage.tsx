import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  MoreHorizontal,
  Pencil,
  Plus,
  Search,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { deleteChart, getCharts, type ChartResponse } from '../../lib/api/charts';
import { get } from '../charts/registry';
import AddToDashboardDialog from '../components/AddToDashboardDialog';
import ConfirmDialog from '../components/ConfirmDialog';
import AppShell from '../layouts/AppShell';
import { ROUTES, graphEditPath } from '../routes';

function formatCreatedAt(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export default function ChartsPage() {
  const navigate = useNavigate();
  const [charts, setCharts] = useState<ChartResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [query, setQuery] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<ChartResponse | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [addToDashboardId, setAddToDashboardId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError('');
      try {
        const list = await getCharts();
        if (cancelled) return;
        setCharts(list);
      } catch {
        if (cancelled) return;
        setError('Could not load charts.');
        setCharts([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const sorted = [...charts].sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    );
    if (!q) return sorted;
    return sorted.filter(
      (c) =>
        c.title.toLowerCase().includes(q) || c.chartType.toLowerCase().includes(q),
    );
  }, [charts, query]);

  function openNewChart() {
    navigate(ROUTES.GRAPHS_NEW, { state: { fromCharts: true } });
  }

  function openEdit(id: string) {
    navigate(graphEditPath(id), { state: { fromCharts: true } });
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await deleteChart(deleteTarget.id);
      setCharts((prev) => prev.filter((c) => c.id !== deleteTarget.id));
      toast.success('Chart deleted.');
      setDeleteTarget(null);
    } catch {
      toast.error('Could not delete chart.');
    } finally {
      setDeleting(false);
    }
  }

  return (
    <AppShell breadcrumbs={[{ label: 'Charts' }]}>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Charts</h1>
          <p className="text-muted-foreground text-sm">
            Browse and manage your saved charts. Add them to a dashboard when you need them.
          </p>
        </div>
        <Button type="button" onClick={openNewChart}>
          <Plus />
          New chart
        </Button>
      </div>

      <div className="relative max-w-md">
        <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
        <Input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search by title or type…"
          className="pl-9"
          aria-label="Search charts"
        />
      </div>

      {error && (
        <div className="border-destructive/40 bg-destructive/10 text-destructive rounded-lg border px-3 py-2 text-sm">
          {error}
        </div>
      )}

      {loading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Card key={i}>
              <CardHeader>
                <Skeleton className="h-5 w-2/3" />
                <Skeleton className="h-4 w-1/3" />
              </CardHeader>
              <CardContent>
                <Skeleton className="h-4 w-1/2" />
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {!loading && !error && charts.length === 0 && (
        <Card>
          <CardHeader>
            <CardTitle>No charts yet</CardTitle>
            <CardDescription>
              Create your first chart from a connected database, then add it to a dashboard.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button type="button" onClick={openNewChart}>
              <Plus />
              New chart
            </Button>
          </CardContent>
        </Card>
      )}

      {!loading && charts.length > 0 && filtered.length === 0 && (
        <p className="text-muted-foreground text-sm">No charts match your search.</p>
      )}

      {!loading && filtered.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map((chart) => {
            const descriptor = get(chart.chartType);
            const Icon = descriptor?.icon;
            return (
              <Card key={chart.id} className="flex flex-col">
                <CardHeader className="flex-row items-start justify-between gap-2 space-y-0">
                  <div className="min-w-0 flex-1 space-y-1.5">
                    <div className="flex items-center gap-2">
                      {Icon ? (
                        <Icon className="text-muted-foreground size-4 shrink-0" />
                      ) : null}
                      <CardTitle className="truncate text-base">{chart.title}</CardTitle>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="secondary" className="capitalize">
                        {chart.chartType}
                      </Badge>
                      <CardDescription>{formatCreatedAt(chart.createdAt)}</CardDescription>
                    </div>
                  </div>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="size-8 shrink-0"
                        aria-label={`Actions for ${chart.title}`}
                      >
                        <MoreHorizontal />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem onClick={() => openEdit(chart.id)}>
                        <Pencil />
                        Edit
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => setAddToDashboardId(chart.id)}>
                        <LayoutDashboard />
                        Add to dashboard
                      </DropdownMenuItem>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem
                        variant="destructive"
                        onClick={() => setDeleteTarget(chart)}
                      >
                        <Trash2 />
                        Delete
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </CardHeader>
              </Card>
            );
          })}
        </div>
      )}

      <ConfirmDialog
        open={!!deleteTarget}
        onOpenChange={(open) => {
          if (!open && !deleting) setDeleteTarget(null);
        }}
        title="Delete chart?"
        description={
          deleteTarget
            ? `“${deleteTarget.title}” will be removed. Dashboards that use it will lose that widget.`
            : ''
        }
        confirmLabel="Delete"
        variant="destructive"
        loading={deleting}
        onConfirm={() => void handleDelete()}
      />

      {addToDashboardId && (
        <AddToDashboardDialog
          open={!!addToDashboardId}
          onOpenChange={(open) => {
            if (!open) setAddToDashboardId(null);
          }}
          savedChartId={addToDashboardId}
        />
      )}
    </AppShell>
  );
}
