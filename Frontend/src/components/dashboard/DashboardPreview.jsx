import { previewInsights, previewMetrics } from '../../data/homePage.js';

const bars = [52, 72, 44, 86, 68, 94, 78, 88];
const linePoints = '0,68 45,52 90,58 135,34 180,42 225,20 270,28 315,12';

export default function DashboardPreview() {
  return (
    <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-4 shadow-ambient">
      <div className="rounded-lg border border-outline-variant bg-surface-container-lowest p-4 shadow-sm">
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="font-mono text-label-caps uppercase text-secondary">Live dashboard</p>
            <h2 className="text-headline-md font-semibold text-on-background">Revenue Intelligence</h2>
          </div>
          <div className="inline-flex items-center gap-2 rounded-full border border-outline-variant bg-surface-container-low px-3 py-2 font-mono text-label-caps text-on-surface-variant">
            <span className="status-dot bg-cyan-action" />
            Synced
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          {previewMetrics.map((metric) => (
            <div key={metric.label} className="rounded-lg border border-outline-variant bg-surface p-4">
              <div className={`mb-4 h-1 w-10 rounded-full ${metric.accent}`} />
              <p className="text-body-sm text-on-surface-variant">{metric.label}</p>
              <p className="mt-2 font-mono text-2xl font-semibold text-on-background">{metric.value}</p>
              <p className="mt-1 text-body-sm text-on-surface-variant">{metric.trend}</p>
            </div>
          ))}
        </div>

        <div className="mt-4 grid gap-4 lg:grid-cols-[1.45fr_0.95fr]">
          <div className="rounded-lg border border-outline-variant bg-surface p-4">
            <div className="mb-4 flex items-center justify-between">
              <p className="font-mono text-label-caps uppercase text-on-surface-variant">Monthly growth</p>
              <span className="rounded-full bg-secondary-container/30 px-3 py-1 text-body-sm font-semibold text-secondary">
                +18.4%
              </span>
            </div>
            <svg viewBox="0 0 320 100" className="h-40 w-full overflow-visible" role="img" aria-label="Linjediagram över tillväxt">
              <polyline
                points={linePoints}
                fill="none"
                stroke="#06b6d4"
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="4"
              />
              <polyline
                points="0,78 45,72 90,76 135,62 180,56 225,48 270,44 315,32"
                fill="none"
                stroke="#3980f4"
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="3"
                opacity="0.65"
              />
            </svg>
          </div>

          <div className="rounded-lg border border-outline-variant bg-surface p-4">
            <p className="mb-4 font-mono text-label-caps uppercase text-on-surface-variant">Segment mix</p>
            <div className="flex h-40 items-end gap-2">
              {bars.map((height, index) => (
                <div
                  key={`${height}-${index}`}
                  className="min-w-0 flex-1 rounded-t bg-cyan-action/80"
                  style={{ height: `${height}%`, opacity: 0.45 + index * 0.055 }}
                />
              ))}
            </div>
          </div>
        </div>

        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          {previewInsights.map((insight) => {
            const Icon = insight.icon;

            return (
              <div key={insight.text} className="flex items-center gap-3 rounded-lg bg-surface-container-low p-3">
                <Icon className="size-5 shrink-0 text-secondary" aria-hidden="true" />
                <p className="text-body-sm text-on-surface-variant">{insight.text}</p>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
