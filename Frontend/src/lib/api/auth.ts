import { apiFetch } from './client';
import { hasActiveSubscription as getHasActiveSubscription } from './subscription';

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  userType: number;
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

export interface UserInfo {
  userId: string;
  email: string;
  roles: string[];
  userType: string;
  firstName?: string | null;
  lastName?: string | null;
  companyRoleName?: string | null;
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

export function logout(): Promise<void> {
  return apiFetch<void>('/api/auth/revoke', {
    method: 'POST',
  });
}

export function getMe(): Promise<UserInfo> {
  return apiFetch<UserInfo>('/api/auth/me');
}

export function hasActiveSubscription(): Promise<boolean> {
  return getHasActiveSubscription();
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  email?: string;
}

export function changePassword(data: ChangePasswordRequest): Promise<void> {
  return apiFetch<void>('/api/auth/change-password', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateProfile(data: UpdateProfileRequest): Promise<void> {
  return apiFetch<void>('/api/auth/profile', {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function deleteAccount(): Promise<void> {
  return apiFetch<void>('/api/auth/account', {
    method: 'DELETE',
  });
}
