import { useEffect, useRef } from 'react';
import type { TextHorizontalAlign, TextVerticalAlign, TextVariant } from '../../lib/api/dashboards';
import { TEXT_ALIGN_H, TEXT_ALIGN_V, TEXT_VARIANT } from '../../lib/api/dashboards';

interface TextWidgetProps {
  content: string;
  variant: TextVariant;
  horizontalAlign: TextHorizontalAlign;
  verticalAlign: TextVerticalAlign;
  editMode: boolean;
  isEditing: boolean;
  onStartEdit: () => void;
  onStopEdit: () => void;
  onChange: (content: string) => void;
}

const VARIANT_CLASSES: Record<TextVariant, string> = {
  [TEXT_VARIANT.Header]: 'text-headline-md font-semibold text-on-background',
  [TEXT_VARIANT.Body]: 'text-body-sm text-on-surface-variant',
};

const PLACEHOLDER: Record<TextVariant, string> = {
  [TEXT_VARIANT.Header]: 'Heading...',
  [TEXT_VARIANT.Body]: 'Body text...',
};

const HORIZONTAL_TEXT: Record<TextHorizontalAlign, string> = {
  [TEXT_ALIGN_H.Left]: 'text-left',
  [TEXT_ALIGN_H.Center]: 'text-center',
  [TEXT_ALIGN_H.Right]: 'text-right',
};

const VERTICAL_JUSTIFY: Record<TextVerticalAlign, string> = {
  [TEXT_ALIGN_V.Top]: 'justify-start',
  [TEXT_ALIGN_V.Center]: 'justify-center',
  [TEXT_ALIGN_V.Bottom]: 'justify-end',
};

export default function TextWidget({
  content,
  variant,
  horizontalAlign,
  verticalAlign,
  editMode,
  isEditing,
  onStartEdit,
  onStopEdit,
  onChange,
}: TextWidgetProps) {
  const className = VARIANT_CLASSES[variant];
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const textAlignClass = HORIZONTAL_TEXT[horizontalAlign];
  const verticalClass = VERTICAL_JUSTIFY[verticalAlign];

  useEffect(() => {
    if (isEditing) textareaRef.current?.focus();
  }, [isEditing]);

  const containerClass = `flex h-full w-full flex-col ${verticalClass}`;

  if (editMode && isEditing) {
    return (
      <div className={containerClass}>
        <textarea
          ref={textareaRef}
          value={content}
          onChange={(e) => onChange(e.target.value)}
          onMouseDown={(e) => e.stopPropagation()}
          onBlur={onStopEdit}
          placeholder={PLACEHOLDER[variant]}
          className={`h-full min-h-[1.5em] w-full resize-none border-0 bg-transparent p-0 outline-none placeholder:text-on-surface-variant/40 ${className} ${textAlignClass}`}
          style={{ userSelect: 'auto' }}
        />
      </div>
    );
  }

  if (editMode) {
    return (
      <div className={containerClass}>
        <div
          onMouseDown={(e) => {
            e.preventDefault();
            e.stopPropagation();
            onStartEdit();
          }}
          className={`w-full cursor-text select-auto whitespace-pre-wrap ${className} ${textAlignClass}`}
        >
          {content || (
            <span className="text-on-surface-variant/40">{PLACEHOLDER[variant]}</span>
          )}
        </div>
      </div>
    );
  }

  if (!content) return null;

  return (
    <div className={containerClass}>
      <div className={`w-full overflow-hidden whitespace-pre-wrap ${className} ${textAlignClass}`}>
        {content}
      </div>
    </div>
  );
}
