import { cloneElement, isValidElement, useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { ComponentProps, ReactElement, ReactNode } from 'react';
import { ChartTooltip } from '@/components/ui/chart';

type TooltipProps = ComponentProps<typeof ChartTooltip>;
type TooltipContentProps = {
  active?: boolean;
  payload?: ReadonlyArray<unknown>;
  label?: string | number;
  [key: string]: unknown;
};

const SLIDE_MS = 200;

/**
 * Recharts 3 custom tooltips remount when content returns null, so the wrapper
 * forgets its last position and animates from (0,0) (top-left). This wrapper:
 * - fades in on first show (no corner fly — position animation off)
 * - slides between points while the tooltip stays active
 * - resets when the pointer leaves so the next open fades again
 */
export default function ChartHoverTooltip({
  content,
  animationDuration = SLIDE_MS,
  animationEasing = 'ease-out',
  ...props
}: TooltipProps) {
  const [allowSlide, setAllowSlide] = useState(false);

  const onDeactivate = useCallback(() => {
    setAllowSlide(false);
  }, []);

  const onFirstPaint = useCallback(() => {
    requestAnimationFrame(() => setAllowSlide(true));
  }, []);

  const renderContent = useCallback(
    (tooltipProps: TooltipContentProps) => {
      const inner = resolveContent(content, tooltipProps);
      return (
        <>
          <TooltipActiveReporter
            active={tooltipProps.active}
            payload={tooltipProps.payload}
            label={tooltipProps.label}
            onDeactivate={onDeactivate}
            onFirstPaint={onFirstPaint}
          />
          {inner}
        </>
      );
    },
    [content, onDeactivate, onFirstPaint],
  );

  return (
    <ChartTooltip
      {...props}
      content={renderContent}
      isAnimationActive={allowSlide}
      animationDuration={animationDuration}
      animationEasing={animationEasing}
    />
  );
}

function tooltipAnchorKey(payload?: ReadonlyArray<unknown>, label?: string | number): string {
  if (label != null && label !== '') return String(label);
  const first = payload?.[0] as { name?: string | number; dataKey?: string | number } | undefined;
  if (first?.name != null && first.name !== '') return String(first.name);
  if (first?.dataKey != null && first.dataKey !== '') return String(first.dataKey);
  return '';
}

function TooltipActiveReporter({
  active,
  payload,
  label,
  onDeactivate,
  onFirstPaint,
}: {
  active?: boolean;
  payload?: ReadonlyArray<unknown>;
  label?: string | number;
  onDeactivate: () => void;
  onFirstPaint: () => void;
}) {
  const isActive = Boolean(active && payload && payload.length > 0);
  const anchorKey = tooltipAnchorKey(payload, label);
  const paintedRef = useRef(false);

  useEffect(() => {
    if (!isActive) {
      paintedRef.current = false;
      onDeactivate();
    }
  }, [isActive, onDeactivate]);

  useLayoutEffect(() => {
    if (!isActive || paintedRef.current) return;
    paintedRef.current = true;
    onFirstPaint();
  }, [isActive, anchorKey, onFirstPaint]);

  return null;
}

function resolveContent(
  content: TooltipProps['content'],
  tooltipProps: TooltipContentProps,
): ReactNode {
  if (typeof content === 'function') {
    return content(tooltipProps as never);
  }
  if (isValidElement(content)) {
    return cloneElement(content as ReactElement<Record<string, unknown>>, tooltipProps);
  }
  return content ?? null;
}
