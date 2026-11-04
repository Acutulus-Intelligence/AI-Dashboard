import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { LogIn, Eye, EyeOff, ShieldCheck, ArrowLeft } from 'lucide-react';
import PageMeta from '../components/PageMeta';
import PublicButton from '../components/PublicButton';
import PublicError from '../components/PublicError';
import PublicAuthFrame from '../layouts/PublicAuthFrame';
import PublicLayout from '../layouts/PublicLayout';
import { cn } from '@/lib/utils';
import { publicInputClass, publicLabelClass, publicLinkClass } from '../layouts/publicChrome';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';

type LoginStep = 'credentials' | 'twofactor';

const CHALLENGE_STORAGE_KEY = 'auth.2fa.challenge';
const CHALLENGE_TTL_MS = 5 * 60 * 1000;

type StoredChallenge = { token: string; expiresAt: number };

function readStoredChallenge(): StoredChallenge | null {
  try {
    const raw = sessionStorage.getItem(CHALLENGE_STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as Partial<StoredChallenge>;
    if (
      typeof parsed.token !== 'string' ||
      typeof parsed.expiresAt !== 'number' ||
      Date.now() >= parsed.expiresAt
    ) {
      sessionStorage.removeItem(CHALLENGE_STORAGE_KEY);
      return null;
    }

    return { token: parsed.token, expiresAt: parsed.expiresAt };
  } catch {
    return null;
  }
}

function storeChallenge(token: string) {
  try {
    sessionStorage.setItem(
      CHALLENGE_STORAGE_KEY,
      JSON.stringify({ token, expiresAt: Date.now() + CHALLENGE_TTL_MS }),
    );
  } catch {
    /* ignore unavailable storage */
  }
}

function clearStoredChallenge() {
  try {
    sessionStorage.removeItem(CHALLENGE_STORAGE_KEY);
  } catch {
    /* ignore unavailable storage */
  }
}

// Returns a same-origin relative target (path+query+hash) or null. Browsers
// normalize backslashes to slashes, so a raw `\` is rejected outright to avoid
// protocol-relative redirects like `/\evil.com` (S4).
function safeRedirectTarget(raw: string | null): string | null {
  if (!raw || raw.includes('\\')) return null;
  try {
    const url = new URL(raw, window.location.origin);
    if (url.origin !== window.location.origin) return null;
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return null;
  }
}

export default function LoginPage() {
  const { login, completeTwoFactorLogin } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const redirectTo = searchParams.get('redirect');

  const [initialChallenge] = useState(readStoredChallenge);
  const [step, setStep] = useState<LoginStep>(initialChallenge ? 'twofactor' : 'credentials');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [code, setCode] = useState('');
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const [challengeToken, setChallengeToken] = useState(initialChallenge?.token ?? '');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function navigateAfterLogin(user: { roles: string[]; isDeactivated?: boolean } | null) {
    if (user?.roles.includes('Admin') || user?.roles.includes('Moderator')) {
      navigate(ROUTES.ADMIN_MAIN);
      return;
    }
    if (user?.isDeactivated) {
      navigate(ROUTES.PROFILE);
      return;
    }
    // Only allow same-origin relative paths to avoid open-redirect abuse. Applied
    // after the admin/deactivation checks so gated users still land correctly.
    const target = safeRedirectTarget(redirectTo);
    if (target) {
      navigate(target);
      return;
    }
    // Always land in-app. Users without a subscription see an in-app upsell
    // (and can still reach profile/settings) rather than the public pricing page.
    navigate(ROUTES.DASHBOARD);
  }

  function messageForError(err: unknown, fallback: string) {
    if (err && typeof err === 'object') {
      const status = 'status' in err ? (err as { status?: number }).status : undefined;
      const code = 'code' in err ? (err as { code?: string }).code : undefined;
      if (code === 'account_locked' || status === 423) {
        return 'Your account is temporarily locked after too many failed attempts. Please try again in a few minutes.';
      }
      if (status === 429) {
        return 'Too many attempts. Please wait a moment and try again.';
      }
    }
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
        storeChallenge(result.challengeToken);
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
      clearStoredChallenge();
      await navigateAfterLogin(user);
    } catch (err: unknown) {
      setError(messageForError(err, 'Verification failed. Please try again.'));
    } finally {
      setLoading(false);
    }
  }

  function backToCredentials() {
    clearStoredChallenge();
    setStep('credentials');
    setChallengeToken('');
    setCode('');
    setUseRecoveryCode(false);
    setError('');
  }

  return (
    <PublicLayout>
      <PageMeta
        title="Log in — AI Dashboard"
        description="Sign in to AI Dashboard to open your dashboards, connections, and charts."
        path={ROUTES.LOGIN}
      />
      <PublicAuthFrame>
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-semibold tracking-tight text-slate-900">
            {step === 'credentials' ? 'Welcome back' : 'Two-factor verification'}
          </h1>
          <p className="mt-2 text-base leading-relaxed text-slate-600">
            {step === 'credentials'
              ? 'Sign in to your account'
              : useRecoveryCode
                ? 'Enter one of your saved recovery codes'
                : 'Enter the code from your authenticator app'}
          </p>
        </div>

        {error && (
          <div className="mb-6">
            <PublicError>{error}</PublicError>
          </div>
        )}

        {step === 'credentials' ? (
          <form onSubmit={handleCredentialsSubmit} className="space-y-5">
            <div>
              <label htmlFor="email" className={publicLabelClass}>
                Email
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                autoComplete="email"
                className={publicInputClass}
              />
            </div>

            <div>
              <label htmlFor="password" className={publicLabelClass}>
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
                  className={cn(publicInputClass, 'pr-12')}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-1.5 top-1/2 inline-flex size-10 -translate-y-1/2 items-center justify-center rounded-full text-slate-500 hover:bg-slate-100 hover:text-slate-900"
                  tabIndex={-1}
                >
                  {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </div>

            <PublicButton type="submit" className="w-full" disabled={loading}>
              {loading ? (
                <span className="flex items-center gap-2">
                  <div className="size-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                  Signing in...
                </span>
              ) : (
                <span className="flex items-center gap-2">
                  <LogIn size={18} />
                  Sign In
                </span>
              )}
            </PublicButton>
          </form>
        ) : (
          <form onSubmit={handleTwoFactorSubmit} className="space-y-5">
            <div className="flex justify-center text-blue-700">
              <ShieldCheck size={40} />
            </div>

            <div>
              <label htmlFor="code" className={publicLabelClass}>
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
                className={`${publicInputClass} text-center text-lg tracking-[0.3em] placeholder:tracking-normal`}
              />
            </div>

            <PublicButton type="submit" className="w-full" disabled={loading}>
              {loading ? (
                <span className="flex items-center gap-2">
                  <div className="size-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                  Verifying...
                </span>
              ) : (
                <span className="flex items-center gap-2">
                  <ShieldCheck size={18} />
                  Verify
                </span>
              )}
            </PublicButton>

            <div className="flex flex-col items-center gap-2 text-sm">
              <button
                type="button"
                onClick={() => {
                  setUseRecoveryCode((v) => !v);
                  setCode('');
                  setError('');
                }}
                className={publicLinkClass}
              >
                {useRecoveryCode ? 'Use an authenticator code instead' : 'Use a recovery code'}
              </button>
              <button
                type="button"
                onClick={backToCredentials}
                className="inline-flex items-center gap-1 text-slate-600 hover:text-slate-900"
              >
                <ArrowLeft size={14} />
                Back to sign in
              </button>
            </div>
          </form>
        )}

        {step === 'credentials' && (
          <>
            <p className="mt-6 text-center text-sm text-slate-600">
              Don&apos;t have an account?{' '}
              <Link to={ROUTES.REGISTER} className={publicLinkClass}>
                Create one
              </Link>
            </p>
            <p className="mt-4 text-center text-sm text-slate-600">
              <Link to={ROUTES.PRIVACY} className={publicLinkClass}>
                Privacy Policy
              </Link>
              {' · '}
              <Link to={ROUTES.TERMS} className={publicLinkClass}>
                Terms of Service
              </Link>
            </p>
          </>
        )}
      </PublicAuthFrame>
    </PublicLayout>
  );
}
