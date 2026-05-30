import { AlignJustify } from 'lucide-react';
import { Link } from 'react-router-dom';
import CreateDropdown from '../components/CreateDropdown';
import logoSrc from '../../assets/images/IconTransNoText.png';

interface DashboardHeaderProps {
  onToggleNavbar: () => void;
  onNewChart: () => void;
  onNewDashboard: () => void;
}

export default function DashboardHeader({ onToggleNavbar, onNewChart, onNewDashboard }: DashboardHeaderProps) {
  return (
    <header className="fixed top-0 z-50 w-full border-b border-outline-variant bg-surface-container-lowest">
      <div className="mx-auto flex h-16 w-full max-w-container-max items-center justify-between px-gutter">
        <div className="flex items-center gap-3">
          <Link to="/" className="flex items-center">
            <img src={logoSrc} alt="Actulus Intelligence" className="h-8 w-auto" />
          </Link>
          <button
            type="button"
            onClick={onToggleNavbar}
            className="flex cursor-pointer items-center justify-center rounded-lg p-2 text-on-surface-variant transition-transform active:scale-90"
          >
            <AlignJustify size={20} />
          </button>
        </div>

        <CreateDropdown onNewChart={onNewChart} onNewDashboard={onNewDashboard} />
      </div>
    </header>
  );
}
