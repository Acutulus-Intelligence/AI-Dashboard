import { estimateTextWidth, formatStyledValue } from './format';
import type { ChartData, ChartValue, ResolvedStyle } from './types';

export function truncateLabel(value: string, max = 14): string {
  const text = String(value ?? '');
  if (max <= 1) return '';
  return text.length <= max ? text : `${text.slice(0, max - 1)}…`;
}

export interface CartesianMarginOptions {
  /** Reserve room for value labels above points/bars. */
  valueLabels?: boolean;
  /** Labels sit to the right of horizontal bars. */
  rightLabels?: boolean;
}

export interface ChartMargin {
  top: number;
  right: number;
  left: number;
  bottom: number;
}

/**
 * Reserves enough gutter for value labels so the last point's label is not
 * clipped by the SVG edge.
 */
export function resolveCartesianMargins(
  data: ChartData,
  style: ResolvedStyle,
  options: CartesianMarginOptions = {},
): ChartMargin {
  const base: ChartMargin = { top: 12, right: 14, left: 4, bottom: 4 };
  if (!options.valueLabels) return base;

  let maxAbs = 0;
  for (const dataset of data.datasets) {
    for (const value of dataset.values) {
      if (typeof value === 'number' && Number.isFinite(value)) {
        maxAbs = Math.max(maxAbs, Math.abs(value));
      }
    }
  }

  const text = formatStyledValue(maxAbs, style);
  const width = estimateTextWidth(text, 11);

  if (options.rightLabels) {
    return {
      ...base,
      top: 14,
      right: Math.max(18, Math.ceil(width) + 12),
    };
  }

  const half = Math.ceil(width / 2) + 8;
  return {
    top: 20,
    right: Math.max(16, half),
    left: Math.max(6, half - 4),
    bottom: 4,
  };
}
export interface XAxisLayout {
  angle: number;
  textAnchor: 'start' | 'middle' | 'end';
  height: number;
  interval: number;
  tickFormatter: (value: string | number) => string;
}

/** Keeps long category labels inside the plot without overlapping ticks. */
export function xAxisLayout(labels: string[], compact: boolean): XAxisLayout {
  const maxLength = labels.reduce((max, label) => Math.max(max, String(label ?? '').length), 0);
  const count = labels.length;
  const rotate = !compact && (maxLength > 10 || count > 8);
  const maxChars = compact ? 8 : rotate ? 16 : 14;
  const interval = compact ? Math.max(0, Math.ceil(count / 5) - 1) : Math.max(0, Math.ceil(count / 9) - 1);

  return {
    angle: rotate ? -32 : 0,
    textAnchor: rotate ? 'end' : 'middle',
    height: rotate ? 52 : 24,
    interval,
    tickFormatter: (value) => truncateLabel(String(value ?? ''), maxChars),
  };
}

/** Estimates the left gutter from the widest formatted tick value. */
export function yAxisWidth(
  data: ChartData,
  style: ResolvedStyle,
  compact: boolean,
): number {
  let maxAbs = 0;
  let sawValue = false;
  for (const dataset of data.datasets) {
    for (const value of dataset.values) {
      if (typeof value !== 'number' || !Number.isFinite(value)) continue;
      sawValue = true;
      maxAbs = Math.max(maxAbs, Math.abs(value));
    }
  }
  if (!sawValue) return 40;

  const text = formatStyledValue(maxAbs, style);
  const width = estimateTextWidth(text, 11) + 14;
  return Math.round(Math.min(compact ? 64 : 96, Math.max(36, width)));
}

/** Left gutter for horizontal bars, driven by category label length. */
export function labelAxisWidth(labels: string[], compact: boolean): number {
  const longest = labels.reduce(
    (max, label) => Math.max(max, truncateLabel(String(label ?? ''), compact ? 12 : 18).length),
    0,
  );
  return Math.round(Math.min(compact ? 88 : 132, Math.max(48, estimateTextWidth('x'.repeat(longest), 11) + 14)));
}

/** Value labels become unreadable long before Recharts stops drawing them. */
export function canShowValueLabels(
  data: ChartData,
  compact: boolean,
  options: { horizontal?: boolean } = {},
): boolean {
  const bars = data.labels.length * Math.max(data.datasets.length, 1);
  if (compact) return bars <= 8;
  if (options.horizontal) return data.labels.length <= 20;
  return bars <= 24;
}

/** For line/area charts: only draw points when there is room for the text. */
export function canShowPointLabels(data: ChartData, compact: boolean): boolean {
  return data.labels.length <= (compact ? 6 : 12);
}

/** Number of x ticks that fit in a compact chart. */
export function compactTickCount(compact: boolean): number {
  return compact ? 4 : 8;
}

export function isNumericValue(value: ChartValue): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}
