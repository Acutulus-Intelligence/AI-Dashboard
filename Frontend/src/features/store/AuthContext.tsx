import { useState, useEffect, useCallback, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authApi from '../../lib/api/auth';
import { AuthContext, type AuthUser } from './AuthContext';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hasActiveSubscription, setHasActiveSubscription] = useState(false);
  const [isSubscriptionLoading, setIsSubscriptionLoading] = useState(false);
  const navigate = useNavigate();

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
      setUser({ ...userInfo, userType: Number(userInfo.userType) });
      return true;
    } catch {
      setUser(null);
      return false;
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

  const login = useCallback(async (email: string, password: string) => {
    await authApi.login({ email, password });
    await fetchUser();
    await refreshSubscriptionStatus();
  }, [fetchUser, refreshSubscriptionStatus]);

  const register = useCallback(async (data: authApi.RegisterRequest) => {
    await authApi.register(data);
    await fetchUser();
    await refreshSubscriptionStatus();
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

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        hasActiveSubscription,
        isSubscriptionLoading,
        refreshSubscriptionStatus,
        login,
        register,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
