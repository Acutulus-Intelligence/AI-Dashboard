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

async function tryRefresh(): Promise<boolean> {
  try {
    const res = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    });
    return res.ok;
  } catch {
    return false;
  }
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

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
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

  if (res.status === 401) {
    const refreshed = await tryRefresh();
    if (refreshed) {
      res = await fetch(`${API_BASE_URL}${path}`, {
        ...options,
        headers,
        credentials: 'include',
      });
    } else {
      throw new ApiError('Session expired', 401);
    }
  }

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
