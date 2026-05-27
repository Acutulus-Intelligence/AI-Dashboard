import { networkIcon } from '../../../data/customerDashboard.js';

const NetworkIcon = networkIcon;

export default function VisualDashboardCard({ dashboard }) {
  const Icon = dashboard.icon;

  return (
    <article className="hover-card group flex h-[280px] flex-col overflow-hidden rounded-lg border border-outline-variant bg-surface-container-lowest transition-all md:col-span-2">
      <div className="relative h-32 overflow-hidden bg-surface-container">
        <div className="absolute inset-0 z-10 bg-gradient-to-t from-surface-container-lowest to-transparent" />
        <div className="absolute inset-0 grid place-items-center bg-primary-container">
          <div className="relative size-28">
            <NetworkIcon className="absolute left-1/2 top-1/2 size-20 -translate-x-1/2 -translate-y-1/2 text-cyan-action/80" aria-hidden="true" />
            <span className="absolute left-2 top-6 size-3 rounded-full bg-cyan-action shadow-[0_0_18px_rgba(6,182,212,0.9)]" />
            <span className="absolute right-4 top-2 size-2 rounded-full bg-tertiary shadow-[0_0_16px_rgba(57,128,244,0.8)]" />
            <span className="absolute bottom-4 left-9 size-2 rounded-full bg-emerald-400 shadow-[0_0_16px_rgba(52,211,153,0.8)]" />
          </div>
        </div>
      </div>

      <div className="flex flex-1 flex-col justify-between p-6">
        <div>
          <h2 className="text-body-lg font-bold transition-colors group-hover:text-secondary">{dashboard.title}</h2>
          <p className="mt-2 text-body-sm text-on-surface-variant">{dashboard.description}</p>
        </div>
        <div className="flex items-center justify-between gap-4 border-t border-outline-variant pt-4">
          <div className="flex min-w-0 items-center gap-2">
            <Icon className="size-4 shrink-0 text-cyan-action" fill="currentColor" aria-hidden="true" />
            <span className="truncate font-mono text-label-caps text-cyan-action">{dashboard.category}</span>
          </div>
          <span className="shrink-0 font-mono text-data-point text-on-surface-variant">{dashboard.modified}</span>
        </div>
      </div>
    </article>
  );
}
