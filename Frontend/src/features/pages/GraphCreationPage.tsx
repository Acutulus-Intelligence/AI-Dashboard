import { useEffect, useState } from 'react';
import { useLocation, useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, Sparkles, BarChart3, Brain, Loader2 } from 'lucide-react';
import { getTablePreview, type TablePreview } from '../../services/connectionsApi';
import { generateChart, type ChartConfigResponse } from '../../services/graphsApi';
import { saveChart } from '../../lib/api/charts';
import { getAll as getAllCharts } from '../charts/registry';

type Mode = 'prompt' | 'prefab' | 'auto';

export default function GraphCreationPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as { connectionId?: string; table?: string } | null;
  const connectionId = state?.connectionId ?? '';
  const tableName = state?.table ?? '';

  const [preview, setPreview] = useState<TablePreview | null>(null);
  const [loading, setLoading] = useState(true);
  const [mode, setMode] = useState<Mode>('auto');
  const [prompt, setPrompt] = useState('');
  const [prefabType, setPrefabType] = useState('');
  const [generating, setGenerating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savedChartId, setSavedChartId] = useState<string | null>(null);
  const [result, setResult] = useState<ChartConfigResponse | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!connectionId || !tableName) return;
    setLoading(true);
    getTablePreview(connectionId, tableName)
      .then(setPreview)
      .catch(() => setError('Failed to load table schema'))
      .finally(() => setLoading(false));
  }, [connectionId, tableName]);

  const handleGenerate = async () => {
    setError('');
    setGenerating(true);
    try {
      const res = await generateChart({
        connectionId,
        tableName,
        prompt: mode === 'prompt' ? prompt : undefined,
        prefabChartType: mode === 'prefab' ? prefabType : undefined,
        mode,
      });
      setResult(res);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Generation failed');
    }
    setGenerating(false);
  };

  if (!connectionId || !tableName) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="text-center">
          <p className="mb-4 text-body-md text-on-surface-variant">No table selected.</p>
          <Link to="/dashboard/connections" className="text-secondary underline">Go to Connections</Link>
        </div>
      </div>
    );
  }

  const chartOptions = getAllCharts();

  return (
    <div className="min-h-screen bg-background p-8">
      <div className="mx-auto max-w-4xl">
        <Link to="/dashboard/connections" className="mb-6 flex items-center gap-2 text-body-sm text-on-surface-variant hover:text-secondary">
          <ArrowLeft size={16} /> Back to Connections
        </Link>

        <h1 className="mb-2 text-headline-lg font-bold text-on-background">Create Graph</h1>
        <p className="mb-8 text-body-md text-on-surface-variant">
          Table: <span className="font-semibold text-on-background">{tableName}</span>
        </p>

        {loading && <div className="text-body-md text-on-surface-variant">Loading schema...</div>}

        {preview && (
          <div className="mb-8 rounded-xl border border-outline-variant bg-surface p-4">
            <h2 className="mb-3 text-headline-md font-semibold text-on-background">Schema</h2>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-body-sm">
                <thead>
                  <tr className="border-b border-outline-variant text-on-surface-variant">
                    <th className="pb-2 pr-4 font-medium">Column</th>
                    <th className="pb-2 pr-4 font-medium">Type</th>
                    <th className="pb-2 font-medium">Nullable</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.columns.map((col) => (
                    <tr key={col.columnName} className="border-b border-outline-variant/50 text-on-background">
                      <td className="py-2 pr-4 font-medium">{col.columnName}</td>
                      <td className="py-2 pr-4 text-on-surface-variant">{col.dataType}</td>
                      <td className="py-2 text-on-surface-variant">{col.isNullable ? 'Yes' : 'No'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {preview.rows.length > 0 && (
              <details className="mt-4">
                <summary className="cursor-pointer text-body-sm font-medium text-secondary">Preview Data (first {preview.rows.length} rows)</summary>
                <div className="mt-2 overflow-x-auto">
                  <table className="w-full text-left text-body-sm">
                    <thead>
                      <tr className="border-b border-outline-variant text-on-surface-variant">
                        {preview.columns.map((col) => (
                          <th key={col.columnName} className="pb-2 pr-4 font-medium">{col.columnName}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {preview.rows.map((row, i) => (
                        <tr key={i} className="border-b border-outline-variant/50 text-on-background">
                          {preview.columns.map((col) => (
                            <td key={col.columnName} className="py-2 pr-4">{String(row[col.columnName] ?? '')}</td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </details>
            )}
          </div>
        )}

        {error && <div className="mb-4 rounded-lg bg-error/10 p-3 text-body-sm text-error">{error}</div>}

        {!result && (
          <div className="mb-8 rounded-xl border border-outline-variant bg-surface p-6">
            <h2 className="mb-4 text-headline-md font-semibold text-on-background">How to visualize?</h2>

            <div className="mb-6 grid grid-cols-3 gap-4">
              {[
                { id: 'prompt' as Mode, icon: Sparkles, label: 'Describe', desc: 'Write in natural language' },
                { id: 'prefab' as Mode, icon: BarChart3, label: 'Pick Type', desc: 'Choose chart type' },
                { id: 'auto' as Mode, icon: Brain, label: 'AI Decide', desc: 'Let AI choose for you' },
              ].map((opt) => (
                <button
                  key={opt.id}
                  onClick={() => setMode(opt.id)}
                  className={`flex flex-col items-center gap-2 rounded-xl border p-4 text-center transition-all cursor-pointer ${
                    mode === opt.id
                      ? 'border-secondary bg-secondary/5'
                      : 'border-outline-variant hover:bg-surface-container'
                  }`}
                >
                  <opt.icon size={24} className={mode === opt.id ? 'text-secondary' : 'text-on-surface-variant'} />
                  <span className="font-semibold text-on-background">{opt.label}</span>
                  <span className="text-body-sm text-on-surface-variant">{opt.desc}</span>
                </button>
              ))}
            </div>

            {mode === 'prompt' && (
              <textarea
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                placeholder="Describe the chart you want... (e.g. Show top 10 products by revenue)"
                rows={3}
                className="mb-4 w-full rounded-lg border border-outline-variant bg-surface p-3 text-body-md text-on-background outline-none focus:border-secondary"
              />
            )}

            {mode === 'prefab' && (
              <div className="mb-4 flex flex-wrap gap-2">
                {chartOptions.map((chart) => (
                  <button
                    key={chart.id}
                    onClick={() => setPrefabType(chart.id)}
                    className={`flex items-center gap-2 rounded-lg border px-4 py-2 cursor-pointer ${
                      prefabType === chart.id
                        ? 'border-secondary bg-secondary/5 text-secondary'
                        : 'border-outline-variant text-on-surface-variant hover:bg-surface-container'
                    }`}
                  >
                    <chart.icon size={16} />
                    {chart.label}
                  </button>
                ))}
              </div>
            )}

            {mode === 'auto' && (
              <p className="mb-4 text-body-md text-on-surface-variant">The AI will automatically select the best chart type based on the data in this table.</p>
            )}

            <button
              onClick={handleGenerate}
              disabled={generating || (mode === 'prompt' && !prompt) || (mode === 'prefab' && !prefabType)}
              className="flex cursor-pointer items-center gap-2 rounded-xl bg-primary px-6 py-3 text-body-md font-semibold text-white transition-transform active:scale-95 disabled:opacity-50"
            >
              {generating ? <Loader2 size={18} className="animate-spin" /> : <Sparkles size={18} />}
              {generating ? 'Generating...' : 'Generate Graph'}
            </button>
          </div>
        )}

        {result && (
          <div className="space-y-6">
            <div className="rounded-xl border border-outline-variant bg-surface p-6">
              <div className="mb-4 flex items-center justify-between">
                <h2 className="text-headline-md font-semibold text-on-background">{result.title}</h2>
                <span className="rounded-full bg-secondary/10 px-3 py-1 text-body-sm font-medium capitalize text-secondary">{result.chartType}</span>
              </div>

              <div className="h-72">
                {(() => {
                  const ChartClass = getAllCharts().find((c) => c.id === result.chartType);
                  if (!ChartClass) return <div className="text-on-surface-variant">Unknown chart type: {result.chartType}</div>;

                  const data = transformResult(result);
                  return ChartClass.render(data);
                })()}
              </div>

              <details className="mt-4">
                <summary className="cursor-pointer text-body-sm font-medium text-secondary">Show SQL</summary>
                <pre className="mt-2 overflow-x-auto rounded-lg bg-surface-container p-3 text-body-sm text-on-surface-variant">{result.sqlQuery}</pre>
              </details>
            </div>

            <div className="flex gap-3">
              {savedChartId ? (
                <>
                  <button
                    onClick={() => navigate('/dashboard')}
                    className="cursor-pointer rounded-xl bg-secondary px-6 py-3 text-body-md font-semibold text-white transition-transform active:scale-95"
                  >
                    View in Dashboard
                  </button>
                  <button
                    onClick={() => { setResult(null); setSavedChartId(null); setError(''); }}
                    className="cursor-pointer rounded-xl border border-outline-variant bg-surface px-6 py-3 text-body-md text-on-surface-variant"
                  >
                    Create Another
                  </button>
                </>
              ) : (
                <>
                  <button
                    onClick={async () => {
                      setSaving(true);
                      try {
                        const res = await saveChart({
                          title: result.title,
                          chartType: result.chartType,
                          xAxis: result.xAxis,
                          yAxis: result.yAxis,
                          aggregation: result.aggregation,
                          groupBy: result.groupBy,
                          sqlQuery: result.sqlQuery,
                          connectionId,
                          tableName,
                        });
                        setSavedChartId(res.id);
                      } catch {
                        setError('Failed to save chart');
                      }
                      setSaving(false);
                    }}
                    disabled={saving}
                    className="flex cursor-pointer items-center gap-2 rounded-xl bg-secondary px-6 py-3 text-body-md font-semibold text-white transition-transform active:scale-95"
                  >
                    {saving ? <Loader2 size={18} className="animate-spin" /> : null}
                    {saving ? 'Saving...' : 'Save Chart'}
                  </button>
                  <button
                    onClick={() => { setResult(null); setError(''); }}
                    className="cursor-pointer rounded-xl border border-outline-variant bg-surface px-6 py-3 text-body-md text-on-surface-variant"
                  >
                    Create Another
                  </button>
                </>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export function transformResult(res: ChartConfigResponse) {
  if (!res.queryResult?.length) return { labels: [], datasets: [], queryResult: [] };

  const labels = res.queryResult.map((row) => String(row[res.xAxis] ?? ''));
  const datasets = res.yAxis.map((axis) => ({
    label: axis,
    values: res.queryResult.map((row) => {
      const v = row[axis];
      const num = typeof v === 'number' ? v : Number(v);
      return isNaN(num) ? 0 : num;
    }),
  }));

  return { labels, datasets, queryResult: res.queryResult };
}
