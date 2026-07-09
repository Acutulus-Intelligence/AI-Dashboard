import { apiFetch } from '../lib/api/client';

export interface ConnectionResponse {
  id: string;
  name: string;
  dbProvider: string;
  isVerified: boolean;
  createdAt: string;
}

export interface CreateConnectionRequest {
  name: string;
  dbProvider: string;
  host: string;
  port: number;
  database: string;
  username: string;
  password: string;
}

export interface TableInfo {
  tableName: string;
  columns: ColumnInfo[];
}

export interface ColumnInfo {
  columnName: string;
  dataType: string;
  isNullable: boolean;
}

export interface TablePreview {
  tableName: string;
  columns: ColumnInfo[];
  rows: Record<string, unknown>[];
}

export async function getConnections(): Promise<ConnectionResponse[]> {
  return apiFetch<ConnectionResponse[]>('/api/connections');
}

export async function createConnection(data: CreateConnectionRequest): Promise<ConnectionResponse> {
  return apiFetch<ConnectionResponse>('/api/connections', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function deleteConnection(id: string): Promise<void> {
  return apiFetch(`/api/connections/${id}`, { method: 'DELETE' });
}

export async function testConnection(id: string): Promise<{ isVerified: boolean }> {
  return apiFetch<{ isVerified: boolean }>(`/api/connections/${id}/test`, { method: 'POST' });
}

export async function getTables(connectionId: string): Promise<TableInfo[]> {
  return apiFetch<TableInfo[]>(`/api/connections/${connectionId}/tables`);
}

export async function getTablePreview(connectionId: string, tableName: string, rows = 5): Promise<TablePreview> {
  return apiFetch<TablePreview>(
    `/api/connections/${connectionId}/tables/${encodeURIComponent(tableName)}/preview?${new URLSearchParams({ rows: String(rows) })}`,
  );
}
