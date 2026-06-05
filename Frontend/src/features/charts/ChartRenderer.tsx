import { get } from './registry';

interface ChartRendererProps {
  chartId: string;
  title: string;
  data?: unknown;
}

export default function ChartRenderer({ chartId, title, data }: ChartRendererProps) {
  const chart = get(chartId);
  if (!chart) {
    return (
      <div className="flex h-full items-center justify-center text-on-surface-variant/50">
        <p className="text-body-sm">Unknown chart type: {chartId}</p>
      </div>
    );
  }
  return <>{chart.render(data)}</>;
}
