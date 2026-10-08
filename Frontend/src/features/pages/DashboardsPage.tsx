import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Loader2,
  MoreHorizontal,
  Pencil,
  PlusCircle,
  Trash2,
  UserPlus,
} from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import AppShell from '../layouts/AppShell';
import ConfirmDialog from '../components/ConfirmDialog';
import DashboardFormDialog from '../components/DashboardFormDialog';
import TransferDashboardDialog from '../components/TransferDashboardDialog';
import { ROUTES, dashboardPath } from '../routes';
import { useAuth } from '../store/useAuth';
import {
  createDashboard,
  deleteDashboard,
  getDashboards,
  renameDashboard,
  type DashboardSummary,
} from '../../lib/api/dashboards';

type FormMode = 'create' | 'rename';

export default function DashboardsPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isCompany = user?.userType === 1;

  const [dashboards, setDashboards] = useState<DashboardSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [active, setActive] = useState<DashboardSummary | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DashboardSummary | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [transferTarget, setTransferTarget] = useState<DashboardSummary | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setDashboards(await getDashboards());
    } catch {
      toast.error('Could not load dashboards.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function openCreate() {
    setFormMode('create');
    setActive(null);
    setFormOpen(true);
  }

  function openRename(dashboard: DashboardSummary) {
    setFormMode('rename');
    setActive(dashboard);
    setFormOpen(true);
  }

  async function handleSubmit(name: string) {
    try {
      if (formMode === 'create') {
        const created = await createDashboard(name);
        toast.success(`Created “${created.name}”.`);
        navigate(dashboardPath(created.id));
      } else if (active) {
        await renameDashboard(active.id, name);
        toast.success('Dashboard renamed.');
        await load();
      }
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Could not save dashboard.');
      throw err;
    }
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await deleteDashboard(deleteTarget.id);
      toast.success('Dashboard deleted.');
      setDeleteTarget(null);
      await load();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Could not delete dashboard.');
    } finally {
      setDeleting(false);
    }
  }

  return (
    <>
      <AppShell
        breadcrumbs={[{ label: 'Dashboards' }]}
        onNewChart={() => navigate(ROUTES.GRAPHS_NEW)}
        onNewDashboard={openCreate}
      >
        {loading ? (
          <div className="text-muted-foreground flex items-center gap-2 text-sm">
            <Loader2 className="size-4 animate-spin" />
            Loading dashboards…
          </div>
        ) : dashboards.length === 0 ? (
          <div className="border-border flex min-h-[360px] flex-col items-center justify-center gap-4 rounded-xl border border-dashed p-8 text-center">
            <div className="bg-muted flex size-12 items-center justify-center rounded-full">
              <PlusCircle className="text-muted-foreground size-6" />
            </div>
            <div className="max-w-sm">
              <h2 className="text-lg font-semibold">No dashboards yet</h2>
              <p className="text-muted-foreground mt-1 text-sm">
                Create your first dashboard to start arranging charts.
              </p>
            </div>
            <Button onClick={openCreate}>
              <PlusCircle />
              Create dashboard
            </Button>
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {dashboards.map((dashboard) => (
              <Card
                key={dashboard.id}
                className="hover:border-primary/40 cursor-pointer transition-colors"
                onClick={() => navigate(dashboardPath(dashboard.id))}
              >
                <CardHeader>
                  <div className="bg-primary/10 text-primary flex size-9 items-center justify-center rounded-lg">
                    <LayoutDashboard className="size-4" />
                  </div>
                  <CardTitle className="truncate text-base">{dashboard.name}</CardTitle>
                  <div className="ml-auto" onClick={(e) => e.stopPropagation()}>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon-sm">
                          <MoreHorizontal />
                          <span className="sr-only">Dashboard actions</span>
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onSelect={() => navigate(dashboardPath(dashboard.id))}>
                          <LayoutDashboard />
                          Open
                        </DropdownMenuItem>
                        <DropdownMenuItem onSelect={() => openRename(dashboard)}>
                          <Pencil />
                          Rename
                        </DropdownMenuItem>
                        {isCompany && (
                          <DropdownMenuItem onSelect={() => setTransferTarget(dashboard)}>
                            <UserPlus />
                            Transfer ownership
                          </DropdownMenuItem>
                        )}
                        <DropdownMenuItem variant="destructive" onSelect={() => setDeleteTarget(dashboard)}>
                          <Trash2 />
                          Delete
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </CardHeader>
                <CardContent className="text-muted-foreground text-sm">
                  {dashboard.widgetCount} widget
                  {dashboard.widgetCount === 1 ? '' : 's'}
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </AppShell>

      <DashboardFormDialog
        key={`${formMode}-${active?.id ?? 'new'}-${formOpen}`}
        open={formOpen}
        onOpenChange={setFormOpen}
        title={formMode === 'create' ? 'New dashboard' : 'Rename dashboard'}
        description={formMode === 'create' ? 'Give your dashboard a name.' : undefined}
        submitLabel={formMode === 'create' ? 'Create' : 'Save'}
        initialName={formMode === 'rename' ? active?.name ?? '' : ''}
        onSubmit={handleSubmit}
      />

      <ConfirmDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title={`Delete “${deleteTarget?.name ?? ''}”?`}
        description="This permanently deletes the dashboard and its widgets. Saved charts are not deleted."
        confirmLabel="Delete"
        variant="destructive"
        loading={deleting}
        onConfirm={handleDelete}
      />

      {transferTarget && (
        <TransferDashboardDialog
          open={transferTarget !== null}
          onOpenChange={(open) => !open && setTransferTarget(null)}
          dashboardId={transferTarget.id}
          dashboardName={transferTarget.name}
          onTransferred={() => void load()}
        />
      )}
    </>
  );
}
