import { Check } from 'lucide-react';
import type { BillingPeriod, SubscriptionPlan } from '../../lib/api/subscription';
import { cn } from '@/lib/utils';
import { publicButtonClass, publicCardClass } from '../layouts/publicChrome';

interface PlanCardsProps {
  billing: BillingPeriod;
  plans: SubscriptionPlan[];
  selectedPlanId?: string;
  actionLabel?: string;
  getActionLabel?: (plan: SubscriptionPlan) => string;
  loadingPlanId?: string | null;
  onSelect: (plan: SubscriptionPlan) => void;
}

function formatPrice(plan: SubscriptionPlan, billing: BillingPeriod) {
  const price = billing === 0 ? plan.monthlyPrice : plan.yearlyPrice;
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: price % 1 === 0 ? 0 : 2,
  }).format(price);
}

function formatLimit(value: number | null, fallback: string) {
  if (value === null) return fallback;
  return new Intl.NumberFormat('en-US').format(value);
}

function trialLabel(plan: SubscriptionPlan) {
  if (plan.trialDays === null) return '7-day free trial';
  if (plan.trialDays <= 0) return 'No trial';
  return `${plan.trialDays}-day free trial`;
}

export default function PlanCards({
  billing,
  plans,
  selectedPlanId,
  actionLabel = 'Select',
  getActionLabel,
  loadingPlanId = null,
  onSelect,
}: PlanCardsProps) {
  return (
    <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
      {plans.map((plan, index) => {
        const highlighted = selectedPlanId ? selectedPlanId === plan.id : index === 1;

        return (
          <article
            key={plan.id}
            className={cn(
              'landing-card flex h-full flex-col p-6 sm:p-8',
              publicCardClass,
              highlighted
                ? 'border-blue-200 bg-gradient-to-b from-blue-50/90 to-white shadow-[0_22px_50px_-28px_rgba(29,78,216,0.55)]'
                : '',
            )}
          >
            <div className="mb-5">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-800">
                {plan.userType === 1 || plan.userType === 'Company' ? 'Company' : 'Individual'}
              </p>
              <h3 className="mt-2 text-xl font-semibold tracking-tight text-slate-900">{plan.name}</h3>
              <p className="mt-2 min-h-12 text-sm leading-relaxed text-slate-600">{plan.description}</p>
            </div>

            <div className="mb-5 flex items-baseline gap-2">
              <span className="text-4xl font-semibold tracking-tight text-slate-900">
                {formatPrice(plan, billing)}
              </span>
              <span className="text-sm text-slate-600">
                /{billing === 0 ? 'month' : 'year'}
              </span>
            </div>

            <ul className="mb-6 flex-1 space-y-3 text-sm text-slate-800">
              <li className="flex items-start gap-2">
                <Check size={17} className="mt-0.5 shrink-0 text-blue-700" aria-hidden="true" />
                <span>{formatLimit(plan.maxAiQueriesPerMonth, 'Unlimited')} AI queries per month</span>
              </li>
              <li className="flex items-start gap-2">
                <Check size={17} className="mt-0.5 shrink-0 text-blue-700" aria-hidden="true" />
                <span>{formatLimit(plan.maxDashboards, 'Unlimited')} dashboards</span>
              </li>
              <li className="flex items-start gap-2">
                <Check size={17} className="mt-0.5 shrink-0 text-blue-700" aria-hidden="true" />
                <span>{formatLimit(plan.maxUsers, 'Single workspace')} users</span>
              </li>
              <li className="flex items-start gap-2">
                <Check size={17} className="mt-0.5 shrink-0 text-blue-700" aria-hidden="true" />
                <span>{trialLabel(plan)}</span>
              </li>
            </ul>

            <button
              type="button"
              className={publicButtonClass(highlighted ? 'primary' : 'secondary', 'w-full')}
              disabled={loadingPlanId === plan.id}
              onClick={() => onSelect(plan)}
            >
              {loadingPlanId === plan.id
                ? 'Preparing...'
                : getActionLabel
                  ? getActionLabel(plan)
                  : actionLabel}
            </button>
          </article>
        );
      })}
    </div>
  );
}
