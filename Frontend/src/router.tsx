import DashboardPage from './features/pages/DashboardPage.tsx';
import LandingPage from './features/pages/LandingPage.tsx';

export default function Router() {
  const isCustomerApp = window.location.pathname.startsWith('/dashboard');

  if (isCustomerApp) {
    return <DashboardPage />;
  }

  return <LandingPage />;
}
