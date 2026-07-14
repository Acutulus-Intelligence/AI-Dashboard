import { createContext } from 'react';
import * as authApi from '../../lib/api/auth';

export interface AuthUser {
  userId: string;
  email: string;
  roles: string[];
  userType: number;
  firstName?: string | null;
  lastName?: string | null;
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
