import { Bolt, Check, History, Sparkles, X } from 'lucide-react';
import Reveal from '../components/Reveal';

const comparisonCards = [
  {
    title: 'Manual Work',
    icon: History,
    tone: 'muted' as const,
    points: [
      { text: 'Hours of SQL coding', icon: X },
      { text: 'Complex Excel formulas', icon: X },
      { text: 'Static and outdated PDF reports', icon: X },
    ],
  },
  {
    title: 'AI Dashboard',
    icon: Bolt,
    tone: 'accent' as const,
    points: [
      { text: 'Direct connection to your data', icon: Check },
      { text: 'Ask in plain text, get answers instantly', icon: Check },
      { text: 'Real-time dashboards', icon: Check },
    ],
  },
];

export default function BenefitsSection() {
  return (
    <section id="benefits" className="bg-background px-gutter pt-24 pb-16">
      <div className="mx-auto max-w-container-max">
        <div className="mb-12 text-center">
          <span className="mb-2 block font-mono text-label-caps uppercase text-outline">Efficiency</span>
          <h2 className="text-headline-lg font-semibold text-on-background">Why choose AI Dashboard?</h2>
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
                      ? 'border-secondary/20 bg-surface-container-lowest shadow-[inset_0_0_15px_rgba(6,182,212,0.1)] border-l-2 border-[#06b6d4]'
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
