import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { FileUp, Loader2, Trash2, Table2, Sparkles, BarChart3, Brain, X } from 'lucide-react';
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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { saveChart } from '../../lib/api/charts';
import {
  deleteDataset,
  generateDatasetChart,
  getDataset,
  getDatasets,
  uploadDataset,
  type DatasetDetailResponse,
  type DatasetResponse,
} from '../../services/datasetsApi';
import type { ChartConfigResponse } from '../../services/graphsApi';
import ChartRenderer from '../charts/ChartRenderer';
import { get, getAll } from '../charts/registry';
import { transformResult } from '../charts/transform';
import AddToDashboardDialog from '../components/AddToDashboardDialog';
import ConfirmDialog from '../components/ConfirmDialog';
import AppShell from '../layouts/AppShell';
import type { Crumb } from '../layouts/AppHeader';

type GenMode = 'prompt' | 'prefab' | 'auto';

const MODES: { id: GenMode; icon: typeof Sparkles; label: string; desc: string }[] = [
  { id: 'prompt', icon: Sparkles, label: 'Describe', desc: 'Write in natural language' },
  { id: 'prefab', icon: BarChart3, label: 'Pick type', desc: 'Choose a chart type' },
  { id: 'auto', icon: Brain, label: 'AI decide', desc: 'Let the AI choose' },
];

const breadcrumbs: Crumb[] = [{ label: 'Datasets' }];

export default function DatasetsPage() {
  const [datasets, setDatasets] = useState<DatasetResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [detail, setDetail] = useState<DatasetDetailResponse | null>(null);

  const [deleteTarget, setDeleteTarget] = useState<DatasetResponse | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [genDataset, setGenDataset] = useState<DatasetDetailResponse | null>(null);
  const [genOpen, setGenOpen] = useState(false);
  const [mode, setMode] = useState<GenMode>('auto');
  const [prompt, setPrompt] = useState('');
  const [prefabType, setPrefabType] = useState('');
  const [generating, setGenerating] = useState(false);
  const [result, setResult] = useState<ChartConfigResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [savedChartId, setSavedChartId] = useState<string | null>(null);
  const [addToDashboardOpen, setAddToDashboardOpen] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const list = await getDatasets();
        if (cancelled) return;
        setDatasets(list);
      } catch {
        if (cancelled) return;
        setError('Failed to load datasets.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function refresh() {
    try {
      const list = await getDatasets();
      setDatasets(list);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load datasets.');
    }
  }

  async function handlePickFile(next: File | null) {
    setFile(next);
    setError('');
    if (!next) return;
    if (!next.name.toLowerCase().endsWith('.csv') && !next.name.toLowerCase().endsWith('.xlsx')) {
      setError('Only .csv or .xlsx files are supported.');
      return;
    }
    setUploading(true);
    try {
      const created = await uploadDataset(next);
      setFile(null);
      toast.success(`Uploaded “${created.name}”.`);
      const target = await getDataset(created.id);
      setDetail(target);
      await refresh();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Upload failed.');
    } finally {
      setUploading(false);
    }
  }

  async function handlePreview(id: string) {
    setError('');
    try {
      setDetail(await getDataset(id));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load dataset.');
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await deleteDataset(deleteTarget.id);
      setDatasets((prev) => prev.filter((d) => d.id !== deleteTarget.id));
      if (detail?.id === deleteTarget.id) setDetail(null);
      toast.success('Dataset deleted.');
      setDeleteTarget(null);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Delete failed.');
    } finally {
      setDeleting(false);
    }
  }

  function openGenerate(ds: DatasetDetailResponse) {
    setGenDataset(ds);
    setResult(null);
    setPrompt('');
    setPrefabType('');
    setMode('auto');
    setSavedChartId(null);
    setError('');
    setGenOpen(true);
  }

  async function handleGenerate() {
    if (!genDataset) return;
    setError('');
    setGenerating(true);
    setSavedChartId(null);
    try {
      const res = await generateDatasetChart(genDataset.id, {
        prompt: mode === 'prompt' ? prompt : undefined,
        prefabChartType: mode === 'prefab' ? prefabType : undefined,
        mode,
      });
      setResult(res);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Generation failed.');
    } finally {
      setGenerating(false);
    }
  }

  async function handleSave(): Promise<string | null> {
    if (!result || !genDataset) return null;
    setSaving(true);
    setError('');
    try {
      const saved = await saveChart({
        title: result.title,
        chartType: result.chartType,
        xAxis: result.xAxis,
        yAxis: result.yAxis,
        aggregation: result.aggregation,
        groupBy: result.groupBy,
        sqlQuery: result.sqlQuery,
        connectionId: null,
        datasetId: genDataset.id,
        tableName: genDataset.tableName,
        styleConfig: result.styleConfig ?? {},
      });
      setSavedChartId(saved.id);
      toast.success('Chart saved.');
      return saved.id;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save chart.';
      setError(message);
      toast.error(message);
      return null;
    } finally {
      setSaving(false);
    }
  }

  async function handleAddToDashboard() {
    const id = savedChartId ?? (await handleSave());
    if (id) setAddToDashboardOpen(true);
  }

  const chartData = useMemo(() => (result ? transformResult(result) : null), [result]);
  const descriptor = result ? get(result.chartType) : undefined;

  async function openGenerateFromList(ds: DatasetResponse) {
    setError('');
    try {
      const target = await getDataset(ds.id);
      openGenerate(target);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load dataset.');
    }
  }

  return (
    <AppShell breadcrumbs={breadcrumbs}>
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Datasets</h1>
        <p className="text-muted-foreground text-sm">
          Upload a CSV or XLSX file to turn it into immediately queryable charts — no database
          connection needed.
        </p>
      </div>

      {error && (
        <div className="border-destructive/40 bg-destructive/10 text-destructive rounded-lg border px-3 py-2 text-sm">
          {error}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <FileUp />
                Upload a file
              </CardTitle>
              <CardDescription>
                Files are stored in your account and queried with a built-in engine.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <label className="border-border hover:bg-muted flex cursor-pointer items-center justify-center gap-2 rounded-lg border border-dashed px-4 py-8 text-sm text-muted-foreground">
                <FileUp />
                {file ? `Ready: ${file.name}` : 'Click to choose a .csv or .xlsx file'}
                <input
                  type="file"
                  accept=".csv,.xlsx,text/csv"
                  className="hidden"
                  onChange={(e) => void handlePickFile(e.target.files?.[0] ?? null)}
                />
              </label>
              {uploading && (
                <p className="mt-2 flex items-center gap-2 text-sm text-muted-foreground">
                  <Loader2 className="animate-spin" /> Uploading…
                </p>
              )}
            </CardContent>
          </Card>

          <Card className="flex-1">
            <CardHeader>
              <CardTitle>Saved datasets</CardTitle>
            </CardHeader>
            <CardContent>
              {loading && <p className="text-muted-foreground text-sm">Loading…</p>}
              {!loading && datasets.length === 0 && (
                <p className="text-muted-foreground text-sm">No datasets yet. Upload one above.</p>
              )}
              {!loading &&
                datasets.map((d) => (
                  <div
                    key={d.id}
                    className="hover:bg-muted flex items-center justify-between gap-3 rounded-lg px-2 py-2.5"
                  >
                    <button
                      type="button"
                      onClick={() => void handlePreview(d.id)}
                      className="flex min-w-0 flex-1 cursor-pointer items-center gap-3 text-left"
                    >
                      <div className="bg-primary/10 text-primary flex size-9 shrink-0 items-center justify-center rounded-lg">
                        <Table2 className="size-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="truncate font-medium">{d.name}</p>
                        <p className="text-muted-foreground text-xs">
                          {d.columnCount} columns · {d.rowCount} rows ·{' '}
                          {new Date(d.createdAt).toLocaleDateString()}
                        </p>
                      </div>
                    </button>
                    <div className="flex shrink-0 items-center gap-1">
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => void openGenerateFromList(d)}
                      >
                        <Sparkles />
                        Chart
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="text-red-500"
                        onClick={() => setDeleteTarget(d)}
                        aria-label={`Delete ${d.name}`}
                      >
                        <Trash2 />
                      </Button>
                    </div>
                  </div>
                ))}
            </CardContent>
          </Card>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Preview</CardTitle>
            <CardDescription>
              {detail ? `${detail.tableName} — first ${detail.rowCount} rows` : 'Select a dataset.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {!detail && <p className="text-muted-foreground text-sm">No dataset selected.</p>}
            {detail && (
              <div className="max-h-[60vh] overflow-auto">
                <table className="min-w-full text-left text-sm">
                  <thead>
                    <tr className="text-muted-foreground border-b">
                      {detail.columns.map((col) => (
                        <th key={col.name} className="sticky top-0 z-10 bg-card pb-2 pr-4 font-medium">
                          <span className="block max-w-40 truncate">{col.name}</span>
                          <span className="block text-[10px] font-normal">{col.type}</span>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {detail.previewRows.map((row, i) => (
                      <tr key={i} className="border-b last:border-0">
                        {detail.columns.map((col) => (
                          <td key={col.name} className="text-muted-foreground max-w-40 truncate py-2 pr-4">
                            {String(row[col.name] ?? '')}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <ConfirmDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete dataset"
        description={`Delete “${deleteTarget?.name ?? ''}”? Charts using this dataset will keep their last data but stop refreshing.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleting}
        onConfirm={() => void confirmDelete()}
      />

      <Dialog open={genOpen} onOpenChange={setGenOpen}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Generate chart from {genDataset?.name ?? ''}</DialogTitle>
            <DialogDescription>
              {descriptor ? 'Live preview below the controls.' : 'Pick a mode to generate.'}
            </DialogDescription>
          </DialogHeader>

          {!result && (
            <div className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-3">
                {MODES.map((opt) => (
                  <button
                    key={opt.id}
                    type="button"
                    onClick={() => setMode(opt.id)}
                    className={cn(
                      'flex cursor-pointer flex-col items-center gap-2 rounded-xl border p-3 text-center transition-colors',
                      mode === opt.id ? 'border-primary bg-primary/5' : 'hover:bg-muted',
                    )}
                  >
                    <opt.icon className={cn('size-5', mode === opt.id ? 'text-primary' : 'text-muted-foreground')} />
                    <span className="font-medium">{opt.label}</span>
                    <span className="text-muted-foreground text-xs">{opt.desc}</span>
                  </button>
                ))}
              </div>

              {mode === 'prompt' && (
                <Textarea
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  placeholder="Describe the chart you want…"
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
                <p className="text-muted-foreground text-sm">The AI picks the best chart for this data.</p>
              )}

              <Button
                onClick={() => void handleGenerate()}
                disabled={generating || (mode === 'prompt' && !prompt) || (mode === 'prefab' && !prefabType)}
              >
                {generating ? <Loader2 className="animate-spin" /> : <Sparkles />}
                {generating ? 'Generating…' : 'Generate'}
              </Button>
            </div>
          )}

          {result && chartData && (
            <div className="min-w-0 space-y-4">
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <h3 className="text-lg font-semibold">{result.title}</h3>
                  <Badge variant="secondary" className="capitalize">
                    {result.chartType}
                  </Badge>
                </div>
                <Button
                  variant="secondary"
                  onClick={() => void handleAddToDashboard()}
                  disabled={saving}
                >
                  Add to dashboard
                </Button>
              </div>
              <div className="bg-muted/20 h-72 rounded-lg border p-2">
                <ChartRenderer chartId={result.chartType} data={chartData} styleConfig={result.styleConfig ?? {}} />
              </div>
              <details>
                <summary className="text-muted-foreground hover:text-foreground cursor-pointer text-sm font-medium">
                  Show SQL
                </summary>
                <pre className="bg-muted mt-2 overflow-x-auto rounded-lg p-3 text-xs">{result.sqlQuery}</pre>
              </details>
              <div className="flex flex-wrap gap-2">
                {!savedChartId && (
                  <Button onClick={() => void handleSave()} disabled={saving}>
                    {saving && <Loader2 className="animate-spin" />}
                    Save chart
                  </Button>
                )}
                <Button variant="outline" onClick={() => setResult(null)}>
                  <X />
                  Try another
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

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