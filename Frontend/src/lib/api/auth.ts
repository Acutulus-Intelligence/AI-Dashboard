import { apiFetch } from './client';
import { hasActiveSubscription as getHasActiveSubscription } from './subscription';

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  userType: number;
  companyName?: string;
  inviteToken?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken?: string;
  refreshToken?: string;
  expiresIn: number;
}

export interface RefreshTokenRequest {
  accessToken: string;
  refreshToken: string;
}

export interface UserInfo {
  userId: string;
  email: string;
  roles: string[];
}

export function register(data: RegisterRequest): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function login(data: LoginRequest): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function refresh(data: RefreshTokenRequest): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/refresh', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function revoke(data: RefreshTokenRequest): Promise<void> {
  return apiFetch<void>('/api/auth/revoke', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function logout(): Promise<void> {
  return apiFetch<void>('/api/auth/revoke', {
    method: 'POST',
  });
}

export function getMe(): Promise<UserInfo> {
  return apiFetch<UserInfo>('/api/auth/me');
}

export function me(): Promise<UserInfo> {
  return getMe();
}

export function hasActiveSubscription(): Promise<boolean> {
  return getHasActiveSubscription();
}
