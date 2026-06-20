import type { BillingPeriod } from '../../lib/api/subscription';

interface BillingToggleProps {
  billing: BillingPeriod;
  onChange: (billing: BillingPeriod) => void;
}

export default function BillingToggle({ billing, onChange }: BillingToggleProps) {
  return (
    <div className="inline-flex rounded-xl border border-outline-variant bg-surface-container-lowest p-1">
      <button
        type="button"
        onClick={() => onChange(0)}
        className={`rounded-lg px-4 py-2 text-body-sm font-semibold transition-all ${
          billing === 0
            ? 'bg-primary text-on-primary'
            : 'text-on-surface-variant hover:text-on-background'
        }`}
      >
        Monthly
      </button>
      <button
        type="button"
        onClick={() => onChange(1)}
        className={`rounded-lg px-4 py-2 text-body-sm font-semibold transition-all ${
          billing === 1
            ? 'bg-primary text-on-primary'
            : 'text-on-surface-variant hover:text-on-background'
        }`}
      >
        Yearly
      </button>
    </div>
  );
}
