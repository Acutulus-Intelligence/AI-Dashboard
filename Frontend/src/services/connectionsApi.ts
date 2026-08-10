import { apiFetch, apiFetchWithHeaders } from '../lib/api/client';

export type ConnectionVisibility = 'Private' | 'Company' | 'Roles';
export type DbProvider = 'PostgreSql' | 'MySql' | 'SqlServer';

export interface ConnectionResponse {
  id: string;
  name: string;
  dbProvider: string;
  isVerified: boolean;
  createdAt: string;
  createdById: string;
  visibility: ConnectionVisibility;
  allowedRoleIds: string[];
  companyId: string | null;
  host: string;
  database: string;
}

export interface ParseConnectionStringResponse {
  provider: DbProvider | null;
  host: string;
  port: number;
  database: string;
  username: string;
  password: string;
}

export interface CreateConnectionRequest {
  name: string;
  dbProvider: string;
  connectionString: string;
  visibility: ConnectionVisibility;
  allowedRoleIds?: string[];
}

export interface UpdateConnectionRequest {
  name: string;
  dbProvider: string;
  connectionString: string;
  visibility: ConnectionVisibility;
  allowedRoleIds?: string[];
}

export interface ConnectionConfigResponse {
  name: string;
  dbProvider: string;
  connectionString: string;
  visibility: ConnectionVisibility;
  allowedRoleIds: string[];
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

export async function getConnectionsWithCount(): Promise<{
  connections: ConnectionResponse[];
  companyConnectionCount: number;
}> {
  const { data, headers } = await apiFetchWithHeaders<ConnectionResponse[]>('/api/connections');
  const count = Number(headers.get('x-company-connection-count') ?? '0');
  return { connections: data, companyConnectionCount: Number.isFinite(count) ? count : 0 };
}

export async function getConnectionConfig(id: string): Promise<ConnectionConfigResponse> {
  return apiFetch<ConnectionConfigResponse>(`/api/connections/${id}/config`);
}

export async function parseConnectionString(
  connectionString: string,
): Promise<ParseConnectionStringResponse> {
  return apiFetch<ParseConnectionStringResponse>('/api/connections/parse', {
    method: 'POST',
    body: JSON.stringify({ connectionString }),
  });
}

export async function createConnection(data: CreateConnectionRequest): Promise<ConnectionResponse> {
  return apiFetch<ConnectionResponse>('/api/connections', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateConnection(id: string, data: UpdateConnectionRequest): Promise<ConnectionResponse> {
  return apiFetch<ConnectionResponse>(`/api/connections/${id}`, {
    method: 'PUT',
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
