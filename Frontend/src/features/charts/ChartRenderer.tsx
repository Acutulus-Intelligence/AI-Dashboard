import { useMemo } from 'react';
import { CircleAlert } from 'lucide-react';
import { resolveStyle } from './palette';
import { get } from './registry';
import type { ChartData, ChartStyleConfig } from './types';

interface ChartRendererProps {
  chartId: string;
  data?: ChartData;
  styleConfig?: ChartStyleConfig;
}

const EMPTY: ChartData = { labels: [], datasets: [] };

export default function ChartRenderer({ chartId, data, styleConfig }: ChartRendererProps) {
  const descriptor = get(chartId);
  const chartData = data ?? EMPTY;

  // Cartesian charts colour by series, pie and radial colour by label, so
  // resolve enough colours for whichever axis the descriptor happens to use.
  const colorCount = Math.max(chartData.datasets.length, chartData.labels.length);

  const style = useMemo(
    () => (descriptor ? resolveStyle(descriptor, styleConfig, colorCount) : null),
    [descriptor, styleConfig, colorCount],
  );

  if (!descriptor || !style) {
    return (
      <div className="text-muted-foreground flex h-full items-center justify-center">
        <div className="text-center">
          <CircleAlert className="mx-auto mb-2 size-8 opacity-40" />
          <p className="text-sm">Unknown chart type: {chartId}</p>
        </div>
      </div>
    );
  }

  return <>{descriptor.render({ data: chartData, style })}</>;
}
