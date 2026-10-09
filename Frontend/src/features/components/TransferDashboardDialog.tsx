import { useEffect, useState } from 'react';
import { AlertTriangle, Loader2, Users } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import { transferDashboard, type TransferDataSourceItem } from '../../lib/api/dashboards';
import * as companyApi from '../../lib/api/company';
import { useAuth } from '../store/useAuth';

interface TransferDashboardDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  dashboardId: string;
  dashboardName: string;
  onTransferred: () => void;
}

export default function TransferDashboardDialog({
  open,
  onOpenChange,
  dashboardId,
  dashboardName,
  onTransferred,
}: TransferDashboardDialogProps) {
  const { user } = useAuth();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [members, setMembers] = useState<companyApi.CompanyUserResponse[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [password, setPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [requiresSharing, setRequiresSharing] = useState<TransferDataSourceItem[] | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setMembers([]);
    setSelectedId(null);
    setPassword('');
    setRequiresSharing(null);

    companyApi
      .getMyCompany()
      .then((company) => companyApi.getCompanyUsers(company.id))
      .then((users) => {
        if (cancelled) return;
        setMembers(users.filter((u) => u.id !== user?.userId));
      })
      .catch(() => {
        if (!cancelled) {
          setError('Transfers are available between members of the same company only.');
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [open, user?.userId]);

  async function handleTransfer() {
    if (!selectedId || !password) return;
    setSaving(true);
    try {
      const result = await transferDashboard(dashboardId, selectedId, password, false);
      if (!result.transferred) {
        setRequiresSharing(result.requiresSharing);
        return;
      }
      toast.success('Dashboard transferred.');
      onTransferred();
      onOpenChange(false);
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Could not transfer dashboard.');
    } finally {
      setSaving(false);
    }
  }

  async function handleConfirmShare() {
    if (!selectedId || !password) return;
    setSaving(true);
    try {
      const result = await transferDashboard(dashboardId, selectedId, password, true);
      if (!result.transferred) {
        setRequiresSharing(result.requiresSharing);
        return;
      }
      toast.success('Dashboard transferred.');
      onTransferred();
      onOpenChange(false);
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Could not transfer dashboard.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !saving && onOpenChange(next)}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Transfer ownership</DialogTitle>
          <DialogDescription>
            Give “{dashboardName}” and its charts to another member of your company.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3 py-1">
          {requiresSharing ? (
            <>
              <div className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-3 text-sm text-amber-800">
                <AlertTriangle className="mt-0.5 size-4 shrink-0" />
                <span>
                  The new owner can’t access these data sources. Share them with your company to
                  complete the transfer.
                </span>
              </div>
              <ul className="grid gap-1.5">
                {requiresSharing.map((item) => (
                  <li
                    key={`${item.type}-${item.id}`}
                    className="flex items-center justify-between rounded-lg border px-3 py-2 text-sm"
                  >
                    <span className="truncate font-medium">{item.name}</span>
                    <span className="text-muted-foreground ml-2 shrink-0 text-xs capitalize">
                      {item.type}
                    </span>
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <>
              {loading && (
                <p className="text-muted-foreground flex items-center gap-2 text-sm">
                  <Loader2 className="size-4 animate-spin" />
                  Loading company members…
                </p>
              )}

              {!loading && error && <p className="text-muted-foreground text-sm">{error}</p>}

              {!loading && !error && members.length === 0 && (
                <p className="text-muted-foreground text-sm">
                  No other company members are available to receive this dashboard.
                </p>
              )}

              {!loading &&
                !error &&
                members.map((member) => {
                  const label =
                    [member.firstName, member.lastName].filter(Boolean).join(' ').trim() ||
                    member.email;
                  return (
                    <button
                      key={member.id}
                      type="button"
                      onClick={() => setSelectedId(member.id)}
                      className={cn(
                        'flex w-full cursor-pointer items-center gap-3 rounded-lg border px-3 py-3 text-left transition-colors',
                        selectedId === member.id
                          ? 'border-primary bg-primary/5'
                          : 'border-border hover:bg-muted',
                      )}
                    >
                      <div className="bg-primary/10 text-primary flex size-9 items-center justify-center rounded-lg">
                        <Users className="size-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="truncate font-medium">{label}</p>
                        <p className="text-muted-foreground truncate text-xs">{member.email}</p>
                      </div>
                    </button>
                  );
                })}

              {!loading && !error && members.length > 0 && (
                <div className="grid gap-2">
                  <Label htmlFor="transfer-password">Your current password</Label>
                  <Input
                    id="transfer-password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                </div>
              )}
            </>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          {requiresSharing ? (
            <Button type="button" onClick={() => void handleConfirmShare()} disabled={saving}>
              {saving && <Loader2 className="animate-spin" />}
              Share &amp; transfer
            </Button>
          ) : (
            <Button
              type="button"
              onClick={() => void handleTransfer()}
              disabled={saving || !selectedId || !password}
            >
              {saving && <Loader2 className="animate-spin" />}
              Transfer
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
