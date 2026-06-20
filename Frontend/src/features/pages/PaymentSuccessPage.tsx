import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { CheckCircle2 } from 'lucide-react';
import Button from '../components/Button';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';

export default function PaymentSuccessPage() {
  const navigate = useNavigate();
  const { refreshSubscriptionStatus } = useAuth();
  const [timedOut, setTimedOut] = useState(false);

  useEffect(() => {
    let elapsed = 0;

    const interval = window.setInterval(async () => {
      elapsed += 2000;
      const isActive = await refreshSubscriptionStatus();

      if (isActive) {
        window.clearInterval(interval);
        navigate(ROUTES.DASHBOARD, { replace: true });
      }

      if (elapsed >= 30000) {
        window.clearInterval(interval);
        setTimedOut(true);
      }
    }, 2000);

    return () => window.clearInterval(interval);
  }, [navigate, refreshSubscriptionStatus]);

  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-4 text-on-background">
      <div className="w-full max-w-md rounded-xl border border-outline-variant bg-surface-container-lowest p-8 text-center">
        <CheckCircle2 className="mx-auto mb-5 size-12 text-secondary" aria-hidden="true" />
        <h1 className="text-headline-lg font-semibold">Payment received</h1>
        <p className="mt-3 text-body-md text-on-surface-variant">
          {timedOut
            ? 'We could not confirm your subscription yet. Contact support if your dashboard stays locked.'
            : 'We are confirming your subscription. You will be redirected automatically.'}
        </p>
        {timedOut && (
          <div className="mt-6 flex justify-center">
            <Link to={ROUTES.CONTACT}>
              <Button>Contact support</Button>
            </Link>
          </div>
        )}
      </div>
    </main>
  );
}
