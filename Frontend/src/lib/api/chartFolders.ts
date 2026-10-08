import { apiFetch } from './client';

export interface ChartFolderResponse {
  id: string;
  name: string;
  chartCount: number;
  createdAt: string;
}

export function getChartFolders(): Promise<ChartFolderResponse[]> {
  return apiFetch<ChartFolderResponse[]>('/api/chart-folders');
}

export function createChartFolder(data: { name: string }): Promise<ChartFolderResponse> {
  return apiFetch<ChartFolderResponse>('/api/chart-folders', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function renameChartFolder(id: string, data: { name: string }): Promise<ChartFolderResponse> {
  return apiFetch<ChartFolderResponse>(`/api/chart-folders/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function deleteChartFolder(id: string): Promise<void> {
  return apiFetch(`/api/chart-folders/${id}`, { method: 'DELETE' });
}

export function moveChartToFolder(chartId: string, folderId: string | null): Promise<void> {
  return apiFetch(`/api/charts/${chartId}/folder`, {
    method: 'PUT',
    body: JSON.stringify({ folderId }),
  });
}
