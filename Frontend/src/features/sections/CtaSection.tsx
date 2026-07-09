import { Link } from 'react-router-dom';
import Button from '../components/Button';
import { ROUTES } from '../routes';

export default function CtaSection() {
  return (
    <section id="pricing" className="relative overflow-hidden bg-primary-container px-gutter py-16">
      <div
        className="absolute inset-0 opacity-10"
        style={{
          backgroundImage: 'radial-gradient(circle at 2px 2px, #06b6d4 1px, transparent 0)',
          backgroundSize: '24px 24px',
        }}
      />
      <div className="relative z-10 mx-auto max-w-container-max text-center">
        <h2 className="mb-6 text-4xl font-bold leading-tight text-on-primary md:text-display-lg">
          Ready to transform your data?
        </h2>
        <p className="mx-auto mb-8 max-w-2xl text-body-lg text-on-primary-container">
          Join thousands of analysts saving time every day with AI Dashboard. Start your
          free trial today.
        </p>
        <div className="flex flex-wrap justify-center gap-4">
          <Link to={ROUTES.PRICING}>
            <Button>Get Started</Button>
          </Link>
          <Link to={ROUTES.LOGIN}>
            <Button variant="surface">Log in</Button>
          </Link>
        </div>
      </div>
    </section>
  );
}
