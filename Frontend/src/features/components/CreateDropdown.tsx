import { ChevronDown } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

interface CreateDropdownProps {
  onNewChart: () => void;
  onNewDashboard: () => void;
}

export default function CreateDropdown({ onNewChart, onNewDashboard }: CreateDropdownProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };

    const onClickOutside = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
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
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((prev) => !prev)}
        className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-b from-emerald-600 to-emerald-700 px-5 py-2.5 text-body-md font-semibold text-white shadow-sm shadow-emerald-800/30 ring-1 ring-emerald-500/20 transition-all hover:from-emerald-500 hover:to-emerald-600 hover:shadow-md hover:shadow-emerald-700/40 active:scale-95 active:from-emerald-700 active:to-emerald-800"
      >
        Create
        <ChevronDown size={18} className={`transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div className="absolute right-0 top-full mt-2 w-48 overflow-hidden rounded-xl border border-outline-variant bg-white shadow-lg">
          <button
            type="button"
            onClick={() => { onNewChart(); setOpen(false); }}
            className="block w-full px-4 py-3 text-left text-body-md text-on-background transition-colors hover:bg-surface-container-low"
          >
            New Chart
          </button>
          <button
            type="button"
            onClick={() => { onNewDashboard(); setOpen(false); }}
            className="block w-full px-4 py-3 text-left text-body-md text-on-background transition-colors hover:bg-surface-container-low"
          >
            New Dashboard
          </button>
        </div>
      )}
    </div>
  );
}
