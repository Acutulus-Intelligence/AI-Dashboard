import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { AlertCircle, ArrowLeft, Asterisk, Building2, Eye, EyeOff, User, UserPlus } from 'lucide-react';
import Button from '../components/Button';
import BillingToggle from '../components/BillingToggle';
import PlanCards from '../components/PlanCards';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';
import {
  BILLING_PERIOD,
  FREE_TRIAL_DAYS,
  USER_TYPE,
  createCheckout,
  getPlans,
  type BillingPeriod,
  type SubscriptionPlan,
} from '../../lib/api/subscription';

type AccountType = 'Individual' | 'Company';
type RegisterStep = 1 | 2 | 3;

function accountTypeToUserType(accountType: AccountType) {
  return accountType === 'Individual' ? USER_TYPE.Individual : USER_TYPE.Company;
}

export default function RegisterPage() {
  const { register } = useAuth();
  const [searchParams] = useSearchParams();

  const preselectedType = searchParams.get('type') === 'company' ? 'Company' : 'Individual';
  const preselectedPlanId = searchParams.get('planId') ?? '';
  const preselectedBilling = searchParams.get('billing') === '1' ? BILLING_PERIOD.Yearly : BILLING_PERIOD.Monthly;

  const [step, setStep] = useState<RegisterStep>(searchParams.get('type') ? 2 : 1);
  const [userType, setUserType] = useState<AccountType>(preselectedType);
  const [billing, setBilling] = useState<BillingPeriod>(preselectedBilling);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [plansLoading, setPlansLoading] = useState(false);
  const [selectedPlanId, setSelectedPlanId] = useState(preselectedPlanId);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const selectedPlan = useMemo(
    () => plans.find((plan) => plan.id === selectedPlanId) ?? null,
    [plans, selectedPlanId],
  );

  useEffect(() => {
    async function loadPlans() {
      setPlansLoading(true);
      setError('');
      try {
        const response = await getPlans(accountTypeToUserType(userType));
        const activePlans = response.filter((plan) => plan.isActive);
        setPlans(activePlans);

        if (!activePlans.some((plan) => plan.id === selectedPlanId)) {
          setSelectedPlanId(activePlans[0]?.id ?? '');
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Could not load plans.');
      } finally {
        setPlansLoading(false);
      }
    }

    void loadPlans();
  }, [selectedPlanId, userType]);

  function validateForm() {
    if (!selectedPlan) return 'Please select a plan.';
    if (!email.trim() || !password || !firstName.trim() || !lastName.trim()) return 'Please fill in all required fields.';
    if (password.length < 8) return 'Password must be at least 8 characters.';
    if (!/[A-Z]/.test(password)) return 'Password must contain at least one uppercase letter.';
    if (!/[a-z]/.test(password)) return 'Password must contain at least one lowercase letter.';
    if (!/[0-9]/.test(password)) return 'Password must contain at least one digit (0-9).';
    if (!/[^a-zA-Z0-9]/.test(password)) return 'Password must contain at least one special character (e.g. !@#$%).';
    if (password !== confirmPassword) return 'Passwords do not match.';
    if (userType === 'Company' && !companyName.trim()) return 'Please enter your company name.';
    return '';
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');

    const validationError = validateForm();
    if (validationError) {
      setError(validationError);
      return;
    }

    setLoading(true);
    try {
      await register({
        email,
        password,
        firstName,
        lastName,
        userType: accountTypeToUserType(userType),
        companyName: userType === 'Company' && companyName.trim() ? companyName.trim() : undefined,
      });

      const checkout = await createCheckout({
        planId: selectedPlanId,
        billingPeriod: billing,
        successUrl: `${window.location.origin}${ROUTES.PAYMENT_SUCCESS}`,
        cancelUrl: `${window.location.origin}${ROUTES.PAYMENT_CANCEL}`,
      });

      window.location.href = checkout.checkoutUrl;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'An unexpected error occurred. Please try again.');
      setLoading(false);
    }
  }

  function chooseAccountType(nextType: AccountType) {
    setUserType(nextType);
    setSelectedPlanId('');
  }

  return (
    <div className="min-h-screen bg-background text-on-background">
      <main className="flex min-h-screen justify-center px-4 py-8">
        <div className="w-full max-w-5xl">
          <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm md:p-8">
            <div className="mb-6">
              <Link
                to={ROUTES.PRICING}
                className="mb-4 inline-flex items-center gap-1.5 text-body-sm font-medium text-primary hover:underline"
              >
                <ArrowLeft size={16} />
                Back to plans
              </Link>
              <h1 className="text-headline-lg font-bold text-on-background">Create your account</h1>
              <p className="mt-2 text-body-md text-on-surface-variant">
                Step {step} of 3
              </p>
            </div>

            {error && (
              <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
                <AlertCircle size={16} className="shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {step === 1 && (
              <div>
                <div className="mb-6 grid gap-3 md:grid-cols-2">
                  <button
                    type="button"
                    onClick={() => chooseAccountType('Individual')}
                    className={`flex flex-col items-center gap-2 rounded-xl border-2 px-4 py-5 text-center transition-all ${
                      userType === 'Individual'
                        ? 'border-primary bg-primary/5 text-primary'
                        : 'border-outline-variant bg-surface-container-lowest text-on-surface-variant hover:border-primary/50'
                    }`}
                  >
                    <User size={28} />
                    <span className="text-body-sm font-semibold">Individual</span>
                    <span className="text-body-xs text-on-surface-variant/70">Single user account</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => chooseAccountType('Company')}
                    className={`flex flex-col items-center gap-2 rounded-xl border-2 px-4 py-5 text-center transition-all ${
                      userType === 'Company'
                        ? 'border-primary bg-primary/5 text-primary'
                        : 'border-outline-variant bg-surface-container-lowest text-on-surface-variant hover:border-primary/50'
                    }`}
                  >
                    <Building2 size={28} />
                    <span className="text-body-sm font-semibold">Company</span>
                    <span className="text-body-xs text-on-surface-variant/70">Team with admin tools</span>
                  </button>
                </div>
                <Button type="button" className="w-full" onClick={() => setStep(2)}>
                  Continue
                </Button>
              </div>
            )}

            {step === 2 && (
              <div>
                <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
                  <div>
                    <h2 className="text-headline-md font-semibold">Choose a plan</h2>
                    <p className="text-body-sm text-on-surface-variant">
                      Showing plans for {userType.toLowerCase()} accounts.
                    </p>
                  </div>
                  <BillingToggle billing={billing} onChange={setBilling} />
                </div>

                {plansLoading ? (
                  <div className="flex min-h-52 items-center justify-center">
                    <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
                  </div>
                ) : (
                  <PlanCards
                    billing={billing}
                    plans={plans}
                    selectedPlanId={selectedPlanId}
                    onSelect={(plan) => setSelectedPlanId(plan.id)}
                  />
                )}

                <div className="mt-6 flex flex-wrap gap-3">
                  <Button type="button" variant="outline" onClick={() => setStep(1)}>
                    Back
                  </Button>
                  <Button type="button" disabled={!selectedPlanId} onClick={() => setStep(3)}>
                    Continue
                  </Button>
                </div>
              </div>
            )}

            {step === 3 && (
              <form onSubmit={handleSubmit}>
                <div className="mb-6 rounded-xl border border-outline-variant bg-surface-container-lowest p-4">
                  <p className="font-mono text-label-caps uppercase text-outline">Selected plan</p>
                  <p className="mt-1 text-body-md font-semibold text-on-background">
                    {selectedPlan?.name ?? 'No plan selected'} · {billing === 0 ? 'Monthly' : 'Yearly'}
                  </p>
                  <p className="mt-1 text-body-sm text-on-surface-variant">
                    Includes a {FREE_TRIAL_DAYS}-day free trial before billing starts.
                  </p>
                </div>

                <div className="space-y-5">
                  {userType === 'Company' && (
                    <div>
                      <label htmlFor="companyName" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                        Company Name
                      </label>
                      <input
                        id="companyName"
                        type="text"
                        value={companyName}
                        onChange={(e) => setCompanyName(e.target.value)}
                        placeholder="Acme Inc."
                        className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                      />
                    </div>
                  )}

                  <div className="grid gap-4 md:grid-cols-2">
                    <div>
                      <label htmlFor="firstName" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                        First Name
                      </label>
                      <input
                        id="firstName"
                        type="text"
                        value={firstName}
                        onChange={(e) => setFirstName(e.target.value)}
                        placeholder="John"
                        className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                      />
                    </div>
                    <div>
                      <label htmlFor="lastName" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                        Last Name
                      </label>
                      <input
                        id="lastName"
                        type="text"
                        value={lastName}
                        onChange={(e) => setLastName(e.target.value)}
                        placeholder="Doe"
                        className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                      />
                    </div>
                  </div>

                  <div>
                    <label htmlFor="regEmail" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                      Email
                    </label>
                    <input
                      id="regEmail"
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      placeholder="you@example.com"
                      autoComplete="email"
                      className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                    />
                  </div>

                  <div>
                    <div className="mb-1.5 flex items-center">
                      <label htmlFor="regPassword" className="text-body-sm font-medium text-on-surface-variant">
                        Password
                      </label>
                      <span className="relative">
                        <Asterisk size={12} className="cursor-help text-red-400 peer" />
                        <span className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 -translate-x-1/2 whitespace-nowrap rounded-lg border border-outline-variant bg-surface px-2.5 py-1.5 text-[11px] leading-snug text-on-surface-variant opacity-0 shadow-sm transition-opacity peer-hover:opacity-100">
                          Min 8 chars, 1 uppercase, 1 lowercase,<br />1 digit, 1 special character
                        </span>
                      </span>
                    </div>
                    <div className="relative">
                      <input
                        id="regPassword"
                        type={showPassword ? 'text' : 'password'}
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        placeholder="At least 8 characters"
                        autoComplete="new-password"
                        className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 pr-11 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                      />
                      <button
                        type="button"
                        onClick={() => setShowPassword(!showPassword)}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-on-background"
                        tabIndex={-1}
                      >
                        {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                      </button>
                    </div>
                  </div>

                  <div>
                    <label htmlFor="confirmPassword" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                      Confirm Password
                    </label>
                    <input
                      id="confirmPassword"
                      type="password"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      placeholder="Repeat your password"
                      autoComplete="new-password"
                      className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                    />
                  </div>

                  <div className="flex flex-wrap gap-3">
                    <Button type="button" variant="outline" onClick={() => setStep(2)}>
                      Back
                    </Button>
                    <Button type="submit" variant="primary" className="flex-1" disabled={loading}>
                      {loading ? (
                        <span className="flex items-center gap-2">
                          <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                          Preparing checkout...
                        </span>
                      ) : (
                        <span className="flex items-center gap-2">
                          <UserPlus size={18} />
                          Create account and checkout
                        </span>
                      )}
                    </Button>
                  </div>
                </div>
              </form>
            )}

            <p className="mt-6 text-center text-body-sm text-on-surface-variant">
              Already have an account?{' '}
              <Link to={ROUTES.LOGIN} className="font-semibold text-primary hover:underline">
                Sign in
              </Link>
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}
