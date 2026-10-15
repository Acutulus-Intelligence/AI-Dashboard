import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import LandingHeader from '../layouts/LandingHeader';
import Footer from '../layouts/Footer';
import HeroSection from '../sections/HeroSection';
import DashboardPreview from '../sections/DashboardPreview';
import FeaturesSection from '../sections/FeaturesSection';
import BentoSection from '../sections/BentoSection';
import TestimonialsSection from '../sections/TestimonialsSection';
import PricingSection from '../sections/PricingSection';
import CtaSection from '../sections/CtaSection';

export default function LandingPage() {
  const { hash } = useLocation();

  useEffect(() => {
    if (!hash) return;
    const el = document.getElementById(hash.slice(1));
    if (!el) return;
    const behavior = window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth';
    el.scrollIntoView({ behavior });
  }, [hash]);

  return (
    <div className="landing-page min-h-screen bg-white text-slate-900 selection:bg-blue-200 selection:text-slate-900">
      <a
        href="#product"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-[60] focus:rounded-full focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-slate-900"
      >
        Skip to content
      </a>
      <LandingHeader />
      <main>
        <HeroSection />
        <div id="demo" className="relative z-10 -mt-28 scroll-mt-24 sm:-mt-36 md:-mt-44">
          <DashboardPreview />
        </div>
        <FeaturesSection />
        <BentoSection />
        <TestimonialsSection />
        <PricingSection />
        <CtaSection />
      </main>
      <Footer />
    </div>
  );
}
