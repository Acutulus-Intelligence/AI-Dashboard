import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { AlertCircle, Building2 } from 'lucide-react';
import Button from '../components/Button';
import { useAuth } from '../store/useAuth';
import { createCompany } from '../../lib/api/company';
import { createCompanyCheckout } from '../../lib/api/subscription';
import { ROUTES } from '../routes';

export default function CompanyCreatePage() {
  const { hasActiveSubscription } = useAuth();
  const [searchParams] = useSearchParams();
  const planId = searchParams.get('planId');
  const billingParam = searchParams.get('billing');
  const billing = billingParam === '1' ? 1 : 0;

  const [companyName, setCompanyName] = useState('');
  const [loading, setLoading] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [error, setError] = useState('');

  async function handleCreateCompany(e: FormEvent) {
    e.preventDefault();
    setError('');

    if (!companyName.trim()) {
      setError('Please enter a company name.');
      return;
    }

    if (!planId) {
      setError('Missing plan information. Please go back and select a plan.');
      return;
    }

    setLoading(true);
    try {
      const response = await createCompany({ name: companyName.trim() });

      setRedirecting(true);
      const successUrl = hasActiveSubscription
        ? `${window.location.origin}${ROUTES.PAYMENT_SUCCESS}?upgrade=true`
        : `${window.location.origin}${ROUTES.PAYMENT_SUCCESS}`;
      const checkout = await createCompanyCheckout(
        response.id,
        planId,
        billing,
        successUrl,
        `${window.location.origin}${ROUTES.PAYMENT_CANCEL}`,
      );
      window.location.href = checkout.checkoutUrl;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create company.');
      setRedirecting(false);
    } finally {
      setLoading(false);
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
                Enter your company name to create the workspace. You'll be redirected to checkout afterwards.
              </p>
            </div>
          </div>

          {error && (
            <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
              <AlertCircle size={16} className="shrink-0" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}

          {redirecting ? (
            <div className="flex min-h-52 flex-col items-center justify-center gap-4">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
              <p className="text-body-md text-on-surface-variant">Redirecting to checkout...</p>
            </div>
          ) : (
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
          )}
        </div>
      </div>
    </main>
  );
}
