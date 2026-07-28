import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { Loader2, TriangleAlert } from 'lucide-react';
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
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import AppShell from '../layouts/AppShell';
import PasswordRequirements from '../components/PasswordRequirements';
import { useAuth } from '../store/useAuth';
import * as authApi from '../../lib/api/auth';
import {
  accountSchema,
  deleteAccountSchema,
  passwordSchema,
  type AccountFormValues,
  type DeleteAccountFormValues,
  type PasswordFormValues,
} from '../validation/profileForms';

function errorMessage(err: unknown, fallback: string) {
  return err instanceof Error ? err.message : fallback;
}

function AccountTab() {
  const { user, refreshUser } = useAuth();

  const form = useForm<AccountFormValues>({
    resolver: zodResolver(accountSchema),
    defaultValues: {
      firstName: user?.firstName ?? '',
      lastName: user?.lastName ?? '',
      email: user?.email ?? '',
    },
  });

  async function onSubmit(values: AccountFormValues) {
    try {
      await authApi.updateProfile({
        firstName: values.firstName,
        lastName: values.lastName,
        email: values.email !== user?.email ? values.email : undefined,
      });
      await refreshUser();
      form.reset(values);
      toast.success('Profile updated.');
    } catch (err) {
      toast.error(errorMessage(err, 'Failed to update profile.'));
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <Card>
          <CardHeader>
            <CardTitle>Account</CardTitle>
            <CardDescription>
              Your name and email as they appear across the workspace.
            </CardDescription>
          </CardHeader>

          <CardContent className="grid gap-4 sm:max-w-lg">
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="firstName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>First name</FormLabel>
                    <FormControl>
                      <Input autoComplete="given-name" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="lastName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Last name</FormLabel>
                    <FormControl>
                      <Input autoComplete="family-name" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Email</FormLabel>
                  <FormControl>
                    <Input type="email" autoComplete="email" {...field} />
                  </FormControl>
                  <FormDescription>Used to sign in and receive notifications.</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>

          <CardFooter className="gap-2">
            <Button type="submit" disabled={form.formState.isSubmitting || !form.formState.isDirty}>
              {form.formState.isSubmitting && <Loader2 className="animate-spin" />}
              Save changes
            </Button>
            {form.formState.isDirty && (
              <Button type="button" variant="ghost" onClick={() => form.reset()}>
                Discard
              </Button>
            )}
          </CardFooter>
        </Card>
      </form>
    </Form>
  );
}

function SecurityTab() {
  const form = useForm<PasswordFormValues>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmNewPassword: '' },
  });

  const newPassword = useWatch({ control: form.control, name: 'newPassword' });

  async function onSubmit(values: PasswordFormValues) {
    try {
      await authApi.changePassword(values);
      form.reset();
      toast.success('Password changed.');
    } catch (err) {
      toast.error(errorMessage(err, 'Failed to change password.'));
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <Card>
          <CardHeader>
            <CardTitle>Password</CardTitle>
            <CardDescription>
              Choose a strong password you do not use anywhere else.
            </CardDescription>
          </CardHeader>

          <CardContent className="grid gap-4 sm:max-w-lg">
            <FormField
              control={form.control}
              name="currentPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Current password</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="current-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="newPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>New password</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="new-password" {...field} />
                  </FormControl>
                  <PasswordRequirements password={newPassword} />
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="confirmNewPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Confirm new password</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="new-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>

          <CardFooter>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting && <Loader2 className="animate-spin" />}
              Change password
            </Button>
          </CardFooter>
        </Card>
      </form>
    </Form>
  );
}

function DangerZoneTab() {
  const { logout } = useAuth();
  const [open, setOpen] = useState(false);

  const form = useForm<DeleteAccountFormValues>({
    resolver: zodResolver(deleteAccountSchema),
    defaultValues: { currentPassword: '' },
  });

  async function onSubmit(values: DeleteAccountFormValues) {
    try {
      await authApi.deleteAccount(values.currentPassword);
      setOpen(false);
      await logout();
    } catch (err) {
      form.setError('currentPassword', {
        message: errorMessage(err, 'Failed to delete account.'),
      });
    }
  }

  return (
    <Card className="border-destructive/40">
      <CardHeader>
        <CardTitle className="text-destructive">Delete account</CardTitle>
        <CardDescription>
          Permanently removes your account along with every dashboard, chart and database
          connection you own.
        </CardDescription>
      </CardHeader>

      <CardContent>
        <Alert variant="destructive">
          <TriangleAlert />
          <AlertTitle>This cannot be undone</AlertTitle>
          <AlertDescription>
            Once deleted, your data cannot be recovered by you or by support.
          </AlertDescription>
        </Alert>
      </CardContent>

      <CardFooter>
        <AlertDialog
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            if (!next) form.reset();
          }}
        >
          <AlertDialogTrigger asChild>
            <Button variant="destructive">Delete my account</Button>
          </AlertDialogTrigger>

          <AlertDialogContent>
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4">
                <AlertDialogHeader>
                  <AlertDialogTitle>Delete your account?</AlertDialogTitle>
                  <AlertDialogDescription>
                    Confirm with your password. This permanently deletes your account and all
                    associated data.
                  </AlertDialogDescription>
                </AlertDialogHeader>

                <FormField
                  control={form.control}
                  name="currentPassword"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Password</FormLabel>
                      <FormControl>
                        <Input type="password" autoComplete="current-password" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <AlertDialogFooter>
                  <AlertDialogCancel type="button">Cancel</AlertDialogCancel>
                  {/* A plain submit button, not AlertDialogAction, so a failed
                      delete keeps the dialog open to show the error. */}
                  <Button type="submit" variant="destructive" disabled={form.formState.isSubmitting}>
                    {form.formState.isSubmitting && <Loader2 className="animate-spin" />}
                    Delete permanently
                  </Button>
                </AlertDialogFooter>
              </form>
            </Form>
          </AlertDialogContent>
        </AlertDialog>
      </CardFooter>
    </Card>
  );
}

export default function ProfilePage() {
  return (
    <AppShell breadcrumbs={[{ label: 'Profile' }]}>
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Profile</h1>
        <p className="text-muted-foreground text-sm">
          Manage your personal details, password and account.
        </p>
      </div>

      <Tabs defaultValue="account" className="gap-6">
        <TabsList>
          <TabsTrigger value="account">Account</TabsTrigger>
          <TabsTrigger value="security">Security</TabsTrigger>
          <TabsTrigger value="danger">Danger zone</TabsTrigger>
        </TabsList>

        <TabsContent value="account">
          <AccountTab />
        </TabsContent>
        <TabsContent value="security">
          <SecurityTab />
        </TabsContent>
        <TabsContent value="danger">
          <DangerZoneTab />
        </TabsContent>
      </Tabs>
    </AppShell>
  );
}
