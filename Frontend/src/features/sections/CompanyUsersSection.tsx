import { useEffect, useState, type FormEvent } from 'react';
import { Mail, X, AlertCircle, UserPlus, Clock, Trash2, Shield, Building2 } from 'lucide-react';
import Button from '../components/Button';
import { ApiError } from '../../lib/api/client';
import * as companyApi from '../../lib/api/company';

export default function CompanyUsersSection() {
  const [company, setCompany] = useState<companyApi.CompanyResponse | null>(null);
  const [users, setUsers] = useState<companyApi.CompanyUserResponse[]>([]);
  const [roles, setRoles] = useState<companyApi.CompanyRoleResponse[]>([]);
  const [invites, setInvites] = useState<companyApi.CompanyInviteResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [noCompany, setNoCompany] = useState(false);
  const [error, setError] = useState('');

  const [showInvite, setShowInvite] = useState(false);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRoleId, setInviteRoleId] = useState('');
  const [inviting, setInviting] = useState(false);
  const [inviteError, setInviteError] = useState('');

  async function loadData() {
    setLoading(true);
    setError('');
    setNoCompany(false);
    try {
      const c = await companyApi.getMyCompany();
      setCompany(c);
      const [userList, roleList, inviteList] = await Promise.all([
        companyApi.getCompanyUsers(c.id),
        companyApi.getCompanyRoles(c.id),
        companyApi.getCompanyInvites(c.id),
      ]);
      setUsers(userList);
      setRoles(roleList);
      setInvites(inviteList);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setNoCompany(true);
      } else {
        setError(err instanceof Error ? err.message : 'Failed to load company data.');
      }
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  async function handleInvite(e: FormEvent) {
    e.preventDefault();
    setInviteError('');

    if (!inviteEmail.trim()) {
      setInviteError('Email is required.');
      return;
    }
    if (!inviteRoleId) {
      setInviteError('Please select a role.');
      return;
    }

    setInviting(true);
    try {
      await companyApi.inviteUser(company!.id, { email: inviteEmail, roleId: inviteRoleId });
      setInviteEmail('');
      setInviteRoleId('');
      setShowInvite(false);
      const updatedInvites = await companyApi.getCompanyInvites(company!.id);
      setInvites(updatedInvites);
    } catch (err: unknown) {
      setInviteError(err instanceof Error ? err.message : 'Failed to send invite.');
    } finally {
      setInviting(false);
    }
  }

  async function handleRemoveUser(userId: string, email: string) {
    if (!window.confirm(`Remove ${email} from the company?`)) return;
    try {
      await companyApi.removeUser(company!.id, userId);
      setUsers((prev) => prev.filter((u) => u.id !== userId));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to remove user.');
    }
  }

  async function handleRevokeInvite(inviteId: string) {
    try {
      await companyApi.revokeInvite(company!.id, inviteId);
      setInvites((prev) => prev.filter((i) => i.id !== inviteId));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to revoke invite.');
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
        <AlertCircle size={16} className="shrink-0" />
        <span>{error}</span>
      </div>
    );
  }

  if (noCompany) {
    return (
      <div className="flex flex-col items-center gap-4 rounded-2xl border border-outline-variant bg-surface p-12 text-center">
        <Building2 size={48} className="text-on-surface-variant/40" />
        <div>
          <h3 className="text-body-lg font-semibold text-on-background">No company account</h3>
          <p className="mt-1 text-body-sm text-on-surface-variant">
            You are not a member of any company. Create a company account or accept an invite to get started.
          </p>
        </div>
      </div>
    );
  }

  if (!company) {
    return (
      <div className="rounded-xl border border-outline-variant bg-surface p-8 text-center text-body-md text-on-surface-variant">
        You are not a member of any company.
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-headline-sm font-bold text-on-background">{company.name || 'My Company'}</h2>
          <p className="text-body-sm text-on-surface-variant">{users.length} member{users.length !== 1 ? 's' : ''}</p>
        </div>
        <Button variant="primary" className="px-4 py-2" onClick={() => setShowInvite(true)}>
          <UserPlus size={16} />
          Invite User
        </Button>
      </div>

      {/* Invite form */}
      {showInvite && (
        <div className="rounded-2xl border border-primary/30 bg-primary/5 p-6">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-body-lg font-semibold text-on-background">Invite a new member</h3>
            <button
              type="button"
              onClick={() => { setShowInvite(false); setInviteError(''); }}
              className="rounded-lg p-1 text-on-surface-variant hover:bg-surface-container"
            >
              <X size={18} />
            </button>
          </div>
          <form onSubmit={handleInvite} className="flex flex-wrap items-end gap-3">
            <div className="min-w-0 flex-1">
              <label htmlFor="inviteEmail" className="mb-1 block text-body-xs font-medium text-on-surface-variant">
                Email address
              </label>
              <div className="relative">
                <Mail size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/60" />
                <input
                  id="inviteEmail"
                  type="email"
                  value={inviteEmail}
                  onChange={(e) => setInviteEmail(e.target.value)}
                  placeholder="colleague@example.com"
                  className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest py-2.5 pl-9 pr-4 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                />
              </div>
            </div>
            <div className="w-40">
              <label htmlFor="inviteRole" className="mb-1 block text-body-xs font-medium text-on-surface-variant">
                Role
              </label>
              <select
                id="inviteRole"
                value={inviteRoleId}
                onChange={(e) => setInviteRoleId(e.target.value)}
                className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
              >
                <option value="">Select role</option>
                {roles
                  .filter((r) => r.name !== 'Owner')
                  .map((r) => (
                    <option key={r.id} value={r.id}>{r.name}</option>
                  ))}
              </select>
            </div>
            <Button type="submit" variant="primary" className="px-5 py-2.5" disabled={inviting}>
              {inviting ? 'Sending...' : 'Send Invite'}
            </Button>
          </form>
          {inviteError && (
            <p className="mt-2 flex items-center gap-1.5 text-body-xs text-red-600">
              <AlertCircle size={12} />
              {inviteError}
            </p>
          )}
        </div>
      )}

      {/* Users table */}
      <div className="overflow-hidden rounded-2xl border border-outline-variant bg-surface shadow-sm">
        <table className="w-full text-left text-body-sm">
          <thead>
            <tr className="border-b border-outline-variant bg-surface-container-lowest text-body-xs font-semibold uppercase tracking-wider text-on-surface-variant">
              <th className="px-5 py-3">Name</th>
              <th className="px-5 py-3">Email</th>
              <th className="px-5 py-3">Role</th>
              <th className="px-5 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-5 py-8 text-center text-on-surface-variant">
                  No users found.
                </td>
              </tr>
            ) : (
              users.map((u) => (
                <tr key={u.id} className="border-b border-outline-variant/50 last:border-0">
                  <td className="px-5 py-3.5 font-medium text-on-background">
                    {u.firstName && u.lastName ? `${u.firstName} ${u.lastName}` : '—'}
                    {u.isOwner && (
                      <span className="ml-2 inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-body-xs text-amber-700">
                        <Shield size={10} />
                        Owner
                      </span>
                    )}
                  </td>
                  <td className="px-5 py-3.5 text-on-surface-variant">{u.email}</td>
                  <td className="px-5 py-3.5 text-on-surface-variant">{u.roleName ?? '—'}</td>
                  <td className="px-5 py-3.5 text-right">
                    {!u.isOwner && (
                      <button
                        type="button"
                        onClick={() => handleRemoveUser(u.id, u.email)}
                        className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-body-xs text-red-600 transition-colors hover:bg-red-50"
                      >
                        <Trash2 size={14} />
                        Remove
                      </button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pending invites */}
      {invites.filter((i) => !i.isAccepted && !i.isExpired).length > 0 && (
        <div>
          <h3 className="mb-3 flex items-center gap-2 text-body-sm font-semibold text-on-surface-variant">
            <Clock size={16} />
            Pending Invites
          </h3>
          <div className="space-y-2">
            {invites
              .filter((i) => !i.isAccepted && !i.isExpired)
              .map((inv) => (
                <div
                  key={inv.id}
                  className="flex items-center justify-between rounded-xl border border-outline-variant bg-surface px-5 py-3"
                >
                  <div>
                    <span className="text-body-sm text-on-background">{inv.email}</span>
                    <span className="ml-2 text-body-xs text-on-surface-variant">
                      Role: {inv.roleName ?? '—'}
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => handleRevokeInvite(inv.id)}
                    className="rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-red-600"
                    title="Revoke invite"
                  >
                    <X size={16} />
                  </button>
                </div>
              ))}
          </div>
        </div>
      )}
    </div>
  );
}
