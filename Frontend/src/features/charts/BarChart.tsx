import type { ReactNode } from 'react';
import { BarChart3 } from 'lucide-react';
import {
  BarChart as RechartsBarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { Chart } from './Chart';

export class BarChartType extends Chart {
  readonly id = 'bar';
  readonly label = 'Bar Chart';
  readonly icon = BarChart3;
  readonly defaultSize = { w: 6, h: 4 };

  render(data?: unknown): ReactNode {
    const chartData = data as { labels: string[]; datasets: { label: string; values: number[] }[] } | undefined;
    if (!chartData?.labels?.length) return this.placeholder();

    const formatted = chartData.labels.map((label, i) => {
      const row: Record<string, string | number> = { name: label };
      chartData.datasets.forEach((ds) => { row[ds.label] = ds.values[i] ?? 0; });
      return row;
    });

    return (
      <ResponsiveContainer width="100%" height="100%">
        <RechartsBarChart data={formatted} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#d8dadc" />
          <XAxis dataKey="name" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} />
          <Tooltip />
          {chartData.datasets.map((ds) => (
            <Bar key={ds.label} dataKey={ds.label} fill="#00687a" radius={[4, 4, 0, 0]} />
          ))}
        </RechartsBarChart>
      </ResponsiveContainer>
    );
  }

  private placeholder() {
    return (
      <div className="flex h-full items-center justify-center text-on-surface-variant">
        <div className="text-center">
          <BarChart3 size={48} className="mx-auto mb-2 text-secondary/40" />
          <p className="text-body-sm font-medium">Bar Chart</p>
        </div>
      </div>
    );
  }
}
