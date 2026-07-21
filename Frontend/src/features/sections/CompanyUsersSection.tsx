import { useEffect, useState, type FormEvent } from 'react';
import { Mail, X, AlertCircle, UserPlus, Clock, Trash2, Shield, Building2, CreditCard, Crown, Plus, Save, Pencil } from 'lucide-react';
import { Link } from 'react-router-dom';
import Button from '../components/Button';
import { ApiError } from '../../lib/api/client';
import * as companyApi from '../../lib/api/company';
import * as subscriptionApi from '../../lib/api/subscription';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';

export default function CompanyUsersSection() {
  const { user } = useAuth();
  const [company, setCompany] = useState<companyApi.CompanyResponse | null>(null);
  const [users, setUsers] = useState<companyApi.CompanyUserResponse[]>([]);
  const [roles, setRoles] = useState<companyApi.CompanyRoleResponse[]>([]);
  const [invites, setInvites] = useState<companyApi.CompanyInviteResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [noCompany, setNoCompany] = useState(false);
  const [error, setError] = useState('');
  const [subscriptionActive, setSubscriptionActive] = useState(false);

  const [showInvite, setShowInvite] = useState(false);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRoleId, setInviteRoleId] = useState('');
  const [inviting, setInviting] = useState(false);
  const [inviteError, setInviteError] = useState('');

  const [changingRole, setChangingRole] = useState<string | null>(null);

  const [showTransferUserId, setShowTransferUserId] = useState<string | null>(null);
  const [transferConfirmText, setTransferConfirmText] = useState('');
  const [transferring, setTransferring] = useState(false);

  const [showCreateRole, setShowCreateRole] = useState(false);
  const [newRoleName, setNewRoleName] = useState('');
  const [newRolePerms, setNewRolePerms] = useState({
    canViewAllDashboards: false,
    canManageUsers: false,
    canManageRoles: false,
    canManageDashboards: false,
  });
  const [creatingRole, setCreatingRole] = useState(false);
  const [createRoleError, setCreateRoleError] = useState('');

  const [editingRoleId, setEditingRoleId] = useState<string | null>(null);
  const [editRoleName, setEditRoleName] = useState('');
  const [editRolePerms, setEditRolePerms] = useState({
    canViewAllDashboards: false,
    canManageUsers: false,
    canManageRoles: false,
    canManageDashboards: false,
  });
  const [savingRole, setSavingRole] = useState(false);
  const [editRoleError, setEditRoleError] = useState('');

  const [pendingInvites, setPendingInvites] = useState<companyApi.CompanyInviteResponse[]>([]);
  const [pendingInvitesLoading, setPendingInvitesLoading] = useState(false);
  const [acceptingInvite, setAcceptingInvite] = useState<string | null>(null);
  const [rejectingInvite, setRejectingInvite] = useState<string | null>(null);

  const [confirmRemoveUserId, setConfirmRemoveUserId] = useState<string | null>(null);
  const [confirmRemoveText, setConfirmRemoveText] = useState('');
  const [confirmRemoveLoading, setConfirmRemoveLoading] = useState(false);

  const [confirmDeleteRoleId, setConfirmDeleteRoleId] = useState<string | null>(null);
  const [confirmDeleteRoleText, setConfirmDeleteRoleText] = useState('');
  const [confirmDeleteRoleName, setConfirmDeleteRoleName] = useState('');
  const [confirmDeleteRoleLoading, setConfirmDeleteRoleLoading] = useState(false);

  const isOwner = company && user?.userId === company.ownerId;
  const me = users.find(u => u.id === user?.userId);
  const myRole = roles.find(r => r.id === me?.roleId);
  const canManageUsers = isOwner || myRole?.canManageUsers;
  const canManageRoles = isOwner || myRole?.canManageRoles;
  const isPeerRole = (targetRoleId: string | null | undefined): boolean => {
    if (!targetRoleId || !myRole) return false;
    const targetRole = roles.find(r => r.id === targetRoleId);
    if (!targetRole) return false;
    return targetRole.canManageUsers === myRole.canManageUsers &&
           targetRole.canManageRoles === myRole.canManageRoles;
  };

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

      try {
        const sub = await subscriptionApi.getCompanySubscription(c.id);
        setSubscriptionActive(sub.status === 0 || sub.status === 1);
      } catch {
        setSubscriptionActive(false);
      }
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setNoCompany(true);
        loadPendingInvites();
      } else {
        setError(err instanceof Error ? err.message : 'Failed to load company data.');
      }
    } finally {
      setLoading(false);
    }
  }

  async function loadPendingInvites() {
    setPendingInvitesLoading(true);
    try {
      const list = await companyApi.getPendingInvites();
      setPendingInvites(list);
    } catch {
      // ignore
    } finally {
      setPendingInvitesLoading(false);
    }
  }

  useEffect(() => { loadData(); }, []);

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

  async function executeRemoveUser() {
    if (!confirmRemoveUserId) return;
    setConfirmRemoveLoading(true);
    setError('');
    try {
      await companyApi.removeUser(company!.id, confirmRemoveUserId);
      setUsers((prev) => prev.filter((u) => u.id !== confirmRemoveUserId));
      setConfirmRemoveUserId(null);
      setConfirmRemoveText('');
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to remove user.');
    } finally {
      setConfirmRemoveLoading(false);
    }
  }

  async function handleChangeRole(userId: string, roleId: string) {
    setChangingRole(userId);
    try {
      await companyApi.updateUserRole(company!.id, userId, { roleId });
      setUsers((prev) =>
        prev.map((u) => {
          if (u.id !== userId) return u;
          const role = roles.find((r) => r.id === roleId);
          return { ...u, roleId, roleName: role?.name ?? null };
        })
      );
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update role.');
    } finally {
      setChangingRole(null);
    }
  }

  async function executeTransferOwnership() {
    if (!showTransferUserId) return;
    setTransferring(true);
    setError('');
    try {
      await companyApi.transferOwnership(company!.id, showTransferUserId);
      setShowTransferUserId(null);
      setTransferConfirmText('');
      await loadData();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to transfer ownership.');
      setTransferring(false);
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

  async function handleAcceptInvite(inviteId: string) {
    setAcceptingInvite(inviteId);
    try {
      await companyApi.acceptInvite({ inviteId });
      setPendingInvites((prev) => prev.filter((i) => i.id !== inviteId));
      await loadData();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to accept invite.');
    } finally {
      setAcceptingInvite(null);
    }
  }

  async function handleRejectInvite(inviteId: string) {
    setRejectingInvite(inviteId);
    try {
      await companyApi.rejectInvite(inviteId);
      setPendingInvites((prev) => prev.filter((i) => i.id !== inviteId));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to reject invite.');
    } finally {
      setRejectingInvite(null);
    }
  }

  async function handleCreateRole(e: FormEvent) {
    e.preventDefault();
    setCreateRoleError('');

    if (!newRoleName.trim()) {
      setCreateRoleError('Role name is required.');
      return;
    }

    setCreatingRole(true);
    try {
      const created = await companyApi.createRole(company!.id, {
        name: newRoleName.trim(),
        ...newRolePerms,
      });
      setRoles((prev) => [...prev, created]);
      setNewRoleName('');
      setNewRolePerms({ canViewAllDashboards: false, canManageUsers: false, canManageRoles: false, canManageDashboards: false });
      setShowCreateRole(false);
    } catch (err: unknown) {
      setCreateRoleError(err instanceof Error ? err.message : 'Failed to create role.');
    } finally {
      setCreatingRole(false);
    }
  }

  function startEditRole(role: companyApi.CompanyRoleResponse) {
    setEditingRoleId(role.id);
    setEditRoleName(role.name);
    setEditRolePerms({
      canViewAllDashboards: role.canViewAllDashboards,
      canManageUsers: role.canManageUsers,
      canManageRoles: role.canManageRoles,
      canManageDashboards: role.canManageDashboards,
    });
    setEditRoleError('');
  }

  function cancelEditRole() {
    setEditingRoleId(null);
    setEditRoleName('');
    setEditRolePerms({ canViewAllDashboards: false, canManageUsers: false, canManageRoles: false, canManageDashboards: false });
    setEditRoleError('');
  }

  async function handleUpdateRole(e: FormEvent) {
    e.preventDefault();
    if (!editingRoleId) return;
    setEditRoleError('');

    if (!editRoleName.trim()) {
      setEditRoleError('Role name is required.');
      return;
    }

    setSavingRole(true);
    try {
      const updated = await companyApi.updateRole(company!.id, editingRoleId, {
        name: editRoleName.trim(),
        ...editRolePerms,
      });
      setRoles((prev) => prev.map((r) => r.id === editingRoleId ? updated : r));
      cancelEditRole();
    } catch (err: unknown) {
      setEditRoleError(err instanceof Error ? err.message : 'Failed to update role.');
    } finally {
      setSavingRole(false);
    }
  }

  async function executeDeleteRole() {
    if (!confirmDeleteRoleId) return;
    setConfirmDeleteRoleLoading(true);
    setError('');
    try {
      await companyApi.deleteRole(company!.id, confirmDeleteRoleId);
      setRoles((prev) => prev.filter((r) => r.id !== confirmDeleteRoleId));
      setConfirmDeleteRoleId(null);
      setConfirmDeleteRoleText('');
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete role.');
    } finally {
      setConfirmDeleteRoleLoading(false);
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
      <div className="space-y-6">
        <div className="flex flex-col items-center gap-4 rounded-2xl border border-outline-variant bg-surface p-12 text-center">
          <Building2 size={48} className="text-on-surface-variant/40" />
          <div>
            <h3 className="text-body-lg font-semibold text-on-background">No company account</h3>
            <p className="mt-1 text-body-sm text-on-surface-variant">
              You are not a member of any company. Create a company account or accept an invite to get started.
            </p>
          </div>
        </div>

        {pendingInvitesLoading ? (
          <div className="flex justify-center py-4">
            <div className="h-6 w-6 animate-spin rounded-full border-4 border-primary border-t-transparent" />
          </div>
        ) : pendingInvites.length > 0 ? (
          <div>
            <h3 className="mb-3 flex items-center gap-2 text-body-sm font-semibold text-on-surface-variant">
              <Mail size={16} />
              Pending Invites
            </h3>
            <div className="space-y-2">
              {pendingInvites.map((inv) => (
                <div
                  key={inv.id}
                  className="flex items-center justify-between rounded-xl border border-outline-variant bg-surface px-5 py-3"
                >
                  <div>
                    <span className="text-body-sm text-on-background">
                      Invited as <span className="font-medium">{inv.roleName ?? 'Member'}</span>
                    </span>
                    {inv.isExpired && (
                      <span className="ml-2 text-body-xs text-red-500">(Expired)</span>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    {!inv.isExpired && (
                      <>
                        <Button
                          variant="ghost"
                          className="px-3 py-1 text-body-xs text-red-600"
                          disabled={rejectingInvite === inv.id}
                          onClick={() => handleRejectInvite(inv.id)}
                        >
                          {rejectingInvite === inv.id ? 'Rejecting...' : 'Reject'}
                        </Button>
                        <Button
                          variant="primary"
                          className="px-3 py-1 text-body-xs"
                          disabled={acceptingInvite === inv.id}
                          onClick={() => handleAcceptInvite(inv.id)}
                        >
                          {acceptingInvite === inv.id ? 'Accepting...' : 'Accept'}
                        </Button>
                      </>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : null}
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
        {canManageUsers && (
          <Button variant="primary" className="px-4 py-2" onClick={() => setShowInvite(true)} disabled={!subscriptionActive} title={!subscriptionActive ? 'Requires an active subscription' : ''}>
            <UserPlus size={16} />
            Invite User
          </Button>
        )}
      </div>

      {!subscriptionActive && (
        <div className="flex items-center justify-between gap-4 rounded-xl border border-amber-300 bg-amber-50 px-5 py-4 text-body-sm text-amber-800">
          <div className="flex items-center gap-2">
            <AlertCircle size={16} className="shrink-0" />
            <span>Company management requires an active subscription.</span>
          </div>
          <Link to={ROUTES.PRICING}>
            <Button variant="outline" className="shrink-0 border-amber-300 text-amber-800 hover:bg-amber-100">
              <CreditCard size={14} className="mr-1" />
              Subscribe
            </Button>
          </Link>
        </div>
      )}

      {/* Invite form */}
      {showInvite && canManageUsers && (
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
                        <Crown size={10} />
                        Owner
                      </span>
                    )}
                  </td>
                  <td className="px-5 py-3.5 text-on-surface-variant">{u.email}</td>
                  <td className="px-5 py-3.5">
                    {u.isOwner ? (
                      <span className="text-on-surface-variant">Owner</span>
                    ) : canManageRoles && u.id !== user?.userId && (isOwner || !isPeerRole(u.roleId)) ? (
                      <select
                        value={u.roleId ?? ''}
                        disabled={changingRole === u.id || !subscriptionActive}
                        onChange={(e) => handleChangeRole(u.id, e.target.value)}
                        className="w-full max-w-40 rounded-lg border border-outline-variant bg-surface-container-lowest px-2 py-1.5 text-body-sm text-on-background focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:opacity-40 disabled:cursor-not-allowed"
                      >
                        <option value="" disabled>Select role</option>
                        {roles
                          .filter((r) => r.name !== 'Owner')
                          .map((r) => (
                            <option key={r.id} value={r.id}>{r.name}</option>
                          ))}
                      </select>
                    ) : (
                      <span className="text-on-surface-variant">{u.roleName ?? '—'}</span>
                    )}
                  </td>
                  <td className="px-5 py-3.5 text-right">
                    {!u.isOwner && (
                      <div className="flex items-center justify-end gap-1">
                        {isOwner && (
                          <button
                            type="button"
                            onClick={() => { setConfirmRemoveUserId(null); setConfirmDeleteRoleId(null); setShowTransferUserId(u.id); setTransferConfirmText(''); }}
                            disabled={transferring || !subscriptionActive}
                            className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-body-xs text-amber-600 transition-colors hover:bg-amber-50 disabled:opacity-40 disabled:cursor-not-allowed"
                            title="Transfer ownership"
                          >
                            <Crown size={14} />
                            Transfer
                          </button>
                        )}
                        {canManageRoles && u.id !== user?.userId && (isOwner || !isPeerRole(u.roleId)) && (
                          <button
                            type="button"
                            onClick={() => { setShowTransferUserId(null); setConfirmDeleteRoleId(null); setConfirmRemoveUserId(u.id); setConfirmRemoveText(''); }}
                            disabled={!subscriptionActive}
                            className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-body-xs text-red-600 transition-colors hover:bg-red-50 disabled:opacity-40 disabled:cursor-not-allowed"
                            title={!subscriptionActive ? 'Requires an active subscription' : ''}
                          >
                            <Trash2 size={14} />
                            Remove
                          </button>
                        )}
                      </div>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Transfer ownership inline confirmation */}
      {showTransferUserId && (
        <div className="rounded-2xl border border-amber-200 bg-amber-50/50 p-6">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-body-md font-semibold text-amber-800">
              Transfer Ownership
            </h3>
            <button
              type="button"
              onClick={() => { setShowTransferUserId(null); setTransferConfirmText(''); }}
              className="rounded-lg p-1 text-amber-600 hover:bg-amber-100"
            >
              <X size={18} />
            </button>
          </div>
          <p className="mb-3 text-body-sm text-amber-700">
            Transfer ownership to <span className="font-semibold">{users.find((u) => u.id === showTransferUserId)?.email}</span>.
            You will lose owner privileges and become a regular member.
          </p>
          <div className="mb-3">
            <label htmlFor="transferConfirm" className="mb-1 block text-body-xs font-medium text-amber-700">
              Type <span className="font-bold">TRANSFER</span> to confirm
            </label>
            <input
              id="transferConfirm"
              type="text"
              value={transferConfirmText}
              onChange={(e) => setTransferConfirmText(e.target.value)}
              placeholder="Type TRANSFER to confirm"
              className="w-full rounded-xl border border-amber-300 bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-amber-500 focus:outline-none focus:ring-2 focus:ring-amber-500/20"
            />
          </div>
          {error && (
            <p className="mb-3 flex items-center gap-1.5 text-body-xs text-red-700">
              <AlertCircle size={12} />
              {error}
            </p>
          )}
          <div className="flex gap-3">
            <Button
              variant="outline"
              className="flex-1"
              disabled={transferring}
              onClick={() => { setShowTransferUserId(null); setTransferConfirmText(''); }}
            >
              Cancel
            </Button>
            <Button
              variant="outline"
              className="flex-1 border-amber-300 text-amber-700 hover:bg-amber-100"
              disabled={transferConfirmText !== 'TRANSFER' || transferring}
              onClick={(e) => { e.preventDefault(); void executeTransferOwnership(); }}
            >
              {transferring ? 'Transferring...' : 'Transfer ownership'}
            </Button>
          </div>
        </div>
      )}

      {/* Remove user confirmation */}
      {confirmRemoveUserId && (
        <div className="rounded-2xl border border-red-200 bg-red-50/50 p-6">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-body-md font-semibold text-red-800">Remove User</h3>
            <button
              type="button"
              onClick={() => { setConfirmRemoveUserId(null); setConfirmRemoveText(''); }}
              className="rounded-lg p-1 text-red-600 hover:bg-red-100"
            >
              <X size={18} />
            </button>
          </div>
          <p className="mb-3 text-body-sm text-red-700">
            Remove <span className="font-semibold">{users.find((u) => u.id === confirmRemoveUserId)?.email}</span> from the company?
            This action cannot be undone.
          </p>
          <div className="mb-3">
            <label htmlFor="removeConfirm" className="mb-1 block text-body-xs font-medium text-red-700">
              Type <span className="font-bold">DELETE</span> to confirm
            </label>
            <input
              id="removeConfirm"
              type="text"
              value={confirmRemoveText}
              onChange={(e) => setConfirmRemoveText(e.target.value)}
              placeholder="Type DELETE to confirm"
              className="w-full rounded-xl border border-red-300 bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-red-500 focus:outline-none focus:ring-2 focus:ring-red-500/20"
            />
          </div>
          {error && (
            <p className="mb-3 flex items-center gap-1.5 text-body-xs text-red-700">
              <AlertCircle size={12} />
              {error}
            </p>
          )}
          <div className="flex gap-3">
            <Button
              variant="outline"
              className="flex-1"
              disabled={confirmRemoveLoading}
              onClick={() => { setConfirmRemoveUserId(null); setConfirmRemoveText(''); }}
            >
              Cancel
            </Button>
            <Button
              variant="outline"
              className="flex-1 border-red-300 text-red-700 hover:bg-red-100"
              disabled={confirmRemoveText !== 'DELETE' || confirmRemoveLoading}
              onClick={(e) => { e.preventDefault(); void executeRemoveUser(); }}
            >
              {confirmRemoveLoading ? 'Removing...' : 'Remove user'}
            </Button>
          </div>
        </div>
      )}

      {/* Pending invites (company-side) */}
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
                  {canManageUsers && (
                    <button
                      type="button"
                      onClick={() => handleRevokeInvite(inv.id)}
                      disabled={!subscriptionActive}
                      className="rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-red-600 disabled:opacity-40 disabled:cursor-not-allowed"
                      title={!subscriptionActive ? 'Requires an active subscription' : ''}
                    >
                      <X size={16} />
                    </button>
                  )}
                </div>
              ))}
          </div>
        </div>
      )}

      {/* Role Management */}
      {canManageRoles && (
        <div>
          <div className="mb-4 flex items-center justify-between">
            <h3 className="flex items-center gap-2 text-body-sm font-semibold text-on-surface-variant">
              <Shield size={16} />
              Roles & Permissions
            </h3>
            {!showCreateRole && (
              <Button variant="outline" className="px-3 py-1.5 text-body-xs" onClick={() => setShowCreateRole(true)}>
                <Plus size={14} />
                Create Role
              </Button>
            )}
          </div>

          {showCreateRole && (
            <div className="mb-4 rounded-2xl border border-primary/30 bg-primary/5 p-6">
              <div className="mb-4 flex items-center justify-between">
                <h4 className="text-body-md font-semibold text-on-background">New Role</h4>
                <button
                  type="button"
                  onClick={() => { setShowCreateRole(false); setCreateRoleError(''); }}
                  className="rounded-lg p-1 text-on-surface-variant hover:bg-surface-container"
                >
                  <X size={18} />
                </button>
              </div>
              <form onSubmit={handleCreateRole} className="space-y-4">
                <div>
                  <label htmlFor="newRoleName" className="mb-1 block text-body-xs font-medium text-on-surface-variant">
                    Role name
                  </label>
                  <input
                    id="newRoleName"
                    type="text"
                    value={newRoleName}
                    onChange={(e) => setNewRoleName(e.target.value)}
                    placeholder="e.g. Analyst"
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                  />
                </div>
                <div className="space-y-2">
                  <p className="text-body-xs font-medium text-on-surface-variant">Permissions</p>
                  {([
                    { key: 'canViewAllDashboards' as const, label: 'View all dashboards' },
                    { key: 'canManageUsers' as const, label: 'Manage users', ownerOnly: true },
                    { key: 'canManageRoles' as const, label: 'Manage roles', ownerOnly: true },
                    { key: 'canManageDashboards' as const, label: 'Manage dashboards' },
                  ]).map(({ key, label, ownerOnly }) => {
                    const isOwnerOnly = ownerOnly && !isOwner;
                    return (
                      <label key={key} className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isOwnerOnly ? false : newRolePerms[key]}
                          disabled={isOwnerOnly}
                          onChange={(e) => setNewRolePerms((prev) => ({ ...prev, [key]: e.target.checked }))}
                          className="h-4 w-4 rounded border-outline-variant text-primary focus:ring-primary/20 disabled:cursor-not-allowed disabled:opacity-40"
                        />
                        <span className="text-body-sm text-on-background">{label}</span>
                      </label>
                    );
                  })}
                </div>
                {createRoleError && (
                  <p className="flex items-center gap-1.5 text-body-xs text-red-600">
                    <AlertCircle size={12} />
                    {createRoleError}
                  </p>
                )}
                <Button type="submit" variant="primary" className="w-full" disabled={creatingRole}>
                  <Save size={14} />
                  {creatingRole ? 'Creating...' : 'Create Role'}
                </Button>
              </form>
            </div>
          )}

          <div className="space-y-2">
            {roles.length === 0 ? (
              <p className="text-body-sm text-on-surface-variant">No roles defined.</p>
            ) : (
              roles.map((r) => (
                editingRoleId === r.id ? (
                  <div
                    key={r.id}
                    className="rounded-2xl border border-primary/30 bg-primary/5 p-6"
                  >
                    <div className="mb-4 flex items-center justify-between">
                      <h4 className="text-body-md font-semibold text-on-background">Edit Role</h4>
                      <button
                        type="button"
                        onClick={cancelEditRole}
                        className="rounded-lg p-1 text-on-surface-variant hover:bg-surface-container"
                      >
                        <X size={18} />
                      </button>
                    </div>
                    <form onSubmit={handleUpdateRole} className="space-y-4">
                      <div>
                        <label htmlFor="editRoleName" className="mb-1 block text-body-xs font-medium text-on-surface-variant">
                          Role name
                        </label>
                        <input
                          id="editRoleName"
                          type="text"
                          value={editRoleName}
                          onChange={(e) => setEditRoleName(e.target.value)}
                          readOnly={roles.find(r => r.id === editingRoleId)?.isSystemRole}
                          className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 read-only:cursor-not-allowed read-only:opacity-60"
                        />
                      </div>
                      <div className="space-y-2">
                        <p className="text-body-xs font-medium text-on-surface-variant">Permissions</p>
                        {([
                          { key: 'canViewAllDashboards' as const, label: 'View all dashboards' },
                          { key: 'canManageUsers' as const, label: 'Manage users', ownerOnly: true },
                          { key: 'canManageRoles' as const, label: 'Manage roles', ownerOnly: true },
                          { key: 'canManageDashboards' as const, label: 'Manage dashboards' },
                        ]).map(({ key, label, ownerOnly }) => {
                          const isOwnerOnly = ownerOnly && !isOwner;
                          return (
                            <label key={key} className="flex items-center gap-2 cursor-pointer">
                              <input
                                type="checkbox"
                                checked={isOwnerOnly ? false : editRolePerms[key]}
                                disabled={isOwnerOnly}
                                onChange={(e) => setEditRolePerms((prev) => ({ ...prev, [key]: e.target.checked }))}
                                className="h-4 w-4 rounded border-outline-variant text-primary focus:ring-primary/20 disabled:cursor-not-allowed disabled:opacity-40"
                              />
                              <span className="text-body-sm text-on-background">{label}</span>
                            </label>
                          );
                        })}
                      </div>
                      {editRoleError && (
                        <p className="flex items-center gap-1.5 text-body-xs text-red-600">
                          <AlertCircle size={12} />
                          {editRoleError}
                        </p>
                      )}
                      <div className="flex gap-3">
                        <Button type="button" variant="outline" className="flex-1" onClick={cancelEditRole}>
                          Cancel
                        </Button>
                        <Button type="submit" variant="primary" className="flex-1" disabled={savingRole}>
                          <Save size={14} />
                          {savingRole ? 'Saving...' : 'Save Role'}
                        </Button>
                      </div>
                    </form>
                  </div>
                ) : (
                  <div
                    key={r.id}
                    className="flex items-center justify-between rounded-xl border border-outline-variant bg-surface px-5 py-3"
                  >
                    <div className="flex items-center gap-3">
                      <span className="text-body-sm font-medium text-on-background">{r.name}</span>
                      {r.isSystemRole && (
                        <span className="inline-flex items-center rounded-full bg-surface-container px-2 py-0.5 text-body-xs text-on-surface-variant">
                          System
                        </span>
                      )}
                      <span className="text-body-xs text-on-surface-variant">{r.userCount} user{r.userCount !== 1 ? 's' : ''}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      {!r.isSystemRole && (isOwner || !isPeerRole(r.id)) && (
                        <button
                          type="button"
                          onClick={() => { setConfirmDeleteRoleId(null); startEditRole(r); }}
                          disabled={!subscriptionActive}
                          className="rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary disabled:opacity-40 disabled:cursor-not-allowed"
                          title="Edit role"
                        >
                          <Pencil size={16} />
                        </button>
                      )}
                      {!r.isSystemRole && (isOwner || !isPeerRole(r.id)) && (
                        <button
                          type="button"
                          onClick={() => { setEditingRoleId(null); setConfirmRemoveUserId(null); setShowTransferUserId(null); setConfirmDeleteRoleId(r.id); setConfirmDeleteRoleName(r.name); setConfirmDeleteRoleText(''); }}
                          disabled={!subscriptionActive}
                          className="rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-red-600 disabled:opacity-40 disabled:cursor-not-allowed"
                          title="Delete role"
                        >
                          <Trash2 size={16} />
                        </button>
                      )}
                    </div>
                  </div>
                )
              ))
            )}
          </div>

          {confirmDeleteRoleId && (
            <div className="rounded-2xl border border-red-200 bg-red-50/50 p-6">
              <div className="mb-4 flex items-center justify-between">
                <h3 className="text-body-md font-semibold text-red-800">Delete Role</h3>
                <button
                  type="button"
                  onClick={() => { setConfirmDeleteRoleId(null); setConfirmDeleteRoleText(''); }}
                  className="rounded-lg p-1 text-red-600 hover:bg-red-100"
                >
                  <X size={18} />
                </button>
              </div>
              <p className="mb-3 text-body-sm text-red-700">
                Delete role <span className="font-semibold">{confirmDeleteRoleName}</span>?
                Users assigned this role will lose its permissions.
              </p>
              <div className="mb-3">
                <label htmlFor="deleteRoleConfirm" className="mb-1 block text-body-xs font-medium text-red-700">
                  Type <span className="font-bold">DELETE</span> to confirm
                </label>
                <input
                  id="deleteRoleConfirm"
                  type="text"
                  value={confirmDeleteRoleText}
                  onChange={(e) => setConfirmDeleteRoleText(e.target.value)}
                  placeholder="Type DELETE to confirm"
                  className="w-full rounded-xl border border-red-300 bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-red-500 focus:outline-none focus:ring-2 focus:ring-red-500/20"
                />
              </div>
              {error && (
                <p className="mb-3 flex items-center gap-1.5 text-body-xs text-red-700">
                  <AlertCircle size={12} />
                  {error}
                </p>
              )}
              <div className="flex gap-3">
                <Button
                  variant="outline"
                  className="flex-1"
                  disabled={confirmDeleteRoleLoading}
                  onClick={() => { setConfirmDeleteRoleId(null); setConfirmDeleteRoleText(''); }}
                >
                  Cancel
                </Button>
                <Button
                  variant="outline"
                  className="flex-1 border-red-300 text-red-700 hover:bg-red-100"
                  disabled={confirmDeleteRoleText !== 'DELETE' || confirmDeleteRoleLoading}
                  onClick={(e) => { e.preventDefault(); void executeDeleteRole(); }}
                >
                  {confirmDeleteRoleLoading ? 'Deleting...' : 'Delete role'}
                </Button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}