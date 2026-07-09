import { API_BASE_URL } from '../config/env';

const TOKEN_KEY = 'access_token';
const REFRESH_KEY = 'refresh_token';

export function getAccessToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setTokens(access: string, refresh: string): void {
  localStorage.setItem(TOKEN_KEY, access);
  localStorage.setItem(REFRESH_KEY, refresh);
}

export function clearTokens(): void {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
}

export function isAuthenticated(): boolean {
  return !!getAccessToken();
}

async function refreshAccessToken(): Promise<string | null> {
  const refresh = localStorage.getItem(REFRESH_KEY);
  if (!refresh) return null;

  try {
    const res = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: refresh }),
    });

    if (!res.ok) {
      clearTokens();
      return null;
    }

    const data = await res.json();
    setTokens(data.accessToken, data.refreshToken);
    return data.accessToken;
  } catch {
    clearTokens();
    return null;
  }
}

export class ApiError extends Error {
  public status: number;
  public title: string;
  public errorCode: string;
  public traceId?: string;

  constructor(
    status: number,
    title: string,
    errorCode: string,
    message: string,
    traceId?: string,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.errorCode = errorCode;
    this.traceId = traceId;
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  params?: Record<string, string | undefined>;
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, params } = options;

  let url = `${API_BASE_URL}${path}`;
  if (params) {
    const search = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined) search.set(key, value);
    }
    const qs = search.toString();
    if (qs) url += `?${qs}`;
  }

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  const token = getAccessToken();
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(url, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (res.status === 401) {
    const newToken = await refreshAccessToken();
    if (newToken) {
      headers['Authorization'] = `Bearer ${newToken}`;
      const retryRes = await fetch(url, {
        method,
        headers,
        body: body ? JSON.stringify(body) : undefined,
      });
      if (retryRes.ok) return retryRes.json();
      if (retryRes.status === 401) {
        clearTokens();
        window.location.href = '/';
        throw new ApiError(401, 'Unauthorized', 'unauthorized', 'Session expired');
      }
      return handleError(retryRes);
    }
    clearTokens();
    window.location.href = '/';
    throw new ApiError(401, 'Unauthorized', 'unauthorized', 'Session expired');
  }

  if (!res.ok) return handleError(res);
  if (res.status === 204) return undefined as T;
  return res.json();
}

async function handleError(res: Response): Promise<never> {
  let title = 'Error';
  let errorCode = 'unknown';
  let detail = 'An unexpected error occurred.';
  let traceId: string | undefined;

  try {
    const body = await res.json();
    title = body.title ?? title;
    errorCode = body.errorCode ?? errorCode;
    detail = body.detail ?? detail;
    traceId = body.traceId;
  } catch {}

  throw new ApiError(res.status, title, errorCode, detail, traceId);
}
