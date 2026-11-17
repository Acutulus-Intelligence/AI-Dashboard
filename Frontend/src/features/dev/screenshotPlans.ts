import type { SubscriptionPlan } from '../../lib/api/subscription';

/**
 * Dev-only stand-ins for `/pricing` screenshots when the API is not running.
 * Import this module only from a branch that Vite removes in production
 * (`import.meta.env.DEV`). It is not a fallback for a failed plan request.
 */
export const screenshotPlans: SubscriptionPlan[] = [
  {
    id: 'screenshot-starter',
    name: 'Starter',
    description: 'For one person exploring a database and a few dashboards.',
    userType: 0,
    monthlyPrice: 19,
    yearlyPrice: 190,
    maxUsers: 1,
    maxDashboards: 3,
    maxAiQueriesPerMonth: 100,
    isActive: true,
    trialDays: 7,
  },
  {
    id: 'screenshot-team',
    name: 'Team',
    description: 'Shared connections, folders, and roles for a company workspace.',
    userType: 1,
    monthlyPrice: 49,
    yearlyPrice: 490,
    maxUsers: 10,
    maxDashboards: null,
    maxAiQueriesPerMonth: 1000,
    isActive: true,
    trialDays: 14,
  },
  {
    id: 'screenshot-enterprise',
    name: 'Enterprise',
    description: 'Higher limits, a security review, and a direct line to our team.',
    userType: 1,
    monthlyPrice: 199,
    yearlyPrice: 1990,
    maxUsers: null,
    maxDashboards: null,
    maxAiQueriesPerMonth: null,
    isActive: true,
    trialDays: 0,
  },
];
