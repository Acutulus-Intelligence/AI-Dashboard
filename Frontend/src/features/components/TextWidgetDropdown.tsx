import { ChevronDown } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import type { TextVariant } from '../../lib/api/dashboards';
import { TEXT_VARIANT } from '../../lib/api/dashboards';

interface TextWidgetDropdownProps {
  onSelect: (variant: TextVariant) => void;
}

export default function TextWidgetDropdown({ onSelect }: TextWidgetDropdownProps) {
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
        className="inline-flex h-9 cursor-pointer items-center gap-1 rounded-lg border border-outline-variant px-2 text-body-sm font-bold text-primary transition-colors hover:bg-primary/10 active:scale-90"
        title="Add text"
      >
        T
        <ChevronDown size={14} className={`transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div className="absolute right-0 top-full z-50 mt-2 w-44 overflow-hidden rounded-xl border border-outline-variant bg-white shadow-lg">
          <button
            type="button"
            onClick={() => { onSelect(TEXT_VARIANT.Header); setOpen(false); }}
            className="block w-full px-4 py-3 text-left transition-colors hover:bg-surface-container-low"
          >
            <span className="block text-headline-md font-semibold text-on-background">Heading</span>
            <span className="mt-0.5 block text-body-sm text-on-surface-variant">Large title text</span>
          </button>
          <div className="border-t border-outline-variant" />
          <button
            type="button"
            onClick={() => { onSelect(TEXT_VARIANT.Body); setOpen(false); }}
            className="block w-full px-4 py-3 text-left transition-colors hover:bg-surface-container-low"
          >
            <span className="block text-body-sm text-on-surface-variant">Body text</span>
            <span className="mt-0.5 block text-body-sm text-on-surface-variant/70">Smaller paragraph text</span>
          </button>
        </div>
      )}
    </div>
  );
}
