import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { CheckCircle2, RefreshCw } from 'lucide-react';
import Button from '../components/Button';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';

export default function PaymentSuccessPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { refreshSubscriptionStatus, logout } = useAuth();
  const [timedOut, setTimedOut] = useState(false);
  const pollingRef = useRef(true);
  const isUpgrade = searchParams.get('upgrade') === 'true';

  useEffect(() => {
    if (!pollingRef.current) return;

    let elapsed = 0;

    const interval = window.setInterval(async () => {
      elapsed += 2000;
      const isActive = await refreshSubscriptionStatus();

      if (isActive) {
        window.clearInterval(interval);
        if (isUpgrade) {
          await logout();
        } else {
          navigate(ROUTES.DASHBOARD, { replace: true });
        }
      }

      if (elapsed >= 30000) {
        window.clearInterval(interval);
        pollingRef.current = false;
        setTimedOut(true);
      }
    }, 3000);

    return () => window.clearInterval(interval);
  }, [navigate, refreshSubscriptionStatus]);

  function handleRetry() {
    pollingRef.current = true;
    setTimedOut(false);
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-4 text-on-background">
      <div className="w-full max-w-md rounded-xl border border-outline-variant bg-surface-container-lowest p-8 text-center">
        <CheckCircle2 className="mx-auto mb-5 size-12 text-secondary" aria-hidden="true" />
        <h1 className="text-headline-lg font-semibold">Payment received</h1>
        <p className="mt-3 text-body-md text-on-surface-variant">
          {timedOut
            ? 'We could not confirm your subscription yet. Try again or contact support.'
            : 'We are confirming your subscription. You will be redirected automatically.'}
        </p>
        {timedOut && (
          <div className="mt-6 flex flex-col items-center gap-3">
            <Button onClick={handleRetry}>
              <RefreshCw className="mr-2 size-4" aria-hidden="true" />
              Try again
            </Button>
            <Link to={ROUTES.CONTACT} className="text-body-sm text-primary underline">
              Contact support
            </Link>
          </div>
        )}
      </div>
    </main>
  );
}
