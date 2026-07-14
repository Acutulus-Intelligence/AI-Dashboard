import type { ReactNode } from 'react';
import {
  AlignHorizontalJustifyCenter,
  AlignHorizontalJustifyEnd,
  AlignHorizontalJustifyStart,
  AlignVerticalJustifyCenter,
  AlignVerticalJustifyEnd,
  AlignVerticalJustifyStart,
} from 'lucide-react';
import type { TextHorizontalAlign, TextVerticalAlign } from '../../lib/api/dashboards';
import { TEXT_ALIGN_H, TEXT_ALIGN_V } from '../../lib/api/dashboards';

interface TextAlignControlsProps {
  horizontal: TextHorizontalAlign;
  vertical: TextVerticalAlign;
  onHorizontalChange: (value: TextHorizontalAlign) => void;
  onVerticalChange: (value: TextVerticalAlign) => void;
}

function AlignButton({
  active,
  title,
  onClick,
  children,
}: {
  active: boolean;
  title: string;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-label={title}
      onMouseDown={(e) => e.stopPropagation()}
      onClick={onClick}
      className={
        'rounded p-1 transition-colors '
        + (active
          ? 'bg-secondary/15 text-secondary'
          : 'text-on-surface-variant hover:bg-surface-container-high hover:text-on-background')
      }
    >
      {children}
    </button>
  );
}

export default function TextAlignControls({
  horizontal,
  vertical,
  onHorizontalChange,
  onVerticalChange,
}: TextAlignControlsProps) {
  return (
    <div
      className="rounded-lg border border-outline-variant bg-surface-container-lowest px-2 py-1.5 shadow-md"
      onMouseDown={(e) => e.stopPropagation()}
    >
      <div className="flex items-center gap-2">
        <div className="flex items-center gap-0.5">
          <AlignButton
            active={horizontal === TEXT_ALIGN_H.Left}
            title="Align left"
            onClick={() => onHorizontalChange(TEXT_ALIGN_H.Left)}
          >
            <AlignHorizontalJustifyStart size={14} />
          </AlignButton>
          <AlignButton
            active={horizontal === TEXT_ALIGN_H.Center}
            title="Align center"
            onClick={() => onHorizontalChange(TEXT_ALIGN_H.Center)}
          >
            <AlignHorizontalJustifyCenter size={14} />
          </AlignButton>
          <AlignButton
            active={horizontal === TEXT_ALIGN_H.Right}
            title="Align right"
            onClick={() => onHorizontalChange(TEXT_ALIGN_H.Right)}
          >
            <AlignHorizontalJustifyEnd size={14} />
          </AlignButton>
        </div>
        <div className="h-4 w-px bg-outline-variant/60" />
        <div className="flex items-center gap-0.5">
          <AlignButton
            active={vertical === TEXT_ALIGN_V.Top}
            title="Align top"
            onClick={() => onVerticalChange(TEXT_ALIGN_V.Top)}
          >
            <AlignVerticalJustifyStart size={14} />
          </AlignButton>
          <AlignButton
            active={vertical === TEXT_ALIGN_V.Center}
            title="Align middle"
            onClick={() => onVerticalChange(TEXT_ALIGN_V.Center)}
          >
            <AlignVerticalJustifyCenter size={14} />
          </AlignButton>
          <AlignButton
            active={vertical === TEXT_ALIGN_V.Bottom}
            title="Align bottom"
            onClick={() => onVerticalChange(TEXT_ALIGN_V.Bottom)}
          >
            <AlignVerticalJustifyEnd size={14} />
          </AlignButton>
        </div>
      </div>
    </div>
  );
}
