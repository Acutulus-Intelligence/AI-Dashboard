import {
  ChartArea,
  ChartBar,
  ChartLine,
  ChartPie,
  ChartScatter,
  Folder,
  Gauge,
  Lock,
  Map,
  Radar,
  Table2,
  Users,
  type LucideIcon,
} from 'lucide-react';
import Reveal from '../components/Reveal';

const chartTypes: { label: string; icon: LucideIcon }[] = [
  { label: 'Bar', icon: ChartBar },
  { label: 'Line', icon: ChartLine },
  { label: 'Area', icon: ChartArea },
  { label: 'Pie', icon: ChartPie },
  { label: 'Radar', icon: Radar },
  { label: 'Radial', icon: Gauge },
  { label: 'Scatter', icon: ChartScatter },
  { label: 'Map', icon: Map },
  { label: 'Table', icon: Table2 },
];

const folderRows = [
  { name: 'All charts', count: 12 },
  { name: 'Revenue', count: 5 },
  { name: 'Operations', count: 4 },
  { name: 'Unfiled', count: 3 },
];

export default function BentoSection() {
  return (
    <section id="platform" className="scroll-mt-24 bg-[#f4f7fb] px-5 py-20 sm:px-6 md:py-28">
      <div className="mx-auto max-w-6xl">
        <Reveal>
          <div className="max-w-2xl">
            <p className="inline-flex rounded-full border border-blue-100 bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-blue-800">
              Platform
            </p>
            <h2 className="mt-4 text-balance text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl md:text-5xl">
              A workspace for charts, folders, and the people using them
            </h2>
          </div>
        </Reveal>

        <div className="mt-12 grid gap-4 md:grid-cols-2 lg:grid-cols-6">
          <Reveal className="md:col-span-2 lg:col-span-4" delay={0}>
            <article className="landing-card h-full overflow-hidden rounded-3xl border border-slate-200/80 bg-gradient-to-br from-white to-blue-50 p-6 shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)] sm:p-8">
              <h3 className="text-xl font-semibold tracking-tight text-slate-900">Ask, then place it</h3>
              <p className="mt-2 max-w-md text-sm leading-relaxed text-slate-600">
                Describe the chart in everyday language. Review the query, save it, and drop the widget onto a dashboard.
              </p>
              <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-blue-700">Prompt</p>
                <p className="mt-1 text-sm text-slate-800">Revenue by month for the last year</p>
              </div>
              <div className="mt-3 rounded-2xl border border-slate-200 bg-white p-4">
                <div className="mb-3 flex items-center justify-between text-xs text-slate-500">
                  <span>Revenue by month</span>
                  <span className="rounded-full bg-blue-50 px-2 py-0.5 font-medium text-blue-800">Line</span>
                </div>
                <svg viewBox="0 0 320 90" className="h-28 w-full" role="img" aria-label="Sample revenue line chart">
                  <path
                    d="M0 70 C 40 64, 70 40, 110 44 S 180 18, 230 24 S 290 12, 320 8"
                    fill="none"
                    stroke="#1d4ed8"
                    strokeWidth="2.5"
                    strokeLinecap="round"
                  />
                </svg>
              </div>
            </article>
          </Reveal>

          <Reveal className="lg:col-span-2" delay={80}>
            <article className="landing-card flex h-full flex-col rounded-3xl border border-slate-200/80 bg-white p-6 shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)]">
              <div className="flex size-11 items-center justify-center rounded-2xl bg-blue-50 text-blue-700">
                <Lock className="size-5" aria-hidden="true" />
              </div>
              <h3 className="mt-5 text-xl font-semibold tracking-tight text-slate-900">Your rows stay put</h3>
              <p className="mt-2 text-sm leading-relaxed text-slate-600">
                Schema inspection sends tables, columns, and relationships. It does not send row data to the model.
              </p>
              <ul className="mt-5 flex flex-wrap gap-2">
                {['Tables', 'Columns', 'Foreign keys'].map((chip) => (
                  <li key={chip} className="rounded-full bg-blue-50 px-3 py-1 text-xs font-medium text-blue-800">
                    {chip}
                  </li>
                ))}
                <li className="rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-500 line-through">
                  Rows
                </li>
              </ul>
            </article>
          </Reveal>

          <Reveal className="lg:col-span-2" delay={0}>
            <article className="landing-card h-full rounded-3xl border border-slate-200/80 bg-white p-6 shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)]">
              <h3 className="text-lg font-semibold tracking-tight text-slate-900">Nine ways to see it</h3>
              <p className="mt-1 text-sm text-slate-600">The chart catalog in the app today.</p>
              <ul className="mt-4 grid grid-cols-3 gap-2">
                {chartTypes.map((chart) => {
                  const Icon = chart.icon;
                  return (
                    <li
                      key={chart.label}
                      className="flex flex-col items-center gap-1 rounded-2xl bg-slate-50 px-1 py-2.5 text-center"
                    >
                      <Icon className="size-4 text-blue-700" aria-hidden="true" />
                      <span className="text-[11px] font-medium text-slate-600">{chart.label}</span>
                    </li>
                  );
                })}
              </ul>
            </article>
          </Reveal>

          <Reveal className="lg:col-span-2" delay={80}>
            <article className="landing-card h-full rounded-3xl border border-slate-200/80 bg-white p-6 shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)]">
              <h3 className="text-lg font-semibold tracking-tight text-slate-900">Chart folders</h3>
              <p className="mt-1 text-sm text-slate-600">File saved charts so the library stays usable.</p>
              <ul className="mt-4 space-y-1.5">
                {folderRows.map((folder, index) => (
                  <li
                    key={folder.name}
                    className={`flex items-center gap-2 rounded-xl px-2.5 py-2 text-sm ${
                      index === 1 ? 'bg-slate-100 font-medium text-slate-900' : 'text-slate-600'
                    }`}
                  >
                    <Folder className={`size-4 ${index === 1 ? 'text-blue-700' : 'text-slate-400'}`} aria-hidden="true" />
                    <span className="min-w-0 flex-1">{folder.name}</span>
                    <span className="text-xs tabular-nums text-slate-400">{folder.count}</span>
                  </li>
                ))}
              </ul>
            </article>
          </Reveal>

          <Reveal className="lg:col-span-2" delay={160}>
            <article className="landing-card h-full rounded-3xl border border-slate-200/80 bg-white p-6 shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)]">
              <div className="flex size-11 items-center justify-center rounded-2xl bg-blue-50 text-blue-700">
                <Users className="size-5" aria-hidden="true" />
              </div>
              <h3 className="mt-5 text-lg font-semibold tracking-tight text-slate-900">Company workspace</h3>
              <p className="mt-2 text-sm leading-relaxed text-slate-600">
                Invite people, assign custom roles, and share connections inside the company.
              </p>
              <ul className="mt-4 space-y-2 text-sm text-slate-700">
                <li className="flex items-center justify-between rounded-xl bg-slate-50 px-3 py-2">
                  <span>Owner</span>
                  <span className="text-xs text-slate-500">System role</span>
                </li>
                <li className="flex items-center justify-between rounded-xl bg-slate-50 px-3 py-2">
                  <span>Custom roles</span>
                  <span className="text-xs text-slate-500">You define them</span>
                </li>
              </ul>
            </article>
          </Reveal>

          <Reveal className="md:col-span-2 lg:col-span-6" delay={40}>
            <article className="landing-card flex flex-col gap-6 rounded-3xl border border-slate-200/80 bg-white p-6 shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)] sm:flex-row sm:items-center sm:justify-between sm:p-8">
              <div className="max-w-xl">
                <h3 className="text-xl font-semibold tracking-tight text-slate-900">Drag, resize, and keep every dashboard</h3>
                <p className="mt-2 text-sm leading-relaxed text-slate-600">
                  Widgets snap into a grid. Opening a dashboard runs its saved queries again, so the charts match the database now.
                </p>
              </div>
              <div className="grid w-full gap-3 sm:max-w-md sm:grid-cols-3">
                {['Sales overview', 'Operations', 'Finance'].map((name, index) => (
                  <div
                    key={name}
                    className={`rounded-2xl border px-3 py-4 ${
                      index === 0 ? 'border-blue-200 bg-blue-50' : 'border-slate-200 bg-slate-50'
                    }`}
                  >
                    <div className="mb-3 h-8 rounded-lg bg-gradient-to-r from-blue-200 to-blue-50" />
                    <p className="text-xs font-medium text-slate-700">{name}</p>
                  </div>
                ))}
              </div>
            </article>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
