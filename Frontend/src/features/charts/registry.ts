import { Chart } from './Chart';
import { ExampleChart } from './ExampleChart';

const _registry = new Map<string, Chart>();

export function register(chart: Chart): void {
  _registry.set(chart.id, chart);
}

export function get(id: string): Chart | undefined {
  return _registry.get(id);
}

export function getAll(): Chart[] {
  return Array.from(_registry.values());
}

// Register built-in chart types
register(new ExampleChart());
