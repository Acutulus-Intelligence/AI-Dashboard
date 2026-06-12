import { AlignJustify, Pencil, Lock, Database } from 'lucide-react';
import { Link } from 'react-router-dom';
import CreateDropdown from '../components/CreateDropdown';
import logoSrc from '../../assets/images/IconTransNoText.png';

interface DashboardHeaderProps {
  onToggleNavbar: () => void;
  onNewChart: () => void;
  onNewDashboard: () => void;
  editMode: boolean;
  onToggleEditMode: () => void;
}

export default function DashboardHeader({
  onToggleNavbar,
  onNewChart,
  onNewDashboard,
  editMode,
  onToggleEditMode,
}: DashboardHeaderProps) {
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
          <Link
            to="/dashboard/connections"
            className="ml-2 flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-body-sm text-on-surface-variant hover:bg-surface-container"
          >
            <Database size={16} />
            Connections
          </Link>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={onToggleEditMode}
            className={`inline-flex cursor-pointer items-center gap-2 rounded-xl px-4 py-2.5 text-body-md font-semibold transition-all active:scale-95 ${
              editMode
                ? 'bg-secondary text-white shadow-sm'
                : 'border border-outline-variant bg-surface text-on-surface-variant hover:bg-surface-container'
            }`}
          >
            {editMode ? <Lock size={16} /> : <Pencil size={16} />}
            {editMode ? 'Done' : 'Edit'}
          </button>
          <CreateDropdown onNewChart={onNewChart} onNewDashboard={onNewDashboard} />
        </div>
      </div>
    </header>
  );
}
