import { useState, useEffect, useRef } from 'react';
import { AlignJustify, Settings, LogOut, Shield, CreditCard } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTES } from '../routes';
import CreateDropdown from '../components/CreateDropdown';
import { useAuth } from '../store/useAuth';
import * as companyApi from '../../lib/api/company';
import logoSrc from '../../assets/images/IconTransNoText.png';

interface DashboardHeaderProps {
  onToggleNavbar: () => void;
  onNewChart: () => void;
  onNewDashboard: () => void;
}

export default function DashboardHeader({
  onToggleNavbar,
  onNewChart,
  onNewDashboard,
}: DashboardHeaderProps) {
  const { user, hasActiveSubscription, logout } = useAuth();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const [companyName, setCompanyName] = useState<string | null>(null);
  const ref = useRef<HTMLDivElement>(null);
  const isCompany = user?.userType === 1;

  useEffect(() => {
    companyApi.getMyCompany().then((c) => setCompanyName(c.name)).catch(() => {});
  }, []);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMenuOpen(false);
    };
    const onClickOutside = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('mousedown', onClickOutside);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('mousedown', onClickOutside);
    };
  }, []);

  return (
    <header className="fixed top-0 z-50 w-full border-b border-outline-variant bg-surface-container-lowest">
      <div className="mx-auto flex h-16 w-full max-w-container-max items-center justify-between px-gutter">
        <div className="flex items-center gap-3">
          <Link to="/" className="flex items-center gap-2">
            <img src={logoSrc} alt="Actulus Intelligence" className="h-8 w-auto" />
            {companyName && (
              <span className="hidden text-body-sm font-semibold text-on-background sm:inline">
                {companyName}
              </span>
            )}
          </Link>
          <button
            type="button"
            onClick={onToggleNavbar}
            className="flex cursor-pointer items-center justify-center rounded-lg p-2 text-on-surface-variant transition-transform active:scale-90"
          >
            <AlignJustify size={20} />
          </button>
        </div>

        <div className="flex items-center gap-3">
          {hasActiveSubscription && (
            <CreateDropdown onNewChart={onNewChart} onNewDashboard={onNewDashboard} />
          )}

          <div ref={ref} className="relative">
            <button
              type="button"
              onClick={() => setMenuOpen((prev) => !prev)}
              className="inline-flex cursor-pointer items-center justify-center rounded-xl border border-outline-variant bg-surface p-2.5 text-on-surface-variant transition-all hover:bg-surface-container active:scale-95"
              title="Settings"
            >
              <Settings size={18} />
            </button>

            {menuOpen && (
              <div className="absolute right-0 top-full mt-2 w-48 overflow-hidden rounded-xl border border-outline-variant bg-white shadow-lg">
                {isCompany ? (
                  <button
                    type="button"
                    onClick={() => { setMenuOpen(false); navigate(ROUTES.ADMIN); }}
                    className="flex w-full items-center gap-2 px-4 py-3 text-left text-body-md text-on-surface-variant transition-colors hover:bg-surface-container-low"
                  >
                    <Shield size={16} />
                    Admin Settings
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => { setMenuOpen(false); navigate(ROUTES.SETTINGS); }}
                    className="flex w-full items-center gap-2 px-4 py-3 text-left text-body-md text-on-surface-variant transition-colors hover:bg-surface-container-low"
                  >
                    <CreditCard size={16} />
                    Settings
                  </button>
                )}
                <div className="border-t border-outline-variant" />
                <button
                  type="button"
                  onClick={() => { setMenuOpen(false); logout(); }}
                  className="flex w-full items-center gap-2 px-4 py-3 text-left text-body-md text-red-600 transition-colors hover:bg-red-50"
                >
                  <LogOut size={16} />
                  Sign Out
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
