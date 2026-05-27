import { searchIcon, topbarActions } from '../../../data/customerDashboard.js';

const SearchIcon = searchIcon;

export default function TopAppBar() {
  return (
    <header className="fixed right-0 top-0 z-40 flex w-full flex-col gap-3 border-b border-outline-variant bg-surface/95 px-gutter py-3 backdrop-blur-md lg:left-[280px] lg:h-16 lg:w-[calc(100%-280px)] lg:flex-row lg:items-center lg:justify-between lg:border-b-0 lg:py-0">
      <div className="flex items-center justify-between gap-4 lg:hidden">
        <a href="/" className="text-headline-md font-bold text-primary">
          Cognitive Nexus
        </a>
        <span className="rounded-full bg-secondary-container/30 px-3 py-1 font-mono text-label-caps text-secondary">
          Open Access
        </span>
      </div>

      <div className="max-w-xl flex-1">
        <label className="relative block">
          <span className="sr-only">Search dashboards</span>
          <SearchIcon className="absolute left-4 top-1/2 size-5 -translate-y-1/2 text-on-surface-variant" aria-hidden="true" />
          <input
            className="w-full rounded-lg border-none bg-surface-container-low py-3 pl-12 pr-4 text-body-sm outline-none transition-all focus:ring-2 focus:ring-cyan-action"
            placeholder="Search dashboards, analytics, or nodes..."
            type="search"
          />
        </label>
      </div>

      <div className="hidden items-center gap-6 lg:flex">
        {topbarActions.map((action) => {
          const Icon = action.icon;

          return (
            <button
              key={action.label}
              type="button"
              className="text-on-surface-variant transition-colors hover:text-secondary"
              aria-label={action.label}
            >
              <Icon className="size-6" aria-hidden="true" />
            </button>
          );
        })}
      </div>
    </header>
  );
}
