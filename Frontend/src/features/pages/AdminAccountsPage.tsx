import { useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Crown, Loader2, Plus, Search, UserMinus } from 'lucide-react';
import { toast } from 'sonner';
import AppShell from '../layouts/AppShell';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import { ROUTES } from '../routes';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useAuth } from '../store/useAuth';
import {
  getAdminUsers,
  createAdminUser,
  setModeratorRole,
  transferAdminRole,
  type AdminUser,
  type UserRole,
} from '../../lib/api/admin';
import { USER_TYPE, type UserType } from '../../lib/api/subscription';

interface CreateForm {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  userType: UserType;
  role: UserRole;
}

const emptyForm: CreateForm = {
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  userType: USER_TYPE.Individual,
  role: 'User',
};

const roleOptions: { value: UserRole; label: string; hint: string }[] = [
  { value: 'User', label: 'User', hint: 'Standard account, no admin access.' },
  { value: 'Moderator', label: 'Moderator', hint: 'Can manage plans and see the overview.' },
];

function roleBadgeClasses(role: string) {
  if (role === 'Admin') return 'bg-green-50 text-green-700';
  if (role === 'Moderator') return 'bg-amber-50 text-amber-700';
  return 'bg-surface-container-low text-on-surface-variant';
}

export default function AdminAccountsPage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [transferTarget, setTransferTarget] = useState<AdminUser | null>(null);
  const [transferLoading, setTransferLoading] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<AdminUser | null>(null);
  const [removeLoading, setRemoveLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<CreateForm>(emptyForm);

  const isAdminUser = user?.roles.includes('Admin');

  useEffect(() => {
    if (!isAdminUser) return;
    let active = true;
    const timer = setTimeout(() => {
      setLoading(true);
      getAdminUsers(search.trim() || undefined, true)
        .then((data) => active && setUsers(data))
        .catch((err) => toast.error(err instanceof Error ? err.message : 'Could not load users.'))
        .finally(() => active && setLoading(false));
    }, 300);
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [isAdminUser, search]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return users;
    return users.filter(
      (u) =>
        u.email.toLowerCase().includes(term) ||
        (u.firstName ?? '').toLowerCase().includes(term) ||
        (u.lastName ?? '').toLowerCase().includes(term),
    );
  }, [users, search]);

  if (!isAdminUser) {
    return <Navigate to={ROUTES.ADMIN_MAIN} replace />;
  }

  async function loadUsers() {
    try {
      setUsers(await getAdminUsers(search.trim() || undefined, true));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not load users.');
    }
  }

  async function handleTransfer() {
    if (!transferTarget) return;
    setTransferLoading(true);
    try {
      await transferAdminRole(transferTarget.id);
      setTransferTarget(null);
      toast.success(`Admin role handed to ${transferTarget.email}. You have been signed out — please sign in again.`);
      await logout();
      navigate(ROUTES.LOGIN);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not transfer the admin role.');
    } finally {
      setTransferLoading(false);
    }
  }

  async function handleRemoveModerator() {
    if (!removeTarget) return;
    setRemoveLoading(true);
    try {
      await setModeratorRole(removeTarget.id, false);
      toast.success(`Moderator access removed from ${removeTarget.email}.`);
      setRemoveTarget(null);
      await loadUsers();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not remove moderator access.');
    } finally {
      setRemoveLoading(false);
    }
  }

  function updateCreateField<K extends keyof CreateForm>(key: K, value: CreateForm[K]) {
    setCreateForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleCreate() {
    if (!createForm.email.trim()) {
      toast.error('Email is required.');
      return;
    }
    if (createForm.password.length < 8) {
      toast.error('Password must be at least 8 characters.');
      return;
    }
    if (createForm.role !== 'User' && createForm.userType === USER_TYPE.Company) {
      toast.error('Staff accounts (moderator or admin) must be individual users.');
      return;
    }
    setCreating(true);
    try {
      const created = await createAdminUser({
        email: createForm.email.trim(),
        password: createForm.password,
        firstName: createForm.firstName.trim(),
        lastName: createForm.lastName.trim(),
        userType: createForm.userType,
        role: createForm.role,
      });
      toast.success(`${created.email} created as ${created.isAdmin ? 'admin' : created.isModerator ? 'moderator' : 'user'}.`);
      setCreateOpen(false);
      setCreateForm(emptyForm);
      await loadUsers();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not create user.');
    } finally {
      setCreating(false);
    }
  }

  return (
    <AppShell
      breadcrumbs={[
        { label: 'Administration', to: ROUTES.ADMIN },
        { label: 'Users' },
      ]}
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Users</h1>
          <p className="text-muted-foreground text-sm">
            Manage moderators and create users. The admin role can only be handed over to a moderator.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="relative w-full max-w-xs">
            <Search size={16} className="absolute top-1/2 left-3 -translate-y-1/2 text-on-surface-variant" />
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search by email or name"
              className="pl-9"
            />
          </div>
          <Button onClick={() => setCreateOpen(true)} className="shrink-0 whitespace-nowrap">
            <Plus size={16} className="mr-1" />
            New user
          </Button>
        </div>
      </div>

      {loading ? (
        <div className="flex min-h-52 items-center justify-center">
          <Loader2 className="size-8 animate-spin text-primary" />
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-outline-variant bg-surface shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-body-sm">
              <thead className="bg-surface-container-low text-label-xs uppercase tracking-wider text-on-surface-variant">
                <tr>
                  <th className="px-4 py-3 font-medium">User</th>
                  <th className="px-4 py-3 font-medium">Email</th>
                  <th className="px-4 py-3 font-medium">Type</th>
                  <th className="px-4 py-3 font-medium">Roles</th>
                  <th className="px-4 py-3 font-medium text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/50">
                {filtered.map((u) => {
                  const isSelf = u.id === user?.userId;
                  return (
                    <tr key={u.id} className="hover:bg-surface-container-lowest/40">
                      <td className="px-4 py-3 font-medium text-on-background">
                        {u.firstName || u.lastName ? `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() : '—'}
                      </td>
                      <td className="px-4 py-3 text-on-surface-variant">{u.email}</td>
                      <td className="px-4 py-3 text-on-surface-variant">
                        {u.userType === 1 ? 'Company' : 'Individual'}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap gap-1">
                          {u.roles.map((role) => (
                            <span
                              key={role}
                              className={`inline-flex items-center rounded-full px-2 py-0.5 text-label-xs font-medium ${roleBadgeClasses(role)}`}
                            >
                              {role}
                            </span>
                          ))}
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-2">
                          {u.isAdmin ? (
                            <span className="text-label-xs text-on-surface-variant">
                              {isSelf ? 'You' : 'Permanent'}
                            </span>
                          ) : (
                            <>
                              <Button
                                variant="outline"
                                className="min-h-9 px-3 py-1.5 text-body-sm"
                                onClick={() => setTransferTarget(u)}
                              >
                                <Crown size={14} />
                                Transfer admin
                              </Button>
                              <Button
                                variant="outline"
                                className="min-h-9 border-red-300 px-3 py-1.5 text-body-sm text-red-600 hover:bg-red-50"
                                onClick={() => setRemoveTarget(u)}
                              >
                                <UserMinus size={14} />
                                Remove
                              </Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
                {filtered.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-4 py-8 text-center text-on-surface-variant">
                      No staff accounts found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={transferTarget !== null}
        onOpenChange={(open) => !transferLoading && setTransferTarget(open ? transferTarget : null)}
        title="Hand over the admin role?"
        description={`${transferTarget?.email ?? ''} becomes an admin. You become a moderator and can no longer manage users.`}
        confirmLabel="Transfer admin role"
        loading={transferLoading}
        onConfirm={handleTransfer}
      />

      <ConfirmDialog
        open={removeTarget !== null}
        onOpenChange={(open) => !removeLoading && setRemoveTarget(open ? removeTarget : null)}
        title="Remove moderator access?"
        description={`${removeTarget?.email ?? ''} will lose admin-area access and become a regular user.`}
        confirmLabel="Remove moderator"
        variant="destructive"
        loading={removeLoading}
        onConfirm={handleRemoveModerator}
      />

      <Dialog open={createOpen} onOpenChange={(open) => !creating && setCreateOpen(open)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Create user</DialogTitle>
            <DialogDescription>
              Adds a new account. Share the credentials with the user after creation.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-1">
            <div className="grid grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="create-first-name">First name</Label>
                <Input
                  id="create-first-name"
                  value={createForm.firstName}
                  onChange={(e) => updateCreateField('firstName', e.target.value)}
                  placeholder="Jane"
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="create-last-name">Last name</Label>
                <Input
                  id="create-last-name"
                  value={createForm.lastName}
                  onChange={(e) => updateCreateField('lastName', e.target.value)}
                  placeholder="Doe"
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="create-email">Email</Label>
              <Input
                id="create-email"
                type="email"
                value={createForm.email}
                onChange={(e) => updateCreateField('email', e.target.value)}
                placeholder="user@example.com"
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="create-password">Password</Label>
              <Input
                id="create-password"
                type="password"
                value={createForm.password}
                onChange={(e) => updateCreateField('password', e.target.value)}
                placeholder="At least 8 characters"
              />
            </div>

            <div className="grid gap-2">
              <Label>Role</Label>
              <div className="grid gap-2">
                {roleOptions.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => updateCreateField('role', option.value)}
                    className={`rounded-xl border px-4 py-2.5 text-left transition-colors ${
                      createForm.role === option.value
                        ? 'border-primary bg-primary/5'
                        : 'border-outline-variant hover:bg-surface-container-low'
                    }`}
                  >
                    <span className="block text-body-md font-medium text-on-background">
                      {option.label}
                    </span>
                    <span className="block text-body-sm text-on-surface-variant">{option.hint}</span>
                  </button>
                ))}
              </div>
            </div>

            <div className="grid gap-2">
              <Label>Account type</Label>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => updateCreateField('userType', USER_TYPE.Individual)}
                  className={`flex-1 rounded-xl border px-4 py-2.5 text-body-md font-medium transition-colors ${
                    createForm.userType === USER_TYPE.Individual
                      ? 'border-primary bg-primary/5 text-primary'
                      : 'border-outline-variant text-on-surface-variant hover:bg-surface-container-low'
                  }`}
                >
                  Individual
                </button>
                <button
                  type="button"
                  disabled={createForm.role !== 'User'}
                  onClick={() => updateCreateField('userType', USER_TYPE.Company)}
                  className={`flex-1 rounded-xl border px-4 py-2.5 text-body-md font-medium transition-colors ${
                    createForm.userType === USER_TYPE.Company
                      ? 'border-primary bg-primary/5 text-primary'
                      : 'border-outline-variant text-on-surface-variant hover:bg-surface-container-low'
                  } disabled:cursor-not-allowed disabled:opacity-40`}
                >
                  Company
                </button>
              </div>
              {createForm.role !== 'User' && (
                <p className="text-body-xs text-on-surface-variant">
                  Staff accounts must be individual users.
                </p>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" disabled={creating} onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => void handleCreate()} disabled={creating}>
              {creating && <Loader2 className="size-4 animate-spin" />}
              Create user
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AppShell>
  );
}
