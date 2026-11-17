import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { toast } from 'sonner';
import PublicLayout from '../layouts/PublicLayout';
import PublicPageIntro from '../layouts/PublicPageIntro';
import { publicButtonClass, publicContainerClass, publicLinkClass, publicPageClass } from '../layouts/publicChrome';
import PageMeta from '../components/PageMeta';
import PublicError from '../components/PublicError';
import BillingToggle from '../components/BillingToggle';
import PlanCards from '../components/PlanCards';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';
import * as companyApi from '../../lib/api/company';
import {
  BILLING_PERIOD,
  USER_TYPE,
  createCheckout,
  createCompanyCheckout,
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

function trialDaysForPlan(plan: SubscriptionPlan): number | null {
  if (plan.trialDays === null) return 7;
  return plan.trialDays;
}

async function loadScreenshotPlans(): Promise<SubscriptionPlan[] | null> {
  // Vite replaces this flag and drops the import from production builds.
  if (import.meta.env.DEV) {
    try {
      const params = new URLSearchParams(window.location.search);
      const enabled = params.get('screenshot') === '1' || sessionStorage.getItem('screenshot-plans') === '1';
      if (!enabled) return null;
    } catch {
      return null;
    }

    const { screenshotPlans } = await import('../dev/screenshotPlans');
    return screenshotPlans;
  }

  return null;
}

export default function PricingPage() {
  const navigate = useNavigate();
  const { isAuthenticated, hasActiveSubscription, user } = useAuth();
  const [billing, setBilling] = useState<BillingPeriod>(BILLING_PERIOD.Monthly);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingPlanId, setLoadingPlanId] = useState<string | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    async function loadPlans() {
      setError('');
      const placeholders = await loadScreenshotPlans();
      if (placeholders) {
        setPlans(placeholders);
        setLoading(false);
        return;
      }

      try {
        const response = await getPlans();
        let active = response.filter((plan) => plan.isActive);
        if (isAuthenticated && hasActiveSubscription && user?.userType !== 1) {
          active = active.filter((plan) => getRegisterType(plan) !== 'individual');
        }
        active.sort((a, b) => {
          const aIsEnterprise = isEnterprisePlan(a) ? 1 : 0;
          const bIsEnterprise = isEnterprisePlan(b) ? 1 : 0;
          return aIsEnterprise - bIsEnterprise;
        });
        setPlans(active);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Could not load plans.');
      } finally {
        setLoading(false);
      }
    }

    void loadPlans();
    // Loaded once on mount, matching the previous pricing page.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleChoosePlan(plan: SubscriptionPlan) {
    if (isEnterprisePlan(plan)) {
      navigate(ROUTES.CONTACT);
      return;
    }

    if (user?.roles.includes('Admin') || user?.roles.includes('Moderator')) {
      toast.error('Admins and moderators do not need a subscription.');
      return;
    }

    if (!isAuthenticated) {
      navigate(ROUTES.REGISTER);
      return;
    }

    if (getRegisterType(plan) === 'company') {
      setLoadingPlanId(plan.id);
      setError('');

      try {
        const existing = await companyApi.getMyCompany();
        if (existing) {
          const checkout = await createCompanyCheckout(
            existing.id,
            plan.id,
            billing,
            `${window.location.origin}${ROUTES.PAYMENT_SUCCESS}`,
            `${window.location.origin}${ROUTES.PAYMENT_CANCEL}`,
          );
          window.location.href = checkout.checkoutUrl;
          return;
        }
      } catch {
        /* no existing company — proceed to create */
      }

      navigate(`${ROUTES.COMPANY_CREATE}?planId=${plan.id}&billing=${billing}`);
      return;
    }

    setLoadingPlanId(plan.id);
    setError('');

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

  const trialDays = plans.reduce((max, plan) => Math.max(max, trialDaysForPlan(plan) ?? 0), 0);

  return (
    <PublicLayout>
      <PageMeta
        title="Pricing — AI Dashboard"
        description="Compare AI Dashboard plans for individuals and companies. Start a free trial and choose monthly or yearly billing."
        path={ROUTES.PRICING}
      />

      <div className={publicPageClass}>
        <section className="py-16 md:py-24">
          <div className={publicContainerClass}>
            <PublicPageIntro eyebrow="Payment plans" title="Choose the right plan for your dashboards.">
              {trialDays > 0 ? (
                <>
                  Start with a {trialDays}-day free trial, then keep the plan that fits your workflow as
                  your team or AI usage grows.
                </>
              ) : null}
            </PublicPageIntro>

            <div className="mb-8 mt-10 flex justify-center">
              <BillingToggle billing={billing} onChange={setBilling} />
            </div>

            {error && (
              <div className="mx-auto mb-8 max-w-2xl">
                <PublicError>{error}</PublicError>
              </div>
            )}

            {loading ? (
              <div className="flex min-h-52 items-center justify-center">
                <div className="size-8 animate-spin rounded-full border-4 border-blue-700 border-t-transparent" />
              </div>
            ) : (
              <PlanCards
                billing={billing}
                plans={plans}
                loadingPlanId={loadingPlanId}
                onSelect={handleChoosePlan}
                getActionLabel={(plan) =>
                  isEnterprisePlan(plan) ? 'Contact Us' : 'Get started'
                }
              />
            )}

            <div className="mt-10 flex justify-center">
              <button
                type="button"
                className={publicButtonClass('secondary')}
                onClick={() => navigate(ROUTES.CONTACT)}
              >
                Talk to us
                <ArrowRight className="size-4" aria-hidden="true" />
              </button>
            </div>
          </div>
        </section>

        <section className="pb-20">
          <p className="text-center text-sm text-slate-600">
            Already have an account?{' '}
            <Link to={ROUTES.LOGIN} className={publicLinkClass}>
              Sign in
            </Link>
          </p>
        </section>
      </div>
    </PublicLayout>
  );
}
