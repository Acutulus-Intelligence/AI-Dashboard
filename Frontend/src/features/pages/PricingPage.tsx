import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Check, ArrowRight } from 'lucide-react';
import Header from '../layouts/Header';
import Footer from '../layouts/Footer';
import Button from '../components/Button';
import { ROUTES } from '../routes';

const plans = [
  {
    id: 'individual',
    name: 'Individual',
    description: 'Perfect for solo analysts and freelancers.',
    monthlyPrice: 4.95,
    yearlyPrice: 49.95,
    features: [
      'Single user account',
      'Connect external databases',
      'AI-powered chart generation',
      'Unlimited dashboards',
      'Real-time collaboration',
    ],
    popular: false,
  },
  {
    id: 'company',
    name: 'Company',
    description: 'For teams that need shared insights.',
    monthlyPrice: 19.95,
    yearlyPrice: 199.95,
    features: [
      'Up to 10 team members',
      'Connect external databases',
      'AI-powered chart generation',
      'Unlimited dashboards',
      'Role-based access control',
      'Admin panel & user management',
      'Team dashboards & sharing',
    ],
    popular: true,
  },
  {
    id: 'enterprise',
    name: 'Enterprise',
    description: 'For organizations with advanced needs.',
    features: [
      'Unlimited users',
      'Custom AI models & fine-tuning',
      'Dedicated support',
      'On-premise deployment option',
      'SSO & advanced security',
      'Custom integrations',
      'SLA guarantees',
    ],
    popular: false,
    custom: true,
  },
];

export default function PricingPage() {
  const [billing, setBilling] = useState<'monthly' | 'yearly'>('monthly');
  const navigate = useNavigate();

  function handleChoosePlan(planId: string) {
    if (planId === 'individual') {
      navigate(`${ROUTES.REGISTER}?type=individual`);
    } else if (planId === 'company') {
      navigate(`${ROUTES.REGISTER}?type=company`);
    }
  }

  return (
    <div className="min-h-screen bg-background text-on-background">
      <Header />

      <main>
        <div className="mx-auto max-w-container-max px-gutter pb-24 pt-24">
          <div className="mb-12 text-center">
            <h1 className="text-display-md font-bold text-on-background">
              Choose your plan
            </h1>
            <p className="mt-3 text-body-lg text-on-surface-variant">
              Pick the right plan for you or your team.
            </p>
          </div>

          <div className="mb-10 flex items-center justify-center gap-3">
            <button
              type="button"
              onClick={() => setBilling('monthly')}
              className={`rounded-lg px-4 py-2 text-body-sm font-semibold transition-all ${
                billing === 'monthly'
                  ? 'bg-primary text-on-primary'
                  : 'text-on-surface-variant hover:text-on-background'
              }`}
            >
              Monthly
            </button>
            <button
              type="button"
              onClick={() => setBilling('yearly')}
              className={`rounded-lg px-4 py-2 text-body-sm font-semibold transition-all ${
                billing === 'yearly'
                  ? 'bg-primary text-on-primary'
                  : 'text-on-surface-variant hover:text-on-background'
              }`}
            >
              Yearly
              <span className="ml-1.5 rounded-full bg-green-100 px-2 py-0.5 text-body-xs text-green-700">
                Save ~16%
              </span>
            </button>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            {plans.map((plan) => (
              <div
                key={plan.id}
                className={`relative flex flex-col rounded-2xl border-2 bg-surface p-8 shadow-sm transition-shadow hover:shadow-md ${
                  plan.popular
                    ? 'border-primary'
                    : 'border-outline-variant'
                }`}
              >
                {plan.popular && (
                  <span className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-primary px-4 py-1 text-body-xs font-semibold text-on-primary">
                    Most popular
                  </span>
                )}

                <div className="mb-6">
                  <h2 className="text-headline-md font-bold text-on-background">
                    {plan.name}
                  </h2>
                  <p className="mt-1 text-body-sm text-on-surface-variant">
                    {plan.description}
                  </p>
                </div>

                {'monthlyPrice' in plan && plan.monthlyPrice !== undefined ? (
                  <div className="mb-6">
                    <div className="flex items-baseline gap-2">
                      <span className="text-display-sm font-bold text-on-background">
                        ${billing === 'monthly' ? plan.monthlyPrice : plan.yearlyPrice}
                      </span>
                      <span className="text-body-md text-on-surface-variant">
                        /{billing === 'monthly' ? 'mo' : 'yr'}
                      </span>
                    </div>
                  </div>
                ) : (
                  <div className="mb-6">
                    <span className="text-display-sm font-bold text-on-background">
                      Custom
                    </span>
                  </div>
                )}

                <ul className="mb-8 flex-1 space-y-3">
                  {plan.features.map((feature) => (
                    <li key={feature} className="flex items-start gap-2 text-body-sm text-on-background">
                      <Check size={18} className="mt-0.5 shrink-0 text-green-500" />
                      <span>{feature}</span>
                    </li>
                  ))}
                </ul>

                {'custom' in plan && plan.custom ? (
                  <Button
                    variant="outline"
                    className="w-full"
                    onClick={() => navigate(ROUTES.CONTACT)}
                  >
                    Contact us
                    <ArrowRight size={16} />
                  </Button>
                ) : (
                  <Button
                    variant={plan.popular ? 'primary' : 'outline'}
                    className="w-full"
                    onClick={() => handleChoosePlan(plan.id)}
                  >
                    Get started
                    <ArrowRight size={16} />
                  </Button>
                )}
              </div>
            ))}
          </div>

          <p className="mt-12 text-center text-body-sm text-on-surface-variant">
            Already have an account?{' '}
            <Link to={ROUTES.LOGIN} className="font-semibold text-primary hover:underline">
              Sign in
            </Link>
          </p>
        </div>
      </main>

      <Footer />
    </div>
  );
}
