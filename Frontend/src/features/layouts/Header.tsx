import { useEffect, useLayoutEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { LayoutDashboard, LogOut, Menu, UserCircle, X } from 'lucide-react';
import Button from '../components/Button';
import SectionLink from '../components/SectionLink';
import { useAuth } from '../store/useAuth';
import { isPublicNavCurrent, PUBLIC_NAV } from '../navigation/publicNav';
import { ROUTES } from '../routes';

export default function Header() {
  const { pathname } = useLocation();
  const { user, isAuthenticated, logout } = useAuth();
  const [hasScrolled, setHasScrolled] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    const onScroll = () => setHasScrolled(window.scrollY > 10);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useLayoutEffect(() => {
    if (!mobileOpen) return;

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMobileOpen(false);
    };

    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', onKey);
    };
  }, [mobileOpen]);

  return (
    <header
      className={`fixed top-0 z-50 w-full border-b border-outline-variant bg-surface/85 backdrop-blur-md transition-shadow ${
        hasScrolled ? 'shadow-xs' : ''
      }`}
    >
      <div className="mx-auto flex h-16 w-full max-w-container-max items-center justify-between gap-4 px-gutter">
        <Link to={ROUTES.HOME} className="text-headline-md font-bold text-on-background" onClick={() => setMobileOpen(false)}>
          AI Dashboard
        </Link>

        <nav className="hidden items-center gap-8 lg:flex" aria-label="Primary">
          {PUBLIC_NAV.map((item) => {
            const current = isPublicNavCurrent(pathname, item);
            return (
              <SectionLink
                key={item.label}
                to={item.to}
                sectionId={item.sectionId}
                aria-current={current ? 'page' : undefined}
                className={`text-body-md transition-colors hover:text-primary ${
                  current ? 'font-semibold text-brand' : 'text-on-surface-variant'
                }`}
              >
                {item.label}
              </SectionLink>
            );
          })}
        </nav>

        <div className="flex items-center gap-2">
          <div className="hidden items-center gap-4 sm:flex">
            {isAuthenticated && user ? (
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setIsMenuOpen((value) => !value)}
                  className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-2 text-body-sm font-semibold text-on-background transition-colors hover:bg-surface-container-low"
                >
                  <UserCircle size={18} aria-hidden="true" />
                  {user.email}
                </button>
                {isMenuOpen && (
                  <div className="absolute right-0 mt-2 w-60 rounded-xl border border-outline-variant bg-surface-container-lowest p-2 shadow-ambient">
                    <Link
                      to={ROUTES.DASHBOARD}
                      className="flex items-center gap-2 rounded-lg px-3 py-2 text-body-sm text-on-background hover:bg-surface-container"
                      onClick={() => setIsMenuOpen(false)}
                    >
                      <LayoutDashboard size={16} aria-hidden="true" />
                      Dashboard
                    </Link>
                    <button
                      type="button"
                      onClick={() => {
                        setIsMenuOpen(false);
                        void logout();
                      }}
                      className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-body-sm text-on-background hover:bg-surface-container"
                    >
                      <LogOut size={16} aria-hidden="true" />
                      Logout
                    </button>
                  </div>
                )}
              </div>
            ) : (
              <>
                <Link to={ROUTES.LOGIN}>
                  <Button variant="ghost" className="px-4 py-2">
                    Login
                  </Button>
                </Link>
                <Link to={ROUTES.REGISTER}>
                  <Button variant="dark" className="px-6 py-3">
                    Sign Up
                  </Button>
                </Link>
              </>
            )}
          </div>

          <button
            type="button"
            className="inline-flex size-11 items-center justify-center rounded-xl text-on-background hover:bg-surface-container-low lg:hidden"
            aria-expanded={mobileOpen}
            aria-controls="site-menu"
            onClick={() => {
              setIsMenuOpen(false);
              setMobileOpen((open) => !open);
            }}
          >
            {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
            <span className="sr-only">{mobileOpen ? 'Close menu' : 'Open menu'}</span>
          </button>
        </div>
      </div>

      {mobileOpen && (
        <div id="site-menu" className="border-t border-outline-variant bg-surface lg:hidden">
          <nav className="mx-auto flex max-w-container-max flex-col gap-1 px-gutter py-4" aria-label="Mobile">
            {PUBLIC_NAV.map((item) => {
              const current = isPublicNavCurrent(pathname, item);
              return (
                <SectionLink
                  key={item.label}
                  to={item.to}
                  sectionId={item.sectionId}
                  aria-current={current ? 'page' : undefined}
                  className={`rounded-xl px-3 py-3 text-body-md font-medium hover:bg-surface-container-low ${
                    current ? 'text-brand' : 'text-on-background'
                  }`}
                  onClick={() => setMobileOpen(false)}
                >
                  {item.label}
                </SectionLink>
              );
            })}
            <div className="mt-3 flex flex-col gap-2 border-t border-outline-variant pt-4 sm:hidden">
              {isAuthenticated && user ? (
                <>
                  <Link
                    to={ROUTES.DASHBOARD}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-primary px-5 text-body-sm font-semibold text-on-primary"
                    onClick={() => setMobileOpen(false)}
                  >
                    <LayoutDashboard size={16} aria-hidden="true" />
                    Dashboard
                  </Link>
                  <button
                    type="button"
                    onClick={() => {
                      setMobileOpen(false);
                      void logout();
                    }}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-outline-variant px-5 text-body-sm font-semibold text-on-background"
                  >
                    <LogOut size={16} aria-hidden="true" />
                    Logout
                  </button>
                </>
              ) : (
                <>
                  <Link to={ROUTES.LOGIN} onClick={() => setMobileOpen(false)}>
                    <Button variant="ghost" className="w-full">
                      Login
                    </Button>
                  </Link>
                  <Link to={ROUTES.REGISTER} onClick={() => setMobileOpen(false)}>
                    <Button variant="dark" className="w-full">
                      Sign Up
                    </Button>
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
