import { useEffect, useMemo, useRef, useState } from 'react';
import { CircleAlert } from 'lucide-react';
import { resolveColorAxis } from './colorAxis';
import { resolveStyle } from './palette';
import { get } from './registry';
import { shapeForDisplay } from './shape';
import type { ChartData, ChartLayout, ChartStyleConfig } from './types';

interface ChartRendererProps {
  chartId: string;
  data?: ChartData;
  styleConfig?: ChartStyleConfig;
  /** Called with the clicked category label; used for dashboard cross-filtering. */
  onCategoryClick?: (category: string) => void;
}

const EMPTY: ChartData = { labels: [], datasets: [] };
const UNMEASURED: ChartLayout = { width: 0, height: 0, compact: false };

function useChartLayout() {
  const ref = useRef<HTMLDivElement>(null);
  const [layout, setLayout] = useState<ChartLayout>(UNMEASURED);

  useEffect(() => {
    const element = ref.current;
    if (!element) return;

    const update = (width: number, height: number) => {
      const compact = width > 0 && height > 0 && (width < 360 || height < 200);
      setLayout((prev) =>
        Math.abs(prev.width - width) < 1 &&
        Math.abs(prev.height - height) < 1 &&
        prev.compact === compact
          ? prev
          : { width, height, compact },
      );
    };

    update(element.clientWidth, element.clientHeight);
    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        update(entry.contentRect.width, entry.contentRect.height);
      }
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return { ref, layout };
}

export default function ChartRenderer({ chartId, data, styleConfig, onCategoryClick }: ChartRendererProps) {
  const descriptor = get(chartId);
  const rawData = data ?? EMPTY;
  const { ref, layout } = useChartLayout();

  const chart = useMemo(() => {
    if (!descriptor) return null;

    // Top N shaping needs only the params, so resolve once with the raw keys.
    const rawAxis = resolveColorAxis(descriptor, rawData, styleConfig);
    const preliminary = resolveStyle(descriptor, styleConfig, rawAxis.keys);
    const shaped = shapeForDisplay(rawData, preliminary);

    const axis = resolveColorAxis(descriptor, shaped, styleConfig);
    const style = resolveStyle(descriptor, styleConfig, axis.keys);
    return { data: shaped, style, axis };
  }, [descriptor, rawData, styleConfig]);

  if (!descriptor || !chart) {
    return (
      <div className="text-muted-foreground flex h-full items-center justify-center">
        <div className="text-center">
          <CircleAlert className="mx-auto mb-2 size-8 opacity-40" />
          <p className="text-sm">Unknown chart type: {chartId}</p>
        </div>
      </div>
    );
  }

  return (
    <div ref={ref} className="h-full min-h-0 w-full">
      {descriptor.render({
        data: chart.data,
        style: chart.style,
        layout,
        colorSlots: chart.axis.slots,
        onCategoryClick,
      })}
    </div>
  );
}
