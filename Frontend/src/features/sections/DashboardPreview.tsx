import {
  BarChart3,
  ChevronRight,
  Database,
  Folder,
  Inbox,
  Layers,
  LayoutDashboard,
  Plus,
  Sparkles,
} from 'lucide-react';
import logoSrc from '../../assets/images/IconTransNoText.png';

const bars = [42, 68, 50, 84, 60, 96, 74];
const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul'];

const folders = [
  { label: 'All charts', count: 12, icon: Layers, active: false },
  { label: 'Revenue', count: 5, icon: Folder, active: true },
  { label: 'Operations', count: 4, icon: Folder, active: false },
  { label: 'Unfiled', count: 3, icon: Inbox, active: false },
];

function LineChart() {
  return (
    <svg viewBox="0 0 320 120" className="h-full w-full" role="img" aria-label="Line chart of monthly revenue">
      <defs>
        <linearGradient id="landing-revenue-fill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#3b82f6" stopOpacity="0.28" />
          <stop offset="100%" stopColor="#3b82f6" stopOpacity="0" />
        </linearGradient>
      </defs>
      {[28, 58, 88].map((y) => (
        <line key={y} x1="0" y1={y} x2="320" y2={y} stroke="#e2e8f0" strokeWidth="1" />
      ))}
      <path
        d="M0 92 C 28 86, 48 70, 72 66 S 120 40, 156 48 S 214 22, 250 28 S 294 14, 320 12 V 120 H 0 Z"
        fill="url(#landing-revenue-fill)"
      />
      <path
        className="landing-draw"
        d="M0 92 C 28 86, 48 70, 72 66 S 120 40, 156 48 S 214 22, 250 28 S 294 14, 320 12"
        fill="none"
        stroke="#1d4ed8"
        strokeWidth="2.5"
        strokeLinecap="round"
        pathLength={1}
      />
    </svg>
  );
}

function BarChart() {
  return (
    <div className="flex h-full items-end gap-2" role="img" aria-label="Bar chart of orders by month">
      {bars.map((height, index) => (
        <div key={months[index]} className="flex h-full min-w-0 flex-1 flex-col justify-end gap-1.5">
          <div className="flex h-full items-end">
            <div
              className="landing-bar w-full rounded-t-md bg-gradient-to-t from-blue-800 to-blue-500"
              style={{ height: `${height}%`, animationDelay: `${index * 70}ms` }}
            />
          </div>
          <span className="text-center text-[10px] text-slate-400">{months[index]}</span>
        </div>
      ))}
    </div>
  );
}

export default function DashboardPreview() {
  return (
    <div className="relative mx-auto w-full max-w-6xl px-4 sm:px-6">
      <div
        className="pointer-events-none absolute left-6 top-8 z-0 hidden w-60 -rotate-6 xl:block"
        aria-hidden="true"
      >
        <div className="landing-float rounded-3xl border border-white/70 bg-white p-4 shadow-[0_24px_60px_-28px_rgba(15,23,42,0.55)]">
          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-400">Chart folders</p>
          <ul className="mt-3 space-y-1">
            {folders.map((folder) => {
              const Icon = folder.icon;
              return (
                <li
                  key={folder.label}
                  className={`flex items-center gap-2 rounded-xl px-2.5 py-2 text-sm ${
                    folder.active ? 'bg-slate-100 font-medium text-slate-900' : 'text-slate-600'
                  }`}
                >
                  <Icon className={`size-4 ${folder.active ? 'text-blue-700' : 'text-slate-400'}`} />
                  <span className="min-w-0 flex-1 truncate">{folder.label}</span>
                  <span className="text-xs tabular-nums text-slate-400">{folder.count}</span>
                </li>
              );
            })}
          </ul>
        </div>
      </div>

      <div
        className="pointer-events-none absolute right-6 top-16 z-0 hidden w-56 rotate-6 xl:block"
        aria-hidden="true"
      >
        <div className="landing-float landing-float-delay rounded-3xl border border-white/70 bg-white p-4 shadow-[0_24px_60px_-28px_rgba(15,23,42,0.55)]">
          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-400">Connection</p>
          <div className="mt-3 flex items-start gap-3">
            <div className="flex size-9 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
              <Database className="size-4" />
            </div>
            <div>
              <p className="text-sm font-semibold text-slate-900">Warehouse</p>
              <p className="text-xs text-slate-500">PostgreSQL</p>
            </div>
          </div>
          <p className="mt-3 inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-800">
            <span className="size-1.5 rounded-full bg-blue-600" />
            Connected
          </p>
          <div className="mt-4 rounded-2xl border border-slate-200 bg-slate-50 px-3 py-2.5">
            <p className="flex items-center gap-1.5 text-[11px] font-medium text-blue-700">
              <Sparkles className="size-3.5" />
              Prompt
            </p>
            <p className="mt-1 text-sm text-slate-700">Show revenue by month</p>
          </div>
        </div>
      </div>

      <div className="relative z-10 mx-auto max-w-4xl overflow-hidden rounded-[28px] border border-white/70 bg-white text-slate-900 shadow-[0_40px_90px_-36px_rgba(7,17,31,0.65)]">
        <div className="flex min-h-[420px] md:min-h-[460px]">
          <aside className="hidden w-[200px] shrink-0 flex-col border-r border-slate-200 bg-white md:flex">
            <div className="flex items-center gap-2.5 px-4 py-4">
              <img src={logoSrc} alt="" className="size-8 object-contain" />
              <div className="min-w-0 leading-tight">
                <p className="truncate text-sm font-semibold">AI Dashboard</p>
                <p className="truncate text-xs text-slate-500">Workspace</p>
              </div>
            </div>
            <div className="px-3 pb-4">
              <p className="px-2 pb-1.5 text-[11px] font-medium uppercase tracking-wider text-slate-400">Overview</p>
              <div className="flex items-center gap-2 rounded-lg bg-slate-100 px-2.5 py-2 text-sm font-medium text-slate-900">
                <LayoutDashboard className="size-4 text-blue-700" aria-hidden="true" />
                Dashboard
              </div>
              <p className="mt-4 px-2 pb-1.5 text-[11px] font-medium uppercase tracking-wider text-slate-400">Data</p>
              <div className="flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm text-slate-600">
                <BarChart3 className="size-4" aria-hidden="true" />
                Charts
              </div>
            </div>
          </aside>

          <div className="min-w-0 flex-1 bg-[#f8fafc]">
            <div className="flex h-14 items-center gap-3 border-b border-slate-200 bg-white/90 px-3 sm:px-4">
              <div className="flex min-w-0 items-center gap-1.5 text-sm text-slate-500">
                <span className="hidden sm:inline">Dashboards</span>
                <ChevronRight className="hidden size-3.5 sm:inline" aria-hidden="true" />
                <span className="truncate font-medium text-slate-900">Sales overview</span>
              </div>
              <div className="ml-auto inline-flex h-8 items-center gap-1 rounded-lg bg-slate-900 px-2.5 text-xs font-medium text-white">
                <Plus className="size-3.5" aria-hidden="true" />
                <span className="hidden sm:inline">Create</span>
              </div>
              <div className="flex size-7 items-center justify-center rounded-full bg-blue-700 text-[11px] font-semibold text-white">
                AD
              </div>
            </div>

            <div className="grid gap-3 p-3 sm:p-4 lg:grid-cols-5">
              <article className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm lg:col-span-3">
                <div className="mb-2 flex items-center justify-between gap-2">
                  <h2 className="text-sm font-medium text-slate-900">Revenue by month</h2>
                  <span className="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-medium text-blue-800">Line</span>
                </div>
                <div className="h-36 sm:h-44">
                  <LineChart />
                </div>
              </article>

              <article className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm lg:col-span-2">
                <div className="mb-2 flex items-center justify-between gap-2">
                  <h2 className="text-sm font-medium text-slate-900">Orders by month</h2>
                  <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600">Bar</span>
                </div>
                <div className="h-36 sm:h-44">
                  <BarChart />
                </div>
              </article>

              <article className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm lg:col-span-5">
                <div className="flex flex-wrap items-center gap-2">
                  <Sparkles className="size-4 text-blue-700" aria-hidden="true" />
                  <p className="text-sm text-slate-600">
                    <span className="font-medium text-slate-900">Prompt.</span> Show revenue by month for the last year
                  </p>
                  <span className="ml-auto hidden text-xs text-slate-400 sm:inline">Saved chart · Revenue folder</span>
                </div>
              </article>
            </div>
          </div>
        </div>
      </div>
      <p className="relative z-10 mx-auto mt-4 max-w-4xl text-center text-sm text-slate-500">
        A preview of the workspace: dashboards, charts, folders, and connections.
      </p>
    </div>
  );
}
