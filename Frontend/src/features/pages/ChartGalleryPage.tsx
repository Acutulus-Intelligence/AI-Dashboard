import ChartRenderer from '../charts/ChartRenderer';
import { getAll } from '../charts/registry';
import type { ChartData } from '../charts/types';

const SAMPLE: ChartData = {
  labels: ['January', 'February', 'March', 'April', 'May', 'June'],
  datasets: [
    { label: 'desktop', values: [186, 305, 237, 173, 209, 264] },
    { label: 'mobile', values: [80, 200, 120, 190, 130, 140] },
  ],
  queryResult: [
    { month: 'January', desktop: 186, mobile: 80 },
    { month: 'February', desktop: 305, mobile: 200 },
    { month: 'March', desktop: 237, mobile: 120 },
  ],
};

export default function ChartGalleryPage() {
  return (
    <main className="bg-background min-h-screen p-6">
      <h1 className="mb-6 text-2xl font-semibold">Chart gallery</h1>
      <div className="space-y-8">
        {getAll().map((descriptor) => (
          <section key={descriptor.id}>
            <h2 className="mb-2 text-lg font-medium">{descriptor.label}</h2>
            <div className="grid gap-4 lg:grid-cols-3">
              {descriptor.variants.map((variant) => (
                <div key={variant.id} className="bg-card rounded-xl border p-3">
                  <p className="text-muted-foreground mb-2 text-xs">{variant.label}</p>
                  <div className="h-56">
                    <ChartRenderer
                      chartId={descriptor.id}
                      data={SAMPLE}
                      styleConfig={{ variant: variant.id }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </section>
        ))}
      </div>
    </main>
  );
}
