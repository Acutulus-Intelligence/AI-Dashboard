import { useCallback, useEffect, useState } from 'react';
import {
  CheckCircle,
  ChevronDown,
  Database,
  Plus,
  RefreshCw,
  Table,
  Trash2,
  XCircle,
} from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import AppShell from '../layouts/AppShell';
import ConfirmDialog from '../components/ConfirmDialog';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';
import {
  getConnections,
  createConnection,
  deleteConnection,
  testConnection,
  getTables,
  type ConnectionResponse,
  type CreateConnectionRequest,
  type TableInfo,
} from '../../services/connectionsApi';

const DB_PROVIDERS = [
  { value: 'PostgreSql', label: 'PostgreSQL' },
  { value: 'MySql', label: 'MySQL' },
];

function canManageConnections(user: { userType: number; companyRoleName?: string | null } | null | undefined) {
  return !user || user.userType !== 1 || user.companyRoleName === 'Owner';
}

export default function ConnectionsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const allowed = canManageConnections(user);
  const isCompany = user?.userType === 1;

  const [connections, setConnections] = useState<ConnectionResponse[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<CreateConnectionRequest>({
    name: '',
    dbProvider: 'PostgreSql',
    host: '',
    port: 5432,
    database: '',
    username: '',
    password: '',
  });
  const [error, setError] = useState('');
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [tables, setTables] = useState<TableInfo[]>([]);
  const [tablesLoading, setTablesLoading] = useState(false);
  const [selectedTable, setSelectedTable] = useState<string | null>(null);
  const [testingAll, setTestingAll] = useState(false);
  const [initialLoading, setInitialLoading] = useState(true);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [deleting, setDeleting] = useState(false);

  const expandConnection = useCallback(async (id: string) => {
    setExpandedId(id);
    setSelectedTable(null);
    setTablesLoading(true);
    try {
      const t = await getTables(id);
      setTables(t);
    } catch {
      setTables([]);
    } finally {
      setTablesLoading(false);
    }
  }, []);

  const testAll = useCallback(async (list: ConnectionResponse[], showToast: boolean) => {
    if (list.length === 0) {
      if (showToast) toast.message('No database connections to test.');
      return;
    }
    setTestingAll(true);
    try {
      await Promise.allSettled(list.map((c) => testConnection(c.id)));
      const refreshed = await getConnections();
      setConnections(refreshed);
      if (showToast) toast.success('All database connections tested.');
    } catch {
      if (showToast) toast.error('Could not test all connections.');
    } finally {
      setTestingAll(false);
    }
  }, []);

  useEffect(() => {
    if (!allowed) {
      navigate(ROUTES.DASHBOARD, { replace: true });
      return;
    }

    let cancelled = false;
    (async () => {
      setInitialLoading(true);
      try {
        const list = await getConnections();
        if (cancelled) return;
        setConnections(list);
        await testAll(list, false);
        if (cancelled) return;

        const expandId = (location.state as { expandConnectionId?: string } | null)?.expandConnectionId;
        if (expandId) {
          await expandConnection(expandId);
          navigate(location.pathname, { replace: true, state: null });
        }
      } catch {
        if (!cancelled) setConnections([]);
      } finally {
        if (!cancelled) setInitialLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
    // Only gate + initial mount; expand from navigation state.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [allowed]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      const created = await createConnection(form);
      setShowForm(false);
      setForm({
        name: '',
        dbProvider: 'PostgreSql',
        host: '',
        port: 5432,
        database: '',
        username: '',
        password: '',
      });
      try {
        await testConnection(created.id);
      } catch {
        /* verification badge updates on reload */
      }
      const list = await getConnections();
      setConnections(list);
      toast.success('Connection added and tested.');
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create connection');
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      const id = deleteTarget.id;
      await deleteConnection(id);
      if (expandedId === id) {
        setExpandedId(null);
        setTables([]);
        setSelectedTable(null);
      }
      setConnections(await getConnections());
      setDeleteTarget(null);
    } finally {
      setDeleting(false);
    }
  };

  const toggleExpand = async (id: string) => {
    if (expandedId === id) {
      setExpandedId(null);
      setTables([]);
      setSelectedTable(null);
      return;
    }
    await expandConnection(id);
  };

  function toggleTable(tableName: string) {
    setSelectedTable((prev) => (prev === tableName ? null : tableName));
  }

  if (!allowed) return null;

  const breadcrumbs = isCompany
    ? [
        { label: 'Administration', to: ROUTES.ADMIN },
        { label: 'Connections' },
      ]
    : [
        { label: 'Settings', to: ROUTES.SETTINGS },
        { label: 'Connections' },
      ];

  return (
    <AppShell breadcrumbs={breadcrumbs}>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Database connections</h1>
          <p className="text-muted-foreground text-sm">
            Connect a PostgreSQL or MySQL database to build charts from.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="icon"
            disabled={testingAll || initialLoading || connections.length === 0}
            onClick={() => void testAll(connections, true)}
            aria-label="Test all connections"
          >
            <RefreshCw className={testingAll ? 'animate-spin' : undefined} />
          </Button>
          <Button type="button" onClick={() => setShowForm(!showForm)}>
            <Plus /> Add connection
          </Button>
        </div>
      </div>

      {showForm && (
        <form
          onSubmit={handleCreate}
          className="border-border bg-card rounded-xl border p-6"
        >
          <h2 className="mb-4 text-lg font-semibold">New connection</h2>
          {error && (
            <div className="bg-destructive/10 text-destructive mb-3 rounded-lg p-3 text-sm">{error}</div>
          )}
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <input
              placeholder="Connection name"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              required
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            />
            <select
              value={form.dbProvider}
              onChange={(e) => setForm({ ...form, dbProvider: e.target.value })}
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            >
              {DB_PROVIDERS.map((p) => (
                <option key={p.value} value={p.value}>
                  {p.label}
                </option>
              ))}
            </select>
            <input
              placeholder="Host"
              value={form.host}
              onChange={(e) => setForm({ ...form, host: e.target.value })}
              required
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            />
            <input
              type="number"
              placeholder="Port"
              value={form.port}
              onChange={(e) => setForm({ ...form, port: Number(e.target.value) })}
              required
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            />
            <input
              placeholder="Database"
              value={form.database}
              onChange={(e) => setForm({ ...form, database: e.target.value })}
              required
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            />
            <input
              placeholder="Username"
              value={form.username}
              onChange={(e) => setForm({ ...form, username: e.target.value })}
              required
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            />
            <input
              type="password"
              placeholder="Password"
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              required
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            />
          </div>
          <div className="mt-4 flex gap-3">
            <Button type="submit">Save</Button>
            <Button type="button" variant="outline" onClick={() => setShowForm(false)}>
              Cancel
            </Button>
          </div>
        </form>
      )}

      <div className="space-y-3">
        {initialLoading && (
          <p className="text-muted-foreground text-sm">Loading connections…</p>
        )}

        {connections.map((conn) => (
          <div key={conn.id} className="border-border bg-card rounded-xl border">
            <div className="flex items-stretch">
              <button
                type="button"
                onClick={() => void toggleExpand(conn.id)}
                aria-expanded={expandedId === conn.id}
                className="hover:bg-muted/50 flex min-w-0 flex-1 cursor-pointer items-center justify-between gap-3 rounded-l-xl p-4 text-left transition-colors"
              >
                <div className="flex min-w-0 items-center gap-3">
                  <Database className="text-brand size-5 shrink-0" />
                  <div className="min-w-0">
                    <span className="font-semibold">{conn.name}</span>
                    <span className="text-muted-foreground ml-2 text-sm">
                      ({conn.dbProvider === 'PostgreSql' ? 'PostgreSQL' : 'MySQL'})
                    </span>
                  </div>
                  {conn.isVerified ? (
                    <CheckCircle className="size-4 shrink-0 text-green-600" />
                  ) : (
                    <XCircle className="size-4 shrink-0 text-red-400" />
                  )}
                </div>
                <span className="text-muted-foreground flex shrink-0 items-center gap-1.5 text-sm">
                  <Table className="size-4" />
                  Tables
                  <ChevronDown
                    className={cn(
                      'size-4 transition-transform',
                      expandedId === conn.id && 'rotate-180',
                    )}
                  />
                </span>
              </button>
              <button
                type="button"
                onClick={() => setDeleteTarget({ id: conn.id, name: conn.name })}
                aria-label={`Delete ${conn.name}`}
                className="text-muted-foreground hover:bg-muted hover:text-destructive flex w-12 cursor-pointer items-center justify-center rounded-r-xl border-l transition-colors"
              >
                <Trash2 className="size-4" />
              </button>
            </div>

            {expandedId === conn.id && (
              <div className="border-border border-t p-4">
                {tablesLoading ? (
                  <div className="text-muted-foreground text-sm">Loading tables…</div>
                ) : (
                  <div className="space-y-1">
                    {tables.map((t) => {
                      const open = selectedTable === t.tableName;
                      return (
                        <div
                          key={t.tableName}
                          className={cn(
                            'rounded-lg border transition-colors',
                            open ? 'border-border bg-muted/40' : 'border-transparent',
                          )}
                        >
                          <button
                            type="button"
                            onClick={() => toggleTable(t.tableName)}
                            aria-expanded={open}
                            className="hover:bg-muted flex w-full cursor-pointer items-center justify-between rounded-lg px-3 py-2.5 text-left text-sm"
                          >
                            <div className="flex items-center gap-2">
                              <Table className="text-brand size-4 shrink-0" />
                              <span className="font-medium">{t.tableName}</span>
                              <span className="text-muted-foreground">
                                ({t.columns.length} columns)
                              </span>
                            </div>
                            <ChevronDown
                              className={cn(
                                'text-muted-foreground size-4 shrink-0 transition-transform',
                                open && 'rotate-180',
                              )}
                            />
                          </button>
                          {open && (
                            <div className="border-border border-t px-3 py-2">
                              <table className="w-full text-left text-sm">
                                <thead>
                                  <tr className="text-muted-foreground border-b">
                                    <th className="pb-1.5 pr-3 font-medium">Column</th>
                                    <th className="pb-1.5 pr-3 font-medium">Type</th>
                                    <th className="pb-1.5 font-medium">Nullable</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {t.columns.map((col) => (
                                    <tr key={col.columnName} className="border-b last:border-0">
                                      <td className="py-1.5 pr-3 font-medium">{col.columnName}</td>
                                      <td className="text-muted-foreground py-1.5 pr-3">
                                        {col.dataType}
                                      </td>
                                      <td className="text-muted-foreground py-1.5">
                                        {col.isNullable ? 'Yes' : 'No'}
                                      </td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          )}
                        </div>
                      );
                    })}
                    {tables.length === 0 && (
                      <div className="text-muted-foreground text-sm">No tables found.</div>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        ))}

        {!initialLoading && connections.length === 0 && !showForm && (
          <div className="border-border text-muted-foreground rounded-xl border border-dashed p-12 text-center">
            <Database className="mx-auto mb-3 size-10 opacity-40" />
            <p className="text-sm">No connections yet. Click &quot;Add connection&quot; to get started.</p>
          </div>
        )}
      </div>

      <ConfirmDialog
        open={!!deleteTarget}
        onOpenChange={(open) => {
          if (!open && !deleting) setDeleteTarget(null);
        }}
        title="Delete connection?"
        description={
          deleteTarget
            ? `This permanently removes “${deleteTarget.name}”. Charts that use this database may stop working.`
            : 'This permanently removes the connection.'
        }
        confirmLabel="Delete"
        variant="destructive"
        loading={deleting}
        onConfirm={handleDelete}
      />
    </AppShell>
  );
}
