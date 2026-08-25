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
  Folder,
  Info,
  Loader2,
  Sparkles,
  Table2,
  Undo2,
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
} from '../../services/connectionsApi';
import {
  generateCollectionChart,
  getCollection,
  getCollectionFile,
  getCollections,
  type CollectionFileResponse,
  type CollectionResponse,
} from '../../services/collectionsApi';
import {
  generateChart,
  type AiGenerationDebug,
  type ChartBaseline,
  type ChartConfigResponse,
} from '../../services/graphsApi';
import ChartRenderer from '../charts/ChartRenderer';
import { DEFAULT_COMPANY_COLORS } from '../charts/companyColors';
import { get, getAll } from '../charts/registry';
import { transformResult } from '../charts/transform';
import type { ChartStyleConfig } from '../charts/types';
import ChartStylePanel from '../components/ChartStylePanel';
import AddToDashboardDialog from '../components/AddToDashboardDialog';
import AppShell from '../layouts/AppShell';
import type { Crumb } from '../layouts/AppHeader';
import { ROUTES, graphEditPath } from '../routes';
import { useAuth } from '../store/useAuth';

interface PreviewColumn {
  columnName: string;
  dataType: string;
  isNullable?: boolean;
}

interface PreviewData {
  columns: PreviewColumn[];
}

type Mode = 'prompt' | 'prefab' | 'auto';
type WizardStep = 'pick-connection' | 'pick-table' | 'generate';

function cloneStyle(style: ChartStyleConfig): ChartStyleConfig {
  return structuredClone(style);
}

function stylesEqual(a: ChartStyleConfig, b: ChartStyleConfig): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

function cloneResult(result: ChartConfigResponse): ChartConfigResponse {
  return structuredClone(result);
}

/** Theme palette XOR per-slice colours — palette wins when both are set. */
function normalizeColorExclusive(style: ChartStyleConfig): ChartStyleConfig {
  if (style.palette != null && style.palette !== '') {
    return { ...style, palette: style.palette, colors: undefined };
  }
  if (style.colors?.length) {
    return { ...style, palette: undefined, colors: style.colors };
  }
  return style;
}

/** Build AI refine baseline — full style for merge; backend slims for the AI prompt separately. */
function toBaseline(
  result: ChartConfigResponse,
  style: ChartStyleConfig,
  title: string,
): ChartBaseline {
  return {
    title: title.trim() || result.title,
    chartType: result.chartType,
    xAxis: result.xAxis,
    yAxis: result.yAxis,
    aggregation: result.aggregation,
    groupBy: result.groupBy,
    sqlQuery: result.sqlQuery,
    styleConfig: cloneStyle(style),
  };
}

function configEqual(a: ChartConfigResponse, b: ChartConfigResponse): boolean {
  return (
    a.chartType === b.chartType &&
    a.xAxis === b.xAxis &&
    JSON.stringify(a.yAxis) === JSON.stringify(b.yAxis) &&
    a.aggregation === b.aggregation &&
    a.groupBy === b.groupBy &&
    a.sqlQuery === b.sqlQuery
  );
}

function uniqueCopyTitle(title: string): string {
  const base = title.trim() || 'Chart';
  return `${base} (copy)`;
}

const MODES: { id: Mode; icon: typeof Sparkles; label: string; desc: string }[] = [
  { id: 'prompt', icon: Sparkles, label: 'Describe', desc: 'Write in natural language' },
  { id: 'prefab', icon: BarChart3, label: 'Pick type', desc: 'Choose a chart type' },
  { id: 'auto', icon: Brain, label: 'AI decide', desc: 'Let AI choose for you' },
];

interface LocationState {
  connectionId?: string;
  table?: string;
  collectionId?: string;
  fileId?: string;
  fromConnections?: boolean;
  fromCharts?: boolean;
}

type SourceType = 'connection' | 'collection';

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
    routeChartId ||
    (navState?.connectionId && navState?.table) ||
    (navState?.collectionId && navState?.fileId)
      ? 'generate'
      : 'pick-connection',
  );
  const [sourceType, setSourceType] = useState<SourceType>(() =>
    navState?.collectionId ? 'collection' : 'connection',
  );
  const [connectionId, setConnectionId] = useState(navState?.connectionId ?? '');
  const [tableName, setTableName] = useState(navState?.table ?? '');
  const [connectionName, setConnectionName] = useState('');
  const [collectionId, setCollectionId] = useState(navState?.collectionId ?? '');
  const [fileId, setFileId] = useState(navState?.fileId ?? '');
  const [collectionName, setCollectionName] = useState('');
  const [fileName, setFileName] = useState('');

  const [connections, setConnections] = useState<ConnectionResponse[]>([]);
  const [connectionsLoading, setConnectionsLoading] = useState(false);
  const [collections, setCollections] = useState<CollectionResponse[]>([]);
  const [collectionsLoading, setCollectionsLoading] = useState(false);
  const [tables, setTables] = useState<TableInfo[]>([]);
  const [tablesLoading, setTablesLoading] = useState(false);
  const [files, setFiles] = useState<CollectionFileResponse[]>([]);
  const [filesLoading, setFilesLoading] = useState(false);

  const [preview, setPreview] = useState<PreviewData | null>(null);
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = useState<Mode>('auto');
  const [prompt, setPrompt] = useState('');
  const [refinePrompt, setRefinePrompt] = useState('');
  const [prefabType, setPrefabType] = useState('');
  const [generating, setGenerating] = useState(false);
  const [refining, setRefining] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savedChartId, setSavedChartId] = useState<string | null>(routeChartId ?? null);
  const [result, setResult] = useState<ChartConfigResponse | null>(null);
  const [editableTitle, setEditableTitle] = useState('');
  const [editingTitle, setEditingTitle] = useState(false);
  const [savedTitleSnapshot, setSavedTitleSnapshot] = useState<string | null>(null);
  const [styleConfig, setStyleConfig] = useState<ChartStyleConfig>({});
  const [savedStyleSnapshot, setSavedStyleSnapshot] = useState<ChartStyleConfig | null>(null);
  const [savedResultSnapshot, setSavedResultSnapshot] = useState<ChartConfigResponse | null>(null);
  /** Chart state from before the last successful AI refine — Undo restores this. */
  const [preRefineSnapshot, setPreRefineSnapshot] = useState<{
    result: ChartConfigResponse;
    styleConfig: ChartStyleConfig;
    title: string;
  } | null>(null);
  const [companyColors, setCompanyColors] = useState<string[]>([...DEFAULT_COMPANY_COLORS]);
  const [addToDashboardOpen, setAddToDashboardOpen] = useState(false);
  const [error, setError] = useState('');
  const [aiDebug, setAiDebug] = useState<AiGenerationDebug | null>(null);
  const [aiDebugOpen, setAiDebugOpen] = useState(false);
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
        if (detail.datasetId) {
          setSourceType('collection');
          setFileId(detail.datasetId);
        }
        setSavedChartId(detail.id);
        setResult(executed);
        setSavedResultSnapshot(cloneResult(executed));
        setEditableTitle(executed.title);
        setSavedTitleSnapshot(executed.title);
        const style = executed.styleConfig ?? detail.styleConfig ?? {};
        setStyleConfig(style);
        setSavedStyleSnapshot(cloneStyle(style));
        setRefinePrompt('');
        setPreRefineSnapshot(null);
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

    setCollectionsLoading(true);
    getCollections()
      .then(setCollections)
      .catch(() => {
        setCollections([]);
        setError('Failed to load collections.');
      })
      .finally(() => setCollectionsLoading(false));
  }, [step]);

  useEffect(() => {
    if (step !== 'pick-table') return;
    if (sourceType === 'connection') {
      if (!connectionId) return;
      setTablesLoading(true);
      getTables(connectionId)
        .then(setTables)
        .catch(() => {
          setTables([]);
          setError('Failed to load tables.');
        })
        .finally(() => setTablesLoading(false));
    } else {
      if (!collectionId) return;
      setFilesLoading(true);
      getCollection(collectionId)
        .then((d) => setFiles(d.files))
        .catch(() => {
          setFiles([]);
          setError('Failed to load collection files.');
        })
        .finally(() => setFilesLoading(false));
    }
  }, [step, sourceType, connectionId, collectionId]);

  useEffect(() => {
    if (isEditingExisting) return;
    if (step !== 'generate') return;
    setLoading(true);
    setError('');

    const task =
      sourceType === 'connection'
        ? connectionId && tableName
          ? getTablePreview(connectionId, tableName).then((p): PreviewData => ({
              columns: p.columns.map((c) => ({
                columnName: c.columnName,
                dataType: c.dataType,
                isNullable: c.isNullable,
              })),
            }))
          : Promise.resolve(null)
        : fileId
          ? getCollectionFile(collectionId, fileId).then((f): PreviewData => ({
              columns: f.columns.map((c) => ({ columnName: c.name, dataType: c.type })),
            }))
          : Promise.resolve(null);

    task
      .then((previewData) => {
        if (previewData) setPreview(previewData);
      })
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : 'Failed to load schema'),
      )
      .finally(() => setLoading(false));
  }, [step, sourceType, connectionId, tableName, collectionId, fileId, isEditingExisting]);

  useEffect(() => {
    if (!connectionName && connectionId) {
      getConnections()
        .then((list) => {
          const found = list.find((c) => c.id === connectionId);
          if (found) setConnectionName(found.name);
        })
        .catch(() => {});
    }
    if (sourceType === 'collection' && !collectionName && collectionId) {
      getCollection(collectionId)
        .then((d) => setCollectionName(d.name))
        .catch(() => {});
    }
  }, [connectionId, connectionName, collectionId, collectionName, sourceType]);

  const chartData = useMemo(() => (result ? transformResult(result) : null), [result]);
  const descriptor = result ? get(result.chartType) : undefined;
  const isStyleDirty =
    !!savedChartId &&
    savedStyleSnapshot !== null &&
    !stylesEqual(styleConfig, savedStyleSnapshot);
  const isTitleDirty =
    !!savedChartId && savedTitleSnapshot !== null && editableTitle.trim() !== savedTitleSnapshot;
  const isConfigDirty =
    !!savedChartId &&
    !!result &&
    !!savedResultSnapshot &&
    !configEqual(result, savedResultSnapshot);
  const isDirty = isStyleDirty || isTitleDirty || isConfigDirty;
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
      crumbs.push({ label: sourceType === 'collection' ? collectionName || 'Collection' : connectionName || 'Database' });
    }
    if (step === 'generate' && (tableName || fileName)) {
      crumbs.push({ label: fileName || tableName });
    }
    return crumbs;
  }, [isEditingExisting, fromCharts, editableTitle, step, connectionName, collectionName, sourceType, tableName, fileName]);

  function selectConnection(conn: ConnectionResponse) {
    setSourceType('connection');
    setConnectionId(conn.id);
    setConnectionName(conn.name);
    setTableName('');
    setTables([]);
    setCollectionId('');
    setFileId('');
    setPreview(null);
    setResult(null);
    setError('');
    setStep('pick-table');
  }

  function selectCollection(coll: CollectionResponse) {
    setSourceType('collection');
    setCollectionId(coll.id);
    setCollectionName(coll.name);
    setFileId('');
    setFiles([]);
    setConnectionId('');
    setTableName('');
    setPreview(null);
    setResult(null);
    setError('');
    setStep('pick-table');
  }

  function selectFile(file: CollectionFileResponse) {
    setFileId(file.id);
    setFileName(file.name);
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
        navigate(sourceType === 'collection' ? ROUTES.CONNECTIONS : ROUTES.CONNECTIONS, {
          state: sourceType === 'connection' ? { expandConnectionId: connectionId } : undefined,
        });
        return;
      }
      resetResult();
      setStep('pick-table');
      return;
    }
    if (step === 'pick-table') {
      if (sourceType === 'collection') {
        setFileId('');
        setFileName('');
      } else {
        setTableName('');
        setTables([]);
      }
      setStep('pick-connection');
      return;
    }
    if (step === 'pick-connection' && fromCharts) {
      navigate(ROUTES.CHARTS);
    }
  }

  async function handleGenerate() {
    if (sourceType === 'collection' && (!collectionId || !fileId)) return;
    if (sourceType === 'connection' && (!connectionId || !tableName)) return;
    setError('');
    setGenerating(true);
    setSavedChartId(null);
    setSavedStyleSnapshot(null);
    setSavedTitleSnapshot(null);
    setSavedResultSnapshot(null);
    setPreRefineSnapshot(null);
    setEditingTitle(false);
    setRefinePrompt('');
    setAiDebug(null);
    try {
      const res =
        sourceType === 'collection'
          ? await generateCollectionChart(collectionId, fileId, {
              prompt: mode === 'prompt' ? prompt : undefined,
              prefabChartType: mode === 'prefab' ? prefabType : undefined,
              mode,
            })
          : await generateChart({
              connectionId,
              tableName,
              prompt: mode === 'prompt' ? prompt : undefined,
              prefabChartType: mode === 'prefab' ? prefabType : undefined,
              mode,
            });
      const style = normalizeColorExclusive(res.styleConfig ?? {});
      setResult({ ...res, styleConfig: style });
      setEditableTitle(res.title);
      setStyleConfig(style);
      setAiDebug(res.aiDebug ?? null);
      if (res.aiDebug) setAiDebugOpen(true);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Generation failed');
    } finally {
      setGenerating(false);
    }
  }

  async function handleRefine() {
    if (!result || !connectionId || !tableName || !refinePrompt.trim()) return;
    const snapshot = {
      result: cloneResult(result),
      styleConfig: cloneStyle(styleConfig),
      title: editableTitle,
    };
    setRefining(true);
    setError('');
    try {
      const res = await generateChart({
        connectionId,
        tableName,
        prompt: refinePrompt.trim(),
        mode: 'prompt',
        currentChart: toBaseline(result, styleConfig, editableTitle),
      });
      setPreRefineSnapshot(snapshot);
      const style = normalizeColorExclusive(res.styleConfig ?? {});
      setResult({ ...res, styleConfig: style });
      setEditableTitle(res.title);
      setStyleConfig(style);
      setRefinePrompt('');
      setAiDebug(res.aiDebug ?? null);
      if (res.aiDebug) setAiDebugOpen(true);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Adjustment failed');
    } finally {
      setRefining(false);
    }
  }

  function handleUndoRefine() {
    if (!preRefineSnapshot) return;
    setResult(cloneResult(preRefineSnapshot.result));
    setStyleConfig(cloneStyle(preRefineSnapshot.styleConfig));
    setEditableTitle(preRefineSnapshot.title);
    setPreRefineSnapshot(null);
    setEditingTitle(false);
    toast.message('Reverted to the version before the last AI adjustment.');
  }

  function applySavedSnapshots(
    id: string,
    title: string,
    style: ChartStyleConfig,
    chart: ChartConfigResponse,
  ) {
    setSavedChartId(id);
    setEditableTitle(title);
    setStyleConfig(style);
    setSavedTitleSnapshot(title);
    setSavedStyleSnapshot(cloneStyle(style));
    setSavedResultSnapshot(cloneResult({ ...chart, title, styleConfig: style }));
    setPreRefineSnapshot(null);
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
          xAxis: result.xAxis,
          yAxis: result.yAxis,
          aggregation: result.aggregation,
          groupBy: result.groupBy,
          sqlQuery: result.sqlQuery,
          styleConfig,
        });
        const savedStyle = updated.styleConfig ?? {};
        const nextResult: ChartConfigResponse = {
          ...result,
          title: updated.title,
          chartType: updated.chartType,
          xAxis: updated.xAxis,
          yAxis: updated.yAxis,
          aggregation: updated.aggregation,
          groupBy: updated.groupBy,
          sqlQuery: updated.sqlQuery,
          styleConfig: savedStyle,
        };
        setResult(nextResult);
        applySavedSnapshots(savedChartId, updated.title, savedStyle, nextResult);
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
          connectionId: sourceType === 'connection' ? connectionId : null,
          tableName: sourceType === 'connection' ? tableName : null,
          datasetId: sourceType === 'collection' ? fileId : (result.dataModel ? undefined : null),
          dataModel: result.dataModel ?? null,
          styleConfig,
        });
        const detail = await getChart(res.id);
        const savedStyle = detail.styleConfig ?? styleConfig;
        const nextResult: ChartConfigResponse = {
          ...result,
          title: detail.title,
          chartType: detail.chartType,
          xAxis: detail.xAxis,
          yAxis: detail.yAxis,
          aggregation: detail.aggregation,
          groupBy: detail.groupBy,
          sqlQuery: detail.sqlQuery,
          styleConfig: savedStyle,
        };
        setResult(nextResult);
        applySavedSnapshots(res.id, detail.title, savedStyle, nextResult);
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

  async function handleSaveAsNew() {
    if (!result || !savedChartId) return;
    const title = uniqueCopyTitle(editableTitle.trim() || result.title);
    setSaving(true);
    setError('');
    try {
      const res = await saveChart({
        title,
        chartType: result.chartType,
        xAxis: result.xAxis,
        yAxis: result.yAxis,
        aggregation: result.aggregation,
        groupBy: result.groupBy,
        sqlQuery: result.sqlQuery,
        connectionId: connectionId || null,
        tableName: tableName || null,
        styleConfig,
      });
      const detail = await getChart(res.id);
      const savedStyle = detail.styleConfig ?? styleConfig;
      const nextResult: ChartConfigResponse = {
        ...result,
        title: detail.title,
        chartType: detail.chartType,
        xAxis: detail.xAxis,
        yAxis: detail.yAxis,
        aggregation: detail.aggregation,
        groupBy: detail.groupBy,
        sqlQuery: detail.sqlQuery,
        styleConfig: savedStyle,
      };
      setResult(nextResult);
      applySavedSnapshots(res.id, detail.title, savedStyle, nextResult);
      toast.success('Saved as new chart.');
      navigate(graphEditPath(res.id), {
        replace: true,
        state: fromCharts ? { fromCharts: true } : undefined,
      });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save as new chart';
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  }

  function handleCancelEdits() {
    if (savedStyleSnapshot) setStyleConfig(cloneStyle(savedStyleSnapshot));
    if (savedTitleSnapshot !== null) setEditableTitle(savedTitleSnapshot);
    if (savedResultSnapshot) setResult(cloneResult(savedResultSnapshot));
    setRefinePrompt('');
    setPreRefineSnapshot(null);
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
    setSavedResultSnapshot(null);
    setPreRefineSnapshot(null);
    setRefinePrompt('');
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
{isEditingExisting && (editableTitle || 'Update title, style, or adjust with AI.')}
            {!isEditingExisting && step === 'pick-connection' && 'Choose a data source to visualize.'}
            {!isEditingExisting && step === 'pick-table' && sourceType === 'connection' && (
              <>
                Tables in <span className="text-foreground font-medium">{connectionName || 'database'}</span>
              </>
            )}
            {!isEditingExisting && step === 'pick-table' && sourceType === 'collection' && (
              <>
                Files in <span className="text-foreground font-medium">{collectionName || 'collection'}</span>
              </>
            )}
            {!isEditingExisting && step === 'generate' && (
              <>
                {sourceType === 'collection' ? 'File' : 'Table'}{' '}
                <span className="text-foreground font-medium">{fileName || tableName}</span>
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
        <div className="border-destructive/40 bg-destructive/10 text-destructive rounded-lg border px-3 py-2 text-sm whitespace-pre-wrap break-words">
          {error}
        </div>
      )}

      {aiDebug && (
        <details
          className="border-border bg-muted/30 rounded-lg border px-3 py-2 text-sm"
          open={aiDebugOpen}
          onToggle={(e) => setAiDebugOpen((e.target as HTMLDetailsElement).open)}
        >
          <summary className="cursor-pointer font-medium select-none">
            AI debug — last generation
            {aiDebug.finishReason ? ` (finish: ${aiDebug.finishReason})` : ''}
          </summary>
          <div className="mt-2 space-y-2">
            {aiDebug.notes && aiDebug.notes.length > 0 && (
              <ul className="text-muted-foreground list-inside list-disc text-xs">
                {aiDebug.notes.map((n) => (
                  <li key={n}>{n}</li>
                ))}
              </ul>
            )}
            <p className="text-muted-foreground text-xs">
              Final: <span className="text-foreground font-mono">{aiDebug.chartType}</span>
            </p>
            <div>
              <p className="text-muted-foreground mb-1 text-xs font-medium">Final styleConfig</p>
              <pre className="bg-background max-h-40 overflow-auto rounded border p-2 text-xs">
                {JSON.stringify(aiDebug.styleConfig ?? null, null, 2)}
              </pre>
            </div>
            <div>
              <p className="text-muted-foreground mb-1 text-xs font-medium">Final SQL</p>
              <pre className="bg-background max-h-32 overflow-auto rounded border p-2 text-xs whitespace-pre-wrap">
                {aiDebug.sqlQuery}
              </pre>
            </div>
            {aiDebug.rawJson && (
              <div>
                <p className="text-muted-foreground mb-1 text-xs font-medium">Raw model JSON</p>
                <pre className="bg-background max-h-56 overflow-auto rounded border p-2 text-xs whitespace-pre-wrap">
                  {aiDebug.rawJson}
                </pre>
              </div>
            )}
          </div>
        </details>
      )}

      {step === 'pick-connection' && (
        <div className="grid gap-6">
          <div>
            <h2 className="text-muted-foreground mb-2 text-xs font-semibold uppercase tracking-wide">
              Database connections
            </h2>
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
                <p className="text-muted-foreground col-span-full text-sm">
                  No database connections yet. Ask an admin to add one.
                </p>
              )}
            </div>
          </div>

          <div>
            <h2 className="text-muted-foreground mb-2 text-xs font-semibold uppercase tracking-wide">
              Data collections
            </h2>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {collectionsLoading && (
                <p className="text-muted-foreground col-span-full text-sm">Loading collections…</p>
              )}
              {!collectionsLoading &&
                collections.map((coll) => (
                  <button
                    key={coll.id}
                    type="button"
                    onClick={() => selectCollection(coll)}
                    className="border-border bg-card hover:border-brand/40 hover:bg-muted/40 flex cursor-pointer flex-col gap-3 rounded-xl border p-5 text-left transition-colors"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="bg-brand/10 text-brand flex size-10 items-center justify-center rounded-lg">
                        <Folder className="size-5" />
                      </div>
                      <span className="text-muted-foreground text-xs">
                        {coll.fileCount} file{coll.fileCount === 1 ? '' : 's'}
                      </span>
                    </div>
                    <div>
                      <p className="font-semibold">{coll.name}</p>
                      <p className="text-muted-foreground text-sm">
                        {coll.description || `${coll.rowCount.toLocaleString()} rows`}
                      </p>
                    </div>
                    <span className="text-muted-foreground flex items-center gap-1 text-xs">
                      Browse files <ChevronRight className="size-3.5" />
                    </span>
                  </button>
                ))}
              {!collectionsLoading && collections.length === 0 && (
                <p className="text-muted-foreground col-span-full text-sm">
                  No data collections yet. Add one on the Connections page.
                </p>
              )}
            </div>
          </div>
        </div>
      )}

      {step === 'pick-table' && (
        <Card>
          <CardHeader>
            <CardTitle>{sourceType === 'collection' ? 'Files' : 'Tables'}</CardTitle>
            <CardDescription>
              {sourceType === 'collection'
                ? 'Pick a file in this collection to generate a chart from.'
                : 'Pick a table to generate a chart from.'}
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-2">
            {sourceType === 'connection' && (
              <>
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
              </>
            )}
            {sourceType === 'collection' && (
              <>
                {filesLoading && <p className="text-muted-foreground text-sm">Loading files…</p>}
                {!filesLoading &&
                  files.map((f) => (
                    <button
                      key={f.id}
                      type="button"
                      onClick={() => selectFile(f)}
                      className="hover:bg-muted flex w-full cursor-pointer items-center justify-between rounded-lg border px-4 py-3 text-left transition-colors"
                    >
                      <div className="flex items-center gap-3">
                        <Table2 className="text-brand size-4" />
                        <div>
                          <p className="font-medium">{f.name}</p>
                          <p className="text-muted-foreground text-xs">
                            {f.columnCount} column{f.columnCount === 1 ? '' : 's'} ·{' '}
                            {f.rowCount.toLocaleString()} rows
                          </p>
                        </div>
                      </div>
                      <ChevronRight className="text-muted-foreground size-4" />
                    </button>
                  ))}
                {!filesLoading && files.length === 0 && (
                  <p className="text-muted-foreground text-sm">No files found in this collection.</p>
                )}
              </>
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

                  {result.sqlQuery ? (
                    <details className="mt-4">
                      <summary className="text-muted-foreground hover:text-foreground cursor-pointer text-sm font-medium">
                        Show SQL
                      </summary>
                      <pre className="bg-muted mt-2 overflow-x-auto rounded-lg p-3 text-xs">
                        {result.sqlQuery}
                      </pre>
                    </details>
                  ) : (
                    <p className="text-muted-foreground mt-4 text-xs">
                      Generated from an uploaded data file via a query model.
                    </p>
                  )}

                  <div className="mt-4 grid gap-2">
                    <label className="text-sm font-medium" htmlFor="refine-prompt">
                      Adjust with AI
                    </label>
                    <Textarea
                      id="refine-prompt"
                      value={refinePrompt}
                      onChange={(e) => setRefinePrompt(e.target.value)}
                      placeholder="Describe how to change this chart… (e.g. Switch to a line chart and use monthly totals)"
                      rows={2}
                      disabled={refining || generating}
                    />
                    <div className="flex flex-wrap gap-2">
                      <Button
                        type="button"
                        variant="secondary"
                        className="w-fit"
                        disabled={refining || generating || !refinePrompt.trim() || !connectionId || !tableName}
                        onClick={() => void handleRefine()}
                      >
                        {refining ? <Loader2 className="animate-spin" /> : <Sparkles />}
                        {refining ? 'Adjusting…' : 'Apply adjustment'}
                      </Button>
                      {/* Cleared in applySavedSnapshots / handleCancelEdits after Save or Cancel. */}
                      {preRefineSnapshot && (
                        <Button
                          type="button"
                          variant="outline"
                          className="w-fit"
                          disabled={refining || generating || saving}
                          onClick={handleUndoRefine}
                        >
                          <Undo2 />
                          Undo last adjustment
                        </Button>
                      )}
                    </div>
                  </div>

                  <div className="mt-6 flex flex-wrap gap-2">
                    {!savedChartId && (
                      <Button onClick={() => void handleSave()} disabled={saving || refining}>
                        {saving && <Loader2 className="animate-spin" />}
                        Save chart
                      </Button>
                    )}
                    {savedChartId && isDirty && (
                      <>
                        <Button onClick={() => void handleSave()} disabled={saving || refining}>
                          {saving && <Loader2 className="animate-spin" />}
                          Save
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          disabled={saving || refining}
                          onClick={() => void handleSaveAsNew()}
                        >
                          Save as new
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          disabled={saving || refining}
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
