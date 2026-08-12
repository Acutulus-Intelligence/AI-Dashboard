import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { BadgeDollarSign, Building2, UserCheck, Users, UserX, UserCog } from 'lucide-react';
import AppShell from '../layouts/AppShell';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';
import { getAdminPlans, getAdminStats, getAdminUsers } from '../../lib/api/admin';

interface StatsCard {
  label: string;
  value: number;
  icon: typeof Users;
}

export default function AdminMainPage() {
  const { user } = useAuth();
  const isAdmin = user?.roles.includes('Admin');
  const [planCount, setPlanCount] = useState<number | null>(null);
  const [userCount, setUserCount] = useState<number | null>(null);
  const [stats, setStats] = useState<{ totalUsers: number; individualSubscribedUsers: number; companySubscribedUsers: number; usersWithoutSubscription: number } | null>(null);

  useEffect(() => {
    getAdminPlans()
      .then((plans) => setPlanCount(plans.length))
      .catch(() => {});
    if (isAdmin) {
      getAdminUsers()
        .then((users) => setUserCount(users.length))
        .catch(() => {});
    }
    getAdminStats()
      .then(setStats)
      .catch(() => {});
  }, [isAdmin]);

  const cards: StatsCard[] = stats
    ? [
        { label: 'Total users', value: stats.totalUsers, icon: Users },
        { label: 'Individual subscriptions', value: stats.individualSubscribedUsers, icon: UserCheck },
        { label: 'Company subscriptions', value: stats.companySubscribedUsers, icon: Building2 },
        { label: 'Without subscription', value: stats.usersWithoutSubscription, icon: UserX },
      ]
    : [];

  return (
    <AppShell breadcrumbs={[{ label: 'Administration' }]}>
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Administration</h1>
        <p className="text-muted-foreground text-sm">
          Manage subscription plans and user accounts.
        </p>
      </div>

      {stats && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {cards.map((card) => (
            <div
              key={card.label}
              className="rounded-2xl border border-outline-variant bg-surface p-5 shadow-xs"
            >
              <div className="mb-3 flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <card.icon size={20} />
              </div>
              <p className="text-2xl font-semibold text-on-background">{card.value}</p>
              <p className="text-body-sm text-on-surface-variant">{card.label}</p>
            </div>
          ))}
        </div>
      )}

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

        {isAdmin && (
          <Link
            to={ROUTES.ADMIN_ACCOUNTS}
            className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs transition-shadow hover:shadow-md"
          >
            <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <UserCog size={24} />
            </div>
            <h2 className="mb-2 text-body-lg font-semibold text-on-background">Users</h2>
            <p className="text-body-sm text-on-surface-variant">Create accounts, view admins and grant or revoke admin access.</p>
            {userCount !== null && (
              <p className="mt-3 text-body-sm font-medium text-primary">{userCount} users</p>
            )}
          </Link>
        )}
      </div>
    </AppShell>
  );
}