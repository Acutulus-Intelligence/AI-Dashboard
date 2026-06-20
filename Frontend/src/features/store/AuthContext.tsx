import { useState, useEffect, useCallback, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authApi from '../../lib/api/auth';
import { getAccessToken, setTokens, clearTokens } from '../../lib/api/client';
import { AuthContext } from './AuthContext';

interface AuthUser {
  userId: string;
  email: string;
  roles: string[];
}

function decodeJwtPayload(token: string): AuthUser | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')));
    const roles = payload.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : [];
    return {
      userId: payload.sub || payload.userId || '',
      email: payload.email || '',
      roles,
    };
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = getAccessToken();
    if (!token) return null;
    const decoded = decodeJwtPayload(token);
    if (!decoded) {
      clearTokens();
      return null;
    }
    return decoded;
  });
  const [isLoading, setIsLoading] = useState(true);
  const [hasActiveSubscription, setHasActiveSubscription] = useState(false);
  const [isSubscriptionLoading, setIsSubscriptionLoading] = useState(false);
  const navigate = useNavigate();

  const refreshSubscriptionStatus = useCallback(async () => {
    if (!getAccessToken()) {
      setHasActiveSubscription(false);
      return false;
    }

    setIsSubscriptionLoading(true);
    try {
      const isActive = await authApi.hasActiveSubscription();
      setHasActiveSubscription(isActive);
      return isActive;
    } finally {
      setIsSubscriptionLoading(false);
    }
  }, []);

  useEffect(() => {
    async function loadSession() {
      await refreshSubscriptionStatus();
      setIsLoading(false);
    }

    void loadSession();
  }, [refreshSubscriptionStatus]);

  const handleAuthResponse = useCallback((data: authApi.AuthResponse) => {
    setTokens(data.accessToken, data.refreshToken);
    const decoded = decodeJwtPayload(data.accessToken);
    if (decoded) {
      setUser(decoded);
    }
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const data = await authApi.login({ email, password });
    handleAuthResponse(data);
    await refreshSubscriptionStatus();
  }, [handleAuthResponse, refreshSubscriptionStatus]);

  const register = useCallback(async (data: authApi.RegisterRequest) => {
    const response = await authApi.register(data);
    handleAuthResponse(response);
    await refreshSubscriptionStatus();
  }, [handleAuthResponse, refreshSubscriptionStatus]);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {
      /* ignore */
    }
    clearTokens();
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
