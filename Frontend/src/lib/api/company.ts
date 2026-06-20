import { apiFetch } from './client';

export interface CompanyResponse {
  id: string;
  name: string;
  ownerId: string;
  ownerName: string;
  userCount: number;
  roles: CompanyRoleResponse[];
  currentSubscription: CompanySubscriptionResponse | null;
}

export interface CompanyUserResponse {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  roleName: string | null;
  roleId: string | null;
  isOwner: boolean;
}

export interface CompanyRoleResponse {
  id: string;
  name: string;
  isSystemRole: boolean;
  canViewAllDashboards: boolean;
  canManageUsers: boolean;
  canManageRoles: boolean;
  canManageDashboards: boolean;
  allowedTables: string[];
  userCount: number;
}

export interface CompanyInviteResponse {
  id: string;
  email: string;
  roleName: string | null;
  roleId: string;
  createdAt: string;
  expiresAt: string;
  isExpired: boolean;
  isAccepted: boolean;
}

export interface CompanySubscriptionResponse {
  id: string;
  planId: string;
  planName: string;
  price: number;
  billingPeriod: number;
  maxUsers: number | null;
  startDate: string;
  endDate: string | null;
  status: number;
}

export interface CreateCompanyRequest {
  name: string;
}

export interface InviteUserRequest {
  email: string;
  roleId: string;
}

export interface AcceptInviteRequest {
  inviteId: string;
}

export interface UpdateUserRoleRequest {
  roleId: string;
}

export interface CreateRoleRequest {
  name: string;
  canViewAllDashboards: boolean;
  canManageUsers: boolean;
  canManageRoles: boolean;
  canManageDashboards: boolean;
  allowedTables?: string[];
}

export interface UpdateRoleRequest {
  name: string;
  canViewAllDashboards: boolean;
  canManageUsers: boolean;
  canManageRoles: boolean;
  canManageDashboards: boolean;
  allowedTables?: string[];
}

export function createCompany(data: CreateCompanyRequest): Promise<CompanyResponse> {
  return apiFetch<CompanyResponse>('/api/companies', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function getMyCompany(): Promise<CompanyResponse> {
  return apiFetch<CompanyResponse>('/api/companies/me');
}

export function getCompanyById(id: string): Promise<CompanyResponse> {
  return apiFetch<CompanyResponse>(`/api/companies/${id}`);
}

export function getCompanyUsers(companyId: string): Promise<CompanyUserResponse[]> {
  return apiFetch<CompanyUserResponse[]>(`/api/companies/${companyId}/users`);
}

export function updateUserRole(companyId: string, userId: string, data: UpdateUserRoleRequest): Promise<void> {
  return apiFetch<void>(`/api/companies/${companyId}/users/${userId}/role`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function removeUser(companyId: string, userId: string): Promise<void> {
  return apiFetch<void>(`/api/companies/${companyId}/users/${userId}`, {
    method: 'DELETE',
  });
}

export function transferOwnership(companyId: string, userId: string): Promise<void> {
  return apiFetch<void>(`/api/companies/${companyId}/transfer-ownership/${userId}`, {
    method: 'POST',
  });
}

export function inviteUser(companyId: string, data: InviteUserRequest): Promise<{ token: string }> {
  return apiFetch<{ token: string }>(`/api/companies/${companyId}/invite`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function acceptInvite(data: AcceptInviteRequest): Promise<void> {
  return apiFetch<void>('/api/companies/accept-invite', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function getCompanyRoles(companyId: string): Promise<CompanyRoleResponse[]> {
  return apiFetch<CompanyRoleResponse[]>(`/api/companies/${companyId}/roles`);
}

export function createRole(companyId: string, data: CreateRoleRequest): Promise<CompanyRoleResponse> {
  return apiFetch<CompanyRoleResponse>(`/api/companies/${companyId}/roles`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateRole(companyId: string, roleId: string, data: UpdateRoleRequest): Promise<CompanyRoleResponse> {
  return apiFetch<CompanyRoleResponse>(`/api/companies/${companyId}/roles/${roleId}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function deleteRole(companyId: string, roleId: string): Promise<void> {
  return apiFetch<void>(`/api/companies/${companyId}/roles/${roleId}`, {
    method: 'DELETE',
  });
}

export function getCompanyInvites(companyId: string): Promise<CompanyInviteResponse[]> {
  return apiFetch<CompanyInviteResponse[]>(`/api/companies/${companyId}/invites`);
}

export function revokeInvite(companyId: string, inviteId: string): Promise<void> {
  return apiFetch<void>(`/api/companies/${companyId}/invites/${inviteId}`, {
    method: 'DELETE',
  });
}

export function getPendingInvites(): Promise<CompanyInviteResponse[]> {
  return apiFetch<CompanyInviteResponse[]>('/api/invites/pending');
}

export function rejectInvite(inviteId: string): Promise<void> {
  return apiFetch<void>(`/api/invites/${inviteId}`, {
    method: 'DELETE',
  });
}

export function deleteCompany(companyId: string): Promise<void> {
  return apiFetch<void>(`/api/companies/${companyId}`, {
    method: 'DELETE',
  });
}
