import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Check, Minus } from 'lucide-react';
import Reveal from '../components/Reveal';
import { ROUTES } from '../routes';
import {
  BILLING_PERIOD,
  getPlans,
  type BillingPeriod,
  type SubscriptionPlan,
} from '../../lib/api/subscription';
import { cn } from '@/lib/utils';

const FALLBACK_PLANS: SubscriptionPlan[] = [
  {
    id: 'individual',
    name: 'Individual',
    description: 'Plan for individual users',
    userType: 'Individual',
    monthlyPrice: 19.99,
    yearlyPrice: 199.99,
    maxUsers: null,
    maxDashboards: null,
    maxAiQueriesPerMonth: null,
    isActive: true,
    trialDays: null,
  },
  {
    id: 'company-starter',
    name: 'Company Starter',
    description: 'Starter plan for small companies',
    userType: 'Company',
    monthlyPrice: 49.99,
    yearlyPrice: 499.99,
    maxUsers: 5,
    maxDashboards: null,
    maxAiQueriesPerMonth: null,
    isActive: true,
    trialDays: null,
  },
  {
    id: 'company-enterprise',
    name: 'Company Enterprise',
    description: 'Enterprise plan for large companies',
    userType: 'Company',
    monthlyPrice: 149.99,
    yearlyPrice: 1499.99,
    maxUsers: null,
    maxDashboards: null,
    maxAiQueriesPerMonth: null,
    isActive: true,
    trialDays: null,
  },
];

function isCompanyPlan(plan: SubscriptionPlan) {
  return plan.userType === 1 || plan.userType === 'Company';
}

function isEnterprisePlan(plan: SubscriptionPlan) {
  return plan.name.toLowerCase().includes('enterprise');
}

function sortPlans(plans: SubscriptionPlan[]) {
  return [...plans].sort((a, b) => {
    const enterpriseDelta = Number(isEnterprisePlan(a)) - Number(isEnterprisePlan(b));
    if (enterpriseDelta !== 0) return enterpriseDelta;
    return a.monthlyPrice - b.monthlyPrice;
  });
}

function formatPrice(plan: SubscriptionPlan, billing: BillingPeriod) {
  const price = billing === BILLING_PERIOD.Monthly ? plan.monthlyPrice : plan.yearlyPrice;
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: price % 1 === 0 ? 0 : 2,
  }).format(price);
}

function formatLimit(value: number | null) {
  if (value === null) return 'Unlimited';
  return new Intl.NumberFormat('en-US').format(value);
}

function usersLabel(plan: SubscriptionPlan) {
  if (plan.maxUsers !== null) return formatLimit(plan.maxUsers);
  return isCompanyPlan(plan) ? 'Unlimited' : '1';
}

function trialDays(plan: SubscriptionPlan) {
  if (plan.trialDays === null) return 7;
  return plan.trialDays;
}

function trialLabel(plan: SubscriptionPlan) {
  const days = trialDays(plan);
  if (days <= 0) return 'No trial';
  return `${days} days`;
}

function planHref(plan: SubscriptionPlan) {
  return isEnterprisePlan(plan) ? ROUTES.CONTACT : ROUTES.PRICING;
}

function planAction(plan: SubscriptionPlan) {
  return isEnterprisePlan(plan) ? 'Contact us' : 'Get started';
}

const comparisonRows: { label: string; value: (plan: SubscriptionPlan) => string }[] = [
  { label: 'Users', value: usersLabel },
  { label: 'Dashboards', value: (plan) => formatLimit(plan.maxDashboards) },
  { label: 'AI queries / month', value: (plan) => formatLimit(plan.maxAiQueriesPerMonth) },
  { label: 'Free trial', value: trialLabel },
  { label: 'Workspace', value: (plan) => (isCompanyPlan(plan) ? 'Company' : 'Individual') },
  { label: 'Company roles', value: (plan) => (isCompanyPlan(plan) ? 'Yes' : 'No') },
];

function CellValue({ value }: { value: string }) {
  if (value === 'Yes') {
    return <Check className="mx-auto size-4 text-blue-700" aria-label="Yes" />;
  }
  if (value === 'No') {
    return <Minus className="mx-auto size-4 text-slate-300" aria-label="No" />;
  }
  return <span>{value}</span>;
}

export default function PricingSection() {
  const [billing, setBilling] = useState<BillingPeriod>(BILLING_PERIOD.Monthly);
  const [plans, setPlans] = useState<SubscriptionPlan[]>(FALLBACK_PLANS);

  useEffect(() => {
    let cancelled = false;

    getPlans()
      .then((response) => {
        if (cancelled) return;
        const active = sortPlans(response.filter((plan) => plan.isActive));
        if (active.length > 0) setPlans(active);
      })
      .catch(() => {
        /* Keep the published seed plans if the API is unreachable. */
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const highlightedIndex = plans.length > 2 ? 1 : 0;
  const maxTrial = Math.max(...plans.map(trialDays));
  const period = billing === BILLING_PERIOD.Monthly ? 'month' : 'year';

  return (
    <section id="pricing" className="scroll-mt-24 bg-[#f7f9fc] px-5 py-20 sm:px-6 md:py-28">
      <div className="mx-auto max-w-6xl">
        <Reveal>
          <div className="mx-auto max-w-2xl text-center">
            <p className="inline-flex rounded-full border border-blue-100 bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-blue-800">
              Pricing
            </p>
            <h2 className="mt-4 text-balance text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl md:text-5xl">
              Choose the right plan for your dashboards
            </h2>
            {maxTrial > 0 && (
              <p className="mt-4 text-pretty text-base leading-relaxed text-slate-600 sm:text-lg">
                Start with a {maxTrial}-day free trial, then keep the plan that fits your workflow.
              </p>
            )}
          </div>
        </Reveal>

        <div className="mt-8 flex justify-center">
          <div className="inline-flex rounded-full border border-slate-200 bg-white p-1" role="group" aria-label="Billing period">
            {(
              [
                [BILLING_PERIOD.Monthly, 'Monthly'],
                [BILLING_PERIOD.Yearly, 'Yearly'],
              ] as const
            ).map(([value, label]) => (
              <button
                key={label}
                type="button"
                aria-pressed={billing === value}
                onClick={() => setBilling(value)}
                className={cn(
                  'min-h-10 rounded-full px-5 text-sm font-semibold transition-colors',
                  billing === value ? 'bg-blue-700 text-white' : 'text-slate-600 hover:text-slate-900',
                )}
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        <div className="mt-8 hidden overflow-hidden rounded-[28px] border border-slate-200 bg-white shadow-[0_20px_50px_-32px_rgba(15,23,42,0.45)] lg:block">
          <table className="w-full border-collapse text-sm">
            <caption className="sr-only">Subscription plan comparison</caption>
            <thead>
              <tr>
                <th scope="col" className="w-[22%] p-6 text-left font-medium text-slate-500">
                  Compare
                </th>
                {plans.map((plan, index) => {
                  const highlighted = index === highlightedIndex;
                  return (
                    <th
                      key={plan.id}
                      scope="col"
                      className={cn('p-6 text-left align-top', highlighted && 'bg-blue-50/80')}
                    >
                      <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                        {isCompanyPlan(plan) ? 'Company' : 'Individual'}
                      </p>
                      <p className="mt-1 text-lg font-semibold text-slate-900">{plan.name}</p>
                      <p className="mt-4 text-3xl font-semibold tracking-tight text-slate-900">
                        {formatPrice(plan, billing)}
                      </p>
                      <p className="text-slate-500">/{period}</p>
                    </th>
                  );
                })}
              </tr>
            </thead>
            <tbody>
              {comparisonRows.map((row) => (
                <tr key={row.label} className="border-t border-slate-100">
                  <th scope="row" className="px-6 py-4 text-left font-medium text-slate-600">
                    {row.label}
                  </th>
                  {plans.map((plan, index) => (
                    <td
                      key={plan.id}
                      className={cn(
                        'px-6 py-4 text-center text-slate-800',
                        index === highlightedIndex && 'bg-blue-50/80',
                      )}
                    >
                      <CellValue value={row.value(plan)} />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-slate-100">
                <th scope="row" className="p-6" />
                {plans.map((plan, index) => (
                  <td key={plan.id} className={cn('p-6', index === highlightedIndex && 'bg-blue-50/80')}>
                    <Link
                      to={planHref(plan)}
                      className={cn(
                        'inline-flex min-h-11 w-full items-center justify-center rounded-full px-4 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700',
                        index === highlightedIndex
                          ? 'bg-blue-700 text-white hover:bg-blue-800'
                          : 'border border-slate-300 text-slate-900 hover:bg-slate-50',
                      )}
                    >
                      {planAction(plan)}
                    </Link>
                  </td>
                ))}
              </tr>
            </tfoot>
          </table>
        </div>

        <div className="mt-8 grid gap-4 lg:hidden">
          {plans.map((plan, index) => {
            const highlighted = index === highlightedIndex;
            return (
              <Reveal key={plan.id} delay={index * 80}>
                <article
                  className={cn(
                    'landing-card rounded-3xl border p-6',
                    highlighted
                      ? 'border-blue-700 bg-blue-700 text-white shadow-[0_20px_40px_-24px_rgba(29,78,216,0.8)]'
                      : 'border-slate-200 bg-white text-slate-900',
                  )}
                >
                  <p className={cn('text-[11px] font-semibold uppercase tracking-[0.14em]', highlighted ? 'text-blue-100' : 'text-slate-500')}>
                    {isCompanyPlan(plan) ? 'Company' : 'Individual'}
                  </p>
                  <h3 className="mt-1 text-xl font-semibold">{plan.name}</h3>
                  <p className={cn('mt-2 text-sm', highlighted ? 'text-blue-100' : 'text-slate-600')}>{plan.description}</p>
                  <p className="mt-5 text-4xl font-semibold tracking-tight">
                    {formatPrice(plan, billing)}
                    <span className={cn('ml-1 text-base font-medium', highlighted ? 'text-blue-100' : 'text-slate-500')}>
                      /{period}
                    </span>
                  </p>
                  <ul className="mt-5 space-y-2.5 text-sm">
                    {comparisonRows.map((row) => (
                      <li key={row.label} className="flex items-center justify-between gap-3">
                        <span className={highlighted ? 'text-blue-100' : 'text-slate-500'}>{row.label}</span>
                        <span className="font-medium">{row.value(plan)}</span>
                      </li>
                    ))}
                  </ul>
                  <Link
                    to={planHref(plan)}
                    className={cn(
                      'mt-6 inline-flex min-h-11 w-full items-center justify-center rounded-full px-4 text-sm font-semibold',
                      highlighted ? 'bg-white text-blue-800' : 'bg-blue-700 text-white',
                    )}
                  >
                    {planAction(plan)}
                  </Link>
                </article>
              </Reveal>
            );
          })}
        </div>

        <p className="mt-8 text-center text-sm text-slate-600">
          Checkout, billing period, and the trial live on the{' '}
          <Link to={ROUTES.PRICING} className="font-semibold text-blue-800 underline-offset-2 hover:underline">
            pricing page
          </Link>
          . Already have an account?{' '}
          <Link to={ROUTES.LOGIN} className="font-semibold text-blue-800 underline-offset-2 hover:underline">
            Sign in
          </Link>
          .
        </p>
      </div>
    </section>
  );
}
