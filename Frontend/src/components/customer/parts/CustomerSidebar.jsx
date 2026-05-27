import { createIcon, sidebarItems } from '../../../data/customerDashboard.js';

const CreateIcon = createIcon;

export default function CustomerSidebar() {
  return (
    <aside className="fixed left-0 top-0 z-50 hidden h-full w-[280px] flex-col border-r border-outline-variant bg-surface-container-lowest px-4 py-6 lg:flex">
      <div className="mb-16">
        <a href="/" className="text-headline-md font-bold text-primary">
          Cognitive Nexus
        </a>
        <p className="mt-1 text-body-sm text-on-surface-variant">AI Intelligence Platform</p>
      </div>

      <nav className="flex-1 space-y-2" aria-label="Kundnavigation">
        {sidebarItems.map((item) => {
          const Icon = item.icon;

          return (
            <a
              key={item.label}
              className={`flex items-center gap-4 px-4 py-3 transition-all ${
                item.active
                  ? 'border-l-4 border-secondary bg-surface-container font-bold text-secondary'
                  : 'text-on-surface-variant hover:bg-surface-container-low hover:text-on-surface'
              }`}
              href="/app"
            >
              <Icon className="size-5" aria-hidden="true" />
              <span className="text-body-md">{item.label}</span>
            </a>
          );
        })}
      </nav>

      <div className="mt-auto pt-6">
        <button
          type="button"
          className="flex w-full items-center justify-center gap-2 rounded-lg bg-cyan-action px-6 py-4 font-mono text-label-caps text-white transition-all hover:brightness-110 active:scale-95"
        >
          <CreateIcon className="size-4" aria-hidden="true" />
          Create New Dashboard
        </button>

        <div className="mt-8 flex items-center gap-4">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-full border border-outline-variant bg-primary-container font-mono text-label-caps text-on-primary">
            AC
          </div>
          <div className="min-w-0">
            <p className="truncate text-body-sm font-bold">Alex Chen</p>
            <p className="truncate text-xs text-on-surface-variant">Senior Architect</p>
          </div>
        </div>
      </div>
    </aside>
  );
}
