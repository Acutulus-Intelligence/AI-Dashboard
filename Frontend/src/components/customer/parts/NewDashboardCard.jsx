export default function NewDashboardCard({ icon }) {
  const Icon = icon;

  return (
    <button
      type="button"
      className="group flex h-[280px] flex-col items-center justify-center gap-4 rounded-lg border-2 border-dashed border-outline-variant bg-surface-container-low p-6 text-center transition-all hover:bg-surface-container"
    >
      <span className="flex size-16 items-center justify-center rounded-full border-2 border-dashed border-outline-variant text-on-surface-variant transition-all group-hover:border-secondary group-hover:text-secondary">
        <Icon className="size-8" aria-hidden="true" />
      </span>
      <span>
        <span className="block text-body-md font-bold text-on-surface">New Dashboard</span>
        <span className="block text-body-sm text-on-surface-variant">Start with a template or blank</span>
      </span>
    </button>
  );
}
