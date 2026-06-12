import { useEffect, useRef, useState } from 'react';
import { X, Loader2 } from 'lucide-react';
import { useAuth } from './AuthContext';

interface LoginModalProps {
  open: boolean;
  onClose: () => void;
  initialMode?: 'login' | 'register';
}

export default function LoginModal({ open, onClose, initialMode = 'login' }: LoginModalProps) {
  const { login, register } = useAuth();
  const [mode, setMode] = useState<'login' | 'register'>(initialMode);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setMode(initialMode);
    setError('');
  }, [initialMode, open]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (submitting) return;
    setError('');
    setSubmitting(true);

    const form = new FormData(e.currentTarget);
    const email = form.get('email') as string;
    const password = form.get('password') as string;

    try {
      if (mode === 'login') {
        await login({ email, password });
      } else {
        const firstName = form.get('firstName') as string;
        const lastName = form.get('lastName') as string;
        await register({ email, password, firstName, lastName });
      }
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Something went wrong');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      ref={overlayRef}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"

    >
      <div className="w-full max-w-md rounded-xl bg-surface p-8 shadow-lg">
        <div className="mb-6 flex items-center justify-between">
          <h2 className="text-headline-md font-bold text-on-background">
            {mode === 'login' ? 'Log In' : 'Sign Up'}
          </h2>
          <button onClick={onClose} className="cursor-pointer rounded-lg p-1 text-on-surface-variant hover:bg-surface-container">
            <X size={20} />
          </button>
        </div>

        {error && (
          <div className="mb-4 rounded-lg bg-error/10 p-3 text-body-sm text-error">{error}</div>
        )}

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          {mode === 'register' && (
            <div className="flex gap-3">
              <input
                name="firstName"
                placeholder="First Name"
                required
                className="w-full rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary"
              />
              <input
                name="lastName"
                placeholder="Last Name"
                required
                className="w-full rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary"
              />
            </div>
          )}

          <input
            name="email"
            type="email"
            placeholder="Email"
            required
            className="w-full rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary"
          />

          <input
            name="password"
            type="password"
            placeholder="Password"
            required
            minLength={8}
            className="w-full rounded-lg border border-outline-variant bg-surface px-4 py-2.5 text-body-md text-on-background outline-none focus:border-secondary"
          />

          <button
            type="submit"
            disabled={submitting}
            className="mt-2 w-full cursor-pointer rounded-xl bg-primary px-6 py-3 text-body-md font-semibold text-white transition-transform active:scale-95 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting ? (
              <span className="flex items-center justify-center gap-2">
                <Loader2 size={16} className="animate-spin" />
                {mode === 'login' ? 'Logging in...' : 'Creating account...'}
              </span>
            ) : (
              mode === 'login' ? 'Log In' : 'Create Account'
            )}
          </button>
        </form>

        <p className="mt-4 text-center text-body-sm text-on-surface-variant">
          {mode === 'login' ? (
            <>Don't have an account?{' '}
              <button onClick={() => { setMode('register'); setError(''); }} className="cursor-pointer font-semibold text-secondary underline">Sign Up</button>
            </>
          ) : (
            <>Already have an account?{' '}
              <button onClick={() => { setMode('login'); setError(''); }} className="cursor-pointer font-semibold text-secondary underline">Log In</button>
            </>
          )}
        </p>
      </div>
    </div>
  );
}
