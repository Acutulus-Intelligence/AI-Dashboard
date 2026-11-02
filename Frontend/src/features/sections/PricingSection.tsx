import { Link } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import Reveal from '../components/Reveal';
import { ROUTES } from '../routes';

export default function PricingSection() {
  return (
    <section className="bg-[#f7f9fc] px-5 py-20 sm:px-6 md:py-24">
      <Reveal>
        <div className="mx-auto max-w-2xl text-center">
          <p className="inline-flex rounded-full border border-blue-100 bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-blue-800">
            Pricing
          </p>
          <h2 className="mt-4 text-balance text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl">
            Plans for individuals and companies
          </h2>
          <p className="mt-4 text-pretty text-base leading-relaxed text-slate-600 sm:text-lg">
            Compare plans, start a free trial, and choose monthly or yearly billing on the pricing page.
          </p>
          <Link
            to={ROUTES.PRICING}
            className="mt-8 inline-flex min-h-12 items-center justify-center gap-2 rounded-full bg-blue-700 px-6 text-sm font-semibold text-white transition-colors hover:bg-blue-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700"
          >
            View pricing
            <ArrowRight className="size-4" aria-hidden="true" />
          </Link>
        </div>
      </Reveal>
    </section>
  );
}
