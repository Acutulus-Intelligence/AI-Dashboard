import AiPulseIndicator from './parts/AiPulseIndicator.jsx';
import CustomerSidebar from './parts/CustomerSidebar.jsx';
import DashboardHeader from './parts/DashboardHeader.jsx';
import DashboardGrid from './parts/DashboardGrid.jsx';
import TopAppBar from './parts/TopAppBar.jsx';

export default function DashboardsPage() {
  return (
    <div className="min-h-screen bg-background text-on-surface">
      <CustomerSidebar />
      <TopAppBar />
      <main className="min-h-screen pt-[136px] lg:ml-[280px] lg:pt-16">
        <div className="px-gutter py-6">
          <DashboardHeader />
          <DashboardGrid />
        </div>
      </main>
      <AiPulseIndicator />
    </div>
  );
}
