import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './features/store/AuthContext.tsx';
import ProtectedRoute from './features/components/ProtectedRoute';
import ScrollToTop from './features/components/ScrollToTop';
import { ROUTES } from './features/routes';

const LandingPage = lazy(() => import('./features/pages/LandingPage'));
const DashboardPage = lazy(() => import('./features/pages/DashboardPage'));
const ConnectionsPage = lazy(() => import('./features/pages/ConnectionsPage'));
const GraphCreationPage = lazy(() => import('./features/pages/GraphCreationPage'));
const AdminPage = lazy(() => import('./features/pages/AdminPage'));
const AdminUsersPage = lazy(() => import('./features/pages/AdminUsersPage'));
const LoginPage = lazy(() => import('./features/pages/LoginPage'));
const RegisterPage = lazy(() => import('./features/pages/RegisterPage'));
const PricingPage = lazy(() => import('./features/pages/PricingPage'));
const ContactPage = lazy(() => import('./features/pages/ContactPage'));
const PaymentSuccessPage = lazy(() => import('./features/pages/PaymentSuccessPage'));
const PaymentCancelPage = lazy(() => import('./features/pages/PaymentCancelPage'));
const CompanyCreatePage = lazy(() => import('./features/pages/CompanyCreatePage'));
const SettingsPage = lazy(() => import('./features/pages/SettingsPage'));
const SubscriptionPage = lazy(() => import('./features/pages/SubscriptionPage'));

function PageLoader() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background text-on-surface-variant">
      Loading...
    </div>
  );
}

function LazyPage({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<PageLoader />}>{children}</Suspense>;
}

export default function Router() {
  return (
    <BrowserRouter>
      <ScrollToTop />
      <AuthProvider>
        <Routes>
          <Route path={ROUTES.LOGIN} element={<LazyPage><LoginPage /></LazyPage>} />
          <Route path={ROUTES.REGISTER} element={<LazyPage><RegisterPage /></LazyPage>} />
          <Route path={ROUTES.PRICING} element={<LazyPage><PricingPage /></LazyPage>} />
          <Route path={ROUTES.CONTACT} element={<LazyPage><ContactPage /></LazyPage>} />
          <Route path={ROUTES.PAYMENT_CANCEL} element={<LazyPage><PaymentCancelPage /></LazyPage>} />
          <Route
            path={ROUTES.PAYMENT_SUCCESS}
            element={
              <ProtectedRoute requireSubscription={false}>
                <LazyPage><PaymentSuccessPage /></LazyPage>
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.COMPANY_CREATE}
            element={
              <ProtectedRoute requireSubscription={false}>
                <LazyPage><CompanyCreatePage /></LazyPage>
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.SETTINGS}
            element={
              <ProtectedRoute requireSubscription={false}>
                <LazyPage><SettingsPage /></LazyPage>
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.SUBSCRIPTION}
            element={
              <ProtectedRoute requireSubscription={false}>
                <LazyPage><SubscriptionPage /></LazyPage>
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/users"
            element={
              <ProtectedRoute requireSubscription={false}>
                <LazyPage><AdminUsersPage /></LazyPage>
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.ADMIN}
            element={
              <ProtectedRoute requireSubscription={false}>
                <LazyPage><AdminPage /></LazyPage>
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.DASHBOARD}
            element={
              <ProtectedRoute>
                <LazyPage><DashboardPage /></LazyPage>
              </ProtectedRoute>
            }
          />
          <Route path="/dashboard/connections" element={<ProtectedRoute><LazyPage><ConnectionsPage /></LazyPage></ProtectedRoute>} />
          <Route path="/dashboard/graphs/new" element={<ProtectedRoute><LazyPage><GraphCreationPage /></LazyPage></ProtectedRoute>} />
          <Route path="*" element={<LazyPage><LandingPage /></LazyPage>} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
