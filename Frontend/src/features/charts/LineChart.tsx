import type { ReactNode } from 'react';
import { TrendingUp } from 'lucide-react';
import {
  LineChart as RechartsLineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { Chart } from './Chart';

export class LineChartType extends Chart {
  readonly id = 'line';
  readonly label = 'Line Chart';
  readonly icon = TrendingUp;
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
        <RechartsLineChart data={formatted} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#d8dadc" />
          <XAxis dataKey="name" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} />
          <Tooltip />
          {chartData.datasets.map((ds, i) => (
            <Line key={ds.label} type="monotone" dataKey={ds.label} stroke={i === 0 ? '#00687a' : '#3980f4'} strokeWidth={2} dot={{ r: 3 }} />
          ))}
        </RechartsLineChart>
      </ResponsiveContainer>
    );
  }

  private placeholder() {
    return (
      <div className="flex h-full items-center justify-center text-on-surface-variant">
        <div className="text-center">
          <TrendingUp size={48} className="mx-auto mb-2 text-secondary/40" />
          <p className="text-body-sm font-medium">Line Chart</p>
        </div>
      </div>
    );
  }
}
