import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './features/store/AuthContext.tsx';
import ProtectedRoute from './features/components/ProtectedRoute';
import ScrollToTop from './features/components/ScrollToTop';
import LandingPage from './features/pages/LandingPage';
import DashboardPage from './features/pages/DashboardPage';
import AdminPage from './features/pages/AdminPage';
import AdminUsersPage from './features/pages/AdminUsersPage';
import LoginPage from './features/pages/LoginPage';
import RegisterPage from './features/pages/RegisterPage';
import PricingPage from './features/pages/PricingPage';
import ContactPage from './features/pages/ContactPage';
import PaymentSuccessPage from './features/pages/PaymentSuccessPage';
import PaymentCancelPage from './features/pages/PaymentCancelPage';
import SubscribePage from './features/pages/SubscribePage';
import CompanyCreatePage from './features/pages/CompanyCreatePage';
import { ROUTES } from './features/routes';

export default function Router() {
  return (
    <BrowserRouter>
      <ScrollToTop />
      <AuthProvider>
        <Routes>
          <Route path={ROUTES.LOGIN} element={<LoginPage />} />
          <Route path={ROUTES.REGISTER} element={<RegisterPage />} />
          <Route path={ROUTES.PRICING} element={<PricingPage />} />
          <Route path={ROUTES.CONTACT} element={<ContactPage />} />
          <Route path={ROUTES.PAYMENT_CANCEL} element={<PaymentCancelPage />} />
          <Route
            path={ROUTES.PAYMENT_SUCCESS}
            element={
              <ProtectedRoute requireSubscription={false}>
                <PaymentSuccessPage />
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.SUBSCRIBE}
            element={
              <ProtectedRoute requireSubscription={false}>
                <SubscribePage />
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.COMPANY_CREATE}
            element={
              <ProtectedRoute requireSubscription={false}>
                <CompanyCreatePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/users"
            element={
              <ProtectedRoute>
                <AdminUsersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.ADMIN}
            element={
              <ProtectedRoute>
                <AdminPage />
              </ProtectedRoute>
            }
          />
          <Route
            path={ROUTES.DASHBOARD}
            element={
              <ProtectedRoute>
                <DashboardPage />
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<LandingPage />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
