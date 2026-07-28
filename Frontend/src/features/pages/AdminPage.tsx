import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Users, Building2, Database, AlertCircle, CheckCircle2, XCircle, CreditCard, Trash2, Crown, X, Palette } from 'lucide-react';
import AppShell from '../layouts/AppShell';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import { ROUTES } from '../routes';
import * as companyApi from '../../lib/api/company';
import * as subscriptionApi from '../../lib/api/subscription';
import { useAuth } from '../store/useAuth';

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
  const { user } = useAuth();
  const [company, setCompany] = useState<companyApi.CompanyResponse | null>(null);
  const [companySub, setCompanySub] = useState<subscriptionApi.CompanySubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [cancelling, setCancelling] = useState(false);
  const [deletingCompany, setDeletingCompany] = useState(false);
  const [error, setError] = useState('');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deletePassword, setDeletePassword] = useState('');
  const [users, setUsers] = useState<companyApi.CompanyUserResponse[]>([]);
  const [showTransferOwner, setShowTransferOwner] = useState(false);
  const [transferUserId, setTransferUserId] = useState('');
  const [transferPassword, setTransferPassword] = useState('');
  const [transferring, setTransferring] = useState(false);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);

  async function loadData() {
    setError('');
    try {
      const c = await companyApi.getMyCompany();
      setCompany(c);

      try {
        const userList = await companyApi.getCompanyUsers(c.id);
        setUsers(userList);
      } catch {
        setUsers([]);
      }

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

    setCancelling(true);
    setError('');
    try {
      await subscriptionApi.cancelCompanySubscription(company.id);
      await loadData();
      setCancelConfirmOpen(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not cancel subscription.');
    } finally {
      setCancelling(false);
    }
  }

  async function executeDeleteCompany() {
    if (!company) return;

    setError('');
    setDeletingCompany(true);
    try {
      await companyApi.deleteCompany(company.id, deletePassword);
      navigate(ROUTES.DASHBOARD);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete company.');
      setDeletingCompany(false);
    }
  }

  async function executeTransferOwner() {
    if (!company || !transferUserId) return;

    setError('');
    setTransferring(true);
    try {
      await companyApi.transferOwnership(company.id, transferUserId, transferPassword);
      setShowTransferOwner(false);
      setTransferUserId('');
      setTransferPassword('');
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to transfer ownership.');
      setTransferring(false);
    }
  }

  const status = companySub ? statusLabel(companySub.status) : null;
  const isOwner = company && user?.userId === company.ownerId;
  const eligibleUsers = users.filter((u) => !u.isOwner);

  return (
    <AppShell breadcrumbs={[{ label: 'Administration' }]}>
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Administration</h1>
        <p className="text-muted-foreground text-sm">
          Manage your company workspace, plan and members.
        </p>
      </div>

          {error && (
            <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
              <AlertCircle size={16} className="shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {companySub && companySub.status !== 0 && companySub.status !== 1 && (
            <div className="mb-6 flex items-center justify-between gap-4 rounded-xl border border-amber-300 bg-amber-50 px-5 py-4 text-body-sm text-amber-800">
              <div className="flex items-center gap-2">
                <AlertCircle size={16} className="shrink-0" />
                <span>Your subscription has been cancelled. <span className="font-medium">Resubscribe</span> to regain dashboard access.</span>
              </div>
              <Button
                variant="outline"
                className="shrink-0 border-amber-300 text-amber-800 hover:bg-amber-100"
                onClick={(e) => { e.preventDefault(); navigate(ROUTES.PRICING); }}
              >
                <CreditCard size={14} className="mr-1" />
                Subscribe
              </Button>
            </div>
          )}

          {loading ? (
            <div className="flex min-h-52 items-center justify-center">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
            </div>
          ) : (
            <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
              <Link
                to={ROUTES.ADMIN_USERS}
                className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs transition-shadow hover:shadow-md"
              >
                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <Users size={24} />
                </div>
                <h2 className="mb-2 text-body-lg font-semibold text-on-background">Team Members</h2>
                <p className="text-body-sm text-on-surface-variant">Manage users, roles, and permissions for your company.</p>
              </Link>

              {isOwner && (
              <Link
                to={ROUTES.CONNECTIONS}
                className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs transition-shadow hover:shadow-md"
              >
                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <Database size={24} />
                </div>
                <h2 className="mb-2 text-body-lg font-semibold text-on-background">Connections</h2>
                <p className="text-body-sm text-on-surface-variant">Connect external databases to generate AI-powered charts.</p>
              </Link>
              )}

              {isOwner && (
              <Link
                to={ROUTES.ADMIN_STYLE}
                className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs transition-shadow hover:shadow-md"
              >
                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <Palette size={24} />
                </div>
                <h2 className="mb-2 text-body-lg font-semibold text-on-background">Style</h2>
                <p className="text-body-sm text-on-surface-variant">Set your company look — colours, appearance and how charts present.</p>
              </Link>
              )}

              {company ? (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs transition-shadow hover:shadow-md">
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
                          Renews {formatDate(companySub.endDate)}
                        </p>
                      )}
                      {isOwner && (companySub.status === 0 || companySub.status === 1) && (
                        <Button
                          variant="outline"
                          className="mt-3 w-full border-red-300 text-red-600 hover:bg-red-50"
                          disabled={cancelling}
                          onClick={(e) => { e.preventDefault(); setCancelConfirmOpen(true); }}
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

                  {isOwner && (
                    <>
                      <hr className="my-4 border-outline-variant/50" />

                      {/* Transfer Ownership */}
                      {!showTransferOwner ? (
                        eligibleUsers.length === 0 ? (
                          <div className="mb-3 flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50/50 p-3 text-body-sm text-amber-700">
                            <Crown size={14} className="shrink-0" />
                            <span>No other team members to transfer to. Invite users first.</span>
                          </div>
                        ) : (
                          <Button
                            variant="outline"
                            className="mb-3 w-full border-amber-300 text-amber-700 hover:bg-amber-50"
                            onClick={(e) => { e.preventDefault(); setShowTransferOwner(true); setTransferUserId(''); setTransferPassword(''); }}
                          >
                            <Crown size={14} />
                            Transfer Ownership
                          </Button>
                        )
                      ) : (
                        <div className="mb-3 space-y-3 rounded-xl border border-amber-200 bg-amber-50/50 p-4">
                          <div className="flex items-center justify-between">
                            <h4 className="text-body-sm font-semibold text-amber-800">Transfer Ownership</h4>
                            <button
                              type="button"
                              onClick={() => { setShowTransferOwner(false); setTransferUserId(''); setTransferPassword(''); }}
                              className="rounded-lg p-1 text-amber-600 hover:bg-amber-100"
                            >
                              <X size={16} />
                            </button>
                          </div>
                          <select
                            value={transferUserId}
                            onChange={(e) => setTransferUserId(e.target.value)}
                            className="w-full rounded-xl border border-amber-300 bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background focus:border-amber-500 focus:outline-hidden focus:ring-2 focus:ring-amber-500/20"
                          >
                            <option value="">Select a team member</option>
                            {users
                              .filter((u) => !u.isOwner)
                              .map((u) => (
                                <option key={u.id} value={u.id}>
                                  {u.firstName && u.lastName ? `${u.firstName} ${u.lastName}` : u.email}
                                </option>
                              ))}
                          </select>
                          {transferUserId && (
                            <>
                              <div>
                                <label htmlFor="transferPassword" className="mb-1 block text-body-xs font-medium text-amber-700">
                                  Enter your password to confirm
                                </label>
                                  <input
                                    id="transferPassword"
                                    type="password"
                                    value={transferPassword}
                                    onChange={(e) => setTransferPassword(e.target.value)}
                                    placeholder="Enter your password"
                                    className="w-full rounded-xl border border-amber-300 bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-amber-500 focus:outline-hidden focus:ring-2 focus:ring-amber-500/20"
                                  />
                              </div>
                              <div className="flex gap-3">
                                <Button
                                  variant="outline"
                                  className="flex-1"
                                  disabled={transferring}
                                  onClick={() => { setShowTransferOwner(false); setTransferUserId(''); setTransferPassword(''); }}
                                >
                                  Cancel
                                </Button>
                                <Button
                                  variant="outline"
                                  className="flex-1 border-amber-300 text-amber-700 hover:bg-amber-100"
                                  disabled={!transferPassword || transferring}
                                  onClick={(e) => { e.preventDefault(); void executeTransferOwner(); }}
                                >
                                  {transferring ? 'Transferring...' : 'Transfer ownership'}
                                </Button>
                              </div>
                            </>
                          )}
                        </div>
                      )}

                      {/* Delete Company */}
                      {!showDeleteConfirm ? (
                        <Button
                          variant="outline"
                          className="w-full border-red-300 text-red-600 hover:bg-red-50"
                          onClick={(e) => { e.preventDefault(); setShowDeleteConfirm(true); }}
                        >
                          <Trash2 size={14} />
                          Delete Company
                        </Button>
                      ) : (
                        <div className="space-y-3">
                          <div className="rounded-xl border border-red-300 bg-red-100/50 px-4 py-3 text-body-sm text-red-700">
                            <p className="mb-2 font-medium">This action is permanent.</p>
                            <p>
                              {company?.name} and all associated data including dashboards, charts, and team member
                              access will be permanently removed. This cannot be undone.
                            </p>
                          </div>

                          <div>
                            <label htmlFor="deleteCompanyPassword" className="mb-1 block text-body-xs font-medium text-red-700">
                              Enter your password to confirm
                            </label>
                            <input
                              id="deleteCompanyPassword"
                              type="password"
                              value={deletePassword}
                              onChange={(e) => setDeletePassword(e.target.value)}
                              placeholder="Enter your password"
                              className="w-full rounded-xl border border-red-300 bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-red-500 focus:outline-hidden focus:ring-2 focus:ring-red-500/20"
                            />
                          </div>

                          {error && (
                            <p className="flex items-center gap-1.5 text-body-xs text-red-700">
                              <AlertCircle size={12} />
                              {error}
                            </p>
                          )}

                          <div className="flex gap-3">
                            <Button
                              variant="outline"
                              className="flex-1"
                              disabled={deletingCompany}
                              onClick={(e) => { e.preventDefault(); setShowDeleteConfirm(false); setDeletePassword(''); setError(''); }}
                            >
                              Cancel
                            </Button>
                            <Button
                              variant="outline"
                              className="flex-1 border-red-300 text-red-600 hover:bg-red-100"
                              disabled={!deletePassword || deletingCompany}
                              onClick={(e) => { e.preventDefault(); void executeDeleteCompany(); }}
                            >
                              {deletingCompany ? 'Deleting...' : 'Delete company'}
                            </Button>
                          </div>
                        </div>
                      )}
                    </>
                  )}
                </div>
              ) : (
                <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs">
                  <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Building2 size={24} />
                  </div>
                  <h2 className="mb-2 text-body-lg font-semibold text-on-background">Company</h2>
                  <p className="text-body-sm text-on-surface-variant">You are not part of a company workspace.</p>
                </div>
              )}
            </div>
          )}

      <ConfirmDialog
        open={cancelConfirmOpen}
        onOpenChange={(open) => {
          if (!cancelling) setCancelConfirmOpen(open);
        }}
        title="Cancel company subscription?"
        description="All team members will lose dashboard access. You can resubscribe later."
        confirmLabel="Cancel subscription"
        variant="destructive"
        loading={cancelling}
        onConfirm={handleCancel}
      />
    </AppShell>
  );
}