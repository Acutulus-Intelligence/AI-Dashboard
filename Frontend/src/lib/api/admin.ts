import { apiFetch } from './client';
import type { UserType } from './subscription';

export interface AdminSubscriptionPlan {
  id: string;
  name: string;
  description: string;
  userType: UserType;
  monthlyPrice: number;
  yearlyPrice: number;
  maxUsers: number | null;
  maxDashboards: number | null;
  maxAiQueriesPerMonth: number | null;
  isActive: boolean;
  stripeProductId: string | null;
  stripeMonthlyPriceId: string | null;
  stripeYearlyPriceId: string | null;
}

export interface AdminStats {
  totalUsers: number;
  individualSubscribedUsers: number;
  companySubscribedUsers: number;
  usersWithoutSubscription: number;
}

export interface CreatePlanRequest {
  name: string;
  description: string;
  userType: UserType;
  monthlyPrice: number;
  yearlyPrice: number;
  maxUsers: number | null;
  maxDashboards: number | null;
  maxAiQueriesPerMonth: number | null;
}

export interface UpdatePlanRequest extends CreatePlanRequest {
  isActive: boolean;
}

export interface AdminUser {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  userType: UserType;
  isAdmin: boolean;
  isModerator: boolean;
  roles: string[];
}

export type UserRole = 'User' | 'Moderator' | 'Admin';

export interface CreateUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  userType: UserType;
  role: UserRole;
}

export function getAdminPlans(): Promise<AdminSubscriptionPlan[]> {
  return apiFetch<AdminSubscriptionPlan[]>('/api/admin/subscription-plans');
}

export function createPlan(data: CreatePlanRequest): Promise<AdminSubscriptionPlan> {
  return apiFetch<AdminSubscriptionPlan>('/api/admin/subscription-plans', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updatePlan(id: string, data: UpdatePlanRequest): Promise<AdminSubscriptionPlan> {
  return apiFetch<AdminSubscriptionPlan>(`/api/admin/subscription-plans/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function deletePlan(id: string): Promise<void> {
  return apiFetch<void>(`/api/admin/subscription-plans/${id}`, {
    method: 'DELETE',
  });
}

export function movePlan(id: string, targetPlanId: string): Promise<void> {
  return apiFetch<void>(`/api/admin/subscription-plans/${id}/move`, {
    method: 'POST',
    body: JSON.stringify({ targetPlanId }),
  });
}

export function getAdminUsers(search?: string): Promise<AdminUser[]> {
  const query = search ? `?search=${encodeURIComponent(search)}` : '';
  return apiFetch<AdminUser[]>(`/api/admin/users${query}`);
}

export function createAdminUser(data: CreateUserRequest): Promise<AdminUser> {
  return apiFetch<AdminUser>('/api/admin/users', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function setModeratorRole(id: string, isModerator: boolean): Promise<AdminUser> {
  return apiFetch<AdminUser>(`/api/admin/users/${id}/moderator-role`, {
    method: 'PUT',
    body: JSON.stringify({ isModerator }),
  });
}

export function transferAdminRole(id: string): Promise<AdminUser> {
  return apiFetch<AdminUser>(`/api/admin/users/${id}/transfer-admin`, {
    method: 'POST',
  });
}

export function getAdminStats(): Promise<AdminStats> {
  return apiFetch<AdminStats>('/api/admin/stats');
}

export function setAdminRole(id: string, isAdmin: boolean): Promise<AdminUser> {
  return apiFetch<AdminUser>(`/api/admin/users/${id}/admin-role`, {
    method: 'PUT',
    body: JSON.stringify({ isAdmin }),
  });
}
