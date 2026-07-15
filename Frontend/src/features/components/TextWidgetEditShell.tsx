import { useCallback, useEffect, useRef, useState, type MouseEvent, type ReactNode } from 'react';
import TextAlignControls from './TextAlignControls';
import type { TextHorizontalAlign, TextVerticalAlign } from '../../lib/api/dashboards';

const HIDE_DELAY_MS = 250;

interface TextWidgetEditShellProps {
  horizontal: TextHorizontalAlign;
  vertical: TextVerticalAlign;
  onHorizontalChange: (value: TextHorizontalAlign) => void;
  onVerticalChange: (value: TextVerticalAlign) => void;
  className?: string;
  onContextMenu?: (e: MouseEvent) => void;
  children: ReactNode;
}

export default function TextWidgetEditShell({
  horizontal,
  vertical,
  onHorizontalChange,
  onVerticalChange,
  className,
  onContextMenu,
  children,
}: TextWidgetEditShellProps) {
  const [open, setOpen] = useState(false);
  const hideTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const show = useCallback(() => {
    if (hideTimer.current) {
      clearTimeout(hideTimer.current);
    }
    setOpen(true);
  }, []);

  const hide = useCallback(() => {
    hideTimer.current = setTimeout(() => setOpen(false), HIDE_DELAY_MS);
  }, []);

  useEffect(() => () => {
    if (hideTimer.current) {
      clearTimeout(hideTimer.current);
    }
  }, []);

  return (
    <div
      className={className}
      onContextMenu={onContextMenu}
      onMouseEnter={show}
      onMouseLeave={hide}
    >
      <div
        className={
          'absolute inset-x-0 bottom-full z-20 flex justify-center transition-opacity duration-150 '
          + (open ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0')
        }
        aria-hidden={!open}
      >
        <div className="translate-y-1 pt-2">
          <TextAlignControls
            horizontal={horizontal}
            vertical={vertical}
            onHorizontalChange={onHorizontalChange}
            onVerticalChange={onVerticalChange}
          />
        </div>
      </div>
      {children}
    </div>
  );
}
