import DashboardHeader from '../layouts/DashboardHeader';

export default function DashboardPage() {
  return (
    <div className="min-h-screen bg-background">
      <DashboardHeader
        onToggleNavbar={() => {}}
        onNewChart={() => {}}
        onNewDashboard={() => {}}
      />
      <main className="pt-16">
        <div className="mx-auto max-w-container-max px-gutter py-16">
          <h1 className="text-headline-lg font-semibold">Dashboard</h1>
        </div>
      </main>
    </div>
  );
}
