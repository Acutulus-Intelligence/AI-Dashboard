import { useState, useCallback, useRef, forwardRef, useImperativeHandle, useEffect, useMemo } from 'react';
import GridLayout, { type Layout, type LayoutItem } from 'react-grid-layout/legacy';
import 'react-grid-layout/css/styles.css';
import { X } from 'lucide-react';
import ChartRenderer from '../charts/ChartRenderer';
import { get } from '../charts/registry';

export interface DashboardGridHandle {
  addWidget: (chartId: string) => void;
  resetDashboard: () => void;
}

interface DashboardGridProps {
  editMode: boolean;
}

interface Widget {
  id: string;
  chartId: string;
  title: string;
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

const defaultWidgets: Widget[] = [];

const defaultLayout: LayoutItem[] = [];

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

const DashboardGrid = forwardRef<DashboardGridHandle, DashboardGridProps>(function DashboardGrid({ editMode }, ref) {
  const { width: containerWidth, containerRef } = useContainerWidth();

  const [widgets, setWidgets] = useState<Widget[]>(defaultWidgets);
  const [layout, setLayout] = useState<LayoutItem[]>(defaultLayout);
  const [editingTitleId, setEditingTitleId] = useState<string | null>(null);

  const widgetById = useMemo(() => {
    const map: Record<string, Widget> = {};
    for (const w of widgets) map[w.id] = w;
    return map;
  }, [widgets]);

  const handleLayoutChange = useCallback((newLayout: Layout) => {
    setLayout(newLayout as LayoutItem[]);
  }, []);

  const addWidget = useCallback((chartId: string) => {
    const chart = get(chartId);
    const size = chart?.defaultSize ?? { w: 4, h: 4 };
    const id = createId();
    setWidgets((prev) => [...prev, { id, chartId, title: chart?.label ?? 'New Chart' }]);
    setLayout((prev) => [
      ...prev,
      { i: id, x: 0, y: (prev.length + 1) * 10, w: size.w, h: size.h, minW: MIN_W, minH: MIN_H },
    ]);
  }, []);

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
    const fresh = defaultWidgets.map((w) => ({ ...w, id: createId() }));
    setWidgets(fresh);
    setLayout(
      defaultLayout.map((item, i) => ({
        ...item,
        i: fresh[i]?.id ?? createId(),
      })),
    );
  }, []);

  useImperativeHandle(ref, () => ({ addWidget, resetDashboard }), [addWidget, resetDashboard]);

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
              onSelectStart={(e) => e.preventDefault()}
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
                  <ChartRenderer chartId={widget.chartId} title={widget.title} />
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
