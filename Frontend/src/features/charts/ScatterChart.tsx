import type { ReactNode } from 'react';
import { Dot } from 'lucide-react';
import {
  ScatterChart as RechartsScatterChart,
  Scatter,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { Chart } from './Chart';

export class ScatterChartType extends Chart {
  readonly id = 'scatter';
  readonly label = 'Scatter Plot';
  readonly icon = Dot;
  readonly defaultSize = { w: 6, h: 4 };

  render(data?: unknown): ReactNode {
    const chartData = data as { labels: string[]; datasets: { label: string; values: number[] }[] } | undefined;
    if (!chartData?.labels?.length) return this.placeholder();

    const formatted = chartData.labels.map((label, i) => ({
      x: label,
      y: chartData.datasets[0]?.values[i] ?? 0,
    }));

    return (
      <ResponsiveContainer width="100%" height="100%">
        <RechartsScatterChart margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#d8dadc" />
          <XAxis dataKey="x" tick={{ fontSize: 12 }} name="x" />
          <YAxis dataKey="y" tick={{ fontSize: 12 }} name="y" />
          <Tooltip cursor={{ strokeDasharray: '3 3' }} />
          <Scatter name="Data" data={formatted} fill="#00687a" />
        </RechartsScatterChart>
      </ResponsiveContainer>
    );
  }

  private placeholder() {
    return (
      <div className="flex h-full items-center justify-center text-on-surface-variant">
        <div className="text-center">
          <Dot size={48} className="mx-auto mb-2 text-secondary/40" />
          <p className="text-body-sm font-medium">Scatter Plot</p>
        </div>
      </div>
    );
  }
}
