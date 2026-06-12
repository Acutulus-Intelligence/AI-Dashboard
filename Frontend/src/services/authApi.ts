import { api, setTokens, clearTokens } from './api';

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UserInfo {
  userId: string;
  email: string;
  roles: string[];
}

export async function register(data: RegisterRequest): Promise<AuthResponse> {
  const res = await api<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: data,
  });
  setTokens(res.accessToken, res.refreshToken);
  return res;
}

export async function login(data: LoginRequest): Promise<AuthResponse> {
  const res = await api<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: data,
  });
  setTokens(res.accessToken, res.refreshToken);
  return res;
}

export async function getMe(): Promise<UserInfo> {
  return api<UserInfo>('/api/auth/me');
}

export function logout(): void {
  clearTokens();
  window.location.href = '/';
}
