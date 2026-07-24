import { useState, useEffect, useRef, useMemo } from 'react';
import { AlignJustify, Settings, LogOut, Shield, CreditCard, User } from 'lucide-react';
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
  editMode?: boolean;
  onSaveEdit?: () => void;
  onCancelEdit?: () => void;
  saving?: boolean;
}

export default function DashboardHeader({
  onToggleNavbar,
  onNewChart,
  onNewDashboard,
  editMode = false,
  onSaveEdit,
  onCancelEdit,
  saving = false,
}: DashboardHeaderProps) {
  const { user, hasActiveSubscription, logout } = useAuth();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const [companyName, setCompanyName] = useState<string | null>(null);
  const ref = useRef<HTMLDivElement>(null);
  const isCompany = user?.userType === 1;

  const headerLabel = useMemo(() => {
    if (isCompany) return companyName;
    const first = user?.firstName?.trim();
    const last = user?.lastName?.trim();
    if (first && last) return `${first} ${last}`;
    if (first) return first;
    if (last) return last;
    return null;
  }, [isCompany, companyName, user?.firstName, user?.lastName]);

  useEffect(() => {
    if (!isCompany) {
      setCompanyName(null);
      return;
    }
    companyApi.getMyCompany().then((c) => setCompanyName(c.name)).catch(() => {});
  }, [isCompany]);

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

  if (editMode) {
    return (
      <header className="fixed top-0 z-50 w-full border-b border-secondary/20 bg-secondary-container/40">
        <div className="mx-auto flex h-16 w-full max-w-container-max items-center justify-between px-gutter">
          <div className="flex items-center gap-3">
            <Link to={ROUTES.DASHBOARD} className="flex items-center gap-2">
              <img src={logoSrc} alt="Actulus Intelligence" className="h-7 w-auto opacity-80" />
            </Link>
            <span className="text-body-md font-medium text-on-background">
              You are in editing mode
            </span>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onCancelEdit}
              disabled={saving}
              className="inline-flex cursor-pointer items-center justify-center rounded-lg border border-outline-variant bg-surface px-4 py-2 text-body-sm font-medium text-on-background transition-colors hover:bg-surface-container disabled:cursor-not-allowed disabled:opacity-60"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={onSaveEdit}
              disabled={saving}
              className="inline-flex cursor-pointer items-center justify-center rounded-lg bg-secondary px-4 py-2 text-body-sm font-medium text-on-secondary transition-colors hover:bg-secondary/90 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {saving ? 'Saving...' : 'Save'}
            </button>
          </div>
        </div>
      </header>
    );
  }

  return (
    <header className="fixed top-0 z-50 w-full border-b border-outline-variant bg-surface-container-lowest">
      <div className="mx-auto flex h-16 w-full max-w-container-max items-center justify-between px-gutter">
        <div className="flex items-center gap-3">
          <Link to={ROUTES.DASHBOARD} className="flex items-center gap-2">
            <img src={logoSrc} alt="Actulus Intelligence" className="h-8 w-auto" />
            {headerLabel && (
              <span className="hidden text-body-sm font-semibold text-on-background sm:inline">
                {headerLabel}
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
                <button
                  type="button"
                  onClick={() => { setMenuOpen(false); navigate(ROUTES.PROFILE); }}
                  className="flex w-full items-center gap-2 px-4 py-3 text-left text-body-md text-on-surface-variant transition-colors hover:bg-surface-container-low"
                >
                  <User size={16} />
                  Profile
                </button>
                {isCompany ? (
                  <button
                    type="button"
                    onClick={() => { setMenuOpen(false); navigate(ROUTES.ADMIN); }}
                    className="flex w-full items-center gap-2 px-4 py-3 text-left text-body-md text-on-surface-variant transition-colors hover:bg-surface-container-low"
                  >
                    <Shield size={16} />
                    {user?.companyRoleName === 'Owner' ? 'Admin Settings' : 'Settings'}
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
