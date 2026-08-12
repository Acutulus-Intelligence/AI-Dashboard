import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

/** Normalised shape every chart renders from, produced by `transformResult`. */
export interface ChartData {
  labels: string[];
  datasets: { label: string; values: number[] }[];
  /** Raw SQL rows, kept so the table chart can show every column. */
  queryResult?: Record<string, unknown>[];
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

/** Persisted per chart as `styleConfig` and merged over the descriptor defaults. */
export interface ChartStyleConfig {
  variant?: string;
  palette?: string;
  /** Per-series colour overrides, indexed like `datasets` (or slices for pie). */
  colors?: string[];
  /** @deprecated Swatches now come from company style; ignored when saving. */
  customColors?: string[];
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
}

export interface ChartRenderContext {
  data: ChartData;
  style: ResolvedStyle;
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
