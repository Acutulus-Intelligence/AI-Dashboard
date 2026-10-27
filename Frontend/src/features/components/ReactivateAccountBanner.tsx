import { useState } from 'react';
import { toast } from 'sonner';
import { Loader2, RotateCcw, TriangleAlert } from 'lucide-react';
import { Alert, AlertAction, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useAuth } from '../store/useAuth';

export default function ReactivateAccountBanner() {
  const { user, reactivateAccount } = useAuth();
  const [open, setOpen] = useState(false);
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  if (!user?.isDeactivated) return null;

  const twoFactorEnabled = user.twoFactorEnabled;
  const scheduled = user.deletionScheduledAt
    ? new Date(user.deletionScheduledAt).toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      })
    : null;

  function reset() {
    setPassword('');
    setCode('');
    setUseRecoveryCode(false);
    setError('');
  }

  async function submit() {
    if (!password) {
      setError('Enter your password.');
      return;
    }
    if (twoFactorEnabled) {
      if (useRecoveryCode) {
        if (!code.trim()) {
          setError('Enter one of your recovery codes.');
          return;
        }
      } else if (!/^\d{6}$/.test(code.trim())) {
        setError('Enter the 6-digit code from your authenticator app.');
        return;
      }
    }

    setBusy(true);
    setError('');
    try {
      await reactivateAccount({
        currentPassword: password,
        code: twoFactorEnabled && !useRecoveryCode ? code.trim() : undefined,
        recoveryCode: twoFactorEnabled && useRecoveryCode ? code.trim() : undefined,
      });
      toast.success('Your account has been reactivated.');
      setOpen(false);
      reset();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reactivate account.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <Alert variant="destructive" className="m-4 mb-0">
      <TriangleAlert />
      <AlertTitle>Your account is scheduled for deletion</AlertTitle>
      <AlertDescription>
        It will be permanently deleted{scheduled ? ` on ${scheduled}` : ' in 14 days'}. Reactivate
        to cancel the deletion.
      </AlertDescription>

      <AlertAction>
        <AlertDialog
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            if (!next) reset();
          }}
        >
          <AlertDialogTrigger asChild>
            <Button size="sm" variant="destructive">
              <RotateCcw />
              Reactivate account
            </Button>
          </AlertDialogTrigger>

          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Reactivate your account</AlertDialogTitle>
              <AlertDialogDescription>
                Confirm with your password{twoFactorEnabled ? ' and two-factor code' : ''}.
              </AlertDialogDescription>
            </AlertDialogHeader>

            <div className="grid gap-4">
              <div className="grid gap-1.5">
                <label htmlFor="reactivate-password" className="text-sm font-medium">
                  Password
                </label>
                <Input
                  id="reactivate-password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>

              {twoFactorEnabled && !useRecoveryCode && (
                <div className="grid gap-1.5">
                  <label htmlFor="reactivate-code" className="text-sm font-medium">
                    Authenticator code
                  </label>
                  <Input
                    id="reactivate-code"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    placeholder="123456"
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                  />
                </div>
              )}

              {twoFactorEnabled && useRecoveryCode && (
                <div className="grid gap-1.5">
                  <label htmlFor="reactivate-recovery" className="text-sm font-medium">
                    Recovery code
                  </label>
                  <Input
                    id="reactivate-recovery"
                    placeholder="XXXXX-XXXXX"
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                  />
                </div>
              )}

              {twoFactorEnabled && (
                <button
                  type="button"
                  onClick={() => {
                    setUseRecoveryCode((v) => !v);
                    setCode('');
                    setError('');
                  }}
                  className="w-fit text-sm font-medium text-primary hover:underline"
                >
                  {useRecoveryCode ? 'Use an authenticator code instead' : 'Use a recovery code'}
                </button>
              )}

              {error && <p className="text-sm text-destructive">{error}</p>}
            </div>

            <AlertDialogFooter>
              <AlertDialogCancel type="button">Cancel</AlertDialogCancel>
              <Button type="button" onClick={() => void submit()} disabled={busy}>
                {busy && <Loader2 className="animate-spin" />}
                Reactivate
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </AlertAction>
    </Alert>
  );
}
