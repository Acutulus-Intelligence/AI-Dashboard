import { useEffect, useState } from 'react';
import { Plus, Trash2, Database, CheckCircle, XCircle, ChevronRight, Table, Eye, ArrowLeft } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import DashboardHeader from '../layouts/DashboardHeader';
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

export default function ConnectionsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const canManageConnections = !user || user.userType !== 1 || user.companyRoleName === 'Owner';
  const [connections, setConnections] = useState<ConnectionResponse[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<CreateConnectionRequest>({
    name: '', dbProvider: 'PostgreSql', host: '', port: 5432, database: '', username: '', password: '',
  });
  const [error, setError] = useState('');
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [tables, setTables] = useState<TableInfo[]>([]);
  const [tablesLoading, setTablesLoading] = useState(false);

  const load = () => getConnections().then(setConnections);

  useEffect(() => { load(); }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await createConnection(form);
      setShowForm(false);
      setForm({ name: '', dbProvider: 'PostgreSql', host: '', port: 5432, database: '', username: '', password: '' });
      await load();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create connection');
    }
  };

  const handleTest = async (id: string) => {
    const { isVerified } = await testConnection(id);
    await load();
    alert(isVerified ? 'Connection successful!' : 'Connection failed.');
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Delete this connection?')) return;
    await deleteConnection(id);
    if (expandedId === id) { setExpandedId(null); setTables([]); }
    await load();
  };

  const toggleExpand = async (id: string) => {
    if (expandedId === id) {
      setExpandedId(null);
      setTables([]);
      return;
    }
    setExpandedId(id);
    setTablesLoading(true);
    try {
      const t = await getTables(id);
      setTables(t);
    } catch { setTables([]); }
    setTablesLoading(false);
  };

  return (
    <div className="min-h-screen bg-background">
      <DashboardHeader
        onToggleNavbar={() => console.log('sidebar toggle')}
        onNewChart={() => {}}
        onNewDashboard={() => {}}
      />
      <main className="pt-16">
        <div className="mx-auto max-w-container-max px-gutter py-8">
          <div className="mb-6 flex items-center justify-between border-b border-outline-variant/40 pb-3">
            <div className="flex items-center gap-3">
              <Link
                to={ROUTES.DASHBOARD}
                className="inline-flex items-center justify-center rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container"
              >
                <ArrowLeft size={18} />
              </Link>
              <h1 className="text-headline-md font-bold text-on-background">Database Connections</h1>
            </div>
            {canManageConnections && (
              <button
                onClick={() => setShowForm(!showForm)}
                className="flex cursor-pointer items-center gap-2 rounded-xl bg-primary px-5 py-3 text-body-md font-semibold text-white transition-transform active:scale-95"
              >
                <Plus size={18} /> Add Connection
              </button>
            )}
          </div>

        {showForm && (
          <form onSubmit={handleCreate} className="mb-8 rounded-xl border border-outline-variant bg-surface p-6">
            <h2 className="mb-4 text-headline-md font-semibold text-on-background">New Connection</h2>
            {error && <div className="mb-3 rounded-lg bg-error/10 p-3 text-body-sm text-error">{error}</div>}
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <input placeholder="Connection Name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required className="rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary" />
              <select value={form.dbProvider} onChange={(e) => setForm({ ...form, dbProvider: e.target.value })} className="rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary">
                {DB_PROVIDERS.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
              </select>
              <input placeholder="Host" value={form.host} onChange={(e) => setForm({ ...form, host: e.target.value })} required className="rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary" />
              <input type="number" placeholder="Port" value={form.port} onChange={(e) => setForm({ ...form, port: Number(e.target.value) })} required className="rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary" />
              <input placeholder="Database" value={form.database} onChange={(e) => setForm({ ...form, database: e.target.value })} required className="rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary" />
              <input placeholder="Username" value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} required className="rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary" />
              <input type="password" placeholder="Password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required className="rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary" />
            </div>
            <div className="mt-4 flex gap-3">
              <button type="submit" className="cursor-pointer rounded-xl bg-secondary px-6 py-2.5 text-body-md font-semibold text-white transition-transform active:scale-95">Save</button>
              <button type="button" onClick={() => setShowForm(false)} className="cursor-pointer rounded-xl border border-outline-variant bg-surface px-6 py-2.5 text-body-md text-on-surface-variant">Cancel</button>
            </div>
          </form>
        )}

        <div className="space-y-3">
          {connections.map((conn) => (
            <div key={conn.id} className="rounded-xl border border-outline-variant bg-surface">
              <div className="flex items-center justify-between p-4">
                <div className="flex items-center gap-3">
                  <Database size={20} className="text-secondary" />
                  <div>
                    <span className="font-semibold text-on-background">{conn.name}</span>
                    <span className="ml-2 text-body-sm text-on-surface-variant">
                      ({conn.dbProvider === 'PostgreSql' ? 'PostgreSQL' : 'MySQL'})
                    </span>
                  </div>
                  {conn.isVerified ? (
                    <CheckCircle size={16} className="text-green-600" />
                  ) : (
                    <XCircle size={16} className="text-red-400" />
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <button onClick={() => handleTest(conn.id)} className="cursor-pointer rounded-lg border border-outline-variant px-3 py-1.5 text-body-sm text-on-surface-variant hover:bg-surface-container">Test</button>
                  <button onClick={() => toggleExpand(conn.id)} className="flex cursor-pointer items-center gap-1 rounded-lg border border-outline-variant px-3 py-1.5 text-body-sm text-on-surface-variant hover:bg-surface-container">
                    <Table size={14} /> Tables
                  </button>
                  {canManageConnections && (
                    <button onClick={() => handleDelete(conn.id)} className="cursor-pointer rounded-lg p-2 text-on-surface-variant hover:bg-surface-container"><Trash2 size={16} /></button>
                  )}
                </div>
              </div>

              {expandedId === conn.id && (
                <div className="border-t border-outline-variant p-4">
                  {tablesLoading ? (
                    <div className="text-body-sm text-on-surface-variant">Loading tables...</div>
                  ) : (
                    <div className="space-y-2">
                      {tables.map((t) => (
                        <button
                          key={t.tableName}
                          onClick={() => navigate(ROUTES.GRAPHS_NEW, { state: { connectionId: conn.id, table: t.tableName } })}
                          className="flex w-full cursor-pointer items-center justify-between rounded-lg p-3 text-body-md text-on-surface-variant hover:bg-surface-container"
                        >
                          <div className="flex items-center gap-2">
                            <Table size={16} className="text-secondary" />
                            <span className="font-medium text-on-background">{t.tableName}</span>
                            <span className="text-body-sm text-on-surface-variant">({t.columns.length} columns)</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <Eye size={16} />
                            <ChevronRight size={16} />
                          </div>
                        </button>
                      ))}
                      {tables.length === 0 && (
                        <div className="text-body-sm text-on-surface-variant">No tables found.</div>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          ))}

          {connections.length === 0 && !showForm && (
            <div className="rounded-xl border border-dashed border-outline-variant p-12 text-center text-on-surface-variant">
              <Database size={40} className="mx-auto mb-3 opacity-40" />
              <p className="text-body-md">{canManageConnections ? 'No connections yet. Click "Add Connection" to get started.' : 'No connections available.'}</p>
            </div>
          )}
        </div>
      </div>
      </main>
    </div>
  );
}
