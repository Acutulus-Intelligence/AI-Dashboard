import { useEffect, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Loader2,
  MoveRight,
  Pencil,
  Plus,
  Trash2,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import AppShell from '../layouts/AppShell';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import { ROUTES } from '../routes';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  getAdminPlans,
  createPlan,
  updatePlan,
  deletePlan,
  movePlan,
  type AdminSubscriptionPlan,
} from '../../lib/api/admin';
import { USER_TYPE, type UserType } from '../../lib/api/subscription';

interface PlanForm {
  name: string;
  description: string;
  userType: UserType;
  monthlyPrice: string;
  yearlyPrice: string;
  maxUsers: string;
  maxDashboards: string;
  maxAiQueriesPerMonth: string;
  isActive: boolean;
}

const emptyForm: PlanForm = {
  name: '',
  description: '',
  userType: USER_TYPE.Individual,
  monthlyPrice: '',
  yearlyPrice: '',
  maxUsers: '',
  maxDashboards: '',
  maxAiQueriesPerMonth: '',
  isActive: true,
};

function toForm(plan: AdminSubscriptionPlan): PlanForm {
  return {
    name: plan.name,
    description: plan.description,
    userType: plan.userType,
    monthlyPrice: String(plan.monthlyPrice),
    yearlyPrice: String(plan.yearlyPrice),
    maxUsers: plan.maxUsers == null ? '' : String(plan.maxUsers),
    maxDashboards: plan.maxDashboards == null ? '' : String(plan.maxDashboards),
    maxAiQueriesPerMonth: plan.maxAiQueriesPerMonth == null ? '' : String(plan.maxAiQueriesPerMonth),
    isActive: plan.isActive,
  };
}

function nullableNumber(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

function planTypeLabel(userType: number): string {
  return userType === USER_TYPE.Company ? 'Company' : 'Individual';
}

export default function AdminPlansPage() {
  const [plans, setPlans] = useState<AdminSubscriptionPlan[]>([]);
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<AdminSubscriptionPlan | null>(null);
  const [form, setForm] = useState<PlanForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState<AdminSubscriptionPlan | null>(null);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [moving, setMoving] = useState<AdminSubscriptionPlan | null>(null);
  const [moveTarget, setMoveTarget] = useState<string>('');
  const [moveLoading, setMoveLoading] = useState(false);

  useEffect(() => {
    getAdminPlans()
      .then((data) => setPlans(data))
      .catch((err) => toast.error(err instanceof Error ? err.message : 'Could not load plans.'))
      .finally(() => setLoading(false));
  }, []);

  async function loadPlans() {
    setLoading(true);
    try {
      setPlans(await getAdminPlans());
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not load plans.');
    } finally {
      setLoading(false);
    }
  }

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setDialogOpen(true);
  }

  function openEdit(plan: AdminSubscriptionPlan) {
    setEditing(plan);
    setForm(toForm(plan));
    setDialogOpen(true);
  }

  function updateField<K extends keyof PlanForm>(key: K, value: PlanForm[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSave() {
    if (!form.name.trim()) {
      toast.error('Plan name is required.');
      return;
    }
    const monthlyPrice = nullableNumber(form.monthlyPrice);
    const yearlyPrice = nullableNumber(form.yearlyPrice);
    if (monthlyPrice == null || monthlyPrice < 0) {
      toast.error('Monthly price must be a valid non-negative number.');
      return;
    }
    if (yearlyPrice == null || yearlyPrice < 0) {
      toast.error('Yearly price must be a valid non-negative number.');
      return;
    }
    setSaving(true);
    try {
      const payload = {
        name: form.name.trim(),
        description: form.description.trim(),
        userType: form.userType,
        monthlyPrice,
        yearlyPrice,
        maxUsers: nullableNumber(form.maxUsers),
        maxDashboards: nullableNumber(form.maxDashboards),
        maxAiQueriesPerMonth: nullableNumber(form.maxAiQueriesPerMonth),
      };
      if (editing) {
        await updatePlan(editing.id, { ...payload, isActive: form.isActive });
        toast.success('Plan updated.');
      } else {
        await createPlan(payload);
        toast.success('Plan created.');
      }
      setDialogOpen(false);
      await loadPlans();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not save plan.');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!deleting) return;
    setDeleteLoading(true);
    try {
      await deletePlan(deleting.id);
      toast.success('Plan removed. Existing subscriptions will end at their current period.');
      setDeleting(null);
      await loadPlans();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not remove plan.');
    } finally {
      setDeleteLoading(false);
    }
  }

  async function handleMove() {
    if (!moving || !moveTarget) return;
    setMoveLoading(true);
    try {
      await movePlan(moving.id, moveTarget);
      toast.success('Subscriptions moved to the target plan.');
      setMoving(null);
      setMoveTarget('');
      await loadPlans();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not move plan subscriptions.');
    } finally {
      setMoveLoading(false);
    }
  }

  return (
    <AppShell
      breadcrumbs={[
        { label: 'Administration', to: ROUTES.ADMIN },
        { label: 'Plans' },
      ]}
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Subscription plans</h1>
          <p className="text-muted-foreground text-sm">
            Manage plan pricing and limits. Changes sync to Stripe automatically.
          </p>
        </div>
        <Button onClick={openCreate}>
          <Plus size={16} className="mr-1" />
          New plan
        </Button>
      </div>

      {loading ? (
        <div className="flex min-h-52 items-center justify-center">
          <Loader2 className="size-8 animate-spin text-primary" />
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-outline-variant bg-surface shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-body-sm">
              <thead className="bg-surface-container-low text-label-xs uppercase tracking-wider text-on-surface-variant">
                <tr>
                  <th className="px-4 py-3 font-medium">Name</th>
                  <th className="px-4 py-3 font-medium">Type</th>
                  <th className="px-4 py-3 font-medium">Monthly</th>
                  <th className="px-4 py-3 font-medium">Yearly</th>
                  <th className="px-4 py-3 font-medium">Users</th>
                  <th className="px-4 py-3 font-medium">Dashboards</th>
                  <th className="px-4 py-3 font-medium">AI queries/mo</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Stripe</th>
                  <th className="px-4 py-3 font-medium text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/50">
                {plans.map((plan) => (
                  <tr key={plan.id} className="hover:bg-surface-container-lowest/40">
                    <td className="px-4 py-3 font-medium text-on-background">{plan.name}</td>
                    <td className="px-4 py-3 text-on-surface-variant">{planTypeLabel(plan.userType)}</td>
                    <td className="px-4 py-3">${plan.monthlyPrice.toFixed(2)}</td>
                    <td className="px-4 py-3">${plan.yearlyPrice.toFixed(2)}</td>
                    <td className="px-4 py-3 text-on-surface-variant">{plan.maxUsers ?? '—'}</td>
                    <td className="px-4 py-3 text-on-surface-variant">{plan.maxDashboards ?? '—'}</td>
                    <td className="px-4 py-3 text-on-surface-variant">{plan.maxAiQueriesPerMonth ?? '—'}</td>
                    <td className="px-4 py-3">
                      {plan.isActive ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-0.5 text-label-xs font-medium text-green-600">
                          <CheckCircle2 size={12} />
                          Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2 py-0.5 text-label-xs font-medium text-gray-600">
                          <XCircle size={12} />
                          Inactive
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {plan.stripeProductId ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-cyan-50 px-2 py-0.5 text-label-xs font-medium text-cyan-700">
                          <CheckCircle2 size={12} />
                          Synced
                        </span>
                      ) : (
                        <span className="text-on-surface-variant text-label-xs">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="outline"
                          className="min-h-9 px-3 py-1.5 text-body-sm"
                          onClick={() => openEdit(plan)}
                        >
                          <Pencil size={14} />
                          Edit
                        </Button>
                        {!plan.isActive && (
                          <>
                            <Button
                              variant="outline"
                              className="min-h-9 px-3 py-1.5 text-body-sm"
                              onClick={() => {
                                setMoving(plan);
                                setMoveTarget('');
                              }}
                            >
                              <MoveRight size={14} />
                              Move
                            </Button>
                            <Button
                              variant="outline"
                              className="min-h-9 border-red-300 px-3 py-1.5 text-body-sm text-red-600 hover:bg-red-50"
                              onClick={() => setDeleting(plan)}
                            >
                              <Trash2 size={14} />
                            </Button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
                {plans.length === 0 && (
                  <tr>
                    <td colSpan={10} className="px-4 py-8 text-center text-on-surface-variant">
                      No plans yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <Dialog open={dialogOpen} onOpenChange={(open) => !saving && setDialogOpen(open)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit plan' : 'New plan'}</DialogTitle>
            <DialogDescription>
              {editing
                ? 'Changes are applied to the database and synced to Stripe.'
                : 'Creates a Stripe product with monthly and yearly prices.'}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-1">
            <div className="grid gap-2">
              <Label htmlFor="plan-name">Name</Label>
              <Input
                id="plan-name"
                value={form.name}
                onChange={(e) => updateField('name', e.target.value)}
                placeholder="e.g. Individual Pro"
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="plan-description">Description</Label>
              <Input
                id="plan-description"
                value={form.description}
                onChange={(e) => updateField('description', e.target.value)}
                placeholder="Short description shown on pricing"
              />
            </div>

            <div className="grid gap-2">
              <Label>Type</Label>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => updateField('userType', USER_TYPE.Individual)}
                  className={`flex-1 rounded-xl border px-4 py-2.5 text-body-md font-medium transition-colors ${
                    form.userType === USER_TYPE.Individual
                      ? 'border-primary bg-primary/5 text-primary'
                      : 'border-outline-variant text-on-surface-variant hover:bg-surface-container-low'
                  }`}
                >
                  Individual
                </button>
                <button
                  type="button"
                  onClick={() => updateField('userType', USER_TYPE.Company)}
                  className={`flex-1 rounded-xl border px-4 py-2.5 text-body-md font-medium transition-colors ${
                    form.userType === USER_TYPE.Company
                      ? 'border-primary bg-primary/5 text-primary'
                      : 'border-outline-variant text-on-surface-variant hover:bg-surface-container-low'
                  }`}
                >
                  Company
                </button>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="plan-monthly">Monthly price ($)</Label>
                <Input
                  id="plan-monthly"
                  type="number"
                  step="0.01"
                  min="0"
                  value={form.monthlyPrice}
                  onChange={(e) => updateField('monthlyPrice', e.target.value)}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="plan-yearly">Yearly price ($)</Label>
                <Input
                  id="plan-yearly"
                  type="number"
                  step="0.01"
                  min="0"
                  value={form.yearlyPrice}
                  onChange={(e) => updateField('yearlyPrice', e.target.value)}
                />
              </div>
            </div>

            <div className="grid grid-cols-3 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="plan-users">Max users</Label>
                <Input
                  id="plan-users"
                  type="number"
                  min="1"
                  value={form.maxUsers}
                  onChange={(e) => updateField('maxUsers', e.target.value)}
                  placeholder="—"
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="plan-dashboards">Max dashboards</Label>
                <Input
                  id="plan-dashboards"
                  type="number"
                  min="1"
                  value={form.maxDashboards}
                  onChange={(e) => updateField('maxDashboards', e.target.value)}
                  placeholder="—"
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="plan-queries">AI queries/mo</Label>
                <Input
                  id="plan-queries"
                  type="number"
                  min="1"
                  value={form.maxAiQueriesPerMonth}
                  onChange={(e) => updateField('maxAiQueriesPerMonth', e.target.value)}
                  placeholder="—"
                />
              </div>
            </div>

            {editing && (
              <label className="flex cursor-pointer items-center justify-between rounded-xl border border-outline-variant px-4 py-3">
                <div>
                  <p className="text-body-md font-medium text-on-background">Active</p>
                  <p className="text-body-sm text-on-surface-variant">
                    Inactive plans are hidden from users and deactivated in Stripe.
                  </p>
                </div>
                <Switch
                  checked={form.isActive}
                  onCheckedChange={(checked) => updateField('isActive', checked)}
                />
              </label>
            )}

            {!editing && form.name.trim() && (
              <p className="flex items-center gap-1.5 text-body-xs text-amber-700">
                <AlertCircle size={12} />
                A Stripe product with both prices will be created for this plan.
              </p>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" disabled={saving} onClick={() => setDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => void handleSave()} disabled={saving}>
              {saving && <Loader2 className="size-4 animate-spin" />}
              {editing ? 'Save changes' : 'Create plan'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !deleteLoading && setDeleting(open ? deleting : null)}
        title="Remove plan?"
        description={`"${deleting?.name ?? ''}" stops renewing. Existing subscribers keep access until their current period ends, then the plan is retired.`}
        confirmLabel="Remove"
        variant="destructive"
        loading={deleteLoading}
        onConfirm={handleDelete}
      />

      <Dialog open={moving !== null} onOpenChange={(open) => !moveLoading && setMoving(open ? moving : null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Move subscriptions</DialogTitle>
            <DialogDescription>
              Active subscriptions on "{moving?.name ?? ''}" will be switched to the target plan's price in Stripe and updated here.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-2">
            <Label htmlFor="move-target">Target plan</Label>
            <Select value={moveTarget} onValueChange={setMoveTarget}>
              <SelectTrigger id="move-target">
                <SelectValue placeholder="Select a plan" />
              </SelectTrigger>
              <SelectContent>
                {plans
                  .filter((p) => p.isActive && p.id !== moving?.id && p.userType === moving?.userType)
                  .map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.name} — ${p.monthlyPrice.toFixed(2)}/mo
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
          </div>

          <DialogFooter>
            <Button variant="outline" disabled={moveLoading} onClick={() => setMoving(null)}>
              Cancel
            </Button>
            <Button onClick={() => void handleMove()} disabled={moveLoading || !moveTarget}>
              {moveLoading && <Loader2 className="size-4 animate-spin" />}
              Move subscriptions
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AppShell>
  );
}
