import { API_BASE_URL } from '../../config/env';

class ApiError extends Error {
  status: number;
  code?: string;
  fieldErrors?: Record<string, string[]>;

  constructor(
    message: string,
    status: number,
    code?: string,
    fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }
}

let refreshInFlight: Promise<boolean> | null = null;

function performRefresh(): Promise<boolean> {
  return fetch(`${API_BASE_URL}/api/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
  })
    .then((res) => res.ok)
    .catch(() => false);
}

/**
 * Refreshes the access token. Concurrent callers share a single in-flight
 * request and, where supported, a cross-tab Web Lock, so parallel 401s never
 * rotate the refresh token at the same time. Without this the server sees the
 * same token reused and revokes every session.
 */
function tryRefresh(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight;

  const locks = (navigator as Navigator & { locks?: LockManager }).locks;
  const run = locks
    ? () => locks.request('ai-dashboard-auth-refresh', performRefresh) as Promise<boolean>
    : performRefresh;

  refreshInFlight = run()
    .catch(() => false)
    .finally(() => {
      refreshInFlight = null;
    });

  return refreshInFlight;
}

function parseErrorBody(body: Record<string, unknown>): {
  message: string;
  code?: string;
  fieldErrors?: Record<string, string[]>;
} {
  let message = 'Request failed';
  let code: string | undefined;
  let fieldErrors: Record<string, string[]> | undefined;

  if (typeof body.detail === 'string') message = body.detail;
  if (typeof body.title === 'string' && !body.detail) message = body.title;
  if (typeof body.code === 'string') code = body.code;
  if (typeof body.errorCode === 'string') code = body.errorCode;

  if (body.errors && typeof body.errors === 'object') {
    fieldErrors = {};
    for (const [key, value] of Object.entries(body.errors as Record<string, unknown>)) {
      if (Array.isArray(value)) {
        fieldErrors[key] = value.filter((v): v is string => typeof v === 'string');
      }
    }
  }

  return { message, code, fieldErrors };
}

/** Auth endpoints where 401 means bad credentials / auth failure, not an expired session. */
function isCredentialAuthPath(path: string): boolean {
  return (
    path.startsWith('/api/auth/login') ||
    path.startsWith('/api/auth/register') ||
    path.startsWith('/api/auth/refresh')
  );
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  return handleResponse<T>(await rawApiFetch(path, options));
}

export async function apiFetchWithHeaders<T>(
  path: string,
  options: RequestInit = {},
): Promise<{ data: T; headers: Headers }> {
  const res = await rawApiFetch(path, options);
  return { data: await handleResponse<T>(res), headers: res.headers };
}

async function rawApiFetch(path: string, options: RequestInit): Promise<Response> {
  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string>),
  };

  if (!headers['Content-Type'] && !(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  let res = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  });

  if (res.status === 401 && !isCredentialAuthPath(path)) {
    const refreshed = await tryRefresh();
    if (refreshed) {
      res = await fetch(`${API_BASE_URL}${path}`, {
        ...options,
        headers,
        credentials: 'include',
      });
    } else {
      window.dispatchEvent(new CustomEvent('auth:session-expired'));
      throw new ApiError('Session expired', 401);
    }
  }

  return res;
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = `Request failed with status ${res.status}`;
    let code: string | undefined;
    let fieldErrors: Record<string, string[]> | undefined;
    try {
      const body = await res.json() as Record<string, unknown>;
      const parsed = parseErrorBody(body);
      message = parsed.message;
      code = parsed.code;
      fieldErrors = parsed.fieldErrors;
    } catch {
      /* ignore */
    }
    throw new ApiError(message, res.status, code, fieldErrors);
  }

  if (res.status === 204) return undefined as T;

  return res.json();
}

export { ApiError };
