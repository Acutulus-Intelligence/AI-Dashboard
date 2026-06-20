import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { User, Building2, Eye, EyeOff, AlertCircle, UserPlus, ArrowLeft, Asterisk } from 'lucide-react';
import Button from '../components/Button';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';

export default function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const preselectedType = searchParams.get('type') === 'company' ? 'Company' : 'Individual';
  const [userType, setUserType] = useState<'Individual' | 'Company'>(preselectedType);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');

    if (!email.trim() || !password || !firstName.trim() || !lastName.trim()) {
      setError('Please fill in all required fields.');
      return;
    }

    if (password.length < 8) {
      setError('Password must be at least 8 characters.');
      return;
    }

    if (!/[A-Z]/.test(password)) {
      setError('Password must contain at least one uppercase letter.');
      return;
    }

    if (!/[a-z]/.test(password)) {
      setError('Password must contain at least one lowercase letter.');
      return;
    }

    if (!/[0-9]/.test(password)) {
      setError('Password must contain at least one digit (0-9).');
      return;
    }

    if (!/[^a-zA-Z0-9]/.test(password)) {
      setError('Password must contain at least one special character (e.g. !@#$%).');
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    if (userType === 'Company' && !companyName.trim()) {
      setError('Please enter your company name.');
      return;
    }

    setLoading(true);
    try {
      await register({
        email,
        password,
        firstName,
        lastName,
        userType: userType === 'Individual' ? 0 : 1,
        companyName: userType === 'Company' && companyName.trim() ? companyName.trim() : undefined,
      });
      navigate(ROUTES.DASHBOARD);
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  }

  const showTypeSelector = !searchParams.get('type');

  return (
    <div className="min-h-screen bg-background text-on-background">
      <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-md">
        <div className="rounded-2xl border border-outline-variant bg-surface p-8 shadow-sm">
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
              {showTypeSelector
                ? 'Choose your account type to get started'
                : `Signing up for the ${userType} plan`}
            </p>
          </div>

          {error && (
            <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
              <AlertCircle size={16} className="shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit}>
            {showTypeSelector && (
            <div className="mb-6 grid grid-cols-2 gap-3">
              <button
                type="button"
                onClick={() => setUserType('Individual')}
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
                onClick={() => setUserType('Company')}
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
            )}

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

              <div className="grid grid-cols-2 gap-4">
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

              <Button
                type="submit"
                variant="primary"
                className="w-full"
                disabled={loading}
              >
                {loading ? (
                  <span className="flex items-center gap-2">
                    <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                    Creating account...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    <UserPlus size={18} />
                    {userType === 'Individual' ? 'Create Individual Account' : 'Create Company Account'}
                  </span>
                )}
              </Button>
            </div>
          </form>

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
