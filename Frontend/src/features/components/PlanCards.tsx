import { Check } from 'lucide-react';
import Button from './Button';
import type { BillingPeriod, SubscriptionPlan } from '../../lib/api/subscription';

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
    <div className="grid gap-5 lg:grid-cols-3">
      {plans.map((plan, index) => {
        const highlighted = selectedPlanId ? selectedPlanId === plan.id : index === 1;

        return (
          <article
            key={plan.id}
            className={`flex h-full flex-col rounded-xl border bg-surface-container-lowest p-6 transition-all hover:border-cyan-action ${
              highlighted
                ? 'border-secondary/20 border-l-2 border-[#06b6d4] shadow-[inset_0_0_15px_rgba(6,182,212,0.1)]'
                : 'border-outline-variant'
            }`}
          >
            <div className="mb-5">
              <p className="font-mono text-label-caps uppercase text-outline">
                {plan.userType === 1 || plan.userType === 'Company' ? 'Company' : 'Individual'}
              </p>
              <h3 className="mt-2 text-headline-md font-semibold text-on-background">{plan.name}</h3>
              <p className="mt-2 min-h-12 text-body-sm text-on-surface-variant">{plan.description}</p>
            </div>

            <div className="mb-5 flex items-baseline gap-2">
              <span className="text-headline-lg font-semibold text-on-background">
                {formatPrice(plan, billing)}
              </span>
              <span className="text-body-sm text-on-surface-variant">
                /{billing === 0 ? 'month' : 'year'}
              </span>
            </div>

            <ul className="mb-6 flex-1 space-y-3 text-body-sm text-on-background">
              <li className="flex items-start gap-2">
                <Check size={17} className="mt-0.5 shrink-0 text-secondary" aria-hidden="true" />
                <span>{formatLimit(plan.maxAiQueriesPerMonth, 'Unlimited')} AI queries per month</span>
              </li>
              <li className="flex items-start gap-2">
                <Check size={17} className="mt-0.5 shrink-0 text-secondary" aria-hidden="true" />
                <span>{formatLimit(plan.maxDashboards, 'Unlimited')} dashboards</span>
              </li>
              <li className="flex items-start gap-2">
                <Check size={17} className="mt-0.5 shrink-0 text-secondary" aria-hidden="true" />
                <span>{formatLimit(plan.maxUsers, 'Single workspace')} users</span>
              </li>
            </ul>

            <Button
              type="button"
              variant={highlighted ? 'primary' : 'outline'}
              className="w-full"
              disabled={loadingPlanId === plan.id}
              onClick={() => onSelect(plan)}
            >
              {loadingPlanId === plan.id
                ? 'Preparing...'
                : getActionLabel
                  ? getActionLabel(plan)
                  : actionLabel}
            </Button>
          </article>
        );
      })}
    </div>
  );
}
