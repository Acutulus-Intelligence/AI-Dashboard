import { useState, useCallback, useRef, forwardRef, useImperativeHandle, useEffect, useMemo } from 'react';
import GridLayout, { type Layout, type LayoutItem } from 'react-grid-layout/legacy';
import 'react-grid-layout/css/styles.css';
import { Link, useNavigate } from 'react-router-dom';
import { Download, FileSpreadsheet, FileText, GripHorizontal, Info, MoreHorizontal, Pencil, PlusCircle, RefreshCw, Trash2, X } from 'lucide-react';
import { toast } from 'sonner';
import ChartRenderer from '../charts/ChartRenderer';
import DashboardRefreshBar, { type RefreshChartOption } from '../components/DashboardRefreshBar';
import { useAuth } from '../store/useAuth';
import { useDashboardPolling, type DashboardRefreshOutcome } from '../../hooks/useDashboardPolling';
import { ApiError } from '../../lib/api/client';
import TextWidget from '../components/TextWidget';
import TextWidgetEditShell from '../components/TextWidgetEditShell';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { getDashboard, saveWidgets, updateDashboardSettings, updateWidgetRefreshSetting, WIDGET_TYPE, TEXT_VARIANT, TEXT_ALIGN_H, TEXT_ALIGN_V, normalizeTextVariant, normalizeTextHorizontalAlign, normalizeTextVerticalAlign, type TextVariant, type TextHorizontalAlign, type TextVerticalAlign, type DashboardWidgetItem } from '../../lib/api/dashboards';
import { executeChart } from '../../lib/api/charts';
import { transformResult } from '../charts/transform';
import { exportChartAsPng } from '../charts/exportChartImage';
import {
  exportChartDataAsCsv,
  exportChartDataAsExcel,
  hasExportData as chartHasExportData,
} from '../charts/exportChartData';
import type { ChartData, ChartStyleConfig } from '../charts/types';
import { ROUTES, graphEditPath } from '../routes';

export interface DashboardGridHandle {
  addWidget: (savedChartId: string) => void;
  addTextWidget: (variant: TextVariant) => void;
  resetDashboard: () => void;
  saveEdit: () => Promise<void>;
  cancelEdit: () => void;
}

interface DashboardGridProps {
  dashboardId: string;
  editMode: boolean;
  onLoaded?: (name: string) => void;
}

interface ChartWidget {
  kind: 'chart';
  id: string;
  savedChartId: string;
  chartId: string;
  title: string;
  data?: ChartData;
  styleConfig?: ChartStyleConfig;
  autoRefreshEnabled: boolean | null;
  dataVersion: number;
}

interface ChartRuntime {
  lastUpdatedAt: number | null;
  status: 'idle' | 'refreshing' | 'error';
  errorAt?: number;
}

interface TextWidgetItem {
  kind: 'text';
  id: string;
  textContent: string;
  textVariant: TextVariant;
  textHorizontalAlign: TextHorizontalAlign;
  textVerticalAlign: TextVerticalAlign;
}

type Widget = ChartWidget | TextWidgetItem;

interface DashboardSnapshot {
  widgets: Widget[];
  layout: LayoutItem[];
}

const COLS = 12;
const ROW_HEIGHT = 80;
const GAP = 16;
const MIN_W = 2;
const MIN_H = 2;
const TEXT_MIN_W = 1;
const TEXT_MIN_H = 1;
const MAX_CONCURRENT_CHART_REFRESHES = 4;

async function runWithConcurrency<T>(
  items: T[],
  limit: number,
  worker: (item: T) => Promise<void>,
): Promise<void> {
  let nextIndex = 0;
  const workers = Array.from(
    { length: Math.min(limit, items.length) },
    async () => {
      while (nextIndex < items.length) {
        const index = nextIndex;
        nextIndex += 1;
        await worker(items[index]);
      }
    },
  );
  await Promise.all(workers);
}

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

function cloneSnapshot(widgets: Widget[], layout: LayoutItem[]): DashboardSnapshot {
  return {
    widgets: structuredClone(widgets),
    layout: structuredClone(layout),
  };
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
    kind: 'chart',
    id,
    savedChartId,
    chartId: result.chartType,
    title: result.title,
    data,
    styleConfig: result.styleConfig ?? undefined,
    autoRefreshEnabled: null,
    dataVersion: 0,
  }]);
  setLayout((prev) => [
    ...prev,
    { i: id, x: 0, y: (prev.length + 1) * 10, w: 6, h: 6, minW: MIN_W, minH: MIN_H },
  ]);
}

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function isServerWidgetId(id: string): boolean {
  return UUID_PATTERN.test(id);
}

function widgetsToRequestItems(widgets: Widget[], layout: LayoutItem[]) {
  return widgets.map((w) => {
    const item = layout.find((l) => l.i === w.id);
    const base = {
      id: isServerWidgetId(w.id) ? w.id : undefined,
      positionX: item?.x ?? 0,
      positionY: item?.y ?? 0,
      width: item?.w ?? 6,
      height: item?.h ?? 6,
    };

    if (w.kind === 'text') {
      return {
        ...base,
        widgetType: WIDGET_TYPE.Text,
        textContent: w.textContent,
        textVariant: w.textVariant,
        textHorizontalAlign: w.textHorizontalAlign,
        textVerticalAlign: w.textVerticalAlign,
      };
    }

    return {
      ...base,
      widgetType: WIDGET_TYPE.Chart,
      savedChartId: w.savedChartId,
    };
  });
}

function mergeWidgetsFromServer(
  widgets: Widget[],
  layout: LayoutItem[],
  saved: DashboardWidgetItem[],
) {
  const usedSavedIds = new Set<string>();

  const nextWidgets = widgets.map((widget) => {
    const layoutItem = layout.find((item) => item.i === widget.id);

    let serverWidget = isServerWidgetId(widget.id)
      ? saved.find((entry) => entry.id === widget.id)
      : undefined;

    if (!serverWidget) {
      serverWidget = saved.find((entry) => {
        if (usedSavedIds.has(entry.id)) return false;
        if (entry.positionX !== (layoutItem?.x ?? 0)) return false;
        if (entry.positionY !== (layoutItem?.y ?? 0)) return false;
        if (widget.kind === 'text') {
          return entry.widgetType === WIDGET_TYPE.Text;
        }
        return entry.widgetType === WIDGET_TYPE.Chart
          && entry.savedChartId === widget.savedChartId;
      });
    }

    if (!serverWidget) return widget;

    usedSavedIds.add(serverWidget.id);

    if (widget.kind === 'text') {
      return {
        ...widget,
        id: serverWidget.id,
        textContent: serverWidget.textContent ?? widget.textContent,
        textVariant: normalizeTextVariant(serverWidget.textVariant),
        textHorizontalAlign: normalizeTextHorizontalAlign(serverWidget.textHorizontalAlign),
        textVerticalAlign: normalizeTextVerticalAlign(serverWidget.textVerticalAlign),
      };
    }

    return { ...widget, id: serverWidget.id };
  });

  const nextLayout = layout.map((item) => {
    const widgetIndex = widgets.findIndex((entry) => entry.id === item.i);
    if (widgetIndex === -1) return item;
    const mergedWidget = nextWidgets[widgetIndex];
    if (mergedWidget.id !== item.i) {
      return { ...item, i: mergedWidget.id };
    }
    return item;
  });

  return { widgets: nextWidgets, layout: nextLayout };
}

function persistPayloadHash(widgets: Widget[], layout: LayoutItem[]) {
  return JSON.stringify(widgetsToRequestItems(widgets, layout));
}

interface LoadDashboardOptions {
  dashboardId: string;
  mode: 'initial' | 'manual' | 'auto';
  previousWidgets: Widget[];
  previousRuntime: Record<string, ChartRuntime>;
  /** Local master switch. Auto polls must use this, not a possibly stale server response. */
  pollingEnabled: boolean;
  signal?: AbortSignal;
}

interface LoadDashboardResult {
  name: string;
  autoRefreshEnabled: boolean;
  widgets: Widget[];
  layout: LayoutItem[];
  runtime: Record<string, ChartRuntime>;
  succeeded: number;
  failed: number;
  blocked: boolean;
  loadedAt: number;
}

async function loadDashboardState({
  dashboardId,
  mode,
  previousWidgets,
  previousRuntime,
  pollingEnabled,
  signal,
}: LoadDashboardOptions): Promise<LoadDashboardResult> {
  const dash = await getDashboard(dashboardId);
  if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');

  const previousById = new Map<string, ChartWidget>();
  const previousByChartId = new Map<string, ChartWidget>();
  const previousRuntimeByChartId = new Map<string, ChartRuntime>();

  for (const widget of previousWidgets) {
    if (widget.kind !== 'chart') continue;
    previousById.set(widget.id, widget);
    previousByChartId.set(widget.savedChartId, widget);
    const runtime = previousRuntime[widget.id];
    if (runtime) previousRuntimeByChartId.set(widget.savedChartId, runtime);
  }

  const widgets: Widget[] = [];
  const layout: LayoutItem[] = [];
  const chartWidgets: ChartWidget[] = [];

  for (const dw of dash.widgets) {
    const isText = dw.widgetType === WIDGET_TYPE.Text;

    if (isText) {
      widgets.push({
        kind: 'text',
        id: dw.id,
        textContent: dw.textContent ?? '',
        textVariant: normalizeTextVariant(dw.textVariant),
        textHorizontalAlign: normalizeTextHorizontalAlign(dw.textHorizontalAlign),
        textVerticalAlign: normalizeTextVerticalAlign(dw.textVerticalAlign),
      });
    } else {
      const previous = previousById.get(dw.id)
        ?? (dw.savedChartId ? previousByChartId.get(dw.savedChartId) : undefined);

      const chartWidget: ChartWidget = {
        kind: 'chart',
        id: dw.id,
        savedChartId: dw.savedChartId ?? '',
        chartId: dw.chartType ?? previous?.chartId ?? 'bar',
        title: dw.chartTitle ?? previous?.title ?? 'Chart',
        data: previous?.data,
        styleConfig: previous?.styleConfig,
        autoRefreshEnabled: dw.autoRefreshEnabled ?? null,
        dataVersion: previous?.dataVersion ?? 0,
      };

      widgets.push(chartWidget);
      chartWidgets.push(chartWidget);
    }

    layout.push({
      i: dw.id,
      x: dw.positionX,
      y: dw.positionY,
      w: dw.width,
      h: dw.height,
      minW: isText ? TEXT_MIN_W : MIN_W,
      minH: isText ? TEXT_MIN_H : MIN_H,
    });
  }

  const targets = chartWidgets.filter((widget) => {
    if (!widget.savedChartId) return false;
    if (mode !== 'auto') return true;
    return pollingEnabled && widget.autoRefreshEnabled !== false;
  });

  const results = new Map<string, {
    data: ChartData;
    styleConfig?: ChartStyleConfig;
    title: string;
    chartId: string;
  }>();
  const failedIds = new Set<string>();
  let succeeded = 0;
  let failed = 0;
  let blocked = false;

  await runWithConcurrency(
    targets,
    MAX_CONCURRENT_CHART_REFRESHES,
    async (widget) => {
      try {
        const result = await executeChart(widget.savedChartId, signal);
        if (signal?.aborted) return;
        if (!result || !Array.isArray(result.queryResult)) {
          throw new Error('Unexpected chart execute response.');
        }

        results.set(widget.id, {
          data: transformResult(result),
          styleConfig: result.styleConfig ?? undefined,
          title: result.title,
          chartId: result.chartType,
        });
        succeeded += 1;
      } catch (err) {
        if (signal?.aborted) return;
        failed += 1;
        failedIds.add(widget.id);
        if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
          blocked = true;
        }
      }
    },
  );

  if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');

  const loadedAt = Date.now();
  const runtime: Record<string, ChartRuntime> = {};

  const hydrated = widgets.map((widget) => {
    if (widget.kind !== 'chart') return widget;

    const update = results.get(widget.id);
    if (update) {
      runtime[widget.id] = { lastUpdatedAt: loadedAt, status: 'idle' };
      return {
        ...widget,
        ...update,
        dataVersion: widget.dataVersion + 1,
      };
    }

    const previousRuntimeForWidget =
      previousRuntime[widget.id] ?? previousRuntimeByChartId.get(widget.savedChartId);

    if (failedIds.has(widget.id)) {
      runtime[widget.id] = {
        lastUpdatedAt: previousRuntimeForWidget?.lastUpdatedAt ?? null,
        status: 'error',
        errorAt: loadedAt,
      };
    } else {
      runtime[widget.id] = previousRuntimeForWidget ?? { lastUpdatedAt: null, status: 'idle' };
    }

    return widget;
  });

  return {
    name: dash.name,
    autoRefreshEnabled: dash.autoRefreshEnabled ?? false,
    widgets: hydrated,
    layout,
    runtime,
    succeeded,
    failed,
    blocked,
    loadedAt,
  };
}

const DashboardGrid = forwardRef<DashboardGridHandle, DashboardGridProps>(function DashboardGrid({ dashboardId, editMode, onLoaded }, ref) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { width: containerWidth, containerRef } = useContainerWidth();

  const [widgets, setWidgets] = useState<Widget[]>([]);
  const [layout, setLayout] = useState<LayoutItem[]>([]);
  const [editingTextId, setEditingTextId] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [autoRefreshEnabled, setAutoRefreshEnabled] = useState(false);
  const [chartRuntime, setChartRuntime] = useState<Record<string, ChartRuntime>>({});
  const [settingsSaving, setSettingsSaving] = useState(false);
  const snapshotRef = useRef<DashboardSnapshot | null>(null);
  const prevEditModeRef = useRef(false);
  const persistInFlightRef = useRef(false);
  const lastPersistHashRef = useRef('');
  const widgetsRef = useRef(widgets);
  const layoutRef = useRef(layout);
  const chartRuntimeRef = useRef<Record<string, ChartRuntime>>({});
  const chartNodeRefs = useRef<Map<string, HTMLDivElement | null>>(new Map());
  const inFlightChartIdsRef = useRef<Set<string>>(new Set());
  const refreshControllersRef = useRef<Set<AbortController>>(new Set());
  const activeReloadControllerRef = useRef<AbortController | null>(null);
  const reloadDashboardRef = useRef<(manual: boolean) => Promise<DashboardRefreshOutcome>>(
    async () => ({ succeeded: 0, failed: 0, lastUpdatedAt: null }),
  );

  useEffect(() => {
    widgetsRef.current = widgets;
  }, [widgets]);

  useEffect(() => {
    layoutRef.current = layout;
  }, [layout]);

  useEffect(() => {
    chartRuntimeRef.current = chartRuntime;
  }, [chartRuntime]);

  const canManageRefreshSettings = !!user && (user.userType !== 1 || user.isCompanyOwner);

  const refreshCharts = useCallback(async (
    ids?: string[],
    options: { onlyPolled?: boolean } = {},
  ) => {
    const targetWidgets = widgetsRef.current.filter((widget): widget is ChartWidget => {
      if (widget.kind !== 'chart' || !widget.savedChartId) return false;
      if (ids && !ids.includes(widget.id)) return false;
      if (options.onlyPolled) {
        if (!autoRefreshEnabled || widget.autoRefreshEnabled === false) return false;
      }
      return !inFlightChartIdsRef.current.has(widget.id);
    });

    if (targetWidgets.length === 0) {
      return { succeeded: 0, failed: 0, lastUpdatedAt: null };
    }

    const targetIds = targetWidgets.map((widget) => widget.id);
    const targetIdSet = new Set(targetIds);
    const controller = new AbortController();
    refreshControllersRef.current.add(controller);

    for (const id of targetIds) inFlightChartIdsRef.current.add(id);
    setChartRuntime((prev) => {
      const next = { ...prev };
      for (const id of targetIds) {
        next[id] = {
          ...(prev[id] ?? { lastUpdatedAt: null }),
          status: 'refreshing',
        };
      }
      return next;
    });

    let succeeded = 0;
    let failed = 0;
    let blocked = false;

    try {
      await runWithConcurrency(
        targetWidgets,
        MAX_CONCURRENT_CHART_REFRESHES,
        async (widget) => {
          try {
            const result = await executeChart(widget.savedChartId, controller.signal);
            if (controller.signal.aborted) return;
            if (!result || !Array.isArray(result.queryResult)) {
              throw new Error('Unexpected chart execute response.');
            }

            const updatedAt = Date.now();
            setWidgets((prev) => prev.map((item) => (
              item.id === widget.id && item.kind === 'chart'
                ? {
                    ...item,
                    data: transformResult(result),
                    styleConfig: result.styleConfig ?? undefined,
                    title: result.title,
                    chartId: result.chartType,
                    dataVersion: item.dataVersion + 1,
                  }
                : item
            )));
            setChartRuntime((prev) => ({
              ...prev,
              [widget.id]: { lastUpdatedAt: updatedAt, status: 'idle' },
            }));
            succeeded += 1;
          } catch (err) {
            if (controller.signal.aborted) return;
            failed += 1;
            if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
              blocked = true;
            }
            setChartRuntime((prev) => ({
              ...prev,
              [widget.id]: {
                lastUpdatedAt: chartRuntimeRef.current[widget.id]?.lastUpdatedAt ?? null,
                status: 'error',
                errorAt: Date.now(),
              },
            }));
          } finally {
            inFlightChartIdsRef.current.delete(widget.id);
          }
        },
      );
    } finally {
      refreshControllersRef.current.delete(controller);
      for (const id of targetIdSet) inFlightChartIdsRef.current.delete(id);
    }

    return {
      succeeded,
      failed,
      lastUpdatedAt: succeeded > 0 ? Date.now() : null,
      blocked,
    };
  }, [autoRefreshEnabled]);

  const refreshDashboard = useCallback(
    (manual: boolean) => reloadDashboardRef.current(manual),
    [],
  );

  const hasAutoRefreshableCharts = useMemo(
    () => widgets.some((widget) => widget.kind === 'chart' && widget.autoRefreshEnabled !== false),
    [widgets],
  );

  const {
    isRefreshing: isDashboardRefreshing,
    lastUpdatedAt,
    nextRefreshAt,
    error: refreshError,
    refreshNow,
    markUpdated,
    markError,
  } = useDashboardPolling({
    enabled: loaded && !editMode && autoRefreshEnabled && hasAutoRefreshableCharts,
    onRefresh: refreshDashboard,
  });

  useEffect(() => {
    const controllers = refreshControllersRef.current;
    return () => {
      for (const controller of controllers) controller.abort();
      controllers.clear();
    };
  }, []);

  useEffect(() => {
    if (!editMode) setEditingTextId(null);
  }, [editMode]);

  const applyDashboardState = useCallback((state: LoadDashboardResult) => {
    setWidgets(state.widgets);
    setLayout(state.layout);
    setChartRuntime(state.runtime);
    lastPersistHashRef.current = persistPayloadHash(state.widgets, state.layout);

    if (state.succeeded > 0) markUpdated(state.loadedAt);
    if (state.failed > 0) {
      markError('Retrying connection… Showing the last known data.');
    } else if (state.succeeded > 0) {
      markError(null);
    }
  }, [markUpdated, markError]);

  const reloadDashboard = useCallback(async (manual: boolean) => {
    const controller = new AbortController();
    activeReloadControllerRef.current = controller;
    refreshControllersRef.current.add(controller);

    try {
      const state = await loadDashboardState({
        dashboardId,
        mode: manual ? 'manual' : 'auto',
        previousWidgets: widgetsRef.current,
        previousRuntime: chartRuntimeRef.current,
        pollingEnabled: autoRefreshEnabled,
        signal: controller.signal,
      });

      if (controller.signal.aborted) {
        throw new DOMException('Aborted', 'AbortError');
      }

      applyDashboardState(state);

      return {
        succeeded: state.succeeded,
        failed: state.failed,
        lastUpdatedAt: state.succeeded > 0 ? state.loadedAt : null,
        blocked: state.blocked,
      };
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        return { succeeded: 0, failed: 0, lastUpdatedAt: null };
      }
      throw err;
    } finally {
      refreshControllersRef.current.delete(controller);
      if (activeReloadControllerRef.current === controller) {
        activeReloadControllerRef.current = null;
      }
    }
  }, [applyDashboardState, autoRefreshEnabled, dashboardId]);

  useEffect(() => {
    reloadDashboardRef.current = reloadDashboard;
  }, [reloadDashboard]);

  useEffect(() => {
    let cancelled = false;

    loadDashboardState({
      dashboardId,
      mode: 'initial',
      previousWidgets: [],
      previousRuntime: {},
      pollingEnabled: false,
    })
      .then((state) => {
        if (cancelled) return;
        onLoaded?.(state.name);
        setAutoRefreshEnabled(state.autoRefreshEnabled);
        applyDashboardState(state);
        setLoaded(true);
      })
      .catch(() => {
        if (!cancelled) setLoaded(true);
      });

    return () => { cancelled = true; };
  }, [dashboardId, onLoaded, applyDashboardState]);

  useEffect(() => {
    if (editMode && !prevEditModeRef.current) {
      snapshotRef.current = cloneSnapshot(widgetsRef.current, layoutRef.current);
    }
    prevEditModeRef.current = editMode;
  }, [editMode]);

  const persistLayout = useCallback(async (w: Widget[], l: LayoutItem[]) => {
    const hash = persistPayloadHash(w, l);
    if (persistInFlightRef.current || hash === lastPersistHashRef.current) {
      return;
    }

    persistInFlightRef.current = true;
    try {
      const items = widgetsToRequestItems(w, l);
      const response = await saveWidgets(dashboardId, items);
      lastPersistHashRef.current = hash;

      const merged = mergeWidgetsFromServer(w, l, response.widgets);
      const idsChanged = merged.widgets.some((widget, index) => widget.id !== w[index]?.id);
      if (idsChanged) {
        setWidgets(merged.widgets);
        setLayout(merged.layout);
        lastPersistHashRef.current = persistPayloadHash(merged.widgets, merged.layout);
      }
    } finally {
      persistInFlightRef.current = false;
    }
  }, [dashboardId]);

  const handleLayoutChange = useCallback((newLayout: Layout) => {
    if (!editMode) return;
    setLayout(newLayout as LayoutItem[]);
  }, [editMode]);

  const addWidget = useCallback(async (savedChartId: string) => {
    try {
      await addWidgetWithData(savedChartId, setWidgets, setLayout);
    } catch {
      /* ignore */
    }
  }, []);

  const addTextWidget = useCallback((variant: TextVariant) => {
    const id = createId();
    const defaultWidth = variant === TEXT_VARIANT.Header ? 3 : 2;
    setWidgets((prev) => [...prev, {
      kind: 'text',
      id,
      textContent: '',
      textVariant: variant,
      textHorizontalAlign: TEXT_ALIGN_H.Left,
      textVerticalAlign: TEXT_ALIGN_V.Top,
    }]);
    setLayout((prev) => [
      ...prev,
      {
        i: id,
        x: 0,
        y: (prev.length + 1) * 10,
        w: defaultWidth,
        h: 1,
        minW: TEXT_MIN_W,
        minH: TEXT_MIN_H,
      },
    ]);
  }, []);

  const removeWidget = useCallback((id: string) => {
    setWidgets((prev) => prev.filter((w) => w.id !== id));
    setLayout((prev) => prev.filter((item) => item.i !== id));
  }, []);

  const handleDownloadChart = useCallback(async (widget: ChartWidget) => {
    try {
      await exportChartAsPng(chartNodeRefs.current.get(widget.id), widget.title);
    } catch {
      toast.error('Could not save the chart as an image.');
    }
  }, []);

  const handleDownloadCsv = useCallback(async (widget: ChartWidget) => {
    if (!widget.data) return;
    try {
      await exportChartDataAsCsv(widget.data, widget.chartId, widget.title, widget.styleConfig);
    } catch {
      toast.error('Could not save the chart data.');
    }
  }, []);

  const handleDownloadExcel = useCallback(async (widget: ChartWidget) => {
    if (!widget.data) return;
    try {
      await exportChartDataAsExcel(widget.data, widget.chartId, widget.title, widget.styleConfig);
    } catch {
      toast.error('Could not save the chart data.');
    }
  }, []);

  const handleTextChange = useCallback((id: string, textContent: string) => {
    setWidgets((prev) => prev.map((w) => (w.id === id && w.kind === 'text' ? { ...w, textContent } : w)));
  }, []);

  const handleTextHorizontalAlign = useCallback((id: string, textHorizontalAlign: TextHorizontalAlign) => {
    setWidgets((prev) => prev.map((w) => (w.id === id && w.kind === 'text' ? { ...w, textHorizontalAlign } : w)));
  }, []);

  const handleTextVerticalAlign = useCallback((id: string, textVerticalAlign: TextVerticalAlign) => {
    setWidgets((prev) => prev.map((w) => (w.id === id && w.kind === 'text' ? { ...w, textVerticalAlign } : w)));
  }, []);

  const applyRefreshSettings = useCallback((response: {
    autoRefreshEnabled: boolean;
    widgets: DashboardWidgetItem[];
  }) => {
    setAutoRefreshEnabled(response.autoRefreshEnabled);
    const refreshById = new Map(response.widgets.map((widget) => [widget.id, widget.autoRefreshEnabled ?? null]));
    setWidgets((prev) => prev.map((widget) => {
      if (widget.kind !== 'chart' || !refreshById.has(widget.id)) return widget;
      return { ...widget, autoRefreshEnabled: refreshById.get(widget.id) ?? null };
    }));
  }, []);

  const handleToggleAutoRefresh = useCallback(async (next: boolean) => {
    if (!canManageRefreshSettings || settingsSaving) return;
    const previous = autoRefreshEnabled;
    setAutoRefreshEnabled(next);
    if (!next) {
      activeReloadControllerRef.current?.abort();
      activeReloadControllerRef.current = null;
    }
    setSettingsSaving(true);
    try {
      const response = await updateDashboardSettings(dashboardId, next);
      applyRefreshSettings(response);
    } catch (err) {
      setAutoRefreshEnabled(previous);
      toast.error(err instanceof Error ? err.message : 'Could not update auto-refresh settings.');
    } finally {
      setSettingsSaving(false);
    }
  }, [applyRefreshSettings, autoRefreshEnabled, canManageRefreshSettings, dashboardId, settingsSaving]);

  const handleToggleChartRefresh = useCallback(async (widgetId: string, next: boolean) => {
    if (!canManageRefreshSettings || settingsSaving) return;
    const previous = widgetsRef.current.find(
      (widget): widget is ChartWidget => widget.id === widgetId && widget.kind === 'chart',
    );
    if (!previous) return;

    const previousValue = previous.autoRefreshEnabled;
    setSettingsSaving(true);
    setWidgets((prev) => prev.map((widget) => (
      widget.id === widgetId && widget.kind === 'chart'
        ? { ...widget, autoRefreshEnabled: next }
        : widget
    )));

    try {
      const response = await updateWidgetRefreshSetting(dashboardId, widgetId, next);
      applyRefreshSettings(response);
    } catch (err) {
      setWidgets((prev) => prev.map((widget) => (
        widget.id === widgetId && widget.kind === 'chart'
          ? { ...widget, autoRefreshEnabled: previousValue }
          : widget
      )));
      toast.error(err instanceof Error ? err.message : 'Could not update chart refresh settings.');
    } finally {
      setSettingsSaving(false);
    }
  }, [applyRefreshSettings, canManageRefreshSettings, dashboardId, settingsSaving]);

  const resetDashboard = useCallback(async () => {
    setWidgets([]);
    setLayout([]);
    try {
      await persistLayout([], []);
    } catch {
      /* ignore */
    }
  }, [persistLayout]);

  const saveEdit = useCallback(async () => {
    lastPersistHashRef.current = '';
    await persistLayout(widgets, layout);
    snapshotRef.current = null;
  }, [persistLayout, widgets, layout]);

  const cancelEdit = useCallback(() => {
    if (snapshotRef.current) {
      setWidgets(snapshotRef.current.widgets);
      setLayout(snapshotRef.current.layout);
      snapshotRef.current = null;
    }
  }, []);

  useImperativeHandle(
    ref,
    () => ({ addWidget, addTextWidget, resetDashboard, saveEdit, cancelEdit }),
    [addWidget, addTextWidget, resetDashboard, saveEdit, cancelEdit],
  );

  useEffect(() => {
    if (!loaded || editMode) return;
    const timer = setTimeout(() => {
      persistLayout(widgets, layout).catch(() => {});
    }, 500);
    return () => clearTimeout(timer);
  }, [widgets, layout, loaded, editMode, persistLayout]);

  const widgetById = useMemo(() => {
    const map: Record<string, Widget> = {};
    for (const w of widgets) map[w.id] = w;
    return map;
  }, [widgets]);

  const refreshChartOptions = useMemo<RefreshChartOption[]>(
    () => widgets
      .filter((widget): widget is ChartWidget => widget.kind === 'chart')
      .map((widget) => ({
        id: widget.id,
        title: widget.title,
        autoRefreshEnabled: widget.autoRefreshEnabled ?? null,
      })),
    [widgets],
  );

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
    ? 'drag-handle flex cursor-grab items-center gap-2 active:cursor-grabbing'
    : 'flex items-center gap-2';

  if (containerWidth <= 0) {
    return <div ref={containerRef} />;
  }

  if (!loaded) {
    return (
      <div ref={containerRef} className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {Array.from({ length: 3 }, (_, i) => (
          <Card key={i} className="overflow-hidden">
            <CardHeader>
              <Skeleton className="h-4 w-1/2" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-48 w-full" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  if (widgets.length === 0) {
    return (
      <div
        ref={containerRef}
        className="border-border flex min-h-[360px] flex-col items-center justify-center gap-4 rounded-xl border border-dashed p-8 text-center"
      >
        <div className="bg-muted flex size-12 items-center justify-center rounded-full">
          <PlusCircle className="text-muted-foreground size-6" />
        </div>
        <div className="max-w-sm">
          <h2 className="text-lg font-semibold">Your dashboard is empty</h2>
          <p className="text-muted-foreground mt-1 text-sm">
            Create a chart from a database connection, or enter edit mode to add an existing one.
          </p>
        </div>
        <Button asChild>
          <Link to={ROUTES.GRAPHS_NEW}>Create a chart</Link>
        </Button>
      </div>
    );
  }

  return (
    <div ref={containerRef} className="w-full">
      {!editMode && refreshChartOptions.length > 0 && (
        <div className="mb-3">
          <DashboardRefreshBar
            enabled={autoRefreshEnabled}
            canManageSettings={canManageRefreshSettings}
            settingsSaving={settingsSaving}
            isRefreshing={isDashboardRefreshing}
            lastUpdatedAt={lastUpdatedAt}
            nextRefreshAt={nextRefreshAt}
            error={refreshError}
            charts={refreshChartOptions}
            onRefresh={() => void refreshNow()}
            onToggleEnabled={(enabled) => void handleToggleAutoRefresh(enabled)}
            onToggleChart={(widgetId, enabled) => void handleToggleChartRefresh(widgetId, enabled)}
          />
        </div>
      )}

      <div
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
          .dashboard-grid .react-grid-item {
            overflow: visible !important;
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
        draggableHandle={editMode ? '.drag-handle, .text-drag-handle' : undefined}
      >
        {layout.map((item) => {
          const widget = widgetById[item.i];
          if (!widget) {
            return <div key={item.i} />;
          }

          if (widget.kind === 'text') {
            const textShellClass =
              'relative flex h-full w-full flex-col rounded-xl bg-card text-card-foreground ring-1 ring-foreground/10'
              + (editMode ? ' overflow-visible' : ' overflow-hidden');

            const textShellBody = (
              <>
                {editMode && (
                  <div className="text-drag-handle flex shrink-0 cursor-grab items-center justify-between border-b px-2 py-1 active:cursor-grabbing">
                    <GripHorizontal size={14} className="text-muted-foreground" />
                    <button
                      type="button"
                      onClick={() => removeWidget(widget.id)}
                      className="cursor-pointer rounded-md p-0.5 text-muted-foreground transition-colors hover:text-error"
                    >
                      <X size={12} />
                    </button>
                  </div>
                )}
                <div className="relative min-h-0 flex-1 overflow-hidden p-2">
                  <TextWidget
                    content={widget.textContent}
                    variant={widget.textVariant}
                    horizontalAlign={widget.textHorizontalAlign}
                    verticalAlign={widget.textVerticalAlign}
                    editMode={editMode}
                    isEditing={editingTextId === widget.id}
                    onStartEdit={() => setEditingTextId(widget.id)}
                    onStopEdit={() => setEditingTextId(null)}
                    onChange={(content) => handleTextChange(widget.id, content)}
                  />
                </div>
              </>
            );

            return (
              <div key={item.i}>
                {editMode ? (
                  <TextWidgetEditShell
                    className={textShellClass}
                    onContextMenu={(e) => e.preventDefault()}
                    horizontal={widget.textHorizontalAlign}
                    vertical={widget.textVerticalAlign}
                    onHorizontalChange={(value) => handleTextHorizontalAlign(widget.id, value)}
                    onVerticalChange={(value) => handleTextVerticalAlign(widget.id, value)}
                  >
                    {textShellBody}
                  </TextWidgetEditShell>
                ) : (
                  <div
                    className={textShellClass}
                    onContextMenu={(e) => e.preventDefault()}
                  >
                    {textShellBody}
                  </div>
                )}
              </div>
            );
          }

          const widgetHasExportData = widget.data
            ? chartHasExportData(widget.data, widget.chartId, widget.styleConfig)
            : false;
          const runtime = chartRuntime[widget.id];
          const chartIsRefreshing = runtime?.status === 'refreshing';

          return (
            <div key={item.i} onContextMenu={(e) => e.preventDefault()}>
              <Card className="flex h-full flex-col gap-0 overflow-hidden py-0">
                <CardHeader className={handleClass + ' border-b px-4 py-2.5'}>
                  {editMode && <GripHorizontal className="text-muted-foreground size-3.5 shrink-0" />}
                  <CardTitle
                    className={'min-w-0 flex-1 truncate text-sm font-medium' + (editMode ? ' cursor-pointer' : '')}
                    onDoubleClick={
                      editMode && widget.savedChartId
                        ? () => navigate(graphEditPath(widget.savedChartId))
                        : undefined
                    }
                    title={editMode ? 'Double-click to edit' : widget.title}
                  >
                    {widget.title}
                  </CardTitle>

                  <div className="ml-auto flex shrink-0 items-center gap-0.5">
                    {!editMode && (
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <button
                            type="button"
                            className="text-muted-foreground hover:text-foreground nodrag inline-flex size-7 cursor-pointer items-center justify-center rounded-md disabled:cursor-not-allowed disabled:opacity-50"
                            aria-label={`Refresh ${widget.title}`}
                            title="Refresh chart"
                            disabled={chartIsRefreshing || !widget.savedChartId}
                            onMouseDown={(e) => e.stopPropagation()}
                            onClick={(e) => {
                              e.stopPropagation();
                              void refreshCharts([widget.id]).then((outcome) => {
                                if (outcome.succeeded > 0) markUpdated();
                              });
                            }}
                          >
                            <RefreshCw
                              className={
                                'size-3.5'
                                + (chartIsRefreshing ? ' animate-spin' : '')
                                + (runtime?.status === 'error' ? ' text-amber-600' : '')
                              }
                            />
                          </button>
                        </TooltipTrigger>
                        <TooltipContent side="bottom">
                          {runtime?.status === 'error'
                            ? 'Last refresh failed — click to retry'
                            : runtime?.lastUpdatedAt
                              ? `Updated ${new Date(runtime.lastUpdatedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`
                              : 'Refresh chart'}
                        </TooltipContent>
                      </Tooltip>
                    )}

                    {widget.styleConfig?.info?.trim() && (
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <button
                            type="button"
                            className="text-muted-foreground hover:text-foreground nodrag inline-flex size-7 cursor-pointer items-center justify-center rounded-md"
                            aria-label="Chart info"
                            onMouseDown={(e) => e.stopPropagation()}
                            onClick={(e) => e.stopPropagation()}
                          >
                            <Info className="size-3.5" />
                          </button>
                        </TooltipTrigger>
                        <TooltipContent side="bottom" className="max-w-xs text-left">
                          {widget.styleConfig.info}
                        </TooltipContent>
                      </Tooltip>
                    )}

                    {(widget.chartId !== 'map' || widgetHasExportData) && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <button
                            type="button"
                            className="text-muted-foreground hover:text-foreground nodrag inline-flex size-7 cursor-pointer items-center justify-center rounded-md"
                            aria-label="Save chart"
                            title="Save chart"
                            onMouseDown={(e) => e.stopPropagation()}
                          >
                            <Download className="size-3.5" />
                          </button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" onMouseDown={(e) => e.stopPropagation()}>
                          {widget.chartId !== 'map' && (
                            <DropdownMenuItem onSelect={() => void handleDownloadChart(widget)}>
                              <Download />
                              Save as image
                            </DropdownMenuItem>
                          )}
                          <DropdownMenuItem disabled={!widgetHasExportData} onSelect={() => void handleDownloadCsv(widget)}>
                            <FileText />
                            Download CSV
                          </DropdownMenuItem>
                          <DropdownMenuItem disabled={!widgetHasExportData} onSelect={() => void handleDownloadExcel(widget)}>
                            <FileSpreadsheet />
                            Download Excel
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}

                    {editMode && (
                      <CardAction>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              className="nodrag"
                              onMouseDown={(e) => e.stopPropagation()}
                            >
                              <MoreHorizontal />
                              <span className="sr-only">Widget actions</span>
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" onMouseDown={(e) => e.stopPropagation()}>
                            <DropdownMenuItem
                              onSelect={() => {
                                if (widget.savedChartId) navigate(graphEditPath(widget.savedChartId));
                              }}
                            >
                              <Pencil />
                              Edit
                            </DropdownMenuItem>
                            {widget.chartId !== 'map' && (
                              <DropdownMenuItem onSelect={() => void handleDownloadChart(widget)}>
                                <Download />
                                Save as image
                              </DropdownMenuItem>
                            )}
                            <DropdownMenuItem variant="destructive" onSelect={() => removeWidget(widget.id)}>
                              <Trash2 />
                              Remove
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </CardAction>
                    )}
                  </div>
                </CardHeader>

                <CardContent className="relative min-h-0 flex-1 p-3">
                  <div
                    className="absolute inset-3"
                    ref={(node) => {
                      if (node) chartNodeRefs.current.set(widget.id, node);
                      else chartNodeRefs.current.delete(widget.id);
                    }}
                  >
                    <ChartRenderer
                      key={`${widget.id}-${widget.dataVersion}`}
                      chartId={widget.chartId}
                      data={widget.data}
                      styleConfig={widget.styleConfig}
                    />
                  </div>
                </CardContent>
              </Card>
            </div>
          );
        })}
        </GridLayout>
      </div>
    </div>
  );
});

export default DashboardGrid;
