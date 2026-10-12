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
  ownerId: string;
  widgets: DashboardWidgetItem[];
}

export interface DashboardSummary {
  id: string;
  name: string;
  ownerId: string;
  widgetCount: number;
  updatedAt: string;
}

export function getDashboards(): Promise<DashboardSummary[]> {
  return apiFetch<DashboardSummary[]>('/api/dashboards');
}

export function getDashboard(id: string): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>(`/api/dashboards/${id}`);
}

export function createDashboard(name: string): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>('/api/dashboards', {
    method: 'POST',
    body: JSON.stringify({ name }),
  });
}

export function renameDashboard(id: string, name: string): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>(`/api/dashboards/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ name }),
  });
}

export function deleteDashboard(id: string): Promise<void> {
  return apiFetch<void>(`/api/dashboards/${id}`, {
    method: 'DELETE',
  });
}

export function saveWidgets(id: string, widgets: WidgetItem[]): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>(`/api/dashboards/${id}/widgets`, {
    method: 'PUT',
    body: JSON.stringify({ widgets }),
  });
}

export interface TransferDataSourceItem {
  type: 'connection' | 'collection';
  id: string;
  name: string;
}

export interface TransferDashboardResult {
  transferred: boolean;
  requiresSharing: TransferDataSourceItem[];
}

export function transferDashboard(
  id: string,
  newOwnerId: string,
  currentPassword: string,
  shareDataSources = false,
): Promise<TransferDashboardResult> {
  return apiFetch<TransferDashboardResult>(`/api/dashboards/${id}/transfer-ownership`, {
    method: 'POST',
    body: JSON.stringify({ newOwnerId, currentPassword, shareDataSources }),
  });
}
