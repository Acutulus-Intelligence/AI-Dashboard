import { useState, useEffect, useCallback, useRef, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authApi from '../../lib/api/auth';
import { AuthContext, type AuthUser, type LoginResult } from './AuthContext';
import { ROUTES } from '../routes';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hasActiveSubscription, setHasActiveSubscription] = useState(false);
  const [isSubscriptionLoading, setIsSubscriptionLoading] = useState(false);
  const sessionChecksPaused = useRef(false);
  const userRef = useRef<AuthUser | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    userRef.current = user;
  }, [user]);

  const pauseSessionChecks = useCallback((active: boolean) => {
    sessionChecksPaused.current = active;
  }, []);

  const refreshSubscriptionStatus = useCallback(async (silent = false) => {
    // Background polls must not toggle the loading flag, otherwise guards like
    // ProtectedRoute flash a full-screen spinner every time they run.
    if (!silent) setIsSubscriptionLoading(true);
    try {
      const isActive = await authApi.hasActiveSubscription();
      setHasActiveSubscription(isActive);
      return isActive;
    } catch {
      setHasActiveSubscription(false);
      return false;
    } finally {
      if (!silent) setIsSubscriptionLoading(false);
    }
  }, []);

  const fetchUser = useCallback(async () => {
    try {
      const userInfo = await authApi.getMe();
      const authUser: AuthUser = {
        ...userInfo,
        userType: Number(userInfo.userType),
        firstName: userInfo.firstName ?? null,
        lastName: userInfo.lastName ?? null,
        companyRoleName: userInfo.companyRoleName ?? null,
        twoFactorEnabled: userInfo.twoFactorEnabled ?? false,
        isDeactivated: userInfo.isDeactivated ?? false,
        deletionScheduledAt: userInfo.deletionScheduledAt ?? null,
      };
      setUser(authUser);
      return authUser;
    } catch {
      setUser(null);
      return null;
    }
  }, []);

  useEffect(() => {
    async function loadSession() {
      const loggedIn = await fetchUser();
      if (loggedIn) {
        await refreshSubscriptionStatus();
      }
      setIsLoading(false);
    }

    void loadSession();
  }, [fetchUser, refreshSubscriptionStatus]);

  useEffect(() => {
    const interval = setInterval(() => {
      if (sessionChecksPaused.current) return;
      // Never poll authenticated endpoints while signed out: a 401 there would
      // otherwise trigger the global session-expired redirect on public pages.
      if (!userRef.current) return;
      void refreshSubscriptionStatus(true);
    }, 60_000);
    return () => clearInterval(interval);
  }, [refreshSubscriptionStatus]);

  const login = useCallback(async (email: string, password: string): Promise<LoginResult> => {
    const response = await authApi.login({ email, password });

    if (response.requiresTwoFactor) {
      return { status: 'twoFactorRequired', challengeToken: response.challengeToken ?? '' };
    }

    const authUser = await fetchUser();
    await refreshSubscriptionStatus();
    return { status: 'success', user: authUser };
  }, [fetchUser, refreshSubscriptionStatus]);

  const completeTwoFactorLogin = useCallback(async (
    challengeToken: string,
    code: string,
    useRecoveryCode: boolean,
  ) => {
    await authApi.confirmTwoFactorLogin({ challengeToken, code, useRecoveryCode });
    const authUser = await fetchUser();
    await refreshSubscriptionStatus();
    return authUser;
  }, [fetchUser, refreshSubscriptionStatus]);

  const register = useCallback(async (data: authApi.RegisterRequest) => {
    await authApi.register(data);
    const authUser = await fetchUser();
    await refreshSubscriptionStatus();
    return authUser;
  }, [fetchUser, refreshSubscriptionStatus]);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {
      /* ignore */
    }
    setUser(null);
    setHasActiveSubscription(false);
    navigate('/');
  }, [navigate]);

  const reactivateAccount = useCallback(async (data: {
    currentPassword: string;
    code?: string;
    recoveryCode?: string;
  }) => {
    await authApi.reactivateAccount(data);
    return await fetchUser();
  }, [fetchUser]);

  useEffect(() => {
    function handleSessionExpired() {
      if (sessionChecksPaused.current) return;
      // Only react when we previously had a session. Anonymous 401s (e.g. the
      // initial /api/auth/me probe on a public page) must not redirect to login.
      if (!userRef.current) return;
      setUser(null);
      setHasActiveSubscription(false);
      navigate(ROUTES.LOGIN);
    }

    window.addEventListener('auth:session-expired', handleSessionExpired);
    return () => window.removeEventListener('auth:session-expired', handleSessionExpired);
  }, [navigate]);

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        hasActiveSubscription,
        isSubscriptionLoading,
        refreshSubscriptionStatus,
        refreshUser: fetchUser,
        login,
        completeTwoFactorLogin,
        register,
        logout,
        reactivateAccount,
        pauseSessionChecks,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
