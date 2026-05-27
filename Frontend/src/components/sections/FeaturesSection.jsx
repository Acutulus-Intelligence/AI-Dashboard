import { features } from '../../data/homePage.js';
import Reveal from '../ui/Reveal.jsx';

export default function FeaturesSection() {
  return (
    <section id="features" className="bg-surface-bright px-gutter py-16">
      <div className="mx-auto grid max-w-container-max gap-8 md:grid-cols-3">
        {features.map((feature, index) => {
          const Icon = feature.icon;

          return (
            <Reveal key={feature.title} delay={index * 120}>
              <article
                className={`group h-full rounded-xl border bg-surface-container-lowest p-8 transition-all hover:border-cyan-action ${
                  feature.highlighted ? 'ai-glow border-secondary/20' : 'border-outline-variant'
                }`}
              >
                <div
                  className={`mb-6 flex size-12 items-center justify-center rounded-lg transition-colors ${
                    feature.highlighted
                      ? 'bg-secondary-container/20 text-secondary'
                      : 'bg-surface-container text-on-background group-hover:bg-cyan-50'
                  }`}
                >
                  <Icon className="size-6" aria-hidden="true" />
                </div>
                <h3 className="mb-4 text-headline-md font-semibold">{feature.title}</h3>
                <p className="text-body-md text-on-surface-variant">{feature.description}</p>
              </article>
            </Reveal>
          );
        })}
      </div>
    </section>
  );
}
