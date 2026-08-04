import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  CheckCircle,
  ChevronDown,
  ClipboardPaste,
  Database,
  Eye,
  EyeOff,
  Globe,
  Lock,
  Pencil,
  Plus,
  RefreshCw,
  Table,
  Trash2,
  Users,
  XCircle,
} from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  getCompanyUsers,
  getMyCompany,
  type CompanyResponse,
  type CompanyRoleResponse,
  type CompanyUserResponse,
} from '@/lib/api/company';
import AppShell from '../layouts/AppShell';
import ConfirmDialog from '../components/ConfirmDialog';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';
import {
  createConnection,
  deleteConnection,
  getConnectionConfig,
  getConnectionsWithCount,
  getTables,
  parseConnectionString,
  testConnection,
  updateConnection,
  type ConnectionResponse,
  type ConnectionVisibility,
  type CreateConnectionRequest,
  type SslMode,
  type TableInfo,
  type UpdateConnectionRequest,
} from '../../services/connectionsApi';

const DB_PROVIDERS = [
  { value: 'PostgreSql', label: 'PostgreSQL' },
  { value: 'MySql', label: 'MySQL' },
  { value: 'SqlServer', label: 'SQL Server' },
  { value: 'Sqlite', label: 'SQLite' },
];

const DEFAULT_PORTS: Record<string, number> = {
  PostgreSql: 5432,
  MySql: 3306,
  SqlServer: 1433,
  Sqlite: 0,
};

const SSL_MODES: { value: SslMode; label: string; hint: string }[] = [
  { value: 'Prefer', label: 'Prefer SSL', hint: 'Encrypt if the server supports it (default)' },
  { value: 'Require', label: 'Require SSL', hint: 'Reject the connection without encryption' },
  { value: 'VerifyFull', label: 'Verify full', hint: 'Encrypt and verify the server certificate' },
  { value: 'None', label: 'No SSL', hint: 'Connect without encryption' },
];

const IS_SQLITE = (provider: string) => provider === 'Sqlite';

const CONNECTION_CREDENTIALS_RE = /^([a-z][a-z0-9+.-]*:\/\/[^:\/@]*:)([^@\/]*)(@)/i;
const PASSWORD_PAIR_RE = /\b((?:password|pwd)\s*=\s*)([^;]*)/gi;

function maskConnectionString(value: string): string {
  if (!value) return value;
  const uriMasked = value.replace(CONNECTION_CREDENTIALS_RE, '$1****$3');
  return uriMasked.replace(PASSWORD_PAIR_RE, (_match, prefix, pwd) =>
    pwd.trim() ? `${prefix}****` : _match,
  );
}

function unmaskConnectionString(masked: string, previousRaw: string): string {
  if (!previousRaw) return masked;
  const uriPrev = previousRaw.match(CONNECTION_CREDENTIALS_RE);
  if (uriPrev) {
    const password = uriPrev[2];
    return masked.replace(/^([a-z][a-z0-9+.-]*:\/\/[^:\/@]*:)\*+(@)/i, `$1${password}$2`);
  }
  const pairPrev = previousRaw.match(/\b(?:password|pwd)\s*=\s*([^;]*)/i);
  if (pairPrev) {
    const password = pairPrev[1];
    return masked.replace(/\b((?:password|pwd)\s*=\s*)\*+/gi, `$1${password}`);
  }
  return masked;
}

function providerLabel(dbProvider: string): string {
  switch (dbProvider) {
    case 'PostgreSql':
      return 'PostgreSQL';
    case 'MySql':
      return 'MySQL';
    case 'SqlServer':
      return 'SQL Server';
    case 'Sqlite':
      return 'SQLite';
    default:
      return dbProvider;
  }
}

const VISIBILITY_OPTIONS: { value: ConnectionVisibility; label: string; hint: string }[] = [
  { value: 'Company', label: 'Entire company', hint: 'Visible to every member' },
  { value: 'Roles', label: 'Specific roles', hint: 'Visible to selected roles only' },
  { value: 'Private', label: 'Only me', hint: 'Visible just to you (and the owner)' },
];

const MAX_COMPANY_CONNECTIONS = 5;
const MAX_INDIVIDUAL_CONNECTIONS = 1;

interface ConnectionFormState {
  name: string;
  dbProvider: string;
  host: string;
  port: number;
  database: string;
  username: string;
  password: string;
  sslMode: SslMode;
  visibility: ConnectionVisibility;
  allowedRoleIds: string[];
}

function emptyForm(): ConnectionFormState {
  return {
    name: '',
    dbProvider: 'PostgreSql',
    host: '',
    port: DEFAULT_PORTS.PostgreSql,
    database: '',
    username: '',
    password: '',
    sslMode: 'Prefer',
    visibility: 'Company',
    allowedRoleIds: [],
  };
}

function visibilityLabel(conn: ConnectionResponse, roles: CompanyRoleResponse[]): string {
  if (conn.visibility === 'Private') return 'Only me';
  if (conn.visibility === 'Company') return 'Company';
  const names = conn.allowedRoleIds
    .map((id) => roles.find((r) => r.id === id)?.name)
    .filter(Boolean);
  return names.length > 0 ? `Roles: ${names.join(', ')}` : 'Selected roles';
}

export default function ConnectionsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const isCompany = user?.userType === 1;

  const [connections, setConnections] = useState<ConnectionResponse[]>([]);
  const [totalCompanyConnections, setTotalCompanyConnections] = useState(0);
  const [company, setCompany] = useState<CompanyResponse | null>(null);
  const [users, setUsers] = useState<CompanyUserResponse[]>([]);
  const [roles, setRoles] = useState<CompanyRoleResponse[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<ConnectionFormState>(emptyForm());
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [tables, setTables] = useState<TableInfo[]>([]);
  const [tablesLoading, setTablesLoading] = useState(false);
  const [selectedTable, setSelectedTable] = useState<string | null>(null);
  const [testingAll, setTestingAll] = useState(false);
  const [initialLoading, setInitialLoading] = useState(true);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [pasteText, setPasteText] = useState('');
  const [parsing, setParsing] = useState(false);
  const lastPasteRawRef = useRef('');
  const [showPassword, setShowPassword] = useState(false);

  const isOwner = isCompany && company !== null && user?.userId === company.ownerId;
  const me = users.find((u) => u.id === user?.userId);
  const myRole = roles.find((r) => r.id === me?.roleId);
  const canManageConnections = !isCompany || isOwner || myRole?.canManageConnections === true;
  const shareableRoles = roles.filter((r) => !(r.isSystemRole && r.name === 'Owner'));

  const duplicateOf = useMemo(() => {
    const host = form.host.trim().toLowerCase().replace(/\.$/, '');
    const database = form.database.trim().toLowerCase();
    if (!host || !database) return null;
    return (
      connections.find(
        (c) =>
          c.id !== editingId &&
          c.host.trim().toLowerCase().replace(/\.$/, '') === host &&
          c.database.trim().toLowerCase() === database,
      ) ?? null
    );
  }, [connections, editingId, form.host, form.database]);

  const canManageConnection = useCallback(
    (conn: ConnectionResponse) => {
      if (!isCompany) return conn.createdById === user?.userId;
      if (conn.createdById === user?.userId) return true;
      if (isOwner) return true;
      return myRole?.canManageConnections === true && conn.visibility !== 'Private';
    },
    [isCompany, isOwner, myRole, user?.userId],
  );

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

  const refreshConnections = useCallback(async (): Promise<ConnectionResponse[]> => {
    const { connections: list, companyConnectionCount } = await getConnectionsWithCount();
    setConnections(list);
    setTotalCompanyConnections(companyConnectionCount);
    return list;
  }, []);

  const testAll = useCallback(async (list: ConnectionResponse[], showToast: boolean) => {
    if (list.length === 0) {
      if (showToast) toast.message('No database connections to test.');
      return;
    }
    setTestingAll(true);
    try {
      await Promise.allSettled(list.map((c) => testConnection(c.id)));
      await refreshConnections();
      if (showToast) toast.success('All database connections tested.');
    } catch {
      if (showToast) toast.error('Could not test all connections.');
    } finally {
      setTestingAll(false);
    }
  }, [refreshConnections]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setInitialLoading(true);
      try {
        const list = await refreshConnections();
        if (cancelled) return;

        if (isCompany && user?.userId) {
          const companyData = await getMyCompany();
          if (cancelled) return;
          setCompany(companyData);
          setRoles(companyData.roles);
          const usersData = await getCompanyUsers(companyData.id);
          if (cancelled) return;
          setUsers(usersData);
        }

        await testAll(list, false);
        if (cancelled) return;

        const expandId = (location.state as { expandConnectionId?: string } | null)?.expandConnectionId;
        if (expandId) {
          await expandConnection(expandId);
          navigate(location.pathname, { replace: true, state: null });
        }
      } catch {
        if (!cancelled) setLoadError('Could not load connections.');
      } finally {
        if (!cancelled) setInitialLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
    // Only gate + initial mount; expand from navigation state.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isCompany, user?.userId]);

  const handleParsePaste = async () => {
    const trimmed = pasteText.trim();
    if (!trimmed) return;
    setParsing(true);
    try {
      const parsed = await parseConnectionString(trimmed);
      setForm((prev) => ({
        ...prev,
        dbProvider: parsed.provider ?? prev.dbProvider,
        host: parsed.host,
        port: parsed.port > 0 ? parsed.port : prev.port,
        database: parsed.database,
        username: parsed.username,
        password: parsed.password,
      }));
      toast.success('Connection string parsed. Review the fields below.');
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Could not parse that connection string.');
    } finally {
      setParsing(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (editingId) {
      await handleEdit();
    } else {
      await handleCreate();
    }
  };

  const handleCreate = async () => {
    setFormError('');
    setSaving(true);
    try {
      const payload: CreateConnectionRequest = {
        name: form.name,
        dbProvider: form.dbProvider,
        host: form.host,
        port: form.port,
        database: form.database,
        username: form.username,
        password: form.password,
        sslMode: form.sslMode,
        visibility: isCompany ? form.visibility : 'Private',
        allowedRoleIds: form.visibility === 'Roles' ? form.allowedRoleIds : [],
      };
      const created = await createConnection(payload);
      setShowForm(false);
      setForm(emptyForm());
      try {
        await testConnection(created.id);
      } catch {
        /* verification badge updates on reload */
      }
      await refreshConnections();
      toast.success('Connection added and tested.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Failed to create connection');
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = async () => {
    if (!editingId) return;
    setFormError('');
    setSaving(true);
    try {
      const payload: UpdateConnectionRequest = {
        name: form.name,
        dbProvider: form.dbProvider,
        host: form.host,
        port: form.port,
        database: form.database,
        username: form.username,
        password: form.password || undefined,
        sslMode: form.sslMode,
        visibility: isCompany ? form.visibility : 'Private',
        allowedRoleIds: form.visibility === 'Roles' ? form.allowedRoleIds : [],
      };
      await updateConnection(editingId, payload);
      setEditingId(null);
      setForm(emptyForm());
      await refreshConnections();
      toast.success('Connection updated.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Failed to update connection');
    } finally {
      setSaving(false);
    }
  };

  const toggleCreateForm = () => {
    setEditingId(null);
    setForm(emptyForm());
    setFormError('');
    setPasteText('');
    setShowPassword(false);
    setShowForm((v) => !v);
  };

  const openEdit = async (conn: ConnectionResponse) => {
    try {
      const config = await getConnectionConfig(conn.id);
      setForm({
        name: config.name,
        dbProvider: config.dbProvider,
        host: config.host,
        port: config.port,
        database: config.database,
        username: config.username,
        password: config.password || '',
        sslMode: config.sslMode,
        visibility: config.visibility,
        allowedRoleIds: config.allowedRoleIds,
      });
      setEditingId(conn.id);
      setShowPassword(false);
      setPasteText('');
      setShowForm(false);
      setFormError('');
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch {
      toast.error('Could not load connection details.');
    }
  };

  const closeForm = () => {
    setEditingId(null);
    setShowForm(false);
    setForm(emptyForm());
    setFormError('');
    setPasteText('');
    setShowPassword(false);
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
      await refreshConnections();
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

  const toggleAllowedRole = (roleId: string) => {
    setForm((prev) => ({
      ...prev,
      allowedRoleIds: prev.allowedRoleIds.includes(roleId)
        ? prev.allowedRoleIds.filter((id) => id !== roleId)
        : [...prev.allowedRoleIds, roleId],
    }));
  };

  const breadcrumbs = isCompany
    ? [
        { label: 'Administration', to: ROUTES.ADMIN },
        { label: 'Connections' },
      ]
    : [
        { label: 'Settings', to: ROUTES.SETTINGS },
        { label: 'Connections' },
      ];

  const formOpen = showForm || editingId !== null;

  return (
    <AppShell breadcrumbs={breadcrumbs}>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Database connections</h1>
          <p className="text-muted-foreground text-sm">
            Connect a PostgreSQL, MySQL, SQL Server, or SQLite database to build charts from.
          </p>
          {canManageConnections && (
            <p className="text-muted-foreground mt-1 text-sm">
              {isCompany
                ? `${totalCompanyConnections}/${MAX_COMPANY_CONNECTIONS} company connections used`
                : `${connections.length}/${MAX_INDIVIDUAL_CONNECTIONS} connection used`}
            </p>
          )}
        </div>
        {canManageConnections && (
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
            <Button type="button" onClick={toggleCreateForm}>
              <Plus /> Add connection
            </Button>
          </div>
        )}
      </div>

      {loadError && (
        <div className="bg-destructive/10 text-destructive mt-4 rounded-lg p-3 text-sm">{loadError}</div>
      )}

      {formOpen && canManageConnections && (
        <form onSubmit={handleSubmit} className="border-border bg-card mt-4 rounded-xl border p-6">
          <h2 className="mb-4 text-lg font-semibold">
            {editingId ? 'Edit connection' : 'New connection'}
          </h2>
          {formError && (
            <div className="bg-destructive/10 text-destructive mb-3 rounded-lg p-3 text-sm">{formError}</div>
          )}

          <div className="border-border mb-4 rounded-lg border p-4">
              <p className="text-muted-foreground mb-2 text-sm font-medium">Or paste a connection string</p>
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                <div className="w-full max-w-lg min-w-0">
                  <textarea
                    rows={1}
                    autoComplete="off"
                    placeholder="postgres://user:pass@host:5432/dbname"
                    value={maskConnectionString(pasteText)}
                    onChange={(e) => {
                      const value = e.target.value;
                      const restored = unmaskConnectionString(value, lastPasteRawRef.current);
                      lastPasteRawRef.current = restored;
                      setPasteText(restored);
                    }}
                    className="border-input bg-background focus:border-ring w-full resize-none rounded-lg border px-3 py-2 text-sm outline-hidden"
                  />
                </div>
                <Button
                  type="button"
                  variant="outline"
                  className="h-auto shrink-0 self-stretch"
                  onClick={() => void handleParsePaste()}
                  disabled={parsing || !pasteText.trim()}
                >
                  <ClipboardPaste />
                  {parsing ? 'Parsing…' : 'Parse & fill'}
                </Button>
              </div>
            </div>

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
              onChange={(e) =>
                setForm({ ...form, dbProvider: e.target.value, port: DEFAULT_PORTS[e.target.value] ?? form.port })
              }
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            >
              {DB_PROVIDERS.map((p) => (
                <option key={p.value} value={p.value}>
                  {p.label}
                </option>
              ))}
            </select>
            {!IS_SQLITE(form.dbProvider) && (
              <>
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
              </>
            )}
            <input
              placeholder={IS_SQLITE(form.dbProvider) ? 'Database file path (e.g. C:\\data\\db.sqlite)' : 'Database'}
              value={form.database}
              onChange={(e) => setForm({ ...form, database: e.target.value })}
              required
              className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
            />
            {!IS_SQLITE(form.dbProvider) && (
              <>
                <input
                  placeholder="Username"
                  value={form.username}
                  onChange={(e) => setForm({ ...form, username: e.target.value })}
                  required
                  className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
                />
                <div className="relative">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    placeholder={editingId ? 'Leave blank to keep current password' : 'Password'}
                    value={form.password}
                    onChange={(e) => setForm({ ...form, password: e.target.value })}
                    required={!editingId}
                    className="border-input bg-background focus:border-ring w-full rounded-lg border py-2.5 pl-4 pr-11 text-sm outline-hidden"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                    className="text-muted-foreground hover:text-foreground absolute inset-y-0 right-0 flex w-10 cursor-pointer items-center justify-center"
                  >
                    {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                  </button>
                </div>
                <select
                  value={form.sslMode}
                  onChange={(e) => setForm({ ...form, sslMode: e.target.value as SslMode })}
                  className="border-input bg-background focus:border-ring rounded-lg border px-4 py-2.5 text-sm outline-hidden"
                >
                  {SSL_MODES.map((m) => (
                    <option key={m.value} value={m.value} title={m.hint}>
                      {m.label}
                    </option>
                  ))}
                </select>
              </>
            )}
          </div>

          {duplicateOf && (
            <div className="bg-amber-500/10 text-amber-700 dark:text-amber-400 mt-4 rounded-lg p-3 text-sm">
              This looks like a duplicate of connection “{duplicateOf.name}” — the same host and database are already
              connected. You can still save it as a separate connection if you want.
            </div>
          )}

          {isCompany && (
            <div className="mt-4">
              <p className="text-muted-foreground mb-2 text-sm font-medium">Share with</p>
              <div className="space-y-2">
                {VISIBILITY_OPTIONS.map((opt) => (
                  <label
                    key={opt.value}
                    className={cn(
                      'border-border hover:bg-muted/50 flex cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm',
                      form.visibility === opt.value && 'border-brand bg-muted/40',
                    )}
                  >
                    <input
                      type="radio"
                      name="visibility"
                      value={opt.value}
                      checked={form.visibility === opt.value}
                      onChange={() => setForm({ ...form, visibility: opt.value })}
                      className="mt-0.5"
                    />
                    <span>
                      <span className="block font-medium">{opt.label}</span>
                      <span className="text-muted-foreground block">{opt.hint}</span>
                    </span>
                  </label>
                ))}
              </div>

              {form.visibility === 'Roles' && (
                <div className="border-border mt-3 rounded-lg border p-3">
                  <p className="text-muted-foreground mb-2 text-sm font-medium">Select roles</p>
                  {shareableRoles.length === 0 ? (
                    <p className="text-muted-foreground text-sm">
                      No roles available. Create roles under Team members first.
                    </p>
                  ) : (
                    <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                      {shareableRoles.map((role) => (
                        <label
                          key={role.id}
                          className="hover:bg-muted flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                        >
                          <input
                            type="checkbox"
                            checked={form.allowedRoleIds.includes(role.id)}
                            onChange={() => toggleAllowedRole(role.id)}
                          />
                          {role.name}
                        </label>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          <div className="mt-4 flex gap-3">
            <Button type="submit" disabled={saving}>
              {editingId ? 'Save changes' : 'Save'}
            </Button>
            <Button type="button" variant="outline" onClick={closeForm} disabled={saving}>
              Cancel
            </Button>
          </div>
        </form>
      )}

      <div className="space-y-3">
        {initialLoading && (
          <p className="text-muted-foreground text-sm">Loading connections…</p>
        )}

        {connections.map((conn) => {
          const canManage = canManageConnection(conn);
          return (
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
                    <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5">
                      <span className="font-semibold">{conn.name}</span>
                      <span className="text-muted-foreground text-sm">
                        ({providerLabel(conn.dbProvider)})
                      </span>
                      <span
                        className="text-muted-foreground inline-flex items-center gap-1 text-sm"
                        title={visibilityLabel(conn, roles)}
                      >
                        {conn.visibility === 'Private' ? (
                          <Lock className="size-3.5" />
                        ) : conn.visibility === 'Company' ? (
                          <Globe className="size-3.5" />
                        ) : (
                          <Users className="size-3.5" />
                        )}
                        {visibilityLabel(conn, roles)}
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
                {canManage && (
                  <>
                    <button
                      type="button"
                      onClick={() => void openEdit(conn)}
                      aria-label={`Edit ${conn.name}`}
                      className="text-muted-foreground hover:bg-muted hover:text-brand flex w-12 cursor-pointer items-center justify-center border-l transition-colors"
                    >
                      <Pencil className="size-4" />
                    </button>
                    <button
                      type="button"
                      onClick={() => setDeleteTarget({ id: conn.id, name: conn.name })}
                      aria-label={`Delete ${conn.name}`}
                      className="text-muted-foreground hover:bg-muted hover:text-destructive flex w-12 cursor-pointer items-center justify-center rounded-r-xl border-l transition-colors"
                    >
                      <Trash2 className="size-4" />
                    </button>
                  </>
                )}
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
          );
        })}

        {!initialLoading && connections.length === 0 && !formOpen && (
          <div className="border-border text-muted-foreground rounded-xl border border-dashed p-12 text-center">
            <Database className="mx-auto mb-3 size-10 opacity-40" />
            <p className="text-sm">
              {isCompany && !canManageConnections
                ? 'No shared connections yet. Ask a company admin to add one.'
                : 'No connections yet. Click "Add connection" to get started.'}
            </p>
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
