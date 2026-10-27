import { Database, FolderKanban, LayoutDashboard, Sparkles, type LucideIcon } from 'lucide-react';
import Reveal from '../components/Reveal';

const features: { title: string; description: string; icon: LucideIcon }[] = [
  {
    title: 'Secure connections',
    description:
      'Connect PostgreSQL, MySQL, or SQL Server. Credentials are encrypted before they are stored.',
    icon: Database,
  },
  {
    title: 'Plain-language charts',
    description:
      'Ask for the view you need. The assistant picks a chart type and returns SELECT-only SQL.',
    icon: Sparkles,
  },
  {
    title: 'Dashboards you arrange',
    description:
      'Drag and resize widgets, keep multiple dashboards, and place a saved chart on any of them.',
    icon: LayoutDashboard,
  },
  {
    title: 'Folders and roles',
    description:
      'Group charts into folders. Company workspaces add members, custom roles, and shared connections.',
    icon: FolderKanban,
  },
];

function IconMedallion({ icon: Icon }: { icon: LucideIcon }) {
  return (
    <div className="relative mx-auto mb-6 size-[4.5rem]" aria-hidden="true">
      <div className="absolute inset-1 rotate-6 rounded-[22px] bg-gradient-to-br from-blue-500 to-blue-800 shadow-lg shadow-blue-700/30" />
      <div className="absolute inset-1 -rotate-3 rounded-[22px] bg-gradient-to-br from-sky-100 to-white shadow-md" />
      <div className="relative flex size-[4.5rem] items-center justify-center">
        <div className="flex size-14 items-center justify-center rounded-2xl bg-white text-blue-700 shadow-[0_10px_24px_-12px_rgba(29,78,216,0.85)]">
          <Icon className="size-7" />
        </div>
      </div>
    </div>
  );
}

export default function FeaturesSection() {
  return (
    <section id="features" className="scroll-mt-24 bg-white px-5 py-20 sm:px-6 md:py-28">
      <div className="mx-auto max-w-6xl">
        <Reveal>
          <div className="mx-auto max-w-2xl text-center">
            <p className="inline-flex rounded-full border border-blue-100 bg-blue-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-blue-800">
              Features
            </p>
            <h2 className="mt-4 text-balance text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl md:text-5xl">
              Everything between your database and a dashboard
            </h2>
            <p className="mt-4 text-pretty text-base leading-relaxed text-slate-600 sm:text-lg">
              Connect a source, ask in plain language, and arrange the result. Schema inspection sends
              metadata only — your rows stay in your database.
            </p>
          </div>
        </Reveal>

        <div className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((feature, index) => (
            <Reveal key={feature.title} delay={index * 90} className="h-full">
              <article className="landing-card h-full rounded-3xl border border-slate-200/80 bg-white px-6 py-8 text-center shadow-[0_16px_40px_-28px_rgba(15,23,42,0.45)]">
                <IconMedallion icon={feature.icon} />
                <h3 className="text-lg font-semibold tracking-tight text-slate-900">{feature.title}</h3>
                <p className="mt-3 text-sm leading-relaxed text-slate-600">{feature.description}</p>
              </article>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}
