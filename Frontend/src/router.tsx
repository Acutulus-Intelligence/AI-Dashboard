import { BrowserRouter, Routes, Route } from 'react-router-dom';
import LandingPage from './features/pages/LandingPage';
import DashboardPage from './features/pages/DashboardPage';
import ConnectionsPage from './features/pages/ConnectionsPage';
import GraphCreationPage from './features/pages/GraphCreationPage';
import ProtectedRoute from './features/auth/ProtectedRoute';

export default function Router() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
        <Route path="/dashboard/connections" element={<ProtectedRoute><ConnectionsPage /></ProtectedRoute>} />
        <Route path="/dashboard/graphs/new" element={<ProtectedRoute><GraphCreationPage /></ProtectedRoute>} />
        <Route path="*" element={<LandingPage />} />
      </Routes>
    </BrowserRouter>
  );
}
