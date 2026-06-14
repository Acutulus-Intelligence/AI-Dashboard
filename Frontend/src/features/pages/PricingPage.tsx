import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, ArrowRight } from 'lucide-react';
import Header from '../layouts/Header';
import Footer from '../layouts/Footer';
import Button from '../components/Button';
import BillingToggle from '../components/BillingToggle';
import PlanCards from '../components/PlanCards';
import { ROUTES } from '../routes';
import {
  BILLING_PERIOD,
  FREE_TRIAL_DAYS,
  USER_TYPE,
  getPlans,
  type BillingPeriod,
  type SubscriptionPlan,
} from '../../lib/api/subscription';

function getRegisterType(plan: SubscriptionPlan) {
  return plan.userType === USER_TYPE.Company || plan.userType === 'Company' ? 'company' : 'individual';
}

function isEnterprisePlan(plan: SubscriptionPlan) {
  return plan.name.toLowerCase().includes('enterprise');
}

export default function PricingPage() {
  const navigate = useNavigate();
  const [billing, setBilling] = useState<BillingPeriod>(BILLING_PERIOD.Monthly);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    async function loadPlans() {
      setError('');
      try {
        const response = await getPlans();
        setPlans(response.filter((plan) => plan.isActive));
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Could not load plans.');
      } finally {
        setLoading(false);
      }
    }

    void loadPlans();
  }, []);

  function handleChoosePlan(plan: SubscriptionPlan) {
    if (isEnterprisePlan(plan)) {
      navigate(ROUTES.CONTACT);
      return;
    }

    const params = new URLSearchParams({
      type: getRegisterType(plan),
      planId: plan.id,
      billing: String(billing),
    });

    navigate(`${ROUTES.REGISTER}?${params.toString()}`);
  }

  return (
    <div className="min-h-screen bg-background text-on-background">
      <Header />

      <main className="pt-16">
        <section className="bg-surface px-gutter pb-16 pt-16">
          <div className="mx-auto max-w-container-max">
            <div className="mx-auto mb-10 max-w-3xl text-center">
              <span className="mb-2 block font-mono text-label-caps uppercase text-outline">
                Payment plans
              </span>
              <h1 className="text-headline-lg font-semibold text-on-background md:text-display-lg">
                Choose the right plan for your dashboards.
              </h1>
              <p className="mt-4 text-body-lg text-on-surface-variant">
                Start with a {FREE_TRIAL_DAYS}-day free trial, then keep the plan that fits
                your workflow as your team or AI usage grows.
              </p>
            </div>

            <div className="mb-8 flex justify-center">
              <BillingToggle billing={billing} onChange={setBilling} />
            </div>

            {error && (
              <div className="mx-auto mb-8 flex max-w-2xl items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
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
                actionLabel="Get started"
                onSelect={handleChoosePlan}
              />
            )}

            <div className="mt-10 flex justify-center">
              <Button variant="outline" onClick={() => navigate(ROUTES.CONTACT)}>
                Talk to us
                <ArrowRight size={16} aria-hidden="true" />
              </Button>
            </div>
          </div>
        </section>

        <section className="bg-background px-gutter pb-24">
          <p className="text-center text-body-sm text-on-surface-variant">
            Already have an account?{' '}
            <Link to={ROUTES.LOGIN} className="font-semibold text-primary hover:underline">
              Sign in
            </Link>
          </p>
        </section>
      </main>

      <Footer />
    </div>
  );
}
