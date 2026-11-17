import type { ReactNode } from 'react';
import LandingHeader from './LandingHeader';
import Footer from './Footer';
import { cn } from '@/lib/utils';

function skipToMainContent() {
  const main = document.getElementById('main-content');
  if (!(main instanceof HTMLElement)) return;
  main.focus();
}

export default function PublicLayout({
  children,
  overlay = false,
}: {
  children: ReactNode;
  /** Header sits on top of the first section. Used by the landing hero. */
  overlay?: boolean;
}) {
  return (
    <div className="landing-page flex min-h-screen flex-col bg-white text-slate-900 selection:bg-blue-200 selection:text-slate-900">
      <button
        type="button"
        onClick={skipToMainContent}
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-[60] focus:rounded-full focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-slate-900"
      >
        Skip to content
      </button>
      <LandingHeader />
      <main
        id="main-content"
        tabIndex={-1}
        className={cn('flex flex-1 flex-col outline-none', !overlay && 'pt-16')}
      >
        {children}
      </main>
      <Footer />
    </div>
  );
}
