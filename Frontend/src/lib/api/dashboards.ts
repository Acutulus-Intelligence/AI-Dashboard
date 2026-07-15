import { apiFetch } from './client';

export const WIDGET_TYPE = {
  Chart: 0,
  Text: 1,
} as const;

export const TEXT_VARIANT = {
  Header: 1,
  Body: 2,
} as const;

export const TEXT_ALIGN_H = {
  Left: 1,
  Center: 2,
  Right: 3,
} as const;

export const TEXT_ALIGN_V = {
  Top: 1,
  Center: 2,
  Bottom: 3,
} as const;

export type WidgetType = (typeof WIDGET_TYPE)[keyof typeof WIDGET_TYPE];
export type TextVariant = (typeof TEXT_VARIANT)[keyof typeof TEXT_VARIANT];
export type TextHorizontalAlign = (typeof TEXT_ALIGN_H)[keyof typeof TEXT_ALIGN_H];
export type TextVerticalAlign = (typeof TEXT_ALIGN_V)[keyof typeof TEXT_ALIGN_V];

export function normalizeTextVariant(value: unknown): TextVariant {
  if (value === TEXT_VARIANT.Header || value === 0 || value === 'Header') {
    return TEXT_VARIANT.Header;
  }
  if (value === TEXT_VARIANT.Body || value === 2 || value === 'Body') {
    return TEXT_VARIANT.Body;
  }
  // Legacy Body value from the first enum version (Body = 1).
  if (value === 1) {
    return TEXT_VARIANT.Body;
  }
  return TEXT_VARIANT.Body;
}

export function normalizeTextHorizontalAlign(value: unknown): TextHorizontalAlign {
  if (value === TEXT_ALIGN_H.Center || value === 2 || value === 'Center') return TEXT_ALIGN_H.Center;
  if (value === TEXT_ALIGN_H.Right || value === 3 || value === 'Right') return TEXT_ALIGN_H.Right;
  return TEXT_ALIGN_H.Left;
}

export function normalizeTextVerticalAlign(value: unknown): TextVerticalAlign {
  if (value === TEXT_ALIGN_V.Center || value === 2 || value === 'Center') return TEXT_ALIGN_V.Center;
  if (value === TEXT_ALIGN_V.Bottom || value === 3 || value === 'Bottom') return TEXT_ALIGN_V.Bottom;
  return TEXT_ALIGN_V.Top;
}

export interface WidgetItem {
  id?: string;
  widgetType: WidgetType;
  savedChartId?: string;
  textContent?: string;
  textVariant?: TextVariant;
  textHorizontalAlign?: TextHorizontalAlign;
  textVerticalAlign?: TextVerticalAlign;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
}

export interface DashboardWidgetItem {
  id: string;
  widgetType: WidgetType;
  savedChartId?: string;
  textContent?: string;
  textVariant?: TextVariant;
  textHorizontalAlign?: TextHorizontalAlign;
  textVerticalAlign?: TextVerticalAlign;
  chartTitle?: string;
  chartType?: string;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
}

export interface DashboardResponse {
  id: string;
  name: string;
  widgets: DashboardWidgetItem[];
}

export function getDashboard(): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>('/api/dashboards');
}

export function saveWidgets(widgets: WidgetItem[]): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>('/api/dashboards/widgets', {
    method: 'PUT',
    body: JSON.stringify({ widgets }),
  });
}
