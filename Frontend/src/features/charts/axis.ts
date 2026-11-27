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

const DAY_MS = 24 * 60 * 60 * 1000;
const YEAR_MS = 365 * DAY_MS;

export function timeSpan(values: number[]): number {
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;
  for (const value of values) {
    if (!Number.isFinite(value)) continue;
    if (value < min) min = value;
    if (value > max) max = value;
  }
  return Number.isFinite(min) ? Math.max(0, max - min) : 0;
}

export function formatTimeTick(value: number, span: number): string {
  if (!Number.isFinite(value)) return '';
  const date = new Date(value);

  if (span <= 2 * DAY_MS) {
    return date.toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }
  if (span <= 120 * DAY_MS) {
    return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
  if (span <= 2 * YEAR_MS) {
    return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short' });
  }
  return date.toLocaleDateString(undefined, { year: 'numeric' });
}

export function formatTimeLabel(value: number, span: number): string {
  if (!Number.isFinite(value)) return '';
  const date = new Date(value);

  return date.toLocaleString(
    undefined,
    span <= 2 * DAY_MS
      ? {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
        }
      : { year: 'numeric', month: 'short', day: 'numeric' },
  );
}

export interface XAxisConfig {
  dataKey: string;
  type: 'category' | 'number';
  scale?: 'time';
  domain?: [string, string];
  minTickGap?: number;
  height: number;
  interval?: number | 'preserveStartEnd';
  angle?: number;
  textAnchor?: 'start' | 'middle' | 'end';
  tickFormatter: (value: string | number) => string;
}

/**
 * Real time scale when every x value is an ISO date; otherwise the original
 * categorical layout. Keeping one helper means line/area/combo stay in sync.
 */
export function xAxisConfig(data: ChartData, compact: boolean): XAxisConfig {
  if (data.xTime && data.xTime.length === data.labels.length) {
    const span = timeSpan(data.xTime);
    return {
      dataKey: '__x',
      type: 'number',
      scale: 'time',
      domain: ['dataMin', 'dataMax'],
      minTickGap: compact ? 28 : 44,
      height: 24,
      tickFormatter: (value) => formatTimeTick(Number(value), span),
    };
  }

  const layout = xAxisLayout(data.labels, compact);
  return {
    dataKey: 'name',
    type: 'category',
    height: layout.height,
    interval: layout.interval,
    angle: layout.angle,
    textAnchor: layout.textAnchor,
    tickFormatter: layout.tickFormatter,
  };
}

/** Tooltip label formatter for a time axis; undefined keeps category labels. */
export function timeLabelFormatter(
  data: ChartData,
): ((value: unknown) => string) | undefined {
  if (!data.xTime?.length) return undefined;
  const span = timeSpan(data.xTime);
  return (value) => {
    const numeric = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(numeric) ? formatTimeLabel(numeric, span) : String(value ?? '');
  };
}

/** Estimates the left gutter from the widest formatted tick value. */
export function yAxisWidth(
  data: ChartData,
  style: ResolvedStyle,
  compact: boolean,
): number {
  return datasetsAxisWidth(data.datasets, style, compact);
}

/** Axis width for a single series (used by combo's secondary axis). */
export function datasetAxisWidth(
  dataset: ChartData['datasets'][number],
  style: ResolvedStyle,
  compact: boolean,
): number {
  return axisWidthFrom([dataset.values], style, compact);
}

export function datasetsAxisWidth(
  datasets: ChartData['datasets'],
  style: ResolvedStyle,
  compact: boolean,
): number {
  return axisWidthFrom(datasets.map((dataset) => dataset.values), style, compact);
}

function axisWidthFrom(
  series: ChartValue[][],
  style: ResolvedStyle,
  compact: boolean,
): number {
  let maxAbs = 0;
  let sawValue = false;
  for (const values of series) {
    for (const value of values) {
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
