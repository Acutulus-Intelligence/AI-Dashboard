import { useState, useCallback, useRef, forwardRef, useImperativeHandle, useEffect, useMemo } from 'react';
import GridLayout, { type Layout, type LayoutItem } from 'react-grid-layout/legacy';
import 'react-grid-layout/css/styles.css';
import { X } from 'lucide-react';
import ChartRenderer from '../charts/ChartRenderer';
import { getDashboard, saveWidgets } from '../../lib/api/dashboards';
import { executeChart } from '../../lib/api/charts';
import { transformResult } from '../pages/GraphCreationPage';

export interface DashboardGridHandle {
  addWidget: (savedChartId: string) => void;
  resetDashboard: () => void;
}

interface DashboardGridProps {
  editMode: boolean;
}

interface WidgetData {
  labels: string[];
  datasets: { label: string; values: number[] }[];
  queryResult?: Record<string, unknown>[];
}

interface Widget {
  id: string;
  savedChartId: string;
  chartId: string;
  title: string;
  data?: WidgetData;
}

const COLS = 12;
const ROW_HEIGHT = 80;
const GAP = 16;
const MIN_W = 2;
const MIN_H = 2;

function useContainerWidth() {
  const ref = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(1200);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        setWidth(entry.contentRect.width);
      }
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  return { width, containerRef: ref };
}

let nextId = 200;

function createId(): string {
  return String(nextId++);
}

function GridOverlay({ containerHeight, containerWidth }: { containerHeight: number; containerWidth: number }) {
  const rowCount = Math.ceil(containerHeight / (ROW_HEIGHT + GAP));
  const lines: React.ReactNode[] = [];

  for (let i = 1; i < COLS; i++) {
    const left = i * (containerWidth - GAP) / COLS + GAP / 2;
    lines.push(
      <div
        key={`gl-v-${i}`}
        className="absolute top-0"
        style={{
          left: left - 0.5,
          width: 1,
          height: containerHeight,
          backgroundColor: 'rgba(0,104,122,0.08)',
        }}
      />,
    );
  }

  for (let j = 1; j <= rowCount; j++) {
    const top = j * (ROW_HEIGHT + GAP) + GAP / 2;
    if (top > containerHeight) break;
    lines.push(
      <div
        key={`gl-h-${j}`}
        className="absolute left-0"
        style={{
          top: top - 0.5,
          height: 1,
          width: '100%',
          backgroundColor: 'rgba(0,104,122,0.08)',
        }}
      />,
    );
  }

  return (
    <div className="pointer-events-none absolute inset-0" style={{ zIndex: 0 }}>
      {lines}
    </div>
  );
}

async function addWidgetWithData(
  savedChartId: string,
  setWidgets: React.Dispatch<React.SetStateAction<Widget[]>>,
  setLayout: React.Dispatch<React.SetStateAction<LayoutItem[]>>,
) {
  const result = await executeChart(savedChartId);
  const data = transformResult(result);
  const id = createId();
  setWidgets((prev) => [...prev, {
    id,
    savedChartId,
    chartId: result.chartType,
    title: result.title,
    data,
  }]);
  setLayout((prev) => [
    ...prev,
    { i: id, x: 0, y: (prev.length + 1) * 10, w: 6, h: 6, minW: MIN_W, minH: MIN_H },
  ]);
}

function widgetsToRequestItems(widgets: Widget[], layout: LayoutItem[]) {
  return widgets.map((w) => {
    const item = layout.find((l) => l.i === w.id);
    return {
      savedChartId: w.savedChartId,
      positionX: item?.x ?? 0,
      positionY: item?.y ?? 0,
      width: item?.w ?? 6,
      height: item?.h ?? 6,
    };
  });
}

const DashboardGrid = forwardRef<DashboardGridHandle, DashboardGridProps>(function DashboardGrid({ editMode }, ref) {
  const { width: containerWidth, containerRef } = useContainerWidth();

  const [widgets, setWidgets] = useState<Widget[]>([]);
  const [layout, setLayout] = useState<LayoutItem[]>([]);
  const [editingTitleId, setEditingTitleId] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getDashboard()
      .then(async (dash) => {
        if (cancelled) return;
        const w: Widget[] = [];
        const l: LayoutItem[] = [];
        for (const dw of dash.widgets) {
          const id = createId();
          w.push({
            id,
            savedChartId: dw.savedChartId,
            chartId: dw.chartType,
            title: dw.chartTitle,
          });
          l.push({
            i: id,
            x: dw.positionX,
            y: dw.positionY,
            w: dw.width,
            h: dw.height,
            minW: MIN_W,
            minH: MIN_H,
          });
        }
        const results = await Promise.allSettled(
          w.map((widget) => executeChart(widget.savedChartId)),
        );
        if (cancelled) return;
        results.forEach((result, i) => {
          if (result.status === 'fulfilled') {
            w[i].data = transformResult(result.value);
          }
        });
        setWidgets(w);
        setLayout(l);
        setLoaded(true);
      })
      .catch(() => setLoaded(true));
    return () => { cancelled = true; };
  }, []);

  const persistLayout = useCallback(async (w: Widget[], l: LayoutItem[]) => {
    const items = widgetsToRequestItems(w, l);
    try {
      await saveWidgets(items);
    } catch {
      /* ignore */
    }
  }, []);

  const handleLayoutChange = useCallback((newLayout: Layout) => {
    setLayout(newLayout as LayoutItem[]);
  }, []);

  const addWidget = useCallback(async (savedChartId: string) => {
    try {
      await addWidgetWithData(savedChartId, setWidgets, setLayout);
    } catch {
      /* ignore */
    }
  }, [widgets, layout]);

  const removeWidget = useCallback((id: string) => {
    setWidgets((prev) => prev.filter((w) => w.id !== id));
    setLayout((prev) => prev.filter((item) => item.i !== id));
  }, []);

  const handleTitleChange = useCallback((id: string, title: string) => {
    setWidgets((prev) => prev.map((w) => (w.id === id ? { ...w, title } : w)));
  }, []);

  const inputRefs = useRef<Record<string, HTMLInputElement | null>>({});

  const startEditing = useCallback((id: string) => {
    setEditingTitleId(id);
    requestAnimationFrame(() => inputRefs.current[id]?.focus());
  }, []);

  const finishEditing = useCallback(() => {
    setEditingTitleId(null);
  }, []);

  const resetDashboard = useCallback(() => {
    setWidgets([]);
    setLayout([]);
    persistLayout([], []);
  }, [persistLayout]);

  useImperativeHandle(ref, () => ({ addWidget, resetDashboard }), [addWidget, resetDashboard]);

  // Persist layout when widgets change
  useEffect(() => {
    if (!loaded) return;
    const timer = setTimeout(() => persistLayout(widgets, layout), 500);
    return () => clearTimeout(timer);
  }, [widgets, layout, loaded, persistLayout]);

  const widgetById = useMemo(() => {
    const map: Record<string, Widget> = {};
    for (const w of widgets) map[w.id] = w;
    return map;
  }, [widgets]);

  const containerHeight = useMemo(() => {
    if (layout.length === 0) return GAP;
    let maxBottom = 0;
    for (const item of layout) {
      const bottom = (item.y + item.h) * (ROW_HEIGHT + GAP) + GAP;
      if (bottom > maxBottom) maxBottom = bottom;
    }
    return maxBottom;
  }, [layout]);

  const handleClass = editMode
    ? 'drag-handle flex cursor-grab items-center justify-between border-b border-outline-variant bg-surface-container-low px-4 py-2.5 active:cursor-grabbing'
    : 'flex items-center justify-between border-b border-outline-variant bg-surface-container-low px-4 py-2.5';

  if (containerWidth <= 0) {
    return <div ref={containerRef} />;
  }

  if (!loaded) {
    return <div ref={containerRef} className="text-center text-on-surface-variant py-8">Loading dashboard...</div>;
  }

  return (
    <div
      ref={containerRef}
      className={'w-full' + (editMode ? ' dashboard-grid' : '')}
      style={{
        position: 'relative',
        height: containerHeight,
        userSelect: editMode ? 'none' : undefined,
      }}
    >
      {editMode && (
        <GridOverlay containerHeight={containerHeight} containerWidth={containerWidth} />
      )}
      {editMode && (
        <style>{`
          .dashboard-grid .react-grid-placeholder {
            background: #d4d4d4;
            opacity: 0.25;
            z-index: 0;
            border-radius: 0.75rem;
          }
        `}</style>
      )}

      <GridLayout
        layout={layout}
        cols={COLS}
        rowHeight={ROW_HEIGHT}
        width={containerWidth}
        margin={[GAP, GAP]}
        compactType="vertical"
        onLayoutChange={handleLayoutChange}
        isDraggable={editMode}
        isResizable={editMode}
        draggableHandle={editMode ? '.drag-handle' : undefined}
      >
        {layout.map((item) => {
          const widget = widgetById[item.i];
          if (!widget) {
            return <div key={item.i} />;
          }
          return (
            <div
              key={item.i}
              className="flex h-full flex-col rounded-xl border border-outline-variant bg-surface-container-lowest shadow-sm"
              onContextMenu={(e) => e.preventDefault()}
            >
              <div className={handleClass}>
                {editMode && editingTitleId === widget.id ? (
                  <input
                    ref={(el) => { inputRefs.current[widget.id] = el; }}
                    type="text"
                    value={widget.title}
                    onChange={(e) => handleTitleChange(widget.id, e.target.value)}
                    onBlur={finishEditing}
                    onKeyDown={(e) => { if (e.key === 'Enter') finishEditing(); }}
                    onMouseDown={(e) => e.stopPropagation()}
                    className="flex-1 bg-transparent text-body-sm font-medium text-on-background outline-none"
                    style={{ userSelect: 'auto' }}
                  />
                ) : (
                  <span
                    className={'text-body-sm font-medium text-on-background' + (editMode ? ' cursor-pointer' : '')}
                    onDoubleClick={editMode ? () => startEditing(widget.id) : undefined}
                    title="Double-click to edit"
                  >
                    {widget.title}
                  </span>
                )}
                {editMode && (
                  <button
                    type="button"
                    onClick={() => removeWidget(widget.id)}
                    className="cursor-pointer rounded-md p-1 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-error"
                  >
                    <X size={14} />
                  </button>
                )}
              </div>
              <div className="relative min-h-0 flex-1">
                <div className="absolute inset-0 p-3">
                  <ChartRenderer chartId={widget.chartId} data={widget.data} />
                </div>
              </div>
            </div>
          );
        })}
      </GridLayout>
    </div>
  );
});

export default DashboardGrid;
