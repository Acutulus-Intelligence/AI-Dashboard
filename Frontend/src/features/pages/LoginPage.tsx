import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { LogIn, Eye, EyeOff, AlertCircle, ShieldCheck, ArrowLeft } from 'lucide-react';
import Button from '../components/Button';
import Header from '../layouts/Header';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';

type LoginStep = 'credentials' | 'twofactor';

export default function LoginPage() {
  const { login, completeTwoFactorLogin, refreshSubscriptionStatus } = useAuth();
  const navigate = useNavigate();

  const [step, setStep] = useState<LoginStep>('credentials');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [code, setCode] = useState('');
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const [challengeToken, setChallengeToken] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function navigateAfterLogin(user: { roles: string[] } | null) {
    if (user?.roles.includes('Admin') || user?.roles.includes('Moderator')) {
      navigate(ROUTES.ADMIN_MAIN);
      return;
    }
    const isActive = await refreshSubscriptionStatus();
    navigate(isActive ? ROUTES.DASHBOARD : ROUTES.PRICING);
  }

  function messageForError(err: unknown, fallback: string) {
    return err instanceof Error ? err.message : fallback;
  }

  async function handleCredentialsSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');

    if (!email.trim() || !password) {
      setError('Please fill in all fields.');
      return;
    }

    setLoading(true);
    try {
      const result = await login(email, password);

      if (result.status === 'twoFactorRequired') {
        setChallengeToken(result.challengeToken);
        setCode('');
        setUseRecoveryCode(false);
        setStep('twofactor');
        return;
      }

      await navigateAfterLogin(result.user);
    } catch (err: unknown) {
      setError(messageForError(err, 'An unexpected error occurred. Please try again.'));
    } finally {
      setLoading(false);
    }
  }

  async function handleTwoFactorSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');

    if (!code.trim()) {
      setError(useRecoveryCode ? 'Enter one of your recovery codes.' : 'Enter the 6-digit code.');
      return;
    }

    setLoading(true);
    try {
      const user = await completeTwoFactorLogin(challengeToken, code.trim(), useRecoveryCode);
      await navigateAfterLogin(user);
    } catch (err: unknown) {
      setError(messageForError(err, 'Verification failed. Please try again.'));
    } finally {
      setLoading(false);
    }
  }

  function backToCredentials() {
    setStep('credentials');
    setChallengeToken('');
    setCode('');
    setUseRecoveryCode(false);
    setError('');
  }

  return (
    <div className="min-h-screen bg-background text-on-background">
      <Header />
      <main className="flex min-h-[calc(100vh-4rem)] items-center justify-center px-4 pt-16">
      <div className="w-full max-w-md">
        <div className="rounded-2xl border border-outline-variant bg-surface p-8 shadow-xs">
          <div className="mb-8 text-center">
            <h1 className="text-headline-lg font-bold text-on-background">
              {step === 'credentials' ? 'Welcome back' : 'Two-factor verification'}
            </h1>
            <p className="mt-2 text-body-md text-on-surface-variant">
              {step === 'credentials'
                ? 'Sign in to your account'
                : useRecoveryCode
                  ? 'Enter one of your saved recovery codes'
                  : 'Enter the code from your authenticator app'}
            </p>
          </div>

          {error && (
            <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
              <AlertCircle size={16} className="shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {step === 'credentials' ? (
            <form onSubmit={handleCredentialsSubmit} className="space-y-5">
              <div>
                <label htmlFor="email" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                  Email
                </label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  autoComplete="email"
                  className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-hidden focus:ring-2 focus:ring-primary/20"
                />
              </div>

              <div>
                <label htmlFor="password" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                  Password
                </label>
                <div className="relative">
                  <input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="Enter your password"
                    autoComplete="current-password"
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 pr-11 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-hidden focus:ring-2 focus:ring-primary/20"
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

              <Button
                type="submit"
                variant="primary"
                className="w-full"
                disabled={loading}
              >
                {loading ? (
                  <span className="flex items-center gap-2">
                    <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                    Signing in...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    <LogIn size={18} />
                    Sign In
                  </span>
                )}
              </Button>
            </form>
          ) : (
            <form onSubmit={handleTwoFactorSubmit} className="space-y-5">
              <div className="flex justify-center text-primary">
                <ShieldCheck size={40} />
              </div>

              <div>
                <label htmlFor="code" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                  {useRecoveryCode ? 'Recovery code' : 'Verification code'}
                </label>
                <input
                  id="code"
                  type="text"
                  inputMode={useRecoveryCode ? 'text' : 'numeric'}
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  placeholder={useRecoveryCode ? 'XXXXX-XXXXX' : '123456'}
                  autoComplete="one-time-code"
                  autoFocus
                  className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3 text-center text-lg tracking-[0.3em] text-on-background placeholder:tracking-normal placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-hidden focus:ring-2 focus:ring-primary/20"
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
                    Verifying...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    <ShieldCheck size={18} />
                    Verify
                  </span>
                )}
              </Button>

              <div className="flex flex-col items-center gap-2 text-body-sm">
                <button
                  type="button"
                  onClick={() => {
                    setUseRecoveryCode((v) => !v);
                    setCode('');
                    setError('');
                  }}
                  className="font-medium text-primary hover:underline"
                >
                  {useRecoveryCode ? 'Use an authenticator code instead' : 'Use a recovery code'}
                </button>
                <button
                  type="button"
                  onClick={backToCredentials}
                  className="inline-flex items-center gap-1 text-on-surface-variant hover:text-on-background"
                >
                  <ArrowLeft size={14} />
                  Back to sign in
                </button>
              </div>
            </form>
          )}

          {step === 'credentials' && (
            <>
              <p className="mt-6 text-center text-body-sm text-on-surface-variant">
                Don&apos;t have an account?{' '}
                <Link to={ROUTES.REGISTER} className="font-semibold text-primary hover:underline">
                  Create one
                </Link>
              </p>
              <p className="mt-4 text-center text-body-sm text-on-surface-variant">
                <Link to={ROUTES.PRIVACY} className="font-medium text-primary hover:underline">
                  Privacy Policy
                </Link>
                {' · '}
                <Link to={ROUTES.TERMS} className="font-medium text-primary hover:underline">
                  Terms of Service
                </Link>
              </p>
            </>
          )}
        </div>
      </div>
      </main>
    </div>
  );
}
