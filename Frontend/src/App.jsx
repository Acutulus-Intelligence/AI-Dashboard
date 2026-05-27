import Footer from './components/layout/Footer.jsx';
import Header from './components/layout/Header.jsx';
import DashboardsPage from './components/customer/DashboardsPage.jsx';
import BenefitsSection from './components/sections/BenefitsSection.jsx';
import CtaSection from './components/sections/CtaSection.jsx';
import FeaturesSection from './components/sections/FeaturesSection.jsx';
import HeroSection from './components/sections/HeroSection.jsx';
import SocialProofSection from './components/sections/SocialProofSection.jsx';

export default function App() {
  const isCustomerApp = window.location.pathname.startsWith('/app');

  if (isCustomerApp) {
    return <DashboardsPage />;
  }

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
