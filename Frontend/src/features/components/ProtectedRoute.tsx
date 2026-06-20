import { Navigate } from 'react-router-dom';
import { useAuth } from '../store/useAuth';
import type { ReactNode } from 'react';
import { ROUTES } from '../routes';

interface ProtectedRouteProps {
  children: ReactNode;
  requireSubscription?: boolean;
}

export default function ProtectedRoute({ children, requireSubscription = true }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, hasActiveSubscription, isSubscriptionLoading } = useAuth();

  if (isLoading || isSubscriptionLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.LOGIN} replace />;
  }

  if (requireSubscription && !hasActiveSubscription) {
    return <Navigate to={ROUTES.SUBSCRIBE} replace />;
  }

  return <>{children}</>;
}
