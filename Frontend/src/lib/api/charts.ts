import { apiFetch } from './client';
import type { ChartStyleConfig } from '../../features/charts/types';

export interface SaveChartRequest {
  title: string;
  chartType: string;
  xAxis: string;
  yAxis: string[];
  aggregation: string;
  groupBy: string | null;
  sqlQuery: string;
  connectionId: string | null;
  tableName: string | null;
  styleConfig?: ChartStyleConfig | null;
}

export interface UpdateChartRequest {
  title: string;
  chartType: string;
  styleConfig?: ChartStyleConfig | null;
}

export interface ChartResponse {
  id: string;
  title: string;
  chartType: string;
  createdAt: string;
}

export interface ChartDetailResponse {
  id: string;
  title: string;
  chartType: string;
  xAxis: string;
  yAxis: string[];
  aggregation: string;
  groupBy: string | null;
  sqlQuery: string;
  connectionId: string | null;
  tableName: string | null;
  createdAt: string;
  styleConfig?: ChartStyleConfig | null;
}

export interface ChartConfigResponse {
  chartType: string;
  title: string;
  xAxis: string;
  yAxis: string[];
  aggregation: string;
  groupBy: string | null;
  sqlQuery: string;
  queryResult: Record<string, unknown>[];
  styleConfig?: ChartStyleConfig | null;
}

export interface CatalogParamOption {
  value: string;
  label: string;
}

export interface CatalogParamSpec {
  kind: 'boolean' | 'number' | 'select' | 0 | 1 | 2;
  key: string;
  label: string;
  default: unknown;
  min?: number | null;
  max?: number | null;
  step?: number | null;
  options?: CatalogParamOption[] | null;
  help?: string | null;
}

export interface CatalogVariantSpec {
  id: string;
  label: string;
  description: string;
}

export interface CatalogTypeSpec {
  id: string;
  label: string;
  description: string;
  variants: CatalogVariantSpec[];
  params: CatalogParamSpec[];
}

export interface CatalogPaletteSpec {
  id: string;
  label: string;
}

export interface ChartCatalogResponse {
  types: CatalogTypeSpec[];
  palettes: CatalogPaletteSpec[];
}

export function saveChart(data: SaveChartRequest): Promise<ChartResponse> {
  return apiFetch<ChartResponse>('/api/charts', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateChart(id: string, data: UpdateChartRequest): Promise<ChartDetailResponse> {
  return apiFetch<ChartDetailResponse>(`/api/charts/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function getCharts(): Promise<ChartResponse[]> {
  return apiFetch<ChartResponse[]>('/api/charts');
}

export function getChart(id: string): Promise<ChartDetailResponse> {
  return apiFetch<ChartDetailResponse>(`/api/charts/${id}`);
}

export function getChartCatalog(): Promise<ChartCatalogResponse> {
  return apiFetch<ChartCatalogResponse>('/api/charts/catalog');
}

export function deleteChart(id: string): Promise<void> {
  return apiFetch(`/api/charts/${id}`, { method: 'DELETE' });
}

export function executeChart(id: string): Promise<ChartConfigResponse> {
  return apiFetch<ChartConfigResponse>(`/api/charts/${id}/execute`, { method: 'POST' });
}
