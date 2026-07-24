import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { CheckCircle2, RefreshCw } from 'lucide-react';
import Button from '../components/Button';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';
import { confirmCheckout } from '../../lib/api/subscription';

export default function PaymentSuccessPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { refreshSubscriptionStatus, logout } = useAuth();
  const [timedOut, setTimedOut] = useState(false);
  const [retryTrigger, setRetryTrigger] = useState(0);
  const pollingRef = useRef(true);
  const isUpgrade = searchParams.get('upgrade') === 'true';
  const sessionId = searchParams.get('session_id');

  useEffect(() => {
    pollingRef.current = true;

    let elapsed = 0;
    let interval: ReturnType<typeof setInterval> | undefined;

    (async () => {
      if (sessionId) {
        try {
          await confirmCheckout(sessionId);
          const isActive = await refreshSubscriptionStatus();
          if (isActive) {
            if (isUpgrade) {
              await logout();
            } else {
              navigate(ROUTES.DASHBOARD, { replace: true });
            }
            return;
          }
        } catch {
          /* confirm failed, fall through to polling */
        }
      }

      interval = window.setInterval(async () => {
        elapsed += 3000;
        const isActive = await refreshSubscriptionStatus();

        if (isActive) {
          window.clearInterval(interval);
          pollingRef.current = false;
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
    })();

    return () => {
      if (interval !== undefined) {
        window.clearInterval(interval);
      }
    };
  }, [navigate, refreshSubscriptionStatus, isUpgrade, sessionId, retryTrigger]);

  function handleRetry() {
    pollingRef.current = true;
    setTimedOut(false);
    setRetryTrigger((c) => c + 1);
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
