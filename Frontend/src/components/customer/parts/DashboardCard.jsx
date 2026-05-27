import { cardActionIcon } from '../../../data/customerDashboard.js';

const ActionIcon = cardActionIcon;

export default function DashboardCard({ dashboard }) {
  const Icon = dashboard.icon;

  return (
    <article
      className={`hover-card group flex h-[280px] flex-col justify-between rounded-lg bg-surface-container-lowest p-6 transition-all ${
        dashboard.featured
          ? 'ai-dashboard-card col-span-1 border-y border-r border-outline-variant border-l-cyan-action md:col-span-2'
          : 'border border-outline-variant'
      }`}
    >
      <div>
        <div className="flex items-start justify-between gap-4">
          <span
            className={`rounded-lg p-2 transition-transform group-hover:scale-110 ${
              dashboard.featured ? 'bg-secondary-container/20 text-cyan-action' : 'bg-surface-container text-secondary'
            }`}
          >
            <Icon className="size-6" fill={dashboard.featured ? 'currentColor' : 'none'} aria-hidden="true" />
          </span>
          <button
            type="button"
            className="text-on-surface-variant opacity-0 transition-opacity hover:text-secondary group-hover:opacity-100"
            aria-label={`More actions for ${dashboard.title}`}
          >
            <ActionIcon className="size-5" aria-hidden="true" />
          </button>
        </div>
        <h2
          className={`mt-6 font-bold transition-colors group-hover:text-secondary ${
            dashboard.featured ? 'text-headline-md' : 'text-body-lg'
          }`}
        >
          {dashboard.title}
        </h2>
        <p className="mt-2 line-clamp-2 text-body-sm text-on-surface-variant">{dashboard.description}</p>
      </div>

      <div className="flex items-center justify-between gap-4 border-t border-outline-variant pt-4">
        <div className="flex min-w-0 items-center gap-2">
          {dashboard.live && <span className="size-2 shrink-0 animate-pulse rounded-full bg-green-500" />}
          <span className="truncate font-mono text-label-caps uppercase text-on-surface-variant">
            {dashboard.category}
          </span>
        </div>
        <span className="shrink-0 font-mono text-data-point text-on-surface-variant">{dashboard.modified}</span>
      </div>
    </article>
  );
}
