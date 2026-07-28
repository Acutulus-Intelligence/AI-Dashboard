import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  ArrowLeft,
  BarChart3,
  Brain,
  CheckCircle,
  ChevronRight,
  Database,
  Info,
  Loader2,
  Sparkles,
  Table2,
  XCircle,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { executeChart, getChart, saveChart, updateChart } from '../../lib/api/charts';
import * as companyApi from '../../lib/api/company';
import {
  getConnections,
  getTables,
  getTablePreview,
  type ConnectionResponse,
  type TableInfo,
  type TablePreview,
} from '../../services/connectionsApi';
import { generateChart, type ChartConfigResponse } from '../../services/graphsApi';
import ChartRenderer from '../charts/ChartRenderer';
import { DEFAULT_COMPANY_COLORS } from '../charts/companyColors';
import { get, getAll } from '../charts/registry';
import { transformResult } from '../charts/transform';
import type { ChartStyleConfig } from '../charts/types';
import ChartStylePanel from '../components/ChartStylePanel';
import AddToDashboardDialog from '../components/AddToDashboardDialog';
import AppShell from '../layouts/AppShell';
import type { Crumb } from '../layouts/AppHeader';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';

type Mode = 'prompt' | 'prefab' | 'auto';
type WizardStep = 'pick-connection' | 'pick-table' | 'generate';

function cloneStyle(style: ChartStyleConfig): ChartStyleConfig {
  return structuredClone(style);
}

function stylesEqual(a: ChartStyleConfig, b: ChartStyleConfig): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

const MODES: { id: Mode; icon: typeof Sparkles; label: string; desc: string }[] = [
  { id: 'prompt', icon: Sparkles, label: 'Describe', desc: 'Write in natural language' },
  { id: 'prefab', icon: BarChart3, label: 'Pick type', desc: 'Choose a chart type' },
  { id: 'auto', icon: Brain, label: 'AI decide', desc: 'Let AI choose for you' },
];

interface LocationState {
  connectionId?: string;
  table?: string;
  fromConnections?: boolean;
  fromCharts?: boolean;
}

export default function GraphCreationPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { chartId: routeChartId } = useParams<{ chartId?: string }>();
  const isEditingExisting = Boolean(routeChartId);
  const navState = (location.state as LocationState | null) ?? null;
  const fromConnections = !!navState?.fromConnections;
  const fromCharts = !!navState?.fromCharts;
  const isCompanyUser = user?.userType === 1;

  const [step, setStep] = useState<WizardStep>(() =>
    routeChartId || (navState?.connectionId && navState?.table)
      ? 'generate'
      : 'pick-connection',
  );
  const [connectionId, setConnectionId] = useState(navState?.connectionId ?? '');
  const [tableName, setTableName] = useState(navState?.table ?? '');
  const [connectionName, setConnectionName] = useState('');

  const [connections, setConnections] = useState<ConnectionResponse[]>([]);
  const [connectionsLoading, setConnectionsLoading] = useState(false);
  const [tables, setTables] = useState<TableInfo[]>([]);
  const [tablesLoading, setTablesLoading] = useState(false);

  const [preview, setPreview] = useState<TablePreview | null>(null);
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = useState<Mode>('auto');
  const [prompt, setPrompt] = useState('');
  const [prefabType, setPrefabType] = useState('');
  const [generating, setGenerating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savedChartId, setSavedChartId] = useState<string | null>(routeChartId ?? null);
  const [result, setResult] = useState<ChartConfigResponse | null>(null);
  const [editableTitle, setEditableTitle] = useState('');
  const [editingTitle, setEditingTitle] = useState(false);
  const [savedTitleSnapshot, setSavedTitleSnapshot] = useState<string | null>(null);
  const [styleConfig, setStyleConfig] = useState<ChartStyleConfig>({});
  const [savedStyleSnapshot, setSavedStyleSnapshot] = useState<ChartStyleConfig | null>(null);
  const [companyColors, setCompanyColors] = useState<string[]>([...DEFAULT_COMPANY_COLORS]);
  const [addToDashboardOpen, setAddToDashboardOpen] = useState(false);
  const [error, setError] = useState('');
  const titleInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!isCompanyUser) {
      setCompanyColors([...DEFAULT_COMPANY_COLORS]);
      return;
    }
    let cancelled = false;
    companyApi
      .getCompanyStyle()
      .then((res) => {
        if (cancelled) return;
        setCompanyColors(res.colors?.length ? res.colors : [...DEFAULT_COMPANY_COLORS]);
      })
      .catch(() => {
        if (!cancelled) setCompanyColors([...DEFAULT_COMPANY_COLORS]);
      });
    return () => {
      cancelled = true;
    };
  }, [isCompanyUser]);

  useEffect(() => {
    if (!routeChartId) return;
    let cancelled = false;
    setLoading(true);
    setError('');
    setStep('generate');

    (async () => {
      try {
        const [detail, executed] = await Promise.all([
          getChart(routeChartId),
          executeChart(routeChartId),
        ]);
        if (cancelled) return;
        setConnectionId(detail.connectionId ?? '');
        setTableName(detail.tableName ?? '');
        setSavedChartId(detail.id);
        setResult(executed);
        setEditableTitle(executed.title);
        setSavedTitleSnapshot(executed.title);
        const style = executed.styleConfig ?? detail.styleConfig ?? {};
        setStyleConfig(style);
        setSavedStyleSnapshot(cloneStyle(style));
      } catch (err: unknown) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load chart.');
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [routeChartId]);

  useEffect(() => {
    if (step !== 'pick-connection') return;
    setConnectionsLoading(true);
    getConnections()
      .then(setConnections)
      .catch(() => {
        setConnections([]);
        setError('Failed to load connections.');
      })
      .finally(() => setConnectionsLoading(false));
  }, [step]);

  useEffect(() => {
    if (step !== 'pick-table' || !connectionId) return;
    setTablesLoading(true);
    getTables(connectionId)
      .then(setTables)
      .catch(() => {
        setTables([]);
        setError('Failed to load tables.');
      })
      .finally(() => setTablesLoading(false));
  }, [step, connectionId]);

  useEffect(() => {
    if (isEditingExisting) return;
    if (step !== 'generate' || !connectionId || !tableName) return;
    setLoading(true);
    setError('');
    getTablePreview(connectionId, tableName)
      .then(setPreview)
      .catch(() => setError('Failed to load table schema'))
      .finally(() => setLoading(false));
  }, [step, connectionId, tableName, isEditingExisting]);

  useEffect(() => {
    if (!connectionId || connectionName) return;
    getConnections()
      .then((list) => {
        const found = list.find((c) => c.id === connectionId);
        if (found) setConnectionName(found.name);
      })
      .catch(() => {});
  }, [connectionId, connectionName]);

  const chartData = useMemo(() => (result ? transformResult(result) : null), [result]);
  const descriptor = result ? get(result.chartType) : undefined;
  const isStyleDirty =
    !!savedChartId &&
    savedStyleSnapshot !== null &&
    !stylesEqual(styleConfig, savedStyleSnapshot);
  const isTitleDirty =
    !!savedChartId && savedTitleSnapshot !== null && editableTitle.trim() !== savedTitleSnapshot;
  const isDirty = isStyleDirty || isTitleDirty;
  const colorSlots = chartData
    ? Math.max(
        chartData.datasets.length,
        chartData.labels.length > 0 &&
          (result?.chartType === 'pie' || result?.chartType === 'radial')
          ? chartData.labels.length
          : 0,
        1,
      )
    : 1;
  const colorLabels =
    result &&
    chartData &&
    (result.chartType === 'pie' || result.chartType === 'radial') &&
    chartData.labels.length > 0
      ? chartData.labels
      : chartData?.datasets.map((d) => d.label);

  const breadcrumbs = useMemo<Crumb[]>(() => {
    if (isEditingExisting) {
      return [
        fromCharts
          ? { label: 'Charts', to: ROUTES.CHARTS }
          : { label: 'Dashboard', to: ROUTES.DASHBOARD },
        { label: editableTitle || 'Edit chart' },
      ];
    }
    const crumbs: Crumb[] = fromCharts
      ? [{ label: 'Charts', to: ROUTES.CHARTS }, { label: 'New chart' }]
      : [{ label: 'New chart' }];
    if (step === 'pick-table' || step === 'generate') {
      crumbs.push({ label: connectionName || 'Database' });
    }
    if (step === 'generate' && tableName) {
      crumbs.push({ label: tableName });
    }
    return crumbs;
  }, [isEditingExisting, fromCharts, editableTitle, step, connectionName, tableName]);

  function selectConnection(conn: ConnectionResponse) {
    setConnectionId(conn.id);
    setConnectionName(conn.name);
    setTableName('');
    setTables([]);
    setPreview(null);
    setResult(null);
    setError('');
    setStep('pick-table');
  }

  function selectTable(table: string) {
    setTableName(table);
    setPreview(null);
    setResult(null);
    setEditableTitle('');
    setEditingTitle(false);
    setSavedChartId(null);
    setSavedStyleSnapshot(null);
    setSavedTitleSnapshot(null);
    setError('');
    setStep('generate');
  }

  function handleBack() {
    if (isEditingExisting) {
      navigate(fromCharts ? ROUTES.CHARTS : ROUTES.DASHBOARD);
      return;
    }
    if (step === 'generate') {
      if (fromConnections) {
        navigate(ROUTES.CONNECTIONS, { state: { expandConnectionId: connectionId } });
        return;
      }
      setResult(null);
      setEditableTitle('');
      setEditingTitle(false);
      setStyleConfig({});
      setSavedChartId(null);
      setSavedStyleSnapshot(null);
      setSavedTitleSnapshot(null);
      setPreview(null);
      setStep('pick-table');
      return;
    }
    if (step === 'pick-table') {
      setTableName('');
      setTables([]);
      setStep('pick-connection');
      return;
    }
    if (step === 'pick-connection' && fromCharts) {
      navigate(ROUTES.CHARTS);
    }
  }

  async function handleGenerate() {
    setError('');
    setGenerating(true);
    setSavedChartId(null);
    setSavedStyleSnapshot(null);
    setSavedTitleSnapshot(null);
    setEditingTitle(false);
    try {
      const res = await generateChart({
        connectionId,
        tableName,
        prompt: mode === 'prompt' ? prompt : undefined,
        prefabChartType: mode === 'prefab' ? prefabType : undefined,
        mode,
      });
      setResult(res);
      setEditableTitle(res.title);
      setStyleConfig(res.styleConfig ?? {});
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Generation failed');
    } finally {
      setGenerating(false);
    }
  }

  async function handleSave() {
    if (!result) return;
    const title = editableTitle.trim() || result.title;
    if (!title) {
      setError('Chart title cannot be empty.');
      return;
    }
    setSaving(true);
    setError('');
    try {
      if (savedChartId) {
        const updated = await updateChart(savedChartId, {
          title,
          chartType: result.chartType,
          styleConfig,
        });
        // Use the sanitized style from the API so preview matches the dashboard.
        const savedStyle = updated.styleConfig ?? {};
        setStyleConfig(savedStyle);
        setEditableTitle(updated.title);
        setSavedTitleSnapshot(updated.title);
        setSavedStyleSnapshot(cloneStyle(savedStyle));
        toast.success('Chart saved.');
      } else {
        const res = await saveChart({
          title,
          chartType: result.chartType,
          xAxis: result.xAxis,
          yAxis: result.yAxis,
          aggregation: result.aggregation,
          groupBy: result.groupBy,
          sqlQuery: result.sqlQuery,
          connectionId,
          tableName,
          styleConfig,
        });
        // Re-fetch so we keep the same sanitized style the dashboard will render.
        const detail = await getChart(res.id);
        const savedStyle = detail.styleConfig ?? styleConfig;
        setSavedChartId(res.id);
        setStyleConfig(savedStyle);
        setEditableTitle(detail.title);
        setSavedTitleSnapshot(detail.title);
        setSavedStyleSnapshot(cloneStyle(savedStyle));
        toast.success('Chart saved.');
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save chart';
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  }

  function handleCancelEdits() {
    if (savedStyleSnapshot) setStyleConfig(cloneStyle(savedStyleSnapshot));
    if (savedTitleSnapshot !== null) setEditableTitle(savedTitleSnapshot);
    setEditingTitle(false);
  }

  function startTitleEdit() {
    setEditingTitle(true);
    requestAnimationFrame(() => {
      const el = titleInputRef.current;
      if (!el) return;
      el.focus();
      el.select();
    });
  }

  function resetResult() {
    setResult(null);
    setEditableTitle('');
    setEditingTitle(false);
    setStyleConfig({});
    setSavedChartId(null);
    setSavedStyleSnapshot(null);
    setSavedTitleSnapshot(null);
    setError('');
  }

  return (
    <AppShell breadcrumbs={breadcrumbs}>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            {isEditingExisting ? 'Edit chart' : 'Create chart'}
          </h1>
          <p className="text-muted-foreground text-sm">
            {isEditingExisting && (editableTitle || 'Update title, style and appearance.')}
            {!isEditingExisting && step === 'pick-connection' && 'Choose a database to visualize.'}
            {!isEditingExisting && step === 'pick-table' && (
              <>
                Tables in <span className="text-foreground font-medium">{connectionName || 'database'}</span>
              </>
            )}
            {!isEditingExisting && step === 'generate' && (
              <>
                Table <span className="text-foreground font-medium">{tableName}</span>
              </>
            )}
          </p>
        </div>
        {(step !== 'pick-connection' || isEditingExisting || fromCharts) && (
          <Button variant="ghost" size="sm" type="button" onClick={handleBack}>
            <ArrowLeft />
            Back
          </Button>
        )}
      </div>

      {error && (
        <div className="border-destructive/40 bg-destructive/10 text-destructive rounded-lg border px-3 py-2 text-sm">
          {error}
        </div>
      )}

      {step === 'pick-connection' && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {connectionsLoading && (
            <p className="text-muted-foreground col-span-full text-sm">Loading databases…</p>
          )}
          {!connectionsLoading &&
            connections.map((conn) => (
              <button
                key={conn.id}
                type="button"
                onClick={() => selectConnection(conn)}
                className="border-border bg-card hover:border-primary/40 hover:bg-muted/40 flex cursor-pointer flex-col gap-3 rounded-xl border p-5 text-left transition-colors"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="bg-primary/10 text-primary flex size-10 items-center justify-center rounded-lg">
                    <Database className="size-5" />
                  </div>
                  {conn.isVerified ? (
                    <CheckCircle className="size-4 text-green-600" />
                  ) : (
                    <XCircle className="size-4 text-red-400" />
                  )}
                </div>
                <div>
                  <p className="font-semibold">{conn.name}</p>
                  <p className="text-muted-foreground text-sm">
                    {conn.dbProvider === 'PostgreSql' ? 'PostgreSQL' : 'MySQL'}
                  </p>
                </div>
                <span className="text-muted-foreground flex items-center gap-1 text-xs">
                  Browse tables <ChevronRight className="size-3.5" />
                </span>
              </button>
            ))}
          {!connectionsLoading && connections.length === 0 && (
            <Card className="col-span-full">
              <CardHeader>
                <CardTitle>No databases yet</CardTitle>
                <CardDescription>
                  Ask an admin to add a connection, or open Settings / Admin settings if you manage
                  connections.
                </CardDescription>
              </CardHeader>
            </Card>
          )}
        </div>
      )}

      {step === 'pick-table' && (
        <Card>
          <CardHeader>
            <CardTitle>Tables</CardTitle>
            <CardDescription>Pick a table to generate a chart from.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-2">
            {tablesLoading && <p className="text-muted-foreground text-sm">Loading tables…</p>}
            {!tablesLoading &&
              tables.map((t) => (
                <button
                  key={t.tableName}
                  type="button"
                  onClick={() => selectTable(t.tableName)}
                  className="hover:bg-muted flex w-full cursor-pointer items-center justify-between rounded-lg border px-4 py-3 text-left transition-colors"
                >
                  <div className="flex items-center gap-3">
                    <Table2 className="text-brand size-4" />
                    <div>
                      <p className="font-medium">{t.tableName}</p>
                      <p className="text-muted-foreground text-xs">{t.columns.length} columns</p>
                    </div>
                  </div>
                  <ChevronRight className="text-muted-foreground size-4" />
                </button>
              ))}
            {!tablesLoading && tables.length === 0 && (
              <p className="text-muted-foreground text-sm">No tables found in this database.</p>
            )}
          </CardContent>
        </Card>
      )}

      {step === 'generate' && (
        <>
          {loading && <p className="text-muted-foreground text-sm">Loading schema…</p>}

          {preview && !result && !isEditingExisting && (
            <Card>
              <CardHeader>
                <CardTitle>Schema</CardTitle>
                <CardDescription>{preview.columns.length} columns available for the AI.</CardDescription>
              </CardHeader>
              <CardContent className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="text-muted-foreground border-b">
                      <th className="pb-2 pr-4 font-medium">Column</th>
                      <th className="pb-2 pr-4 font-medium">Type</th>
                      <th className="pb-2 font-medium">Nullable</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.columns.map((col) => (
                      <tr key={col.columnName} className="border-b last:border-0">
                        <td className="py-2 pr-4 font-medium">{col.columnName}</td>
                        <td className="text-muted-foreground py-2 pr-4">{col.dataType}</td>
                        <td className="text-muted-foreground py-2">{col.isNullable ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </CardContent>
            </Card>
          )}

          {!result && !loading && !isEditingExisting && (
            <Card>
              <CardHeader>
                <CardTitle>How to visualize?</CardTitle>
                <CardDescription>
                  Describe what you want, pick a type, or let the AI decide.
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-6">
                <div className="grid gap-3 sm:grid-cols-3">
                  {MODES.map((opt) => (
                    <button
                      key={opt.id}
                      type="button"
                      onClick={() => setMode(opt.id)}
                      className={cn(
                        'flex cursor-pointer flex-col items-center gap-2 rounded-xl border p-4 text-center transition-colors',
                        mode === opt.id
                          ? 'border-primary bg-primary/5'
                          : 'border-border hover:bg-muted',
                      )}
                    >
                      <opt.icon
                        className={cn(
                          'size-5',
                          mode === opt.id ? 'text-primary' : 'text-muted-foreground',
                        )}
                      />
                      <span className="font-medium">{opt.label}</span>
                      <span className="text-muted-foreground text-xs">{opt.desc}</span>
                    </button>
                  ))}
                </div>

                {mode === 'prompt' && (
                  <Textarea
                    value={prompt}
                    onChange={(e) => setPrompt(e.target.value)}
                    placeholder="Describe the chart you want… (e.g. Show top 10 products by revenue)"
                    rows={3}
                  />
                )}

                {mode === 'prefab' && (
                  <div className="flex flex-wrap gap-2">
                    {getAll().map((chart) => (
                      <Button
                        key={chart.id}
                        type="button"
                        size="sm"
                        variant={prefabType === chart.id ? 'default' : 'outline'}
                        onClick={() => setPrefabType(chart.id)}
                      >
                        <chart.icon />
                        {chart.label}
                      </Button>
                    ))}
                  </div>
                )}

                {mode === 'auto' && (
                  <p className="text-muted-foreground text-sm">
                    The AI will pick the best chart type, variant and styling for this table.
                  </p>
                )}

                <Button
                  onClick={() => void handleGenerate()}
                  disabled={
                    generating ||
                    (mode === 'prompt' && !prompt) ||
                    (mode === 'prefab' && !prefabType)
                  }
                  className="w-fit"
                >
                  {generating ? <Loader2 className="animate-spin" /> : <Sparkles />}
                  {generating ? 'Generating…' : 'Generate chart'}
                </Button>
              </CardContent>
            </Card>
          )}

          {result && chartData && (
            <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
              <Card>
                <CardHeader className="gap-1.5 space-y-0">
                  <div className="flex items-center gap-2">
                    {editingTitle ? (
                      <Input
                        ref={titleInputRef}
                        value={editableTitle}
                        onChange={(e) => setEditableTitle(e.target.value)}
                        onBlur={() => setEditingTitle(false)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') {
                            e.preventDefault();
                            setEditingTitle(false);
                          }
                          if (e.key === 'Escape') {
                            if (savedTitleSnapshot !== null) setEditableTitle(savedTitleSnapshot);
                            else if (result) setEditableTitle(result.title);
                            setEditingTitle(false);
                          }
                        }}
                        className="h-8 min-w-0 flex-1 text-base font-semibold"
                        aria-label="Chart title"
                      />
                    ) : (
                      <CardTitle
                        className="min-w-0 flex-1 cursor-pointer truncate hover:underline"
                        onClick={startTitleEdit}
                        title="Click to rename"
                      >
                        {editableTitle || result.title}
                      </CardTitle>
                    )}
                    {styleConfig.info?.trim() && (
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <button
                            type="button"
                            className="text-muted-foreground hover:text-foreground inline-flex size-7 shrink-0 cursor-pointer items-center justify-center rounded-md"
                            aria-label="Chart info"
                          >
                            <Info className="size-4" />
                          </button>
                        </TooltipTrigger>
                        <TooltipContent side="left" className="max-w-xs text-left">
                          {styleConfig.info}
                        </TooltipContent>
                      </Tooltip>
                    )}
                    <Badge variant="secondary" className="shrink-0 capitalize">
                      {result.chartType}
                    </Badge>
                  </div>
                  <CardDescription>
                    Live preview — changes on the right apply instantly.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="bg-muted/20 h-80 rounded-lg border p-2">
                    <ChartRenderer
                      chartId={result.chartType}
                      data={chartData}
                      styleConfig={styleConfig}
                    />
                  </div>

                  <details className="mt-4">
                    <summary className="text-muted-foreground hover:text-foreground cursor-pointer text-sm font-medium">
                      Show SQL
                    </summary>
                    <pre className="bg-muted mt-2 overflow-x-auto rounded-lg p-3 text-xs">
                      {result.sqlQuery}
                    </pre>
                  </details>

                  <div className="mt-6 flex flex-wrap gap-2">
                    {!savedChartId && (
                      <Button onClick={() => void handleSave()} disabled={saving}>
                        {saving && <Loader2 className="animate-spin" />}
                        Save chart
                      </Button>
                    )}
                    {savedChartId && isDirty && (
                      <>
                        <Button onClick={() => void handleSave()} disabled={saving}>
                          {saving && <Loader2 className="animate-spin" />}
                          Save changes
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          disabled={saving}
                          onClick={handleCancelEdits}
                        >
                          Cancel
                        </Button>
                      </>
                    )}
                    {savedChartId && (
                      <Button
                        type="button"
                        variant="secondary"
                        onClick={() => setAddToDashboardOpen(true)}
                      >
                        Add to dashboard
                      </Button>
                    )}
                    {!isEditingExisting && (
                      <Button variant="outline" onClick={resetResult}>
                        Create another
                      </Button>
                    )}
                    {isEditingExisting && (
                      <Button
                        variant="outline"
                        onClick={() => navigate(fromCharts ? ROUTES.CHARTS : ROUTES.DASHBOARD)}
                      >
                        {fromCharts ? 'Back to charts' : 'Back to dashboard'}
                      </Button>
                    )}
                  </div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Style</CardTitle>
                  <CardDescription>
                    {descriptor
                      ? 'Adjust the AI starting point before you save.'
                      : 'Unknown chart type — styling unavailable.'}
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {descriptor ? (
                    <ChartStylePanel
                      descriptor={descriptor}
                      value={styleConfig}
                      onChange={setStyleConfig}
                      companyColors={companyColors}
                      colorSlots={Math.min(colorSlots, 8)}
                      colorLabels={colorLabels?.slice(0, Math.min(colorSlots, 8))}
                    />
                  ) : (
                    <p className="text-muted-foreground text-sm">No style controls for this type.</p>
                  )}
                </CardContent>
              </Card>
            </div>
          )}
        </>
      )}

      {savedChartId && (
        <AddToDashboardDialog
          open={addToDashboardOpen}
          onOpenChange={setAddToDashboardOpen}
          savedChartId={savedChartId}
        />
      )}
    </AppShell>
  );
}
