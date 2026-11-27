import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

/** One plotted value. Null means a real gap in the data (rendered as a break, not zero). */
export type ChartValue = number | null;

/** Normalised shape every chart renders from, produced by `transformResult`. */
export interface ChartData {
  labels: string[];
  datasets: { label: string; values: ChartValue[] }[];
  /** Raw SQL rows, kept so the table chart can show every column. */
  queryResult?: Record<string, unknown>[];
  /** X-axis column name from the query, used as the first export header. */
  xAxis?: string;
  /** Second grouping column (used by heatmap). */
  groupBy?: string | null;
  /** SQL aggregation ("sum", "avg", "min", "max", "count", "none"). */
  aggregation?: string;
  /**
   * Epoch milliseconds for the x-axis when every label parses as an ISO date.
   * Renderers use this for a real time scale instead of evenly spaced categories.
   */
  xTime?: number[];
}

/**
 * A user- and AI-adjustable knob on a chart. The backend catalog mirrors these
 * specs so the AI prompt and the style panel stay in sync with one definition.
 */
export type ParamSpec =
  | { kind: 'boolean'; key: string; label: string; default: boolean; help?: string }
  | {
      kind: 'number';
      key: string;
      label: string;
      default: number;
      min: number;
      max: number;
      step: number;
      help?: string;
    }
  | {
      kind: 'select';
      key: string;
      label: string;
      default: string;
      options: { value: string; label: string }[];
      help?: string;
    };

export interface ChartVariant {
  id: string;
  label: string;
  description: string;
}

export type DecimalMode = 'round' | 'truncate';
export type NumberNotation = 'auto' | 'full' | 'compact';

/** Persisted per chart as `styleConfig` and merged over the descriptor defaults. */
export interface ChartStyleConfig {
  variant?: string;
  palette?: string;
  /** Per-series colour overrides, indexed like `datasets` (or slices for pie). */
  colors?: string[];
  /** Colour overrides keyed by series or category name. Wins over `colors`. */
  colorsByKey?: Record<string, string>;
  params?: Record<string, unknown>;
  /** Shown before numeric values (e.g. "$"). */
  valuePrefix?: string;
  /** Shown after numeric values (e.g. "%"). */
  valueSuffix?: string;
  /** Short info text for the title info icon tooltip. */
  info?: string;
  /** Fixed decimal places; omit/undefined = show full precision. */
  decimals?: number;
  /** How to apply `decimals`. Ignored when `decimals` is unset. */
  decimalMode?: DecimalMode;
  /** How large numbers are abbreviated. Omit for the historical auto behaviour. */
  notation?: NumberNotation;
  /** Style schema version written by the backend. */
  schemaVersion?: number;
}

/** A `ChartStyleConfig` with every gap filled from the descriptor defaults. */
export interface ResolvedStyle {
  variant: string;
  palette: string;
  /** CSS colour per series, already resolved to `var(--chart-N)` or a hex. */
  colors: string[];
  params: Record<string, unknown>;
  valuePrefix: string;
  valueSuffix: string;
  info: string;
  decimals: number | null;
  decimalMode: DecimalMode;
  notation: NumberNotation;
}

/** Measured chart area, supplied by `ChartRenderer` so renderers can adapt. */
export interface ChartLayout {
  width: number;
  height: number;
  /** True for small dashboard widgets (hide legend/labels first). */
  compact: boolean;
}

/** One editable colour slot, mapped back to its row/dataset index. */
export interface ColorSlot {
  /** Index into `data.labels` (category axis) or `data.datasets` (series axis). */
  index: number;
  /** Name used for `colorsByKey`; empty for unnamed/duplicate labels. */
  key: string;
  /** Human-readable label shown in the colour editor. */
  label: string;
}

export interface ChartRenderContext {
  data: ChartData;
  style: ResolvedStyle;
  layout?: ChartLayout;
  /** Colour slots in the same order as `style.colors`. */
  colorSlots?: ColorSlot[];
  /** Cross-filter hook: called with the clicked category label (dashboard only). */
  onCategoryClick?: (category: string) => void;
}

/** Which shared style controls apply to this chart type (defaults: all on). */
export interface ChartStyleCapabilities {
  /** Prefix / suffix / decimals on numeric values. */
  valueLabels?: boolean;
  /** Theme palette + series/slice colour pickers. */
  colors?: boolean;
  /** Info tooltip next to the chart title. */
  info?: boolean;
}

export interface ChartDescriptor {
  id: string;
  label: string;
  description: string;
  icon: LucideIcon;
  defaultSize: { w: number; h: number };
  minSize: { w: number; h: number };
  variants: ChartVariant[];
  params: ParamSpec[];
  /** Which data dimension receives colours. Defaults to `series`. */
  colorAxis?: 'series' | 'category';
  /** Param key that can switch the colour axis at runtime (e.g. bar `colorBy`). */
  colorAxisParam?: string;
  /** Limit category colour slots to labels the chart actually draws. */
  colorSlotFilter?: (data: ChartData, index: number) => boolean;
  /** Omit or leave fields true for chart/line/pie-style value formatting & colours. */
  styleCapabilities?: ChartStyleCapabilities;
  render: (ctx: ChartRenderContext) => ReactNode;
}

export function resolveStyleCapabilities(
  descriptor: ChartDescriptor,
): Required<ChartStyleCapabilities> {
  const c = descriptor.styleCapabilities;
  return {
    valueLabels: c?.valueLabels !== false,
    colors: c?.colors !== false,
    info: c?.info !== false,
  };
}

/** Reads a param with a compile-time fallback, since params are loosely typed. */
export function param<T>(style: ResolvedStyle, key: string, fallback: T): T {
  const value = style.params[key];
  return (value === undefined ? fallback : value) as T;
}
