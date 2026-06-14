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

export const FREE_TRIAL_DAYS = 7;

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
}

export interface UserSubscription {
  id: string;
  planId: string;
  planName: string;
  price: number;
  billingPeriod: BillingPeriod | 'Monthly' | 'Yearly';
  startDate: string;
  endDate: string | null;
  status: number | 'Trial' | 'Active' | 'Expired' | 'Canceled';
}

export interface CreateCheckoutRequest {
  planId: string;
  billingPeriod: BillingPeriod;
  successUrl: string;
  cancelUrl: string;
  trialDays?: number;
  companyId?: string;
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

export async function hasActiveSubscription(): Promise<boolean> {
  try {
    const response = await apiFetch<boolean | { hasActiveSubscription?: boolean; isActive?: boolean }>(
      '/api/subscriptions/has-active',
    );

    if (typeof response === 'boolean') return response;
    if (typeof response.hasActiveSubscription === 'boolean') return response.hasActiveSubscription;
    if (typeof response.isActive === 'boolean') return response.isActive;
  } catch {
    /* fallback to the current backend contract */
  }

  try {
    const subscription = await getCurrentSubscription();
    return subscription.status === 0 || subscription.status === 1 || subscription.status === 'Trial' || subscription.status === 'Active';
  } catch {
    return false;
  }
}

export function createCheckout(data: CreateCheckoutRequest): Promise<CreateCheckoutResponse> {
  return apiFetch<CreateCheckoutResponse>('/api/subscriptions/create-checkout', {
    method: 'POST',
    body: JSON.stringify({
      trialDays: FREE_TRIAL_DAYS,
      ...data,
    }),
  });
}

export function cancel(): Promise<void> {
  return apiFetch<void>('/api/subscriptions/cancel', {
    method: 'POST',
  });
}
