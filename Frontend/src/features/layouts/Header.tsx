import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { LayoutDashboard, LogOut, Menu, UserCircle } from 'lucide-react';
import Button from '../components/Button';
import HashLink from '../components/HashLink';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';

const navItems = [
  { label: 'Product', to: '/#product' },
  { label: 'Features', to: '/#features' },
  { label: 'Benefits', to: '/#benefits' },
  { label: 'Pricing', to: ROUTES.PRICING },
];

export default function Header() {
  const [hasScrolled, setHasScrolled] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const { user, isAuthenticated, logout } = useAuth();

  useEffect(() => {
    const onScroll = () => setHasScrolled(window.scrollY > 10);

    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });

    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <header
      className={`fixed top-0 z-50 w-full border-b border-outline-variant bg-surface/85 backdrop-blur-md transition-shadow ${
        hasScrolled ? 'shadow-sm' : ''
      }`}
    >
      <div className="mx-auto flex h-16 w-full max-w-container-max items-center justify-between gap-4 px-gutter">
        <Link to="/" className="text-headline-md font-bold text-on-background">
          AI Dashboard
        </Link>

        <nav className="hidden items-center gap-8 md:flex" aria-label="Primary navigation">
          {navItems.map((item, index) => (
            <HashLink
              key={item.label}
              className={`text-body-md transition-opacity hover:text-primary ${
                index === 0
                  ? 'border-b-2 border-secondary pb-1 font-semibold text-secondary'
                  : 'text-on-surface-variant'
              }`}
              to={item.to}
            >
              {item.label}
            </HashLink>
          ))}
        </nav>

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
          className="inline-flex size-11 items-center justify-center rounded-lg border border-outline-variant text-on-background md:hidden"
          aria-label="Open menu"
        >
          <Menu size={22} aria-hidden="true" />
        </button>
      </div>
    </header>
  );
}
