import { api } from './api';

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

export interface GenerateChartRequest {
  connectionId: string;
  tableName: string;
  prompt?: string;
  prefabChartType?: string;
  mode: 'prompt' | 'prefab' | 'auto';
}

export async function generateChart(data: GenerateChartRequest): Promise<ChartConfigResponse> {
  return api<ChartConfigResponse>('/api/graphs/generate', { method: 'POST', body: data });
}

export async function generateChartManual(data: GenerateChartRequest): Promise<ChartConfigResponse> {
  return api<ChartConfigResponse>('/api/graphs/manual', { method: 'POST', body: data });
}
