import { trustLogos } from '../../data/homePage.js';

export default function SocialProofSection() {
  return (
    <section className="bg-surface px-gutter py-12">
      <div className="mx-auto max-w-container-max text-center">
        <p className="mb-8 font-mono text-label-caps uppercase text-outline">Betrodd av datadrivna team</p>
        <div className="flex flex-wrap items-center justify-center gap-10 opacity-50 grayscale lg:gap-16">
          {trustLogos.map((logo) => (
            <span key={logo} className="text-headline-lg font-bold">
              {logo}
            </span>
          ))}
        </div>
      </div>
    </section>
  );
}
