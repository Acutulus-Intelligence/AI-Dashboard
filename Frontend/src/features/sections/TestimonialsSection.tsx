import { ChartColumn, Database, ShieldCheck } from 'lucide-react';
import CountUp from '../components/CountUp';
import Reveal from '../components/Reveal';

const stats = [
  { value: 9, suffix: '', label: 'Chart types in the catalog' },
  { value: 3, suffix: '', label: 'Databases you can connect' },
  { value: 7, suffix: '', label: 'Days in the default free trial' },
  { value: 100, suffix: '%', label: 'Queries kept SELECT-only' },
];

const stories = [
  {
    icon: ChartColumn,
    role: 'Analytics',
    text: 'Ask for the top customers this quarter and get a chart you can drop on a dashboard, instead of a SQL draft and an export.',
  },
  {
    icon: Database,
    role: 'Operations',
    text: 'Charts live in folders, each dashboard stays separate, and the grid is yours to drag and resize.',
  },
  {
    icon: ShieldCheck,
    role: 'Company owner',
    text: 'Invite the team, set roles, and keep each connection inside the company workspace.',
  },
];

export default function TestimonialsSection() {
  return (
    <section id="testimonials" className="scroll-mt-24 bg-white px-5 py-20 sm:px-6 md:py-28">
      <div className="mx-auto max-w-6xl">
        <Reveal>
          <div className="max-w-2xl">
            <p className="inline-flex rounded-full border border-blue-100 bg-blue-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-blue-800">
              Testimonials
            </p>
            <h2 className="mt-4 text-balance text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl md:text-5xl">
              From a question to a dashboard
            </h2>
            <p className="mt-4 text-pretty text-base leading-relaxed text-slate-600 sm:text-lg">
              Describe the chart. Place it on the dashboard. Open it later and the saved query runs again.
            </p>
          </div>
        </Reveal>

        <Reveal delay={80}>
          <dl className="mt-12 grid grid-cols-2 gap-x-6 gap-y-8 rounded-[28px] border border-slate-200 bg-[#f7faff] px-6 py-8 sm:px-10 md:grid-cols-4">
            {stats.map((stat) => (
              <div key={stat.label}>
                <dd className="text-4xl font-semibold tracking-tight text-slate-900 tabular-nums sm:text-5xl">
                  <CountUp value={stat.value} suffix={stat.suffix} />
                </dd>
                <dt className="mt-2 text-sm leading-snug text-slate-600">{stat.label}</dt>
              </div>
            ))}
          </dl>
        </Reveal>

        <div className="mt-5 grid gap-4 md:grid-cols-3">
          {stories.map((story, index) => {
            const Icon = story.icon;
            return (
              <Reveal key={story.role} delay={index * 90} className="h-full">
                <figure className="landing-card flex h-full flex-col rounded-3xl border border-slate-200 bg-white p-6 shadow-[0_16px_40px_-28px_rgba(15,23,42,0.35)]">
                  <div className="flex size-11 items-center justify-center rounded-2xl bg-blue-50 text-blue-700">
                    <Icon className="size-5" aria-hidden="true" />
                  </div>
                  <blockquote className="mt-5 text-sm leading-relaxed text-slate-700">“{story.text}”</blockquote>
                  <figcaption className="mt-5 text-sm font-semibold text-slate-900">{story.role}</figcaption>
                  <p className="text-xs text-slate-500">Example workflow</p>
                </figure>
              </Reveal>
            );
          })}
        </div>
      </div>
    </section>
  );
}
