import { useEffect, useLayoutEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Menu, X } from 'lucide-react';
import SectionLink from '../components/SectionLink';
import { useAuth } from '../store/useAuth';
import { isPublicNavCurrent, PUBLIC_NAV } from '../navigation/publicNav';
import { ROUTES } from '../routes';
import { cn } from '@/lib/utils';
import logoSrc from '../../assets/images/IconTransNoText.png';

export default function LandingHeader() {
  const { pathname } = useLocation();
  const { isAuthenticated, isLoading } = useAuth();
  const [hasScrolled, setHasScrolled] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const solid = hasScrolled || menuOpen;

  useEffect(() => {
    const onScroll = () => setHasScrolled(window.scrollY > 12);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useLayoutEffect(() => {
    if (!menuOpen) return;

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMenuOpen(false);
    };

    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', onKey);
    };
  }, [menuOpen]);

  return (
    <header
      className={cn(
        'fixed inset-x-0 top-0 z-50 transition-colors duration-300',
        solid
          ? 'border-b border-slate-200/80 bg-white/90 text-slate-900 shadow-[0_8px_30px_-18px_rgba(15,23,42,0.45)] backdrop-blur-md'
          : 'text-white',
      )}
    >
      <div className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between gap-3 px-5 sm:px-6">
        <Link to={ROUTES.HOME} className="flex min-w-0 items-center gap-2.5" onClick={() => setMenuOpen(false)}>
          <img src={logoSrc} alt="" className="size-8 shrink-0 object-contain" />
          <span className="truncate text-base font-semibold tracking-tight">AI Dashboard</span>
        </Link>

        <nav className="hidden items-center gap-7 lg:flex" aria-label="Primary">
          {PUBLIC_NAV.map((item) => {
            const current = isPublicNavCurrent(pathname, item);
            return (
              <SectionLink
                key={item.label}
                to={item.to}
                sectionId={item.sectionId}
                aria-current={current ? 'page' : undefined}
                className={cn(
                  'text-sm font-medium transition-opacity hover:opacity-100',
                  solid ? 'text-slate-600 hover:text-slate-900' : 'text-white/80 hover:text-white',
                  current && (solid ? 'text-slate-900' : 'text-white'),
                )}
                onClick={() => setMenuOpen(false)}
              >
                {item.label}
              </SectionLink>
            );
          })}
        </nav>

        <div className="hidden items-center gap-2 sm:flex">
          {isLoading ? (
            <div className="h-11 w-28" aria-hidden="true" />
          ) : isAuthenticated ? (
            <Link
              to={ROUTES.DASHBOARD}
              className={cn(
                'inline-flex min-h-11 items-center rounded-full px-5 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2',
                solid
                  ? 'bg-blue-700 text-white hover:bg-blue-800 focus-visible:outline-blue-700'
                  : 'bg-white text-slate-900 hover:bg-sky-50 focus-visible:outline-white',
              )}
            >
              Dashboard
            </Link>
          ) : (
            <>
              <Link
                to={ROUTES.LOGIN}
                className={cn(
                  'inline-flex min-h-11 items-center rounded-full px-4 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2',
                  solid
                    ? 'text-slate-700 hover:bg-slate-100 focus-visible:outline-blue-700'
                    : 'text-white/90 hover:bg-white/10 focus-visible:outline-white',
                )}
              >
                Log in
              </Link>
              <Link
                to={ROUTES.REGISTER}
                className={cn(
                  'inline-flex min-h-11 items-center rounded-full px-5 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2',
                  solid
                    ? 'bg-blue-700 text-white hover:bg-blue-800 focus-visible:outline-blue-700'
                    : 'bg-white text-slate-900 hover:bg-sky-50 focus-visible:outline-white',
                )}
              >
                Get started
              </Link>
            </>
          )}
        </div>

        <button
          type="button"
          className={cn(
            'inline-flex size-11 items-center justify-center rounded-full lg:hidden',
            solid ? 'text-slate-900 hover:bg-slate-100' : 'text-white hover:bg-white/10',
          )}
          aria-expanded={menuOpen}
          aria-controls="landing-menu"
          onClick={() => setMenuOpen((open) => !open)}
        >
          {menuOpen ? <X className="size-5" /> : <Menu className="size-5" />}
          <span className="sr-only">{menuOpen ? 'Close menu' : 'Open menu'}</span>
        </button>
      </div>

      {menuOpen && (
        <div id="landing-menu" className="border-t border-slate-200 bg-white lg:hidden">
          <nav className="mx-auto flex max-w-6xl flex-col gap-1 px-5 py-4 sm:px-6" aria-label="Mobile">
            {PUBLIC_NAV.map((item) => {
              const current = isPublicNavCurrent(pathname, item);
              return (
                <SectionLink
                  key={item.label}
                  to={item.to}
                  sectionId={item.sectionId}
                  aria-current={current ? 'page' : undefined}
                  className={cn(
                    'rounded-xl px-3 py-3 text-base font-medium text-slate-800 hover:bg-slate-50',
                    current && 'bg-slate-50 text-slate-950',
                  )}
                  onClick={() => setMenuOpen(false)}
                >
                  {item.label}
                </SectionLink>
              );
            })}
            <div className="mt-3 flex flex-col gap-2 border-t border-slate-100 pt-4 sm:hidden">
              {isAuthenticated ? (
                <Link
                  to={ROUTES.DASHBOARD}
                  className="inline-flex min-h-11 items-center justify-center rounded-full bg-blue-700 px-5 text-sm font-semibold text-white"
                  onClick={() => setMenuOpen(false)}
                >
                  Dashboard
                </Link>
              ) : (
                <>
                  <Link
                    to={ROUTES.LOGIN}
                    className="inline-flex min-h-11 items-center justify-center rounded-full border border-slate-200 px-5 text-sm font-semibold text-slate-800"
                    onClick={() => setMenuOpen(false)}
                  >
                    Log in
                  </Link>
                  <Link
                    to={ROUTES.REGISTER}
                    className="inline-flex min-h-11 items-center justify-center rounded-full bg-blue-700 px-5 text-sm font-semibold text-white"
                    onClick={() => setMenuOpen(false)}
                  >
                    Get started
                  </Link>
                </>
              )}
            </div>
          </nav>
        </div>
      )}
    </header>
  );
}
