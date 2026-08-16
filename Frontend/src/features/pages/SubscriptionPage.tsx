import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, Building2, CheckCircle2, CreditCard, XCircle } from 'lucide-react';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import AppShell from '../layouts/AppShell';
import { ROUTES } from '../routes';
import * as subscriptionApi from '../../lib/api/subscription';
import { usePolling } from '../../hooks/usePolling';

function formatDate(dateStr: string | null) {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

function statusLabel(status: number | string): { text: string; color: string } {
  if (status === 0 || status === 'Trial') return { text: 'Trial', color: 'text-blue-600 bg-blue-50' };
  if (status === 1 || status === 'Active') return { text: 'Active', color: 'text-green-600 bg-green-50' };
  if (status === 2 || status === 'Expired') return { text: 'Expired', color: 'text-red-600 bg-red-50' };
  return { text: 'Canceled', color: 'text-gray-600 bg-gray-100' };
}

export default function SubscriptionPage() {
  const [subscription, setSubscription] = useState<subscriptionApi.UserSubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [cancelling, setCancelling] = useState(false);
  const [error, setError] = useState('');
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);

  async function loadSubscription() {
    setError('');
    try {
      const sub = await subscriptionApi.getCurrentSubscription();
      setSubscription(sub);
    } catch {
      setSubscription(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadSubscription();
  }, []);

  usePolling({ onPoll: loadSubscription });

  async function handleCancel() {
    setCancelling(true);
    setError('');
    try {
      await subscriptionApi.cancel();
      await loadSubscription();
      setCancelConfirmOpen(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not cancel subscription.');
    } finally {
      setCancelling(false);
    }
  }

  const status = subscription ? statusLabel(subscription.status) : null;

  return (
    <AppShell breadcrumbs={[{ label: 'Subscription' }]}>
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My subscription</h1>
        <p className="text-muted-foreground text-sm">
          Review your plan, billing period and payment status.
        </p>
      </div>

          {error && (
            <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
              <AlertCircle size={16} className="shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {loading ? (
            <div className="flex min-h-52 items-center justify-center">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
            </div>
          ) : !subscription ? (
            <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-8 text-center">
              <CreditCard className="mx-auto mb-4 size-10 text-on-surface-variant" />
              <h2 className="text-headline-md font-semibold text-on-background">No active subscription</h2>
              <p className="mt-2 text-body-md text-on-surface-variant">
                Subscribe to a plan to unlock your dashboard.
              </p>
              <div className="mt-6 flex justify-center">
                <Link to={ROUTES.PRICING}>
                  <Button>View plans</Button>
                </Link>
              </div>
            </div>
          ) : (
            <div className="grid gap-6 lg:grid-cols-3">
              <div className="lg:col-span-2 space-y-6">
                <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-6">
                  <div className="mb-4 flex items-center justify-between">
                    <h2 className="text-headline-sm font-semibold text-on-background">Plan details</h2>
                    {status && (
                      <span className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-label-sm font-medium ${status.color}`}>
                        {status.text === 'Active' || status.text === 'Trial' ? (
                          <CheckCircle2 size={14} />
                        ) : (
                          <XCircle size={14} />
                        )}
                        {status.text}
                      </span>
                    )}
                  </div>

                  <dl className="space-y-4 text-body-md">
                    <div className="flex justify-between">
                      <dt className="text-on-surface-variant">Plan</dt>
                      <dd className="font-semibold text-on-background">{subscription.planName}</dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-on-surface-variant">Price</dt>
                      <dd className="font-semibold text-on-background">
                        ${subscription.price.toFixed(2)}/{subscription.billingPeriod === 0 || subscription.billingPeriod === 'Monthly' ? 'mo' : 'yr'}
                      </dd>
                    </div>
                    {subscription.nextPrice != null && (
                      <div className="flex items-center gap-2 rounded-xl border border-amber-300 bg-amber-50 px-4 py-3 text-body-sm text-amber-800">
                        <AlertCircle size={16} className="shrink-0" />
                        <span>
                          Your subscription price will change to ${subscription.nextPrice.toFixed(2)} at your next renewal{subscription.nextPriceEffectiveDate ? ` on ${formatDate(subscription.nextPriceEffectiveDate)}` : ''}.
                        </span>
                      </div>
                    )}
                    <div className="flex justify-between">
                      <dt className="text-on-surface-variant">Billing</dt>
                      <dd className="font-semibold text-on-background">
                        {subscription.billingPeriod === 0 || subscription.billingPeriod === 'Monthly' ? 'Monthly' : 'Yearly'}
                      </dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-on-surface-variant">Start date</dt>
                      <dd className="text-on-background">{formatDate(subscription.startDate)}</dd>
                    </div>
                    {subscription.endDate && (
                      <div className="flex justify-between">
                        <dt className="text-on-surface-variant">Renews</dt>
                        <dd className="text-on-background">{formatDate(subscription.endDate)}</dd>
                      </div>
                    )}
                    {subscription.trialEndDate && (
                      <div className="flex justify-between">
                        <dt className="text-on-surface-variant">Trial ends</dt>
                        <dd className="text-on-background">{formatDate(subscription.trialEndDate)}</dd>
                      </div>
                    )}
                  </dl>
                </div>
              </div>

              <div className="space-y-6">
                <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-6">
                  <div className="mb-4 flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Building2 size={22} />
                  </div>
                  <h3 className="text-body-lg font-semibold text-on-background">Upgrade to Company</h3>
                  <p className="mt-2 text-body-sm text-on-surface-variant">
                    Create a company workspace with team management, shared dashboards, and more.
                  </p>
                  <div className="mt-4">
                    <Link to={ROUTES.PRICING}>
                      <Button variant="outline" className="w-full">Create company</Button>
                    </Link>
                  </div>
                </div>

                {(subscription.status === 0 || subscription.status === 1 || subscription.status === 'Trial' || subscription.status === 'Active') && (
                  <div className="rounded-xl border border-red-200 bg-surface-container-lowest p-6">
                    <h3 className="text-body-lg font-semibold text-red-700">Cancel subscription</h3>
                    <p className="mt-2 text-body-sm text-on-surface-variant">
                      Your dashboard will be locked and your data will become inaccessible.
                    </p>
                    <div className="mt-4">
                      <Button
                        variant="outline"
                        className="w-full border-red-300 text-red-600 hover:bg-red-50"
                        disabled={cancelling}
                        onClick={() => setCancelConfirmOpen(true)}
                      >
                        {cancelling ? 'Cancelling...' : 'Cancel subscription'}
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

      <ConfirmDialog
        open={cancelConfirmOpen}
        onOpenChange={(open) => {
          if (!cancelling) setCancelConfirmOpen(open);
        }}
        title="Cancel subscription?"
        description="Your dashboard access will be revoked. You can resubscribe later."
        confirmLabel="Cancel subscription"
        variant="destructive"
        loading={cancelling}
        onConfirm={handleCancel}
      />
    </AppShell>
  );
}
