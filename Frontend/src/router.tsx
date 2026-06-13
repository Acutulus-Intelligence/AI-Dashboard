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

export default function Router() {
  return (
    <BrowserRouter>
      <ScrollToTop />
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/pricing" element={<PricingPage />} />
          <Route path="/contact" element={<ContactPage />} />
          <Route
            path="/admin/users"
            element={
              <ProtectedRoute>
                <AdminUsersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <ProtectedRoute>
                <AdminPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/dashboard"
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
