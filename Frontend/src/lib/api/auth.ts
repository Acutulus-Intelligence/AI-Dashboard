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
  requiresTwoFactor?: boolean;
  challengeToken?: string | null;
}

export interface UserInfo {
  userId: string;
  email: string;
  roles: string[];
  userType: string;
  firstName?: string | null;
  lastName?: string | null;
  companyRoleName?: string | null;
  twoFactorEnabled: boolean;
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

export function deleteAccount(currentPassword: string): Promise<void> {
  return apiFetch<void>('/api/auth/account', {
    method: 'DELETE',
    body: JSON.stringify({ currentPassword }),
  });
}

export interface TwoFactorLoginRequest {
  challengeToken: string;
  code: string;
  useRecoveryCode: boolean;
}

export interface TwoFactorSetupResponse {
  sharedKey: string;
  authenticatorUri: string;
}

export interface TwoFactorRecoveryCodesResponse {
  recoveryCodes: string[];
}

export function confirmTwoFactorLogin(data: TwoFactorLoginRequest): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/login/2fa', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function setupTwoFactor(password: string): Promise<TwoFactorSetupResponse> {
  return apiFetch<TwoFactorSetupResponse>('/api/auth/2fa/setup', {
    method: 'POST',
    body: JSON.stringify({ password }),
  });
}

export function enableTwoFactor(code: string): Promise<TwoFactorRecoveryCodesResponse> {
  return apiFetch<TwoFactorRecoveryCodesResponse>('/api/auth/2fa/enable', {
    method: 'POST',
    body: JSON.stringify({ code }),
  });
}

export function disableTwoFactor(code: string, password: string): Promise<void> {
  return apiFetch<void>('/api/auth/2fa/disable', {
    method: 'POST',
    body: JSON.stringify({ code, password }),
  });
}

export function regenerateRecoveryCodes(
  code: string,
  password: string,
): Promise<TwoFactorRecoveryCodesResponse> {
  return apiFetch<TwoFactorRecoveryCodesResponse>('/api/auth/2fa/recovery-codes', {
    method: 'POST',
    body: JSON.stringify({ code, password }),
  });
}
