import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import QRCode from 'react-qr-code';
import { toast } from 'sonner';
import {
  ArrowLeft,
  Check,
  Copy,
  Download,
  KeyRound,
  Loader2,
  ShieldCheck,
  ShieldOff,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { useAuth } from '../store/useAuth';
import * as authApi from '../../lib/api/auth';
import { ROUTES } from '../routes';

type Mode = 'status' | 'setup' | 'recovery';

function errorMessage(err: unknown, fallback: string) {
  return err instanceof Error ? err.message : fallback;
}

function RecoveryCodesView({ codes, onDone }: { codes: string[]; onDone: () => void }) {
  const [copied, setCopied] = useState(false);

  async function copyAll() {
    try {
      await navigator.clipboard.writeText(codes.join('\n'));
      setCopied(true);
      toast.success('Recovery codes copied.');
    } catch {
      toast.error('Could not copy to clipboard.');
    }
  }

  function download() {
    const blob = new Blob(
      [`AI-Dashboard recovery codes\n\n${codes.join('\n')}\n`],
      { type: 'text/plain' },
    );
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'ai-dashboard-recovery-codes.txt';
    anchor.click();
    URL.revokeObjectURL(url);
  }

  return (
    <>
      <CardHeader>
        <CardTitle>Save your recovery codes</CardTitle>
        <CardDescription>
          Each code can be used once to sign in if you lose access to your authenticator app.
          Store them somewhere safe — they will not be shown again.
        </CardDescription>
      </CardHeader>

      <CardContent className="grid gap-4">
        <div className="grid grid-cols-2 gap-2 rounded-lg border bg-muted/40 p-4 font-mono text-sm sm:max-w-md">
          {codes.map((code) => (
            <span key={code}>{code}</span>
          ))}
        </div>
      </CardContent>

      <CardFooter className="flex-wrap gap-2">
        <Button type="button" variant="outline" onClick={copyAll}>
          {copied ? <Check /> : <Copy />}
          Copy
        </Button>
        <Button type="button" variant="outline" onClick={download}>
          <Download />
          Download
        </Button>
        <Button type="button" onClick={onDone}>
          I&apos;ve saved my codes
        </Button>
      </CardFooter>
    </>
  );
}

export default function TwoFactorSettings() {
  const { user, refreshUser, logout, pauseSessionChecks } = useAuth();
  const navigate = useNavigate();

  const enabled = user?.twoFactorEnabled ?? false;

  const [mode, setMode] = useState<Mode>('status');
  const [setupData, setSetupData] = useState<authApi.TwoFactorSetupResponse | null>(null);
  const [codes, setCodes] = useState<string[]>([]);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [logoutAfterRecovery, setLogoutAfterRecovery] = useState(false);

  useEffect(() => () => pauseSessionChecks(false), [pauseSessionChecks]);

  function resetToStatus() {
    setMode('status');
    setSetupData(null);
    setCodes([]);
    setCode('');
    setError('');
  }

  async function startSetup() {
    setBusy(true);
    setError('');
    try {
      const data = await authApi.setupTwoFactor();
      setSetupData(data);
      setCode('');
      setMode('setup');
    } catch (err) {
      toast.error(errorMessage(err, 'Failed to start two-factor setup.'));
    } finally {
      setBusy(false);
    }
  }

  async function verifySetup() {
    if (!/^\d{6}$/.test(code.trim())) {
      setError('Enter the 6-digit code from your authenticator app.');
      return;
    }

    setBusy(true);
    setError('');
    try {
      const result = await authApi.enableTwoFactor(code.trim());
      // Enabling revokes all sessions; hold the UI steady so the user can save the codes.
      pauseSessionChecks(true);
      setLogoutAfterRecovery(true);
      setCodes(result.recoveryCodes);
      setCode('');
      setMode('recovery');
      toast.success('Two-factor authentication enabled.');
    } catch (err) {
      setError(errorMessage(err, 'Invalid verification code.'));
    } finally {
      setBusy(false);
    }
  }

  async function finishRecovery() {
    if (logoutAfterRecovery) {
      toast.success('Please sign in again with your two-factor authentication.');
      await logout();
      navigate(ROUTES.LOGIN, { replace: true });
      pauseSessionChecks(false);
      return;
    }

    await refreshUser();
    resetToStatus();
  }

  async function disable() {
    if (!/^\d{6}$/.test(code.trim())) {
      setError('Enter the 6-digit code from your authenticator app.');
      return;
    }

    setBusy(true);
    setError('');
    try {
      await authApi.disableTwoFactor(code.trim());
      toast.success('Two-factor authentication disabled. Please sign in again.');
      await logout();
      navigate(ROUTES.LOGIN, { replace: true });
    } catch (err) {
      setError(errorMessage(err, 'Invalid verification code.'));
    } finally {
      setBusy(false);
    }
  }

  async function regenerate() {
    if (!/^\d{6}$/.test(code.trim())) {
      setError('Enter the 6-digit code from your authenticator app.');
      return;
    }

    setBusy(true);
    setError('');
    try {
      const result = await authApi.regenerateRecoveryCodes(code.trim());
      setLogoutAfterRecovery(false);
      setCodes(result.recoveryCodes);
      setCode('');
      setMode('recovery');
    } catch (err) {
      setError(errorMessage(err, 'Invalid verification code.'));
    } finally {
      setBusy(false);
    }
  }

  if (mode === 'recovery') {
    return (
      <Card>
        <RecoveryCodesView codes={codes} onDone={finishRecovery} />
      </Card>
    );
  }

  if (mode === 'setup' && setupData) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Set up two-factor authentication</CardTitle>
          <CardDescription>
            Scan the QR code with Microsoft Authenticator, Google Authenticator, or any TOTP app,
            then enter the 6-digit code to confirm.
          </CardDescription>
        </CardHeader>

        <CardContent className="grid gap-6 sm:max-w-lg">
          <div className="flex justify-center">
            <div className="rounded-xl bg-white p-4">
              <QRCode value={setupData.authenticatorUri} size={180} />
            </div>
          </div>

          <div className="grid gap-1.5">
            <span className="text-sm font-medium text-muted-foreground">
              Can&apos;t scan? Enter this key manually
            </span>
            <code className="block rounded-lg border bg-muted/40 px-3 py-2 font-mono text-sm break-all">
              {setupData.sharedKey}
            </code>
          </div>

          <div className="grid gap-1.5">
            <label htmlFor="totp-code" className="text-sm font-medium">
              Verification code
            </label>
            <Input
              id="totp-code"
              inputMode="numeric"
              autoComplete="one-time-code"
              placeholder="123456"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              aria-invalid={!!error}
            />
            {error && <p className="text-sm text-destructive">{error}</p>}
          </div>
        </CardContent>

        <CardFooter className="flex-wrap gap-2">
          <Button type="button" onClick={verifySetup} disabled={busy}>
            {busy && <Loader2 className="animate-spin" />}
            Verify and enable
          </Button>
          <Button type="button" variant="ghost" onClick={resetToStatus} disabled={busy}>
            <ArrowLeft />
            Cancel
          </Button>
        </CardFooter>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          {enabled ? <ShieldCheck className="text-primary" /> : <ShieldOff className="text-muted-foreground" />}
          Two-factor authentication
        </CardTitle>
        <CardDescription>
          {enabled
            ? 'Your account is protected with an authenticator app.'
            : 'Add an extra layer of security using an authenticator app.'}
        </CardDescription>
      </CardHeader>

      {enabled ? (
        <>
          <CardContent className="grid gap-4 sm:max-w-lg">
            <div className="grid gap-1.5">
              <label htmlFor="disable-code" className="text-sm font-medium">
                Enter a code to disable or regenerate recovery codes
              </label>
              <Input
                id="disable-code"
                inputMode="numeric"
                autoComplete="one-time-code"
                placeholder="123456"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                aria-invalid={!!error}
              />
              {error && <p className="text-sm text-destructive">{error}</p>}
            </div>
          </CardContent>

          <CardFooter className="flex-wrap gap-2">
            <Button type="button" variant="outline" onClick={regenerate} disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <KeyRound />}
              Regenerate recovery codes
            </Button>
            <Button type="button" variant="destructive" onClick={disable} disabled={busy}>
              <ShieldOff />
              Disable
            </Button>
          </CardFooter>
        </>
      ) : (
        <CardFooter>
          <Button type="button" onClick={startSetup} disabled={busy}>
            {busy ? <Loader2 className="animate-spin" /> : <ShieldCheck />}
            Enable two-factor authentication
          </Button>
        </CardFooter>
      )}
    </Card>
  );
}
