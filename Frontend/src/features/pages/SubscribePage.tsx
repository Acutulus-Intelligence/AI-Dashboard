import { useEffect, useState } from 'react';
import { AlertCircle } from 'lucide-react';
import BillingToggle from '../components/BillingToggle';
import PlanCards from '../components/PlanCards';
import {
  BILLING_PERIOD,
  FREE_TRIAL_DAYS,
  USER_TYPE,
  createCheckout,
  getPlans,
  type BillingPeriod,
  type SubscriptionPlan,
} from '../../lib/api/subscription';
import { ROUTES } from '../routes';

export default function SubscribePage() {
  const [billing, setBilling] = useState<BillingPeriod>(BILLING_PERIOD.Monthly);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingPlanId, setLoadingPlanId] = useState<string | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    async function loadPlans() {
      setError('');
      try {
        const response = await getPlans(USER_TYPE.Individual);
        setPlans(response.filter((plan) => plan.isActive));
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Could not load plans.');
      } finally {
        setLoading(false);
      }
    }

    void loadPlans();
  }, []);

  async function handleSubscribe(plan: SubscriptionPlan) {
    setError('');
    setLoadingPlanId(plan.id);
    try {
      const checkout = await createCheckout({
        planId: plan.id,
        billingPeriod: billing,
        successUrl: `${window.location.origin}${ROUTES.PAYMENT_SUCCESS}`,
        cancelUrl: `${window.location.origin}${ROUTES.PAYMENT_CANCEL}`,
      });
      window.location.href = checkout.checkoutUrl;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start checkout.');
      setLoadingPlanId(null);
    }
  }

  return (
    <main className="min-h-screen bg-background px-gutter py-16 text-on-background">
      <div className="mx-auto max-w-container-max">
        <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
          <div>
            <span className="mb-2 block font-mono text-label-caps uppercase text-outline">
              Subscribe
            </span>
            <h1 className="text-headline-lg font-semibold">Choose a plan to unlock your dashboard</h1>
            <p className="mt-2 text-body-md text-on-surface-variant">
              Your account is ready. Pick a subscription to continue with a {FREE_TRIAL_DAYS}-day
              free trial.
            </p>
          </div>
          <BillingToggle billing={billing} onChange={setBilling} />
        </div>

        {error && (
          <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
            <AlertCircle size={16} className="shrink-0" aria-hidden="true" />
            <span>{error}</span>
          </div>
        )}

        {loading ? (
          <div className="flex min-h-52 items-center justify-center">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
          </div>
        ) : (
          <PlanCards
            billing={billing}
            plans={plans}
            actionLabel="Subscribe"
            loadingPlanId={loadingPlanId}
            onSelect={handleSubscribe}
          />
        )}
      </div>
    </main>
  );
}
