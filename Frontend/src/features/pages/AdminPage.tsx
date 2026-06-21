import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Users, Shield, Building2, AlertCircle, CheckCircle2, XCircle, CreditCard } from 'lucide-react';
import DashboardHeader from '../layouts/DashboardHeader';
import Button from '../components/Button';
import { ROUTES } from '../routes';
import * as companyApi from '../../lib/api/company';
import * as subscriptionApi from '../../lib/api/subscription';

function statusLabel(status: number): { text: string; color: string } {
  if (status === 0) return { text: 'Trial', color: 'text-blue-600 bg-blue-50' };
  if (status === 1) return { text: 'Active', color: 'text-green-600 bg-green-50' };
  if (status === 2) return { text: 'Expired', color: 'text-red-600 bg-red-50' };
  return { text: 'Canceled', color: 'text-gray-600 bg-gray-100' };
}

function formatDate(dateStr: string | null) {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

export default function AdminPage() {
  const navigate = useNavigate();
  const [company, setCompany] = useState<companyApi.CompanyResponse | null>(null);
  const [companySub, setCompanySub] = useState<subscriptionApi.CompanySubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [cancelling, setCancelling] = useState(false);
  const [error, setError] = useState('');

  async function loadData() {
    setError('');
    try {
      const c = await companyApi.getMyCompany();
      setCompany(c);

      try {
        const sub = await subscriptionApi.getCompanySubscription(c.id);
        setCompanySub(sub);
      } catch {
        setCompanySub(null);
      }
    } catch {
      setCompany(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadData();
  }, []);

  async function handleCancel() {
    if (!company || !companySub) return;
    if (!window.confirm('Are you sure you want to cancel the company subscription? All team members will lose dashboard access.')) return;

    setCancelling(true);
    setError('');
    try {
      await subscriptionApi.cancelCompanySubscription(company.id);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not cancel subscription.');
    } finally {
      setCancelling(false);
    }
  }

  const status = companySub ? statusLabel(companySub.status) : null;

  return (
    <div className="min-h-screen bg-background">
      <DashboardHeader
        onToggleNavbar={() => console.log('sidebar toggle')}
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
            <h1 className="text-headline-md font-bold text-on-background">Administrator</h1>
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
              <Link
                to="/admin/users"
                className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md"
              >
                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <Users size={24} />
                </div>
                <h2 className="mb-2 text-body-lg font-semibold text-on-background">Team Members</h2>
                <p className="text-body-sm text-on-surface-variant">Manage users, roles, and permissions for your company.</p>
              </Link>

              <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <Shield size={24} />
                </div>
                <h2 className="mb-2 text-body-lg font-semibold text-on-background">Security</h2>
                <p className="text-body-sm text-on-surface-variant">Manage your account security and authentication settings.</p>
              </div>

              {company ? (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
                  <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Building2 size={24} />
                  </div>
                  <h2 className="mb-2 text-body-lg font-semibold text-on-background">{company.name}</h2>

                  {companySub && status ? (
                    <div className="space-y-3 text-body-sm">
                      <div className="flex items-center gap-2">
                        {status.text === 'Active' || status.text === 'Trial' ? (
                          <CheckCircle2 size={14} className="text-green-600" />
                        ) : (
                          <XCircle size={14} className="text-gray-500" />
                        )}
                        <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-label-xs font-medium ${status.color}`}>
                          {status.text}
                        </span>
                      </div>
                      <p className="text-on-surface-variant">
                        {companySub.planName} &middot; ${companySub.price.toFixed(2)}/{companySub.billingPeriod === 0 ? 'mo' : 'yr'}
                      </p>
                      <p className="text-on-surface-variant">
                        Started {formatDate(companySub.startDate)}
                      </p>
                      {companySub.endDate && (
                        <p className="text-on-surface-variant">
                          Ends {formatDate(companySub.endDate)}
                        </p>
                      )}
                      {(companySub.status === 0 || companySub.status === 1) && (
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
                  ) : (
                    <div className="space-y-3">
                      <p className="text-body-sm text-on-surface-variant">No active subscription for this company.</p>
                      <Button
                        variant="outline"
                        className="w-full"
                        onClick={(e) => { e.preventDefault(); navigate(ROUTES.PRICING); }}
                      >
                        <CreditCard size={14} className="mr-1" />
                        Subscribe
                      </Button>
                    </div>
                  )}
                </div>
              ) : (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm">
                  <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Building2 size={24} />
                  </div>
                  <h2 className="mb-2 text-body-lg font-semibold text-on-background">Company</h2>
                  <p className="text-body-sm text-on-surface-variant">You are not part of a company workspace.</p>
                </div>
              )}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
