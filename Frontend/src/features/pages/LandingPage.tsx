import { useLayoutEffect, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import PageMeta from '../components/PageMeta';
import PublicLayout from '../layouts/PublicLayout';
import HeroSection from '../sections/HeroSection';
import DashboardPreview from '../sections/DashboardPreview';
import FeaturesSection from '../sections/FeaturesSection';
import BentoSection from '../sections/BentoSection';
import TestimonialsSection from '../sections/TestimonialsSection';
import PricingSection from '../sections/PricingSection';
import CtaSection from '../sections/CtaSection';
import {
  isLandingSectionId,
  readScrollTarget,
  scrollToLandingSection,
} from '../navigation/publicNav';
import { ROUTES } from '../routes';

function hashId(hash: string) {
  const raw = hash.startsWith('#') ? hash.slice(1) : hash;
  try {
    return decodeURIComponent(raw).trim().toLowerCase();
  } catch {
    return raw.trim().toLowerCase();
  }
}

export default function LandingPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const handledNavigation = useRef<string | null>(null);

  useLayoutEffect(() => {
    const target = readScrollTarget(location.state);
    const legacyId = location.hash ? hashId(location.hash) : '';

    if (!target && !legacyId) {
      handledNavigation.current = null;
      return;
    }

    const signature = `${location.pathname}${location.search}${location.hash}:${target ?? ''}`;
    if (handledNavigation.current === signature) return;
    handledNavigation.current = signature;

    if (legacyId === 'pricing') {
      navigate(ROUTES.PRICING, { replace: true });
      return;
    }

    const sectionId = target ?? (isLandingSectionId(legacyId) ? legacyId : null);
    if (sectionId) {
      // Runs after ScrollToTop and before paint. Must not be cancelled when this
      // effect re-runs to drop the temporary history state or the legacy hash.
      queueMicrotask(() => scrollToLandingSection(sectionId));
    }

    navigate(
      { pathname: location.pathname, search: location.search, hash: '' },
      { replace: true, state: null },
    );
  }, [location.hash, location.pathname, location.search, location.state, navigate]);

  return (
    <PublicLayout overlay>
      <PageMeta
        title="AI Dashboard — From database to insights"
        description="Connect PostgreSQL, MySQL, or SQL Server and describe the chart you want. AI Dashboard writes a read-only query and places it on a dashboard you can arrange."
        path={ROUTES.HOME}
      />
      <HeroSection />
      <div id="demo" className="relative z-10 -mt-28 scroll-mt-24 sm:-mt-36 md:-mt-44">
        <DashboardPreview />
      </div>
      <FeaturesSection />
      <BentoSection />
      <TestimonialsSection />
      <PricingSection />
      <CtaSection />
    </PublicLayout>
  );
}
