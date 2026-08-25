import { apiFetch } from './client';

export type BillingPeriod = 0 | 1;
export type UserType = 0 | 1;

export const BILLING_PERIOD = {
  Monthly: 0,
  Yearly: 1,
} as const satisfies Record<string, BillingPeriod>;

export const USER_TYPE = {
  Individual: 0,
  Company: 1,
} as const satisfies Record<string, UserType>;

export interface SubscriptionPlan {
  id: string;
  name: string;
  description: string;
  userType: UserType | 'Individual' | 'Company';
  monthlyPrice: number;
  yearlyPrice: number;
  maxUsers: number | null;
  maxDashboards: number | null;
  maxAiQueriesPerMonth: number | null;
  isActive: boolean;
  trialDays: number | null;
}

export interface UserSubscription {
  id: string;
  planId: string;
  planName: string;
  price: number;
  nextPrice: number | null;
  nextPriceEffectiveDate: string | null;
  billingPeriod: BillingPeriod | 'Monthly' | 'Yearly';
  startDate: string;
  endDate: string | null;
  status: number | 'Trial' | 'Active' | 'Expired' | 'Canceled';
  cancelAtPeriodEnd: boolean;
  trialEndDate: string | null;
}

export interface CreateCheckoutRequest {
  planId: string;
  billingPeriod: BillingPeriod;
  successUrl: string;
  cancelUrl: string;
}

export interface UpgradeToCompanyRequest {
  companyName: string;
  planId: string;
  billingPeriod: BillingPeriod;
  successUrl: string;
  cancelUrl: string;
}

export interface CompanySubscription {
  id: string;
  planId: string;
  planName: string;
  price: number;
  nextPrice: number | null;
  nextPriceEffectiveDate: string | null;
  billingPeriod: number;
  maxUsers: number | null;
  startDate: string;
  endDate: string | null;
  status: number;
  cancelAtPeriodEnd: boolean;
  trialEndDate: string | null;
}

export interface CreateCheckoutResponse {
  checkoutUrl: string;
  sessionId?: string;
}

export function getPlans(userType?: UserType): Promise<SubscriptionPlan[]> {
  const query = userType === undefined ? '' : `?userType=${userType}`;
  return apiFetch<SubscriptionPlan[]>(`/api/subscriptions/plans${query}`);
}

export function getCurrentSubscription(): Promise<UserSubscription> {
  return apiFetch<UserSubscription>('/api/subscriptions/current');
}

/**
 * Estimates the Stripe proration credit for unused time on the current (paid) subscription.
 * Returns null when there is nothing creditable (trial, no end date, or expired period).
 * Stripe finalizes the exact amount on the invoice — treat this as an approximation.
 */
export function estimateUpgradeCredit(sub: UserSubscription): number | null {
  if (sub.status !== 1 && sub.status !== 'Active') return null;
  if (!sub.endDate) return null;

  const now = Date.now();
  const end = new Date(sub.endDate).getTime();
  if (!Number.isFinite(end) || end <= now) return null;

  const start = new Date(sub.startDate).getTime();
  if (!Number.isFinite(start) || end <= start) return null;

  const credit = sub.price * ((end - now) / (end - start));
  return credit > 0 ? Math.round(credit * 100) / 100 : null;
}

export async function hasActiveSubscription(): Promise<boolean> {
  const response = await apiFetch<{ hasActiveSubscription: boolean }>(
    '/api/subscriptions/has-active',
  );
  return response.hasActiveSubscription;
}

export function createCheckout(data: CreateCheckoutRequest): Promise<CreateCheckoutResponse> {
  return apiFetch<CreateCheckoutResponse>('/api/subscriptions/create-checkout', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function createCompanyCheckout(
  companyId: string,
  planId: string,
  billingPeriod: BillingPeriod,
  successUrl: string,
  cancelUrl: string,
): Promise<CreateCheckoutResponse> {
  return apiFetch<CreateCheckoutResponse>(`/api/subscriptions/company/${companyId}/create-checkout`, {
    method: 'POST',
    body: JSON.stringify({ planId, billingPeriod, successUrl, cancelUrl }),
  });
}

export function upgradeToCompany(data: UpgradeToCompanyRequest): Promise<CreateCheckoutResponse> {
  return apiFetch<CreateCheckoutResponse>('/api/subscriptions/upgrade-to-company', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function confirmCheckout(sessionId: string): Promise<void> {
  return apiFetch<void>('/api/subscriptions/confirm', {
    method: 'POST',
    body: JSON.stringify({ sessionId }),
  });
}

export function cancel(): Promise<void> {
  return apiFetch<void>('/api/subscriptions/cancel', {
    method: 'POST',
  });
}

export function reactivate(): Promise<void> {
  return apiFetch<void>('/api/subscriptions/reactivate', {
    method: 'POST',
  });
}

export function getCompanySubscription(companyId: string): Promise<CompanySubscription> {
  return apiFetch<CompanySubscription>(`/api/subscriptions/company/${companyId}/current`);
}

export function cancelCompanySubscription(companyId: string): Promise<void> {
  return apiFetch<void>(`/api/subscriptions/company/${companyId}/cancel`, {
    method: 'POST',
  });
}

export function reactivateCompanySubscription(companyId: string): Promise<void> {
  return apiFetch<void>(`/api/subscriptions/company/${companyId}/reactivate`, {
    method: 'POST',
  });
}
