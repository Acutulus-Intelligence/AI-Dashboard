import { cn } from '@/lib/utils';

/** Shared with the landing pricing call-to-action. */
const publicButtonBase =
  'inline-flex min-h-12 cursor-pointer items-center justify-center gap-2 rounded-full px-6 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-60';

export const publicButtonVariants = {
  primary:
    'bg-blue-700 text-white hover:bg-blue-800 focus-visible:outline-blue-700',
  secondary:
    'border border-slate-200 bg-white text-slate-800 hover:bg-slate-50 focus-visible:outline-blue-700',
  ghost:
    'text-blue-700 hover:bg-blue-50 focus-visible:outline-blue-700',
} as const;

export type PublicButtonVariant = keyof typeof publicButtonVariants;

export function publicButtonClass(variant: PublicButtonVariant = 'primary', className?: string) {
  return cn(publicButtonBase, publicButtonVariants[variant], className);
}

export const publicEyebrowClass =
  'inline-flex rounded-full border border-blue-100 bg-blue-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-blue-800';

export const publicTitleClass =
  'text-balance text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl md:text-5xl';

export const publicLeadClass =
  'text-pretty text-base leading-relaxed text-slate-600 sm:text-lg';

export const publicCardClass =
  'rounded-3xl border border-slate-200/80 bg-white shadow-[0_16px_40px_-28px_rgba(15,23,42,0.45)]';

export const publicInputClass =
  'w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-base text-slate-900 placeholder:text-slate-400 transition-colors focus:border-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-700/20';

export const publicInputErrorClass =
  'border-red-400 focus:border-red-400 focus:ring-red-200';

export const publicLabelClass = 'mb-1.5 block text-sm font-medium text-slate-700';

export const publicLinkClass =
  'font-semibold text-blue-700 underline-offset-2 hover:text-blue-800 hover:underline';

export const publicErrorBannerClass =
  'flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700';

export const publicPageClass = 'bg-[#f7f9fc]';

export const publicContainerClass = 'mx-auto w-full max-w-6xl px-5 sm:px-6';
