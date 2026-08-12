import { Navigate } from 'react-router-dom';
import { useAuth } from '../store/useAuth';
import type { ReactNode } from 'react';
import { ROUTES } from '../routes';

interface AdminRouteProps {
  children: ReactNode;
}

export default function AdminRoute({ children }: AdminRouteProps) {
  const { isAuthenticated, isLoading, user } = useAuth();

  const isStaff =
    user?.roles.includes('Admin') || user?.roles.includes('Moderator');

  if (!isLoading && !isAuthenticated) {
    return <Navigate to={ROUTES.LOGIN} replace />;
  }

  if (!isLoading && isAuthenticated && !isStaff) {
    return <Navigate to={ROUTES.DASHBOARD} replace />;
  }

  return (
    <div className="relative">
      {isLoading && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-background/80">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      )}
      {children}
    </div>
  );
}
