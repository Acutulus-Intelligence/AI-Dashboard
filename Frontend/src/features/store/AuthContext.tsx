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
  const navigate = useNavigate();

  const pauseSessionChecks = useCallback((active: boolean) => {
    sessionChecksPaused.current = active;
  }, []);

  const refreshSubscriptionStatus = useCallback(async () => {
    setIsSubscriptionLoading(true);
    try {
      const isActive = await authApi.hasActiveSubscription();
      setHasActiveSubscription(isActive);
      return isActive;
    } catch {
      setHasActiveSubscription(false);
      return false;
    } finally {
      setIsSubscriptionLoading(false);
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
      void refreshSubscriptionStatus();
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

  useEffect(() => {
    function handleSessionExpired() {
      if (sessionChecksPaused.current) return;
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
        pauseSessionChecks,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
