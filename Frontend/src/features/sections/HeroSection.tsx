import { Link } from 'react-router-dom';
import { ArrowUpRight } from 'lucide-react';
import HashLink from '../components/HashLink';
import { ROUTES } from '../routes';

export default function HeroSection() {
  return (
    <section id="product" className="relative scroll-mt-24 overflow-hidden bg-[#07111f] pb-36 pt-28 text-white sm:pb-44 sm:pt-32 md:pb-52">
      <div className="pointer-events-none absolute inset-0" aria-hidden="true">
        <div className="absolute inset-0 bg-[linear-gradient(180deg,#07111f_0%,#0c2348_52%,#123a7a_100%)]" />
        <div className="landing-orb absolute -left-24 top-8 size-[28rem] rounded-full bg-blue-500/30 blur-3xl" />
        <div className="landing-orb landing-orb-delay absolute -right-16 top-24 size-[24rem] rounded-full bg-sky-400/25 blur-3xl" />
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(96,165,250,0.35),transparent_58%)]" />
        <div className="absolute inset-0 opacity-40 [background-image:radial-gradient(rgba(255,255,255,0.22)_1px,transparent_1px)] [background-size:22px_22px] [mask-image:linear-gradient(to_bottom,black,transparent_78%)]" />
      </div>

      <div className="relative z-10 mx-auto max-w-4xl px-5 text-center sm:px-6">
        <p className="mb-6 inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1.5 text-xs font-medium text-sky-100 shadow-[inset_0_1px_0_rgba(255,255,255,0.18)] backdrop-blur-sm sm:text-sm">
          <span className="size-1.5 rounded-full bg-sky-300" aria-hidden="true" />
          Now in beta: AI visualization
        </p>

        <h1 className="text-balance text-[2.55rem] font-semibold leading-[1.05] tracking-[-0.04em] text-white sm:text-6xl lg:text-7xl">
          From database to <span className="text-sky-200">insights</span> in seconds
        </h1>

        <p className="mx-auto mt-6 max-w-2xl text-pretty text-base leading-relaxed text-slate-200 sm:text-lg">
          Connect PostgreSQL, MySQL, or SQL Server and describe the chart you want. AI Dashboard
          writes a read-only query and places it on a dashboard you can arrange.
        </p>

        <div className="mt-9 flex items-center justify-center gap-3">
          <Link
            to={ROUTES.REGISTER}
            className="inline-flex min-h-12 items-center justify-center rounded-full bg-white px-6 text-sm font-semibold text-slate-900 shadow-[0_12px_30px_-12px_rgba(255,255,255,0.7)] transition-transform hover:bg-sky-50 motion-safe:hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white sm:text-base"
          >
            Start your free trial
          </Link>
          <HashLink
            to="/#demo"
            aria-label="View the product"
            className="inline-flex size-12 items-center justify-center rounded-full border border-white/25 bg-white/10 text-white transition-transform hover:bg-white/20 motion-safe:hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
          >
            <ArrowUpRight className="size-5" aria-hidden="true" />
          </HashLink>
        </div>
      </div>
    </section>
  );
}
