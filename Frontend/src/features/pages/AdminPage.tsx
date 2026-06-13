import DashboardHeader from '../layouts/DashboardHeader';
import { ROUTES } from '../routes';
import { Link } from 'react-router-dom';
import { ArrowLeft, Users, Shield, Building2 } from 'lucide-react';

export default function AdminPage() {
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

            <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
              <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <Building2 size={24} />
              </div>
              <h2 className="mb-2 text-body-lg font-semibold text-on-background">Company</h2>
              <p className="text-body-sm text-on-surface-variant">View and edit your company details and subscription.</p>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
