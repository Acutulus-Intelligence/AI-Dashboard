import { useEffect, useRef } from 'react';
import { X } from 'lucide-react';
import { getAll } from '../charts/registry';

interface ChartTypePickerProps {
  open: boolean;
  onClose: () => void;
  onSelect: (chartId: string) => void;
}

export default function ChartTypePicker({ open, onClose, onSelect }: ChartTypePickerProps) {
  const ref = useRef<HTMLDivElement>(null);
  const charts = getAll();

  useEffect(() => {
    if (!open) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40">
      <div
        ref={ref}
        className="w-full max-w-sm rounded-xl border border-outline-variant bg-surface-container-lowest p-6 shadow-ambient"
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-headline-md font-semibold text-on-background">New Chart</h2>
          <button
            type="button"
            onClick={onClose}
            className="cursor-pointer rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container"
          >
            <X size={20} />
          </button>
        </div>

        <div className="grid grid-cols-2 gap-3">
          {charts.map((chart) => {
            const Icon = chart.icon;
            return (
              <button
                key={chart.id}
                type="button"
                onClick={() => { onSelect(chart.id); onClose(); }}
                className="flex cursor-pointer flex-col items-center gap-2 rounded-xl border border-outline-variant bg-surface p-5 transition-all hover:border-secondary hover:bg-secondary-container/10 hover:shadow-sm active:scale-95"
              >
                <Icon size={28} className="text-secondary" />
                <span className="text-body-sm font-medium text-on-background">{chart.label}</span>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
