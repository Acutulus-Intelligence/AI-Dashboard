import type { BillingPeriod } from '../../lib/api/subscription';
import { cn } from '@/lib/utils';

interface BillingToggleProps {
  billing: BillingPeriod;
  onChange: (billing: BillingPeriod) => void;
}

export default function BillingToggle({ billing, onChange }: BillingToggleProps) {
  return (
    <div className="inline-flex rounded-full border border-slate-200 bg-white p-1 shadow-[0_10px_30px_-20px_rgba(15,23,42,0.45)]">
      <button
        type="button"
        aria-pressed={billing === 0}
        onClick={() => onChange(0)}
        className={cn(
          'rounded-full px-5 py-2 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700',
          billing === 0 ? 'bg-blue-700 text-white' : 'text-slate-600 hover:text-slate-900',
        )}
      >
        Monthly
      </button>
      <button
        type="button"
        aria-pressed={billing === 1}
        onClick={() => onChange(1)}
        className={cn(
          'rounded-full px-5 py-2 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700',
          billing === 1 ? 'bg-blue-700 text-white' : 'text-slate-600 hover:text-slate-900',
        )}
      >
        Yearly
      </button>
    </div>
  );
}
