import { ChevronRight } from 'lucide-react';
import { dashboardControls, quickStats } from '../../../data/customerDashboard.js';

export default function DashboardHeader() {
  return (
    <section className="mb-8">
      <div className="flex flex-col gap-6 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <nav className="mb-2 flex items-center gap-2 text-on-surface-variant" aria-label="Breadcrumb">
            <span className="font-mono text-label-caps">Platform</span>
            <ChevronRight className="size-4" aria-hidden="true" />
            <span className="font-mono text-label-caps font-bold text-secondary">Dashboards</span>
          </nav>
          <h1 className="text-headline-lg font-semibold text-primary">Dashboards</h1>
        </div>

        <div className="flex flex-wrap gap-3">
          {dashboardControls.map((control) => {
            const Icon = control.icon;

            return (
              <button
                key={control.label}
                type="button"
                className="flex items-center gap-2 rounded-lg border border-outline px-4 py-3 font-mono text-label-caps text-on-surface transition-all hover:bg-surface-container"
              >
                <Icon className="size-4" aria-hidden="true" />
                {control.label}
              </button>
            );
          })}
        </div>
      </div>

      <div className="mt-6 grid gap-4 md:grid-cols-3">
        {quickStats.map((stat) => (
          <div key={stat.label} className="rounded-lg border border-outline-variant bg-surface-container-lowest p-4">
            <div className={`mb-3 h-1 w-10 rounded-full ${stat.accent}`} />
            <p className="font-mono text-label-caps uppercase text-on-surface-variant">{stat.label}</p>
            <p className="mt-2 font-mono text-2xl font-semibold text-on-background">{stat.value}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
