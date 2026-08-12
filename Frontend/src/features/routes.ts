export const ROUTES = {
  HOME: '/',
  DASHBOARD: '/dashboard',
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
