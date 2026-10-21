import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, Eye, EyeOff, Mail, ArrowLeft } from 'lucide-react';
import Button from '../components/Button';
import PasswordRequirements from '../components/PasswordRequirements';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';
import { ApiError } from '../../lib/api/client';
import * as invitationsApi from '../../lib/api/invitations';
import * as companyApi from '../../lib/api/company';
import {
  type RegisterFieldErrors,
  mapRegisterApiError,
  validateRegisterForm,
} from '../validation/registerForm';

const INVITE_TOKEN_STORAGE_KEY = 'invite.token';

// The invite token arrives in the URL fragment (never the path, so it is not
// logged or leaked via Referer). We persist it to sessionStorage and strip it
// from the address bar so it also stays out of browser history (S3).
function readInviteToken(): string {
  const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : '';
  if (hash) {
    try {
      sessionStorage.setItem(INVITE_TOKEN_STORAGE_KEY, hash);
    } catch {
      /* ignore unavailable storage */
    }
    window.history.replaceState(null, '', window.location.pathname + window.location.search);
    return hash;
  }
  try {
    return sessionStorage.getItem(INVITE_TOKEN_STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}

function clearStoredInviteToken() {
  try {
    sessionStorage.removeItem(INVITE_TOKEN_STORAGE_KEY);
  } catch {
    /* ignore unavailable storage */
  }
}

function formatExpiry(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function fieldClass(hasError: boolean) {
  return `w-full rounded-xl border bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:outline-hidden focus:ring-2 ${
    hasError
      ? 'border-red-400 focus:border-red-400 focus:ring-red-200'
      : 'border-outline-variant focus:border-primary focus:ring-primary/20'
  }`;
}

export default function InviteAcceptPage() {
  const [token] = useState(readInviteToken);
  const navigate = useNavigate();
  const { user, isLoading: authLoading, refreshUser } = useAuth();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [invitation, setInvitation] = useState<invitationsApi.InvitationDetails | null>(null);

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState('');
  const [fieldErrors, setFieldErrors] = useState<RegisterFieldErrors>({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      setLoadError('');
      if (!token) {
        setLoadError('This invitation is invalid or has expired.');
        setLoading(false);
        return;
      }
      try {
        const details = await invitationsApi.lookupInvitation(token);
        if (active) setInvitation(details);
      } catch (err: unknown) {
        if (!active) return;
        setLoadError(
          err instanceof Error ? err.message : 'This invitation is invalid or has expired.',
        );
      } finally {
        if (active) setLoading(false);
      }
    }
    void load();
    return () => {
      active = false;
    };
  }, [token]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setFormError('');
    setFieldErrors({});

    const validation = validateRegisterForm({
      firstName,
      lastName,
      email: invitation?.email ?? '',
      password,
      confirmPassword,
    });
    if (!validation.isValid) {
      setFieldErrors(validation.fieldErrors);
      return;
    }

    setSubmitting(true);
    try {
      await invitationsApi.acceptEmailInvitation(token, { firstName, lastName, password });
      clearStoredInviteToken();
      await refreshUser();
      navigate(ROUTES.DASHBOARD, { replace: true });
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        const mapped = mapRegisterApiError(err);
        setFieldErrors(mapped.fieldErrors);
        if (mapped.formError) setFormError(mapped.formError);
      } else {
        setFormError(err instanceof Error ? err.message : 'Failed to accept the invitation.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleAcceptExisting() {
    if (!invitation) return;
    setFormError('');
    setSubmitting(true);
    try {
      await companyApi.acceptInvite({ inviteId: invitation.inviteId });
      clearStoredInviteToken();
      await refreshUser();
      navigate(ROUTES.DASHBOARD, { replace: true });
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Failed to accept the invitation.');
    } finally {
      setSubmitting(false);
    }
  }

  function renderBody() {
    if (loading || authLoading) {
      return (
        <div className="flex items-center justify-center py-10 text-on-surface-variant">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        </div>
      );
    }

    if (loadError || !invitation) {
      return (
        <div className="text-center">
          <div className="mb-4 flex justify-center">
            <AlertCircle size={40} className="text-red-500" />
          </div>
          <h1 className="text-headline-md font-bold text-on-background">Invitation unavailable</h1>
          <p className="mt-2 text-body-md text-on-surface-variant">
            {loadError || 'This invitation is invalid or has expired.'}
          </p>
          <Link
            to={ROUTES.LOGIN}
            className="mt-6 inline-flex items-center gap-1.5 text-body-sm font-medium text-primary hover:underline"
          >
            <ArrowLeft size={16} />
            Go to sign in
          </Link>
        </div>
      );
    }

    const emailMatches = user?.email?.toLowerCase() === invitation.email.toLowerCase();

    if (user && !emailMatches) {
      return (
        <div className="text-center">
          <h1 className="text-headline-md font-bold text-on-background">Wrong account</h1>
          <p className="mt-2 text-body-md text-on-surface-variant">
            This invitation is for <strong>{invitation.email}</strong>, but you are signed in as{' '}
            <strong>{user.email}</strong>. Please sign out and try again.
          </p>
        </div>
      );
    }

    if (user && emailMatches) {
      return (
        <div>
          <h1 className="text-headline-lg font-bold text-on-background">Accept your invitation</h1>
          <p className="mt-2 text-body-md text-on-surface-variant">
            You&apos;ve been invited to join <strong>{invitation.companyName}</strong> as{' '}
            <strong>{invitation.roleName}</strong>.
          </p>
          <p className="mt-1 text-body-xs text-on-surface-variant">
            This invitation expires on {formatExpiry(invitation.expiresAt)}.
          </p>
          {formError && (
            <div className="mt-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
              <AlertCircle size={16} className="shrink-0" />
              <span role="alert">{formError}</span>
            </div>
          )}
          <Button
            type="button"
            variant="primary"
            className="mt-6 w-full"
            disabled={submitting}
            onClick={handleAcceptExisting}
          >
            {submitting ? 'Accepting...' : 'Accept invitation'}
          </Button>
        </div>
      );
    }

    if (invitation.hasAccount) {
      return (
        <div className="text-center">
          <h1 className="text-headline-md font-bold text-on-background">You already have an account</h1>
          <p className="mt-2 text-body-md text-on-surface-variant">
            An account already exists for <strong>{invitation.email}</strong>. Sign in to accept your
            invitation to <strong>{invitation.companyName}</strong>.
          </p>
          <Button
            type="button"
            variant="primary"
            className="mt-6 w-full"
            onClick={() =>
              navigate(
                `${ROUTES.LOGIN}?redirect=${encodeURIComponent(ROUTES.INVITE_ACCEPT)}`,
              )
            }
          >
            Sign in to continue
          </Button>
        </div>
      );
    }

    return (
      <div>
        <div className="mb-6">
          <h1 className="text-headline-lg font-bold text-on-background">Create your account</h1>
          <p className="mt-2 text-body-md text-on-surface-variant">
            Join <strong>{invitation.companyName}</strong> as <strong>{invitation.roleName}</strong>.
            Set your name and password to complete your account.
          </p>
          <p className="mt-1 text-body-xs text-on-surface-variant">
            This invitation expires on {formatExpiry(invitation.expiresAt)}.
          </p>
        </div>

        {formError && (
          <div className="mb-6 flex items-center gap-2 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-body-sm text-red-700">
            <AlertCircle size={16} className="shrink-0" />
            <span role="alert">{formError}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate>
          <div className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label htmlFor="inviteFirstName" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                  First Name
                </label>
                <input
                  id="inviteFirstName"
                  type="text"
                  value={firstName}
                  onChange={(e) => setFirstName(e.target.value)}
                  placeholder="John"
                  aria-invalid={!!fieldErrors.firstName}
                  className={fieldClass(!!fieldErrors.firstName)}
                />
                {fieldErrors.firstName && (
                  <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.firstName}</p>
                )}
              </div>
              <div>
                <label htmlFor="inviteLastName" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                  Last Name
                </label>
                <input
                  id="inviteLastName"
                  type="text"
                  value={lastName}
                  onChange={(e) => setLastName(e.target.value)}
                  placeholder="Doe"
                  aria-invalid={!!fieldErrors.lastName}
                  className={fieldClass(!!fieldErrors.lastName)}
                />
                {fieldErrors.lastName && (
                  <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.lastName}</p>
                )}
              </div>
            </div>

            <div>
              <label className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                Email
              </label>
              <div className="relative">
                <Mail size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/60" />
                <input
                  type="email"
                  value={invitation.email}
                  readOnly
                  disabled
                  className="w-full cursor-not-allowed rounded-xl border border-outline-variant bg-surface-container px-4 py-3 pl-9 text-body-md text-on-surface-variant"
                />
              </div>
            </div>

            <div>
              <label htmlFor="invitePassword" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                Password
              </label>
              <div className="relative">
                <input
                  id="invitePassword"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Create a strong password"
                  autoComplete="new-password"
                  aria-invalid={!!fieldErrors.password}
                  className={`${fieldClass(!!fieldErrors.password)} pr-11`}
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
              {fieldErrors.password && (
                <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.password}</p>
              )}
              <PasswordRequirements password={password} />
            </div>

            <div>
              <label htmlFor="inviteConfirmPassword" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
                Confirm Password
              </label>
              <input
                id="inviteConfirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Repeat your password"
                autoComplete="new-password"
                aria-invalid={!!fieldErrors.confirmPassword}
                className={fieldClass(!!fieldErrors.confirmPassword)}
              />
              {fieldErrors.confirmPassword && (
                <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.confirmPassword}</p>
              )}
            </div>

            <Button type="submit" variant="primary" className="w-full" disabled={submitting}>
              {submitting ? (
                <span className="flex items-center justify-center gap-2">
                  <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                  Creating account...
                </span>
              ) : (
                'Create account'
              )}
            </Button>
          </div>
        </form>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background text-on-background">
      <main className="flex min-h-screen justify-center px-4 py-8">
        <div className="w-full max-w-lg">
          <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-xs md:p-8">
            {renderBody()}
          </div>
        </div>
      </main>
    </div>
  );
}
