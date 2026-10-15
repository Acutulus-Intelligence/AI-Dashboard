import { Navigate } from 'react-router-dom';
import { useAuth } from '../store/useAuth';
import type { ReactNode } from 'react';
import { ROUTES } from '../routes';
import SubscriptionRequired from './SubscriptionRequired';

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
    const isStaff = user?.roles.includes('Admin') || user?.roles.includes('Moderator');
    if (isStaff) {
      return <Navigate to={ROUTES.ADMIN_MAIN} replace />;
    }
    if (user?.userType === 1) {
      return <Navigate to={ROUTES.ADMIN} replace />;
    }
    // A deactivated user must always be able to reach the reactivation banner,
    // which lives on the profile page; product features stay gated.
    if (user?.isDeactivated) {
      return <Navigate to={ROUTES.PROFILE} replace />;
    }
    // Stay in the app and show an upsell instead of ejecting to the public
    // pricing page, so profile/settings remain reachable.
    return <SubscriptionRequired />;
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
