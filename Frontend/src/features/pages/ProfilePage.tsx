import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, ArrowLeft, CheckCircle2, Lock, Trash2, User } from 'lucide-react';
import Button from '../components/Button';
import DashboardHeader from '../layouts/DashboardHeader';
import { ROUTES } from '../routes';
import * as authApi from '../../lib/api/auth';
import { useAuth } from '../store/useAuth';

export default function ProfilePage() {
  const { user, logout } = useAuth();
  const isCompany = user?.userType === 1;
  const backRoute = isCompany ? ROUTES.ADMIN : ROUTES.SETTINGS;

  const [profileFirstName, setProfileFirstName] = useState(user?.firstName ?? '');
  const [profileLastName, setProfileLastName] = useState(user?.lastName ?? '');
  const [profileEmail, setProfileEmail] = useState(user?.email ?? '');
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileSuccess, setProfileSuccess] = useState(false);
  const [profileError, setProfileError] = useState('');

  const [pwCurrent, setPwCurrent] = useState('');
  const [pwNew, setPwNew] = useState('');
  const [pwConfirm, setPwConfirm] = useState('');
  const [pwSaving, setPwSaving] = useState(false);
  const [pwSuccess, setPwSuccess] = useState(false);
  const [pwError, setPwError] = useState('');

  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState('');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');

  async function handleUpdateProfile(e: FormEvent) {
    e.preventDefault();
    setProfileError('');
    setProfileSuccess(false);

    if (!profileFirstName.trim() || !profileLastName.trim()) {
      setProfileError('First name and last name are required.');
      return;
    }

    setProfileSaving(true);
    try {
      await authApi.updateProfile({
        firstName: profileFirstName.trim(),
        lastName: profileLastName.trim(),
        email: profileEmail.trim() !== user?.email ? profileEmail.trim() || undefined : undefined,
      });
      setProfileSuccess(true);
      setTimeout(() => setProfileSuccess(false), 3000);
    } catch (err) {
      setProfileError(err instanceof Error ? err.message : 'Failed to update profile.');
    } finally {
      setProfileSaving(false);
    }
  }

  async function handleChangePassword(e: FormEvent) {
    e.preventDefault();
    setPwError('');
    setPwSuccess(false);

    if (!pwCurrent || !pwNew || !pwConfirm) {
      setPwError('All password fields are required.');
      return;
    }

    if (pwNew !== pwConfirm) {
      setPwError('New passwords do not match.');
      return;
    }

    if (pwNew.length < 6) {
      setPwError('New password must be at least 6 characters.');
      return;
    }

    setPwSaving(true);
    try {
      await authApi.changePassword({
        currentPassword: pwCurrent,
        newPassword: pwNew,
        confirmNewPassword: pwConfirm,
      });
      setPwSuccess(true);
      setPwCurrent('');
      setPwNew('');
      setPwConfirm('');
      setTimeout(() => setPwSuccess(false), 3000);
    } catch (err) {
      setPwError(err instanceof Error ? err.message : 'Failed to change password.');
    } finally {
      setPwSaving(false);
    }
  }

  async function executeDelete() {
    setDeleteError('');
    setDeleting(true);
    try {
      await authApi.deleteAccount();
      await logout();
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : 'Failed to delete account.');
      setDeleting(false);
    }
  }

  return (
    <div className="min-h-screen bg-background">
      <DashboardHeader
        onToggleNavbar={() => {}}
        onNewChart={() => {}}
        onNewDashboard={() => {}}
      />
      <main className="pt-16">
        <div className="mx-auto max-w-container-max px-gutter py-8">
          <div className="mb-6 flex items-center gap-3 border-b border-outline-variant/40 pb-3">
            <Link
              to={backRoute}
              className="inline-flex items-center justify-center rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container"
            >
              <ArrowLeft size={18} />
            </Link>
            <h1 className="text-headline-md font-bold text-on-background">Profile</h1>
          </div>

          <div className="grid gap-6 md:grid-cols-2">
            {/* Profile Info */}
            <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm">
              <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <User size={24} />
              </div>
              <h2 className="mb-3 text-body-lg font-semibold text-on-background">Profile Information</h2>
              <form onSubmit={handleUpdateProfile} className="space-y-3">
                <div>
                  <label htmlFor="profileFirstName" className="mb-1 block text-body-xs font-medium text-on-surface-variant">First name</label>
                  <input
                    id="profileFirstName"
                    type="text"
                    value={profileFirstName}
                    onChange={(e) => setProfileFirstName(e.target.value)}
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                  />
                </div>
                <div>
                  <label htmlFor="profileLastName" className="mb-1 block text-body-xs font-medium text-on-surface-variant">Last name</label>
                  <input
                    id="profileLastName"
                    type="text"
                    value={profileLastName}
                    onChange={(e) => setProfileLastName(e.target.value)}
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                  />
                </div>
                <div>
                  <label htmlFor="profileEmail" className="mb-1 block text-body-xs font-medium text-on-surface-variant">Email</label>
                  <input
                    id="profileEmail"
                    type="email"
                    value={profileEmail}
                    onChange={(e) => setProfileEmail(e.target.value)}
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                  />
                </div>
                {profileError && (
                  <p className="flex items-center gap-1.5 text-body-xs text-red-600">
                    <AlertCircle size={12} />
                    {profileError}
                  </p>
                )}
                {profileSuccess && (
                  <p className="flex items-center gap-1.5 text-body-xs text-green-600">
                    <CheckCircle2 size={12} />
                    Profile updated successfully.
                  </p>
                )}
                <Button type="submit" variant="primary" className="w-full" disabled={profileSaving}>
                  {profileSaving ? 'Saving...' : 'Save Profile'}
                </Button>
              </form>
            </div>

            {/* Change Password */}
            <div className="rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm">
              <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <Lock size={24} />
              </div>
              <h2 className="mb-3 text-body-lg font-semibold text-on-background">Change Password</h2>
              <form onSubmit={handleChangePassword} className="space-y-3">
                <div>
                  <label htmlFor="pwCurrent" className="mb-1 block text-body-xs font-medium text-on-surface-variant">Current password</label>
                  <input
                    id="pwCurrent"
                    type="password"
                    value={pwCurrent}
                    onChange={(e) => setPwCurrent(e.target.value)}
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                  />
                </div>
                <div>
                  <label htmlFor="pwNew" className="mb-1 block text-body-xs font-medium text-on-surface-variant">New password</label>
                  <input
                    id="pwNew"
                    type="password"
                    value={pwNew}
                    onChange={(e) => setPwNew(e.target.value)}
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                  />
                </div>
                <div>
                  <label htmlFor="pwConfirm" className="mb-1 block text-body-xs font-medium text-on-surface-variant">Confirm new password</label>
                  <input
                    id="pwConfirm"
                    type="password"
                    value={pwConfirm}
                    onChange={(e) => setPwConfirm(e.target.value)}
                    className="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                  />
                </div>
                {pwError && (
                  <p className="flex items-center gap-1.5 text-body-xs text-red-600">
                    <AlertCircle size={12} />
                    {pwError}
                  </p>
                )}
                {pwSuccess && (
                  <p className="flex items-center gap-1.5 text-body-xs text-green-600">
                    <CheckCircle2 size={12} />
                    Password changed successfully.
                  </p>
                )}
                <Button type="submit" variant="primary" className="w-full" disabled={pwSaving}>
                  {pwSaving ? 'Changing...' : 'Change Password'}
                </Button>
              </form>
            </div>

            {/* Danger Zone */}
            <div className="rounded-2xl border border-red-200 bg-red-50/50 p-6 shadow-sm">
              <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-red-100 text-red-600">
                <Trash2 size={24} />
              </div>
              <h2 className="mb-2 text-body-lg font-semibold text-red-700">Danger Zone</h2>

              {!showDeleteConfirm ? (
                <>
                  <p className="mb-4 text-body-sm text-red-600">
                    Permanently delete your account and all associated data. This cannot be undone.
                  </p>
                  <Button
                    variant="outline"
                    className="w-full border-red-300 text-red-600 hover:bg-red-100"
                    onClick={(e) => { e.preventDefault(); setShowDeleteConfirm(true); }}
                  >
                    Delete Account
                  </Button>
                </>
              ) : (
                <div className="space-y-3">
                  <div className="rounded-xl border border-red-300 bg-red-100/50 px-4 py-3 text-body-sm text-red-700">
                    <p className="mb-2 font-medium">This action is permanent.</p>
                    <p>
                      All your data including dashboards, charts, and connections will be permanently removed.
                      This cannot be undone.
                    </p>
                  </div>

                  <div>
                    <label htmlFor="deleteConfirm" className="mb-1 block text-body-xs font-medium text-red-700">
                      Type <span className="font-bold">DELETE</span> to confirm
                    </label>
                    <input
                      id="deleteConfirm"
                      type="text"
                      value={deleteConfirmText}
                      onChange={(e) => setDeleteConfirmText(e.target.value)}
                      placeholder="Type DELETE to confirm"
                      className="w-full rounded-xl border border-red-300 bg-surface-container-lowest px-3 py-2.5 text-body-md text-on-background placeholder:text-on-surface-variant/50 focus:border-red-500 focus:outline-none focus:ring-2 focus:ring-red-500/20"
                    />
                  </div>

                  {deleteError && (
                    <p className="flex items-center gap-1.5 text-body-xs text-red-700">
                      <AlertCircle size={12} />
                      {deleteError}
                    </p>
                  )}

                  <div className="flex gap-3">
                    <Button
                      variant="outline"
                      className="flex-1"
                      disabled={deleting}
                      onClick={(e) => { e.preventDefault(); setShowDeleteConfirm(false); setDeleteConfirmText(''); setDeleteError(''); }}
                    >
                      Cancel
                    </Button>
                    <Button
                      variant="outline"
                      className="flex-1 border-red-300 text-red-600 hover:bg-red-100"
                      disabled={deleteConfirmText !== 'DELETE' || deleting}
                      onClick={(e) => { e.preventDefault(); void executeDelete(); }}
                    >
                      {deleting ? 'Deleting...' : 'Delete my account'}
                    </Button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}