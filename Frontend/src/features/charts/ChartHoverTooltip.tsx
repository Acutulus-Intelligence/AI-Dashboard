import { cloneElement, isValidElement, useCallback, useLayoutEffect, useRef, useState } from 'react';
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
/** Give Recharts a few frames to measure the tooltip before revealing it in place. */
const REVEAL_FRAME_LIMIT = 8;

/**
 * Recharts 3 places the tooltip at (0,0) until it has measured the box, then
 * translates it to the point. A CSS transform transition during that first
 * move flies in from the top-left. This wrapper:
 * - stays invisible until that translate exists, then fades in on the point
 * - slides between later points
 * - drops the slide transition when the pointer leaves, so the next open fades again
 */
export default function ChartHoverTooltip({
  content,
  animationDuration = SLIDE_MS,
  animationEasing = 'ease-out',
  wrapperStyle,
  ...props
}: TooltipProps) {
  const [visible, setVisible] = useState(false);
  const [allowSlide, setAllowSlide] = useState(false);

  const onReset = useCallback(() => {
    setVisible(false);
    setAllowSlide(false);
  }, []);

  const onReveal = useCallback(() => {
    setVisible(true);
  }, []);

  const onArmSlide = useCallback(() => {
    setAllowSlide(true);
  }, []);

  const renderContent = useCallback(
    (tooltipProps: TooltipContentProps) => {
      const shown = Boolean(tooltipProps.active && tooltipProps.payload && tooltipProps.payload.length > 0);
      const inner = resolveContent(content, tooltipProps);
      return (
        <>
          <TooltipActiveReporter
            shown={shown}
            onReset={onReset}
            onReveal={onReveal}
            onArmSlide={onArmSlide}
          />
          <div
            data-tooltip-motion={shown ? (allowSlide ? 'slide' : visible ? 'fade' : 'wait') : 'hidden'}
            className={motionClass(shown, visible)}
          >
            {inner}
          </div>
        </>
      );
    },
    [allowSlide, content, onArmSlide, onReset, onReveal, visible],
  );

  return (
    <ChartTooltip
      {...props}
      content={renderContent}
      isAnimationActive={false}
      animationDuration={animationDuration}
      animationEasing={animationEasing}
      wrapperStyle={{
        ...wrapperStyle,
        transition: allowSlide
          ? `transform ${animationDuration}ms ${animationEasing}`
          : 'none',
      }}
    />
  );
}

function motionClass(shown: boolean, visible: boolean): string | undefined {
  if (!shown) return undefined;
  if (!visible) return 'opacity-0';
  return 'animate-in fade-in-0 duration-200 motion-reduce:animate-none motion-reduce:opacity-100';
}

function TooltipActiveReporter({
  shown,
  onReset,
  onReveal,
  onArmSlide,
}: {
  shown: boolean;
  onReset: () => void;
  onReveal: () => void;
  onArmSlide: () => void;
}) {
  const markerRef = useRef<HTMLSpanElement>(null);

  useLayoutEffect(() => {
    if (!shown) {
      onReset();
      return;
    }

    let cancelled = false;
    let frames = 0;
    let revealed = false;
    let slideArmed = false;
    let raf = 0;

    const watch = () => {
      if (cancelled) return;
      frames += 1;
      const positioned = isTooltipPositioned(markerRef.current);

      if (!revealed && (positioned || frames >= REVEAL_FRAME_LIMIT)) {
        revealed = true;
        onReveal();
      }

      // Arm on a frame after the positioned paint so the first translate is not animated.
      if (positioned && revealed && !slideArmed && !prefersReducedMotion()) {
        slideArmed = true;
        onArmSlide();
        return;
      }

      if (frames < 24) raf = requestAnimationFrame(watch);
    };

    raf = requestAnimationFrame(watch);
    return () => {
      cancelled = true;
      cancelAnimationFrame(raf);
    };
  }, [onArmSlide, onReset, onReveal, shown]);

  return <span ref={markerRef} className="hidden" aria-hidden />;
}

/** True once Recharts has translated the wrapper off the chart origin. */
function isTooltipPositioned(node: HTMLElement | null): boolean {
  const wrapper = node?.closest('.recharts-tooltip-wrapper');
  if (!(wrapper instanceof HTMLElement)) return false;
  const transform = wrapper.style.transform;
  if (!transform || transform === 'none') return false;
  const match = /translate(?:3d)?\(\s*(-?[\d.]+)px,\s*(-?[\d.]+)px/.exec(transform);
  if (!match) return false;
  const x = Number(match[1]);
  const y = Number(match[2]);
  return Math.hypot(x, y) > 2;
}

function prefersReducedMotion(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
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
