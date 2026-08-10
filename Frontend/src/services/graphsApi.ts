import { apiFetch } from '../lib/api/client';
import type { ChartStyleConfig } from '../features/charts/types';
import type { DataQueryModel } from '../lib/api/charts';

export interface AiGenerationDebug {
  rawJson?: string | null;
  chartType: string;
  sqlQuery: string;
  styleConfig?: ChartStyleConfig | null;
  finishReason?: string | null;
  notes?: string[] | null;
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
  dataModel?: DataQueryModel | null;
  aiDebug?: AiGenerationDebug | null;
}
/** Metadata sent as refine context — never includes queryResult. */
export interface ChartBaseline {
  title: string;
  chartType: string;
  xAxis: string;
  yAxis: string[];
  aggregation: string;
  groupBy: string | null;
  sqlQuery: string;
  styleConfig?: ChartStyleConfig | null;
}

export interface GenerateChartRequest {
  connectionId: string;
  tableName: string;
  prompt?: string;
  prefabChartType?: string;
  mode: 'prompt' | 'prefab' | 'auto';
  currentChart?: ChartBaseline | null;
}

export async function generateChart(data: GenerateChartRequest): Promise<ChartConfigResponse> {
  return apiFetch<ChartConfigResponse>('/api/graphs/generate', { method: 'POST', body: JSON.stringify(data) });
}

export async function generateChartManual(data: GenerateChartRequest): Promise<ChartConfigResponse> {
  return apiFetch<ChartConfigResponse>('/api/graphs/manual', { method: 'POST', body: JSON.stringify(data) });
}
