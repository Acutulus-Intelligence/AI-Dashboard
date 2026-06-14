import { createContext } from 'react';
import * as authApi from '../../lib/api/auth';

interface AuthUser {
  userId: string;
  email: string;
  roles: string[];
}

export interface AuthContextType {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  hasActiveSubscription: boolean;
  isSubscriptionLoading: boolean;
  refreshSubscriptionStatus: () => Promise<boolean>;
  login: (email: string, password: string) => Promise<void>;
  register: (data: authApi.RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextType | null>(null);
