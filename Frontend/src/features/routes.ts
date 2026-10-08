export const ROUTES = {
  HOME: '/',
  DASHBOARD: '/dashboard',
  DASHBOARD_VIEW: '/dashboard/:dashboardId',
  LOGIN: '/login',
  REGISTER: '/register',
  PRICING: '/pricing',
  ADMIN: '/admin',
  ADMIN_MAIN: '/admin/main',
  ADMIN_USERS: '/admin/users',
  ADMIN_STYLE: '/admin/style',
  ADMIN_PLANS: '/admin/plans',
  ADMIN_ACCOUNTS: '/admin/accounts',
  CONTACT: '/contact',
  PRIVACY: '/privacy',
  TERMS: '/terms',
  PAYMENT_SUCCESS: '/payment/success',
  PAYMENT_CANCEL: '/payment/cancel',
  COMPANY_CREATE: '/company/create',
  CONNECTIONS: '/dashboard/connections',
  CHARTS: '/dashboard/charts',
  GRAPHS_NEW: '/dashboard/graphs/new',
  GRAPHS_EDIT: '/dashboard/graphs/:chartId/edit',
  SETTINGS: '/settings',
  SUBSCRIPTION: '/subscription',
  PROFILE: '/profile',
} as const;

export function graphEditPath(chartId: string) {
  return `/dashboard/graphs/${chartId}/edit`;
}

export function dashboardPath(dashboardId: string) {
  return `/dashboard/${dashboardId}`;
}
