import { useState, useCallback, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authApi from '../../lib/api/auth';
import { getAccessToken, getRefreshToken, setTokens, clearTokens } from '../../lib/api/client';
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
  const [isLoading] = useState(() => !getAccessToken());
  const navigate = useNavigate();

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
  }, [handleAuthResponse]);

  const register = useCallback(async (data: authApi.RegisterRequest) => {
    const response = await authApi.register(data);
    handleAuthResponse(response);
  }, [handleAuthResponse]);

  const logout = useCallback(async () => {
    try {
      const accessToken = getAccessToken();
      const refreshToken = getRefreshToken();
      if (accessToken && refreshToken) {
        await authApi.revoke({ accessToken, refreshToken });
      }
    } catch {
      /* ignore */
    }
    clearTokens();
    setUser(null);
    navigate('/');
  }, [navigate]);

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        login,
        register,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
