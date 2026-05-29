import { BrowserRouter, Routes, Route } from 'react-router-dom';
import LandingPage from './features/pages/LandingPage';
import DashboardPage from './features/pages/DashboardPage';

export default function Router() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="*" element={<LandingPage />} />
      </Routes>
    </BrowserRouter>
  );
}
