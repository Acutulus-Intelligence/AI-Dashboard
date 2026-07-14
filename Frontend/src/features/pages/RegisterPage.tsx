import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, ArrowLeft, Eye, EyeOff } from 'lucide-react';
import Button from '../components/Button';
import PasswordRequirements from '../components/PasswordRequirements';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';
import { ApiError } from '../../lib/api/client';
import {
  type RegisterFieldErrors,
  mapRegisterApiError,
  validateRegisterForm,
} from '../validation/registerForm';

function fieldClass(hasError: boolean) {
  return `w-full rounded-xl border bg-surface-container-lowest px-4 py-3 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:outline-none focus:ring-2 ${
    hasError
      ? 'border-red-400 focus:border-red-400 focus:ring-red-200'
      : 'border-outline-variant focus:border-primary focus:ring-primary/20'
  }`;
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
    <div className="min-h-screen bg-background text-on-background">
      <main className="flex min-h-screen justify-center px-4 py-8">
        <div className="w-full max-w-lg">
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
                Sign up to get started with your free trial.
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
                    <label htmlFor="firstName" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
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
                      <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.firstName}</p>
                    )}
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
                      aria-invalid={!!fieldErrors.lastName}
                      className={fieldClass(!!fieldErrors.lastName)}
                    />
                    {fieldErrors.lastName && (
                      <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.lastName}</p>
                    )}
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
                    aria-invalid={!!fieldErrors.email}
                    className={fieldClass(!!fieldErrors.email)}
                  />
                  {fieldErrors.email && (
                    <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.email}</p>
                  )}
                </div>

                <div>
                  <label htmlFor="regPassword" className="mb-1.5 block text-body-sm font-medium text-on-surface-variant">
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
                    aria-invalid={!!fieldErrors.confirmPassword}
                    className={fieldClass(!!fieldErrors.confirmPassword)}
                  />
                  {fieldErrors.confirmPassword && (
                    <p className="mt-1 text-body-sm text-red-600" role="alert">{fieldErrors.confirmPassword}</p>
                  )}
                </div>

                <Button type="submit" variant="primary" className="w-full" disabled={loading}>
                  {loading ? (
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
