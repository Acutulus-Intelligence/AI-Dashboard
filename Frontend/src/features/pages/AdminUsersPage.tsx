import DashboardHeader from '../layouts/DashboardHeader';
import CompanyUsersSection from '../sections/CompanyUsersSection';
import { ROUTES } from '../routes';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';

export default function AdminUsersPage() {
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
              to={ROUTES.ADMIN}
              className="inline-flex items-center justify-center rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container"
            >
              <ArrowLeft size={18} />
            </Link>
            <h1 className="text-headline-md font-bold text-on-background">Team Members</h1>
          </div>

          <CompanyUsersSection />
        </div>
      </main>
    </div>
  );
}
