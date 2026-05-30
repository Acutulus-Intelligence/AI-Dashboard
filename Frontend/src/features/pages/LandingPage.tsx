import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import Header from '../layouts/Header';
import Footer from '../layouts/Footer';
import HeroSection from '../sections/HeroSection';
import BenefitsSection from '../sections/BenefitsSection';
import FeaturesSection from '../sections/FeaturesSection';
import SocialProofSection from '../sections/SocialProofSection';
import CtaSection from '../sections/CtaSection';

export default function LandingPage() {
  const { hash } = useLocation();

  useEffect(() => {
    if (hash) {
      const el = document.getElementById(hash.slice(1));
      if (el) {
        el.scrollIntoView({ behavior: 'smooth' });
      }
    }
  }, [hash]);

  return (
    <div className="min-h-screen bg-background text-on-background selection:bg-secondary-container selection:text-on-secondary-container">
      <Header />
      <main className="pt-16">
        <HeroSection />
        <BenefitsSection />
        <FeaturesSection />
        <SocialProofSection />
        <CtaSection />
      </main>
      <Footer />
    </div>
  );
}
