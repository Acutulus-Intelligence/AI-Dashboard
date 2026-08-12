import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { BadgeDollarSign, UserCog } from 'lucide-react';
import AppShell from '../layouts/AppShell';
import { ROUTES } from '../routes';
import { getAdminPlans, getAdminUsers } from '../../lib/api/admin';

export default function AdminMainPage() {
  const [planCount, setPlanCount] = useState<number | null>(null);
  const [userCount, setUserCount] = useState<number | null>(null);

  useEffect(() => {
    getAdminPlans()
      .then((plans) => setPlanCount(plans.length))
      .catch(() => {});
    getAdminUsers()
      .then((users) => setUserCount(users.length))
      .catch(() => {});
  }, []);

  return (
    <AppShell breadcrumbs={[{ label: 'Administration' }]}>
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Administration</h1>
        <p className="text-muted-foreground text-sm">
          Manage subscription plans and user accounts.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        <Link
          to={ROUTES.ADMIN_PLANS}
          className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs transition-shadow hover:shadow-md"
        >
          <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <BadgeDollarSign size={24} />
          </div>
          <h2 className="mb-2 text-body-lg font-semibold text-on-background">Plans</h2>
          <p className="text-body-sm text-on-surface-variant">Create and manage subscription plans.</p>
          {planCount !== null && (
            <p className="mt-3 text-body-sm font-medium text-primary">{planCount} active plans</p>
          )}
        </Link>

        <Link
          to={ROUTES.ADMIN_ACCOUNTS}
          className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs transition-shadow hover:shadow-md"
        >
          <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <UserCog size={24} />
          </div>
          <h2 className="mb-2 text-body-lg font-semibold text-on-background">Users</h2>
          <p className="text-body-sm text-on-surface-variant">View accounts and grant or revoke admin access.</p>
          {userCount !== null && (
            <p className="mt-3 text-body-sm font-medium text-primary">{userCount} users</p>
          )}
        </Link>
      </div>
    </AppShell>
  );
}
