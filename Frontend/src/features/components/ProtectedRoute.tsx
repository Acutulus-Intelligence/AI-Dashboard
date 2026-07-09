import { Navigate } from 'react-router-dom';
import { useAuth } from '../store/useAuth';
import type { ReactNode } from 'react';
import { ROUTES } from '../routes';

interface ProtectedRouteProps {
  children: ReactNode;
  requireSubscription?: boolean;
}

export default function ProtectedRoute({ children, requireSubscription = true }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, hasActiveSubscription, isSubscriptionLoading, user } = useAuth();

  if (!isLoading && !isSubscriptionLoading && !isAuthenticated) {
    return <Navigate to={ROUTES.LOGIN} replace />;
  }

  if (!isLoading && !isSubscriptionLoading && requireSubscription && !hasActiveSubscription) {
    const target = user?.userType === 1 ? ROUTES.ADMIN : ROUTES.PRICING;
    return <Navigate to={target} replace />;
  }

  return (
    <div className="relative">
      {(isLoading || isSubscriptionLoading) && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-background/80">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      )}
      {children}
    </div>
  );
}
