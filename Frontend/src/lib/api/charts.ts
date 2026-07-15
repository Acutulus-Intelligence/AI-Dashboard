import { apiFetch } from './client';

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
}

export function saveChart(data: SaveChartRequest): Promise<ChartResponse> {
  return apiFetch<ChartResponse>('/api/charts', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function getCharts(): Promise<ChartResponse[]> {
  return apiFetch<ChartResponse[]>('/api/charts');
}

export function getChart(id: string): Promise<ChartDetailResponse> {
  return apiFetch<ChartDetailResponse>(`/api/charts/${id}`);
}

export function deleteChart(id: string): Promise<void> {
  return apiFetch(`/api/charts/${id}`, { method: 'DELETE' });
}

export function executeChart(id: string): Promise<ChartConfigResponse> {
  return apiFetch<ChartConfigResponse>(`/api/charts/${id}/execute`, { method: 'POST' });
}
