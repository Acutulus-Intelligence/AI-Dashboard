import { cloneElement, isValidElement, useCallback, useEffect, useRef, useState } from 'react';
import type { ComponentProps, ReactElement, ReactNode } from 'react';
import { ChartTooltip } from '@/components/ui/chart';

type TooltipProps = ComponentProps<typeof ChartTooltip>;
type TooltipContentProps = {
  active?: boolean;
  payload?: ReadonlyArray<unknown>;
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
  const wasActiveRef = useRef(false);
  const [allowSlide, setAllowSlide] = useState(false);

  const onActiveChange = useCallback((active: boolean) => {
    if (active) {
      if (!wasActiveRef.current) {
        wasActiveRef.current = true;
        setAllowSlide(false);
        // After the first paint at the target point, enable slide for later moves.
        requestAnimationFrame(() => {
          requestAnimationFrame(() => setAllowSlide(true));
        });
      }
      return;
    }

    wasActiveRef.current = false;
    setAllowSlide(false);
  }, []);

  const renderContent = useCallback(
    (tooltipProps: TooltipContentProps) => {
      const inner = resolveContent(content, tooltipProps);
      return (
        <>
          <TooltipActiveReporter
            active={tooltipProps.active}
            payload={tooltipProps.payload}
            onActiveChange={onActiveChange}
          />
          {inner}
        </>
      );
    },
    [content, onActiveChange],
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

function TooltipActiveReporter({
  active,
  payload,
  onActiveChange,
}: {
  active?: boolean;
  payload?: ReadonlyArray<unknown>;
  onActiveChange: (active: boolean) => void;
}) {
  const isActive = Boolean(active && payload && payload.length > 0);

  useEffect(() => {
    onActiveChange(isActive);
  }, [isActive, onActiveChange]);

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
