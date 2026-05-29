import { Link } from 'react-router-dom';
import { Menu } from 'lucide-react';
import { useEffect, useState } from 'react';
import Button from '../components/Button';
import HashLink from '../components/HashLink';

const navItems = [
  { label: 'Product', to: '/#product' },
  { label: 'Features', to: '/#features' },
  { label: 'Benefits', to: '/#benefits' },
  { label: 'Pricing', to: '/#pricing' },
];

export default function Header() {
  const [hasScrolled, setHasScrolled] = useState(false);

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
          <Button variant="ghost" className="px-4 py-2">
            Login
          </Button>
          <Button variant="dark" className="px-6 py-3">
            Sign Up
          </Button>
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
