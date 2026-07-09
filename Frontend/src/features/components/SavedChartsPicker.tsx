import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Search, BarChart3, LineChart, PieChart, LayoutDashboard, Table2, X, Loader2 } from 'lucide-react';
import { getCharts, type ChartResponse } from '../../lib/api/charts';

interface SavedChartsPickerProps {
  open: boolean;
  onClose: () => void;
  onSelect: (savedChartId: string) => void;
}

const chartIcons: Record<string, typeof BarChart3> = {
  bar: BarChart3,
  line: LineChart,
  pie: PieChart,
  area: LayoutDashboard,
  scatter: BarChart3,
  table: Table2,
};

export default function SavedChartsPicker({ open, onClose, onSelect }: SavedChartsPickerProps) {
  const navigate = useNavigate();
  const [charts, setCharts] = useState<ChartResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (!open) return;
    setLoading(true);
    getCharts()
      .then(setCharts)
      .catch(() => setCharts([]))
      .finally(() => setLoading(false));
  }, [open]);

  if (!open) return null;

  const filtered = charts.filter((c) =>
    c.title.toLowerCase().includes(search.toLowerCase()),
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onClose}>
      <div
        className="mx-4 w-full max-w-lg rounded-2xl border border-outline-variant bg-surface-container-lowest p-6 shadow-lg"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-headline-md font-bold text-on-background">Saved Charts</h2>
          <button
            type="button"
            onClick={onClose}
            className="cursor-pointer rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-container"
          >
            <X size={18} />
          </button>
        </div>

        <div className="relative mb-4">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant" />
          <input
            type="text"
            placeholder="Search saved charts..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-xl border border-outline-variant bg-surface py-2.5 pl-9 pr-4 text-body-md text-on-background outline-none focus:border-secondary"
          />
        </div>

        <button
          type="button"
          onClick={() => {
            onClose();
            navigate('/dashboard/connections');
          }}
          className="mb-4 flex w-full cursor-pointer items-center gap-3 rounded-xl border border-dashed border-secondary bg-secondary/5 px-4 py-3 text-left text-body-md font-semibold text-secondary transition-colors hover:bg-secondary/10"
        >
          <Plus size={18} />
          New Graph
        </button>

        <div className="max-h-80 space-y-2 overflow-y-auto">
          {loading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 size={20} className="animate-spin text-on-surface-variant" />
            </div>
          ) : filtered.length === 0 ? (
            <p className="py-8 text-center text-body-md text-on-surface-variant">
              {search ? 'No charts match your search.' : 'No saved charts yet.'}
            </p>
          ) : (
            filtered.map((chart) => {
              const IconComponent = chartIcons[chart.chartType] ?? BarChart3;
              return (
                <button
                  key={chart.id}
                  type="button"
                  onClick={() => {
                    onSelect(chart.id);
                    onClose();
                  }}
                  className="flex w-full cursor-pointer items-center gap-3 rounded-xl border border-outline-variant bg-surface px-4 py-3 text-left transition-colors hover:bg-surface-container"
                >
                  <IconComponent size={18} className="text-secondary shrink-0" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-body-md font-medium text-on-background">{chart.title}</p>
                    <p className="text-body-sm text-on-surface-variant">
                      {chart.chartType} &middot; {new Date(chart.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                </button>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
}
