import { useEffect, useMemo, useState } from 'react';
import { Loader2, Search, ShieldCheck, ShieldOff } from 'lucide-react';
import { toast } from 'sonner';
import AppShell from '../layouts/AppShell';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import { ROUTES } from '../routes';
import { Input } from '@/components/ui/input';
import { useAuth } from '../store/useAuth';
import { getAdminUsers, setAdminRole, type AdminUser } from '../../lib/api/admin';

export default function AdminAccountsPage() {
  const { user } = useAuth();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [pending, setPending] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<AdminUser | null>(null);
  const [confirmLoading, setConfirmLoading] = useState(false);

  const filtered = useMemo(() => {
    const admins = users.filter((u) => u.isAdmin);
    const term = search.trim().toLowerCase();
    if (!term) return admins;
    return admins.filter(
      (u) =>
        u.email.toLowerCase().includes(term) ||
        (u.firstName ?? '').toLowerCase().includes(term) ||
        (u.lastName ?? '').toLowerCase().includes(term),
    );
  }, [users, search]);

  useEffect(() => {
    getAdminUsers()
      .then((data) => setUsers(data))
      .catch((err) => toast.error(err instanceof Error ? err.message : 'Could not load users.'))
      .finally(() => setLoading(false));
  }, []);

  async function toggleRole(target: AdminUser) {
    if (target.isAdmin) {
      setConfirming(target);
      return;
    }
    await applyRole(target, true);
  }

  async function applyRole(target: AdminUser, isAdmin: boolean) {
    setPending(target.id);
    try {
      const updated = await setAdminRole(target.id, isAdmin);
      setUsers((prev) => prev.map((u) => (u.id === updated.id ? updated : u)));
      toast.success(isAdmin ? `${target.email} is now an admin.` : `Admin role revoked from ${target.email}.`);
      setConfirming(null);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not update admin role.');
    } finally {
      setPending(null);
      setConfirmLoading(false);
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
            View admin accounts and revoke the global admin role.
          </p>
        </div>
        <div className="relative w-full max-w-xs">
          <Search size={16} className="absolute top-1/2 left-3 -translate-y-1/2 text-on-surface-variant" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by email or name"
            className="pl-9"
          />
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
                  <th className="px-4 py-3 font-medium text-right">Admin</th>
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
                              className="inline-flex items-center rounded-full bg-surface-container-low px-2 py-0.5 text-label-xs font-medium text-on-surface-variant"
                            >
                              {role}
                            </span>
                          ))}
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end">
                          <Button
                            variant={u.isAdmin ? 'outline' : 'surface'}
                            className={`min-h-9 px-3 py-1.5 text-body-sm ${
                              u.isAdmin ? 'border-green-300 text-green-700 hover:bg-green-50' : ''
                            }`}
                            disabled={pending === u.id || (isSelf && u.isAdmin)}
                            onClick={() => void toggleRole(u)}
                          >
                            {pending === u.id ? (
                              <Loader2 size={14} className="animate-spin" />
                            ) : u.isAdmin ? (
                              <ShieldOff size={14} />
                            ) : (
                              <ShieldCheck size={14} />
                            )}
                            {u.isAdmin ? 'Revoke' : 'Grant'}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
                {filtered.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-4 py-8 text-center text-on-surface-variant">
                      No users found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={confirming !== null}
        onOpenChange={(open) => !confirmLoading && setConfirming(open ? confirming : null)}
        title="Revoke admin role?"
        description={`${confirming?.email ?? ''} will lose access to the admin area. You can grant it again later.`}
        confirmLabel="Revoke role"
        variant="destructive"
        loading={confirmLoading}
        onConfirm={() => {
          if (!confirming) return;
          setConfirmLoading(true);
          void applyRole(confirming, false);
        }}
      />
    </AppShell>
  );
}
