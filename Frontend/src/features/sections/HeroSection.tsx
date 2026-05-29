import { Play } from 'lucide-react';
import Button from '../components/Button';
import DashboardPreview from './DashboardPreview';

export default function HeroSection() {
  return (
    <section id="product" className="overflow-hidden bg-surface px-gutter pt-16 lg:pt-[120px] pb-10 lg:pb-16">
      <div className="mx-auto grid max-w-container-max items-center gap-12 lg:grid-cols-2">
        <div className="z-10">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full bg-secondary-container/20 px-3 py-1">
            <span className="size-2 animate-pulse rounded-full bg-cyan-action" />
            <span className="font-mono text-label-caps uppercase text-secondary">
              Now in Beta: AI Visualization v2.0
            </span>
          </div>
          <h1 className="mb-6 max-w-2xl text-4xl font-bold leading-tight text-on-background md:text-display-lg">
            From Database to <span className="text-secondary">Insights</span> in Seconds
          </h1>
          <p className="mb-8 max-w-xl text-body-lg text-on-surface-variant">
            Generate professional charts automatically with AI. Connect your database and let our
            intelligent assistant visualize your data for you.
          </p>
          <div className="flex flex-wrap gap-4">
            <Button>Sign up now</Button>
            <Button variant="outline">
              <Play size={18} aria-hidden="true" />
              Watch demo
            </Button>
          </div>
        </div>

        <DashboardPreview />
      </div>
    </section>
  );
}
