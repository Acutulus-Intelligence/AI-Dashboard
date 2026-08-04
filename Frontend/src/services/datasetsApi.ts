import { apiFetch } from '../lib/api/client';

export interface DatasetResponse {
  id: string;
  name: string;
  columnCount: number;
  rowCount: number;
  createdAt: string;
}

export interface DatasetColumn {
  name: string;
  type: string;
}

export interface DatasetDetailResponse {
  id: string;
  name: string;
  tableName: string;
  columns: DatasetColumn[];
  rowCount: number;
  createdAt: string;
  previewRows: Record<string, unknown>[];
}

export interface GenerateDatasetChartRequest {
  prompt?: string;
  prefabChartType?: string;
  mode: 'prompt' | 'prefab' | 'auto';
}

export async function getDatasets(): Promise<DatasetResponse[]> {
  return apiFetch<DatasetResponse[]>('/api/datasets');
}

export async function getDataset(id: string): Promise<DatasetDetailResponse> {
  return apiFetch<DatasetDetailResponse>(`/api/datasets/${id}`);
}

export async function uploadDataset(file: File): Promise<DatasetResponse> {
  const formData = new FormData();
  formData.append('file', file);
  return apiFetch<DatasetResponse>('/api/datasets/upload', { method: 'POST', body: formData });
}

export async function deleteDataset(id: string): Promise<void> {
  return apiFetch(`/api/datasets/${id}`, { method: 'DELETE' });
}

export async function generateDatasetChart(
  id: string,
  data: GenerateDatasetChartRequest,
): Promise<import('./graphsApi').ChartConfigResponse> {
  return apiFetch<import('./graphsApi').ChartConfigResponse>(`/api/datasets/${id}/generate`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}
