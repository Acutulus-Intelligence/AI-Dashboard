import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { getAccessToken, clearTokens } from '../../services/api';
import { getMe, login as apiLogin, register as apiRegister, logout as apiLogout, type RegisterRequest, type LoginRequest } from '../../services/authApi';

interface User {
  userId: string;
  email: string;
  roles: string[];
}

interface AuthContextType {
  user: User | null;
  loading: boolean;
  login: (data: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (getAccessToken()) {
      getMe()
        .then((info) => setUser(info))
        .catch(() => clearTokens())
        .finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, []);

  const login = async (data: LoginRequest) => {
    await apiLogin(data);
    const info = await getMe();
    setUser(info);
  };

  const register = async (data: RegisterRequest) => {
    await apiRegister(data);
    const info = await getMe();
    setUser(info);
  };

  const logout = () => {
    setUser(null);
    apiLogout();
  };

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextType {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
