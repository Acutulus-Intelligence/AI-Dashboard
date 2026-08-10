import { apiFetch } from '../lib/api/client';
import type { ChartConfigResponse } from './graphsApi';

export type CollectionVisibility = 'Private' | 'Company' | 'Roles';

export interface CollectionResponse {
  id: string;
  name: string;
  description?: string | null;
  companyId?: string | null;
  createdById: string;
  visibility: CollectionVisibility;
  allowedRoleIds: string[];
  fileCount: number;
  rowCount: number;
  createdAt: string;
}

export interface CollectionFileResponse {
  id: string;
  name: string;
  tableName: string;
  columnCount: number;
  rowCount: number;
  createdAt: string;
}

export interface CollectionDetailResponse {
  id: string;
  name: string;
  description?: string | null;
  companyId?: string | null;
  createdById: string;
  visibility: CollectionVisibility;
  allowedRoleIds: string[];
  createdAt: string;
  files: CollectionFileResponse[];
}

export interface CollectionFileDetailResponse {
  id: string;
  name: string;
  tableName: string;
  columns: { name: string; type: string }[];
  rowCount: number;
  createdAt: string;
  previewRows: Record<string, unknown>[];
}

export interface CreateCollectionRequest {
  name: string;
  description?: string | null;
  visibility?: CollectionVisibility;
  allowedRoleIds?: string[];
}

export type UpdateCollectionRequest = CreateCollectionRequest;

export interface GenerateCollectionChartRequest {
  prompt?: string;
  prefabChartType?: string;
  mode: 'prompt' | 'prefab' | 'auto';
}

export async function getCollections(): Promise<CollectionResponse[]> {
  return apiFetch<CollectionResponse[]>('/api/collections');
}

export async function getCollection(id: string): Promise<CollectionDetailResponse> {
  return apiFetch<CollectionDetailResponse>(`/api/collections/${id}`);
}

export async function createCollection(data: CreateCollectionRequest): Promise<CollectionResponse> {
  return apiFetch<CollectionResponse>('/api/collections', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateCollection(id: string, data: UpdateCollectionRequest): Promise<CollectionResponse> {
  return apiFetch<CollectionResponse>(`/api/collections/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function deleteCollection(id: string): Promise<void> {
  return apiFetch(`/api/collections/${id}`, { method: 'DELETE' });
}

export async function uploadCollectionFile(id: string, file: File): Promise<CollectionFileResponse> {
  const formData = new FormData();
  formData.append('file', file);
  return apiFetch<CollectionFileResponse>(`/api/collections/${id}/files`, {
    method: 'POST',
    body: formData,
  });
}

export async function getCollectionFile(id: string, fileId: string): Promise<CollectionFileDetailResponse> {
  return apiFetch<CollectionFileDetailResponse>(`/api/collections/${id}/files/${fileId}`);
}

export async function deleteCollectionFile(id: string, fileId: string): Promise<void> {
  return apiFetch(`/api/collections/${id}/files/${fileId}`, { method: 'DELETE' });
}

export async function generateCollectionChart(
  id: string,
  fileId: string,
  data: GenerateCollectionChartRequest,
): Promise<ChartConfigResponse> {
  return apiFetch<ChartConfigResponse>(`/api/collections/${id}/files/${fileId}/generate`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}