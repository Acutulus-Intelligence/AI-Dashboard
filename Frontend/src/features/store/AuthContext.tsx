import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authApi from '../../lib/api/auth';
import { getAccessToken, getRefreshToken, setTokens, clearTokens } from '../../lib/api/client';

interface AuthUser {
  userId: string;
  email: string;
  roles: string[];
}

interface AuthContextType {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (data: authApi.RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | null>(null);

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
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const navigate = useNavigate();

  const restoreSession = useCallback(() => {
    const token = getAccessToken();
    if (token) {
      const decoded = decodeJwtPayload(token);
      if (decoded) {
        setUser(decoded);
      } else {
        clearTokens();
      }
    }
    setIsLoading(false);
  }, []);

  useEffect(() => {
    restoreSession();
  }, [restoreSession]);

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

export function useAuth(): AuthContextType {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
}
