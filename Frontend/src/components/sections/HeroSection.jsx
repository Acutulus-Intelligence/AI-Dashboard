import { Play } from 'lucide-react';
import DashboardPreview from '../dashboard/DashboardPreview.jsx';
import Button from '../ui/Button.jsx';

export default function HeroSection() {
  return (
    <section id="product" className="overflow-hidden bg-surface px-gutter py-16 lg:py-[120px]">
      <div className="mx-auto grid max-w-container-max items-center gap-12 lg:grid-cols-2">
        <div className="z-10">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full bg-secondary-container/20 px-3 py-1">
            <span className="status-dot animate-pulse bg-cyan-action" />
            <span className="font-mono text-label-caps uppercase text-secondary">
              Nu i Beta: AI Visualisering v2.0
            </span>
          </div>
          <h1 className="mb-6 max-w-2xl text-4xl font-bold leading-tight text-on-background md:text-display-lg">
            Från Databas till <span className="text-secondary">Insikter</span> på Sekunder
          </h1>
          <p className="mb-8 max-w-xl text-body-lg text-on-surface-variant">
            Generera professionella grafer automatiskt med AI. Koppla upp din databas och låt vår
            intelligenta assistent visualisera din data åt dig.
          </p>
          <div className="flex flex-wrap gap-4">
            <Button href="/app">Registrera dig nu</Button>
            <Button variant="outline">
              <Play size={18} aria-hidden="true" />
              Se demo
            </Button>
          </div>
        </div>

        <DashboardPreview />
      </div>
    </section>
  );
}
