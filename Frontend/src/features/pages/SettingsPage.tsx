import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, ArrowLeft, Building2, CheckCircle2, CreditCard, Database, Mailbox, User, XCircle } from 'lucide-react';
import Button from '../components/Button';
import DashboardHeader from '../layouts/DashboardHeader';
import { ROUTES } from '../routes';
import * as subscriptionApi from '../../lib/api/subscription';
import * as companyApi from '../../lib/api/company';
import { useAuth } from '../store/useAuth';

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

export default function SettingsPage() {
  const { user, refreshUser } = useAuth();
  const navigate = useNavigate();
  const [subscription, setSubscription] = useState<subscriptionApi.UserSubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [cancelling, setCancelling] = useState(false);
  const [error, setError] = useState('');
  const [invites, setInvites] = useState<companyApi.CompanyInviteResponse[]>([]);
  const [invitesLoading, setInvitesLoading] = useState(true);
  const [acceptingId, setAcceptingId] = useState<string | null>(null);
  const [rejectingId, setRejectingId] = useState<string | null>(null);

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

  async function loadInvites() {
    try {
      const data = await companyApi.getPendingInvites();
      setInvites(data);
    } catch {
      setInvites([]);
    } finally {
      setInvitesLoading(false);
    }
  }

  async function handleAccept(inviteId: string) {
    setError('');
    setAcceptingId(inviteId);
    try {
      await companyApi.acceptInvite({ inviteId });
      await refreshUser();
      navigate(ROUTES.DASHBOARD);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to accept invite.');
      setAcceptingId(null);
    }
  }

  async function handleReject(inviteId: string) {
    setError('');
    setRejectingId(inviteId);
    try {
      await companyApi.rejectInvite(inviteId);
      setInvites((prev) => prev.filter((i) => i.id !== inviteId));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reject invite.');
    } finally {
      setRejectingId(null);
    }
  }

  useEffect(() => {
    void loadSubscription();
  }, []);

  useEffect(() => {
    if (user?.userType === 0) {
      void loadInvites();
    }
  }, [user?.userType]);

  async function handleCancel() {
    if (!window.confirm('Are you sure you want to cancel your subscription? Your dashboard access will be revoked.')) return;

    setCancelling(true);
    setError('');
    try {
      await subscriptionApi.cancel();
      await loadSubscription();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not cancel subscription.');
    } finally {
      setCancelling(false);
    }
  }

  const status = subscription ? statusLabel(subscription.status) : null;

  return (
    <div className="min-h-screen bg-background">
      <DashboardHeader
        onToggleNavbar={() => {}}
        onNewChart={() => {}}
        onNewDashboard={() => {}}
      />
      <main className="pt-16">
        <div className="mx-auto max-w-container-max px-gutter py-8">
          <div className="mb-6 flex items-center gap-3 border-b border-outline-variant/40 pb-3">
            <Link
              to={ROUTES.DASHBOARD}
              className="inline-flex items-center justify-center rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container"
            >
              <ArrowLeft size={18} />
            </Link>
            <h1 className="text-headline-md font-bold text-on-background">Settings</h1>
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
          ) : (
            <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
              {/* Profile */}
              <Link
                to={ROUTES.PROFILE}
                className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md"
              >
                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <User size={24} />
                </div>
                <h2 className="mb-2 text-body-lg font-semibold text-on-background">Profile</h2>
                <p className="text-body-sm text-on-surface-variant">
                  Update your name, email, password, and manage your account.
                </p>
              </Link>

              {subscription ? (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
                  <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <CreditCard size={24} />
                  </div>
                  <h2 className="mb-2 text-body-lg font-semibold text-on-background">
                    {subscription.planName}
                  </h2>

                  <div className="space-y-3 text-body-sm">
                    <div className="flex items-center gap-2">
                      {status && (status.text === 'Active' || status.text === 'Trial') ? (
                        <CheckCircle2 size={14} className="text-green-600" />
                      ) : (
                        <XCircle size={14} className="text-gray-500" />
                      )}
                      <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-label-xs font-medium ${status?.color}`}>
                        {status?.text}
                      </span>
                    </div>
                    <p className="text-on-surface-variant">
                      ${subscription.price.toFixed(2)}/{subscription.billingPeriod === 0 || subscription.billingPeriod === 'Monthly' ? 'mo' : 'yr'}
                    </p>
                    <p className="text-on-surface-variant">
                      Started {formatDate(subscription.startDate)}
                    </p>
                    {subscription.endDate && (
                      <p className="text-on-surface-variant">
                        Renews {formatDate(subscription.endDate)}
                      </p>
                    )}
                    {subscription.trialEndDate && (
                      <p className="text-on-surface-variant">
                        Trial ends {formatDate(subscription.trialEndDate)}
                      </p>
                    )}
                    {(subscription.status === 0 || subscription.status === 1 || subscription.status === 'Trial' || subscription.status === 'Active') && (
                      <Button
                        variant="outline"
                        className="mt-3 w-full border-red-300 text-red-600 hover:bg-red-50"
                        disabled={cancelling}
                        onClick={(e) => { e.preventDefault(); void handleCancel(); }}
                      >
                        {cancelling ? 'Cancelling...' : 'Cancel subscription'}
                      </Button>
                    )}
                  </div>
                </div>
              ) : (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
                  <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <CreditCard size={24} />
                  </div>
                  <h2 className="mb-2 text-body-lg font-semibold text-on-background">Subscription</h2>
                  <p className="text-body-sm text-on-surface-variant">No active subscription.</p>
                  <div className="mt-4">
                    <Link to={ROUTES.PRICING}>
                      <Button variant="outline" className="w-full">
                        <CreditCard size={14} className="mr-1" />
                        View plans
                      </Button>
                    </Link>
                  </div>
                </div>
              )}

              <Link
                to={ROUTES.CONNECTIONS}
                className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md"
              >
                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <Database size={24} />
                </div>
                <h2 className="mb-2 text-body-lg font-semibold text-on-background">Connections</h2>
                <p className="text-body-sm text-on-surface-variant">
                  Connect external databases to generate AI-powered charts.
                </p>
              </Link>

              {subscription && user?.userType !== 1 && (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
                  <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Building2 size={24} />
                  </div>
                  <h2 className="mb-2 text-body-lg font-semibold text-on-background">Upgrade to Company</h2>
                  <p className="text-body-sm text-on-surface-variant">
                    Create a company workspace with team management, shared dashboards, and more.
                  </p>
                  <div className="mt-4">
                    <Link to={ROUTES.PRICING}>
                      <Button variant="outline" className="w-full">Create company</Button>
                    </Link>
                  </div>
                </div>
              )}

              {user?.userType === 0 && (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
                  <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Mailbox size={24} />
                  </div>
                  <h2 className="mb-2 text-body-lg font-semibold text-on-background">Invites</h2>

                  {invitesLoading ? (
                    <div className="flex justify-center py-4">
                      <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
                    </div>
                  ) : invites.length === 0 ? (
                    <p className="text-body-sm text-on-surface-variant">No pending invites.</p>
                  ) : (
                    <div className="space-y-3">
                      {invites.map((invite) => (
                        <div key={invite.id} className="rounded-xl border border-outline-variant/60 bg-surface-container-lowest p-3">
                          <div className="mb-1 flex items-center justify-between">
                            <span className="text-body-sm font-medium text-on-background">{invite.companyName}</span>
                            <span className="text-label-xs text-on-surface-variant">
                              {new Date(invite.expiresAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
                            </span>
                          </div>
                          <p className="text-body-xs text-on-surface-variant">Role: {invite.roleName ?? 'Member'}</p>
                          <div className="mt-2 flex gap-2">
                            <Button
                              variant="outline"
                              className="flex-1 border-primary/30 text-primary hover:bg-primary/5"
                              disabled={acceptingId === invite.id || rejectingId === invite.id}
                              onClick={(e) => { e.preventDefault(); void handleAccept(invite.id); }}
                            >
                              {acceptingId === invite.id ? 'Accepting...' : 'Accept'}
                            </Button>
                            <Button
                              variant="outline"
                              className="flex-1 border-red-300 text-red-600 hover:bg-red-50"
                              disabled={acceptingId === invite.id || rejectingId === invite.id}
                              onClick={(e) => { e.preventDefault(); void handleReject(invite.id); }}
                            >
                              {rejectingId === invite.id ? 'Rejecting...' : 'Reject'}
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}