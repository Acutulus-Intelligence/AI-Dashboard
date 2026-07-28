import AppShell from '../layouts/AppShell';
import CompanyUsersSection from '../sections/CompanyUsersSection';
import { ROUTES } from '../routes';

export default function AdminUsersPage() {
  return (
    <AppShell
      breadcrumbs={[
        { label: 'Administration', to: ROUTES.ADMIN },
        { label: 'Team members' },
      ]}
    >
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Team members</h1>
        <p className="text-muted-foreground text-sm">
          Invite teammates and manage their access to your company workspace.
        </p>
      </div>

      <CompanyUsersSection />
    </AppShell>
  );
}
