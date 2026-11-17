import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Eye, EyeOff } from 'lucide-react';
import PageMeta from '../components/PageMeta';
import PasswordRequirements from '../components/PasswordRequirements';
import PublicButton from '../components/PublicButton';
import PublicError from '../components/PublicError';
import PublicAuthFrame from '../layouts/PublicAuthFrame';
import PublicLayout from '../layouts/PublicLayout';
import { cn } from '@/lib/utils';
import { publicInputClass, publicInputErrorClass, publicLabelClass, publicLinkClass } from '../layouts/publicChrome';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';
import { ApiError } from '../../lib/api/client';
import {
  type RegisterFieldErrors,
  mapRegisterApiError,
  validateRegisterForm,
} from '../validation/registerForm';

function fieldClass(hasError: boolean) {
  return cn(publicInputClass, hasError && publicInputErrorClass);
}

export default function RegisterPage() {
  const navigate = useNavigate();
  const { register } = useAuth();

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState('');
  const [fieldErrors, setFieldErrors] = useState<RegisterFieldErrors>({});
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setFormError('');
    setFieldErrors({});

    const validation = validateRegisterForm({
      firstName,
      lastName,
      email,
      password,
      confirmPassword,
    });

    if (!validation.isValid) {
      setFieldErrors(validation.fieldErrors);
      return;
    }

    setLoading(true);
    try {
      await register({
        email,
        password,
        firstName,
        lastName,
        userType: 0,
      });
      navigate(ROUTES.PRICING, { replace: true });
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        const mapped = mapRegisterApiError(err);
        setFieldErrors(mapped.fieldErrors);
        if (mapped.formError) setFormError(mapped.formError);
      } else {
        setFormError(err instanceof Error ? err.message : 'An unexpected error occurred. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <PublicLayout>
      <PageMeta
        title="Sign up — AI Dashboard"
        description="Create an AI Dashboard account and start a free trial."
        path={ROUTES.REGISTER}
      />
      <PublicAuthFrame wide>
          <div className="mb-6">
            <Link
              to={ROUTES.PRICING}
              className={`mb-4 inline-flex items-center gap-1.5 text-sm ${publicLinkClass}`}
            >
              <ArrowLeft size={16} />
              Back to plans
            </Link>
            <h1 className="text-3xl font-semibold tracking-tight text-slate-900">Create your account</h1>
            <p className="mt-2 text-base leading-relaxed text-slate-600">
              Sign up to get started with your free trial.
            </p>
          </div>

          {formError && (
            <div className="mb-6">
              <PublicError>{formError}</PublicError>
            </div>
          )}

            <form onSubmit={handleSubmit} noValidate>
              <div className="space-y-5">
                <div className="grid gap-4 md:grid-cols-2">
                  <div>
                    <label htmlFor="firstName" className={publicLabelClass}>
                      First Name
                    </label>
                    <input
                      id="firstName"
                      type="text"
                      value={firstName}
                      onChange={(e) => setFirstName(e.target.value)}
                      placeholder="John"
                      aria-invalid={!!fieldErrors.firstName}
                      className={fieldClass(!!fieldErrors.firstName)}
                    />
                    {fieldErrors.firstName && (
                      <p className="mt-1 text-sm text-red-600" role="alert">{fieldErrors.firstName}</p>
                    )}
                  </div>
                  <div>
                    <label htmlFor="lastName" className={publicLabelClass}>
                      Last Name
                    </label>
                    <input
                      id="lastName"
                      type="text"
                      value={lastName}
                      onChange={(e) => setLastName(e.target.value)}
                      placeholder="Doe"
                      aria-invalid={!!fieldErrors.lastName}
                      className={fieldClass(!!fieldErrors.lastName)}
                    />
                    {fieldErrors.lastName && (
                      <p className="mt-1 text-sm text-red-600" role="alert">{fieldErrors.lastName}</p>
                    )}
                  </div>
                </div>

                <div>
                  <label htmlFor="regEmail" className={publicLabelClass}>
                    Email
                  </label>
                  <input
                    id="regEmail"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="you@example.com"
                    autoComplete="email"
                    aria-invalid={!!fieldErrors.email}
                    className={fieldClass(!!fieldErrors.email)}
                  />
                  {fieldErrors.email && (
                    <p className="mt-1 text-sm text-red-600" role="alert">{fieldErrors.email}</p>
                  )}
                </div>

                <div>
                  <label htmlFor="regPassword" className={publicLabelClass}>
                    Password
                  </label>
                  <div className="relative">
                    <input
                      id="regPassword"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      placeholder="Create a strong password"
                      autoComplete="new-password"
                      aria-invalid={!!fieldErrors.password}
                      className={cn(fieldClass(!!fieldErrors.password), 'pr-12')}
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
                  {fieldErrors.password && (
                    <p className="mt-1 text-sm text-red-600" role="alert">{fieldErrors.password}</p>
                  )}
                  <PasswordRequirements password={password} tone="public" />
                </div>

                <div>
                  <label htmlFor="confirmPassword" className={publicLabelClass}>
                    Confirm Password
                  </label>
                  <input
                    id="confirmPassword"
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder="Repeat your password"
                    autoComplete="new-password"
                    aria-invalid={!!fieldErrors.confirmPassword}
                    className={fieldClass(!!fieldErrors.confirmPassword)}
                  />
                  {fieldErrors.confirmPassword && (
                    <p className="mt-1 text-sm text-red-600" role="alert">{fieldErrors.confirmPassword}</p>
                  )}
                </div>

                <p className="text-center text-sm text-slate-600">
                  By creating an account, you agree to the{' '}
                  <Link to={ROUTES.TERMS} className={publicLinkClass}>
                    Terms of Service
                  </Link>{' '}
                  and{' '}
                  <Link to={ROUTES.PRIVACY} className={publicLinkClass}>
                    Privacy Policy
                  </Link>
                  .
                </p>

                <PublicButton type="submit" className="w-full" disabled={loading}>
                  {loading ? (
                    <span className="flex items-center justify-center gap-2">
                      <div className="size-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                      Creating account...
                    </span>
                  ) : (
                    'Create account'
                  )}
                </PublicButton>
              </div>
            </form>

            <p className="mt-6 text-center text-sm text-slate-600">
              Already have an account?{' '}
              <Link to={ROUTES.LOGIN} className={publicLinkClass}>
                Sign in
              </Link>
            </p>
      </PublicAuthFrame>
    </PublicLayout>
  );
}
