import { Link } from 'react-router-dom';
import { ROUTES } from '../routes';

export default function CtaSection() {
  return (
    <section className="bg-white px-5 py-16 sm:px-6 md:py-24">
      <div className="relative mx-auto max-w-6xl overflow-hidden rounded-[32px] bg-[#07111f] px-6 py-14 text-center text-white shadow-[0_30px_80px_-36px_rgba(7,17,31,0.8)] sm:px-12 md:py-20">
        <div className="pointer-events-none absolute inset-0" aria-hidden="true">
          <div className="landing-orb absolute -left-16 top-0 size-64 rounded-full bg-blue-500/30 blur-3xl" />
          <div className="landing-orb landing-orb-delay absolute -right-10 bottom-0 size-56 rounded-full bg-sky-400/25 blur-3xl" />
        </div>
        <div className="relative">
          <h2 className="text-balance text-3xl font-semibold tracking-tight sm:text-4xl md:text-5xl">
            Ready to transform your data?
          </h2>
          <p className="mx-auto mt-4 max-w-2xl text-pretty text-base leading-relaxed text-slate-200 sm:text-lg">
            Connect a database, describe a chart, and put it on a dashboard. Start with a free trial.
          </p>
          <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <Link
              to={ROUTES.PRICING}
              className="inline-flex min-h-12 w-full items-center justify-center rounded-full bg-white px-6 text-sm font-semibold text-slate-900 transition-colors hover:bg-sky-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white sm:w-auto"
            >
              Get started
            </Link>
            <Link
              to={ROUTES.LOGIN}
              className="inline-flex min-h-12 w-full items-center justify-center rounded-full border border-white/25 px-6 text-sm font-semibold text-white transition-colors hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white sm:w-auto"
            >
              Log in
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
