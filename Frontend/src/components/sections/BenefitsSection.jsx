import { Sparkles } from 'lucide-react';
import { comparisonCards } from '../../data/homePage.js';
import Reveal from '../ui/Reveal.jsx';

export default function BenefitsSection() {
  return (
    <section id="benefits" className="bg-background px-gutter py-16">
      <div className="mx-auto max-w-container-max">
        <div className="mb-12 text-center">
          <span className="mb-2 block font-mono text-label-caps uppercase text-outline">Effektivitet</span>
          <h2 className="text-headline-lg font-semibold text-on-background">Varför välja AI Dashboard?</h2>
        </div>

        <div className="grid gap-8 md:grid-cols-2">
          {comparisonCards.map((card, index) => {
            const Icon = card.icon;
            const isAccent = card.tone === 'accent';

            return (
              <Reveal key={card.title} delay={index * 120}>
                <article
                  className={`relative overflow-hidden rounded-xl border p-8 ${
                    isAccent
                      ? 'ai-glow border-secondary/20 bg-surface-container-lowest'
                      : 'border-outline-variant bg-surface-container-low opacity-70'
                  }`}
                >
                  {isAccent && (
                    <Sparkles
                      className="absolute right-5 top-5 size-6 animate-spin text-secondary"
                      style={{ animationDuration: '4s' }}
                      aria-hidden="true"
                    />
                  )}
                  <div className="mb-4 flex items-center gap-3">
                    <Icon className={`size-6 ${isAccent ? 'text-secondary' : 'text-error'}`} aria-hidden="true" />
                    <h3 className="text-headline-md font-semibold">{card.title}</h3>
                  </div>
                  <ul className={`space-y-4 ${isAccent ? 'font-semibold text-on-background' : 'text-on-surface-variant'}`}>
                    {card.points.map((point) => {
                      const PointIcon = point.icon;

                      return (
                        <li key={point.text} className="flex items-center gap-2">
                          <PointIcon
                            className={`size-5 shrink-0 ${isAccent ? 'text-secondary' : 'text-on-surface-variant'}`}
                            aria-hidden="true"
                          />
                          {point.text}
                        </li>
                      );
                    })}
                  </ul>
                </article>
              </Reveal>
            );
          })}
        </div>
      </div>
    </section>
  );
}
