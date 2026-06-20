import { useEffect, useState, type FormEvent } from 'react';
import { AlertCircle, Building2 } from 'lucide-react';
import Button from '../components/Button';
import BillingToggle from '../components/BillingToggle';
import PlanCards from '../components/PlanCards';
import { createCompany, type CompanyResponse } from '../../lib/api/company';
import {
  BILLING_PERIOD,
  FREE_TRIAL_DAYS,
  USER_TYPE,
  createCompanyCheckout,
  getPlans,
  type BillingPeriod,
  type SubscriptionPlan,
} from '../../lib/api/subscription';
import { ROUTES } from '../routes';

export default function CompanyCreatePage() {
  const [companyName, setCompanyName] = useState('');
  const [company, setCompany] = useState<CompanyResponse | null>(null);
  const [billing, setBilling] = useState<BillingPeriod>(BILLING_PERIOD.Monthly);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [loading, setLoading] = useState(false);
  const [plansLoading, setPlansLoading] = useState(false);
  const [loadingPlanId, setLoadingPlanId] = useState<string | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    async function loadPlans() {
      if (!company) return;

      setPlansLoading(true);
      setError('');
      try {
        const response = await getPlans(USER_TYPE.Company);
        setPlans(response.filter((plan) => plan.isActive));
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Could not load company plans.');
      } finally {
        setPlansLoading(false);
      }
    }

    void loadPlans();
  }, [company]);

  async function handleCreateCompany(e: FormEvent) {
    e.preventDefault();
    setError('');

    if (!companyName.trim()) {
      setError('Please enter a company name.');
      return;
    }

    setLoading(true);
    try {
      const response = await createCompany({ name: companyName.trim() });
      setCompany(response);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create company.');
    } finally {
      setLoading(false);
    }
  }

  async function handleCheckout(plan: SubscriptionPlan) {
    if (!company) return;

    setError('');
    setLoadingPlanId(plan.id);
    try {
      const checkout = await createCompanyCheckout(
        company.id,
        plan.id,
        billing,
        `${window.location.origin}${ROUTES.PAYMENT_SUCCESS}`,
        `${window.location.origin}${ROUTES.PAYMENT_CANCEL}`,
      );
      window.location.href = checkout.checkoutUrl;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start checkout.');
      setLoadingPlanId(null);
    }
  }

  return (
    <main className="min-h-screen bg-background px-gutter py-16 text-on-background">
      <div className="mx-auto max-w-4xl">
        <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-8">
          <div className="mb-6 flex items-center gap-3">
            <Building2 className="size-7 text-secondary" aria-hidden="true" />
            <div>
              <h1 className="text-headline-lg font-semibold">Create company workspace</h1>
              <p className="text-body-md text-on-surface-variant">
                Add your company, then choose the subscription for the workspace. The first
                {FREE_TRIAL_DAYS} days are free.
              </p>
            </div>
          </div>

          {error && (
            <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
              <AlertCircle size={16} className="shrink-0" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}

          {!company ? (
            <form onSubmit={handleCreateCompany} className="space-y-5">
              <div>
                <label htmlFor="companyName" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                  Company name
                </label>
                <input
                  id="companyName"
                  type="text"
                  value={companyName}
                  onChange={(e) => setCompanyName(e.target.value)}
                  placeholder="Acme Inc."
                  className="w-full rounded-xl border border-outline-variant bg-surface px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                />
              </div>
              <Button type="submit" disabled={loading}>
                {loading ? 'Creating...' : 'Create company'}
              </Button>
            </form>
          ) : (
            <div>
              <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
                <div>
                  <p className="font-mono text-label-caps uppercase text-outline">Company created</p>
                  <h2 className="mt-1 text-headline-md font-semibold">{company.name}</h2>
                </div>
                <BillingToggle billing={billing} onChange={setBilling} />
              </div>

              {plansLoading ? (
                <div className="flex min-h-52 items-center justify-center">
                  <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
                </div>
              ) : (
                <PlanCards
                  billing={billing}
                  plans={plans}
                  actionLabel="Choose company plan"
                  loadingPlanId={loadingPlanId}
                  onSelect={handleCheckout}
                />
              )}
            </div>
          )}
        </div>
      </div>
    </main>
  );
}
