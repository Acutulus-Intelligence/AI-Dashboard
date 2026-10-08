import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Folder,
  FolderInput,
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
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { deleteChart, getCharts, type ChartResponse } from '../../lib/api/charts';
import {
  createChartFolder,
  deleteChartFolder,
  getChartFolders,
  moveChartToFolder,
  renameChartFolder,
  type ChartFolderResponse,
} from '../../lib/api/chartFolders';
import { get } from '../charts/registry';
import AddToDashboardDialog from '../components/AddToDashboardDialog';
import ChartFolderSidebar, {
  ALL_FOLDER,
  UNFILED_FOLDER,
} from '../components/ChartFolderSidebar';
import ConfirmDialog from '../components/ConfirmDialog';
import FolderNameDialog from '../components/FolderNameDialog';
import AppShell from '../layouts/AppShell';
import { ROUTES, graphEditPath } from '../routes';

function formatCreatedAt(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

type FolderDialog = { mode: 'create' } | { mode: 'rename'; folder: ChartFolderResponse };

export default function ChartsPage() {
  const navigate = useNavigate();
  const [charts, setCharts] = useState<ChartResponse[]>([]);
  const [folders, setFolders] = useState<ChartFolderResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [query, setQuery] = useState('');
  const [selectedFolder, setSelectedFolder] = useState<string>(ALL_FOLDER);
  const [deleteTarget, setDeleteTarget] = useState<ChartResponse | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [addToDashboardId, setAddToDashboardId] = useState<string | null>(null);

  const [folderDialog, setFolderDialog] = useState<FolderDialog | null>(null);
  const [folderSaving, setFolderSaving] = useState(false);
  const [folderDeleteTarget, setFolderDeleteTarget] = useState<ChartFolderResponse | null>(null);
  const [folderDeleting, setFolderDeleting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError('');
      try {
        const [chartList, folderList] = await Promise.all([
          getCharts(),
          getChartFolders().catch(() => [] as ChartFolderResponse[]),
        ]);
        if (cancelled) return;
        setCharts(chartList);
        setFolders([...folderList].sort((a, b) => a.name.localeCompare(b.name)));
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

  const folderCounts = useMemo(() => {
    const map = new Map<string, number>();
    for (const chart of charts) {
      if (chart.folderId) map.set(chart.folderId, (map.get(chart.folderId) ?? 0) + 1);
    }
    return map;
  }, [charts]);

  const unfiledCount = useMemo(
    () => charts.filter((chart) => !chart.folderId).length,
    [charts],
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    let list = [...charts].sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    );
    if (selectedFolder === UNFILED_FOLDER) {
      list = list.filter((c) => !c.folderId);
    } else if (selectedFolder !== ALL_FOLDER) {
      list = list.filter((c) => c.folderId === selectedFolder);
    }
    if (!q) return list;
    return list.filter(
      (c) =>
        c.title.toLowerCase().includes(q) || c.chartType.toLowerCase().includes(q),
    );
  }, [charts, query, selectedFolder]);

  const folderNames = useMemo(() => {
    const map = new Map<string, string>();
    for (const folder of folders) map.set(folder.id, folder.name);
    return map;
  }, [folders]);

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

  async function handleMove(chartId: string, folderId: string | null) {
    try {
      await moveChartToFolder(chartId, folderId);
      setCharts((prev) => prev.map((c) => (c.id === chartId ? { ...c, folderId } : c)));
      toast.success(folderId ? 'Chart moved to folder.' : 'Chart moved to Unfiled.');
    } catch {
      toast.error('Could not move chart.');
    }
  }

  async function handleFolderSubmit(name: string) {
    if (!folderDialog) return;
    setFolderSaving(true);
    try {
      if (folderDialog.mode === 'create') {
        const created = await createChartFolder({ name });
        setFolders((prev) => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
        setSelectedFolder(created.id);
        toast.success('Folder created.');
      } else {
        const updated = await renameChartFolder(folderDialog.folder.id, { name });
        setFolders((prev) =>
          prev
            .map((f) => (f.id === updated.id ? { ...f, name: updated.name } : f))
            .sort((a, b) => a.name.localeCompare(b.name)),
        );
        toast.success('Folder renamed.');
      }
      setFolderDialog(null);
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Could not save folder.');
    } finally {
      setFolderSaving(false);
    }
  }

  async function handleFolderDelete() {
    if (!folderDeleteTarget) return;
    setFolderDeleting(true);
    try {
      await deleteChartFolder(folderDeleteTarget.id);
      setFolders((prev) => prev.filter((f) => f.id !== folderDeleteTarget.id));
      setCharts((prev) =>
        prev.map((c) => (c.folderId === folderDeleteTarget.id ? { ...c, folderId: null } : c)),
      );
      if (selectedFolder === folderDeleteTarget.id) setSelectedFolder(ALL_FOLDER);
      toast.success('Folder deleted. Its charts are now unfiled.');
      setFolderDeleteTarget(null);
    } catch {
      toast.error('Could not delete folder.');
    } finally {
      setFolderDeleting(false);
    }
  }

  const emptyMessage = query.trim()
    ? 'No charts match your search.'
    : selectedFolder === UNFILED_FOLDER
      ? 'No unfiled charts.'
      : selectedFolder !== ALL_FOLDER
        ? 'No charts in this folder yet.'
        : 'No charts match your search.';

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

      <div className="flex flex-col gap-6 md:flex-row">
        <ChartFolderSidebar
          className="md:w-56 md:shrink-0"
          folders={folders}
          counts={folderCounts}
          totalCount={charts.length}
          unfiledCount={unfiledCount}
          selected={selectedFolder}
          onSelect={setSelectedFolder}
          onCreate={() => setFolderDialog({ mode: 'create' })}
          onRename={(folder) => setFolderDialog({ mode: 'rename', folder })}
          onDelete={setFolderDeleteTarget}
        />

        <div className="min-w-0 flex-1 space-y-4">
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
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
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
            <p className="text-muted-foreground text-sm">{emptyMessage}</p>
          )}

          {!loading && filtered.length > 0 && (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {filtered.map((chart) => {
                const descriptor = get(chart.chartType);
                const Icon = descriptor?.icon;
                const folderName = chart.folderId ? folderNames.get(chart.folderId) : undefined;
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
                          {folderName && (
                            <span className="text-muted-foreground inline-flex items-center gap-1 text-xs">
                              <Folder className="size-3" />
                              {folderName}
                            </span>
                          )}
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
                          <DropdownMenuSub>
                            <DropdownMenuSubTrigger>
                              <FolderInput />
                              Move to folder
                            </DropdownMenuSubTrigger>
                            <DropdownMenuSubContent>
                              <DropdownMenuRadioGroup
                                value={chart.folderId ?? '__none__'}
                                onValueChange={(value) =>
                                  void handleMove(chart.id, value === '__none__' ? null : value)
                                }
                              >
                                <DropdownMenuRadioItem value="__none__">
                                  No folder
                                </DropdownMenuRadioItem>
                                {folders.map((folder) => (
                                  <DropdownMenuRadioItem key={folder.id} value={folder.id}>
                                    {folder.name}
                                  </DropdownMenuRadioItem>
                                ))}
                              </DropdownMenuRadioGroup>
                            </DropdownMenuSubContent>
                          </DropdownMenuSub>
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
        </div>
      </div>

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

      <ConfirmDialog
        open={!!folderDeleteTarget}
        onOpenChange={(open) => {
          if (!open && !folderDeleting) setFolderDeleteTarget(null);
        }}
        title="Delete folder?"
        description={
          folderDeleteTarget
            ? `“${folderDeleteTarget.name}” will be deleted. Its charts are kept and become unfiled.`
            : ''
        }
        confirmLabel="Delete"
        variant="destructive"
        loading={folderDeleting}
        onConfirm={() => void handleFolderDelete()}
      />

      <FolderNameDialog
        open={folderDialog?.mode === 'create'}
        onOpenChange={(open) => {
          if (!open) setFolderDialog(null);
        }}
        title="New folder"
        submitLabel="Create"
        loading={folderSaving}
        onSubmit={handleFolderSubmit}
      />

      <FolderNameDialog
        open={folderDialog?.mode === 'rename'}
        onOpenChange={(open) => {
          if (!open) setFolderDialog(null);
        }}
        title="Rename folder"
        submitLabel="Save"
        initialName={folderDialog?.mode === 'rename' ? folderDialog.folder.name : ''}
        loading={folderSaving}
        onSubmit={handleFolderSubmit}
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
