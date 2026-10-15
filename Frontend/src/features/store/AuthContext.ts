import { createContext } from 'react';
import * as authApi from '../../lib/api/auth';

export interface AuthUser {
  userId: string;
  email: string;
  roles: string[];
  userType: number;
  firstName?: string | null;
  lastName?: string | null;
  companyRoleName?: string | null;
  twoFactorEnabled: boolean;
  isDeactivated: boolean;
  deletionScheduledAt?: string | null;
}

export type LoginResult =
  | { status: 'success'; user: AuthUser | null }
  | { status: 'twoFactorRequired'; challengeToken: string };

export interface AuthContextType {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  hasActiveSubscription: boolean;
  isSubscriptionLoading: boolean;
  refreshSubscriptionStatus: (silent?: boolean) => Promise<boolean>;
  refreshUser: () => Promise<AuthUser | null>;
  login: (email: string, password: string) => Promise<LoginResult>;
  completeTwoFactorLogin: (
    challengeToken: string,
    code: string,
    useRecoveryCode: boolean,
  ) => Promise<AuthUser | null>;
  register: (data: authApi.RegisterRequest) => Promise<AuthUser | null>;
  logout: () => Promise<void>;
  reactivateAccount: (data: {
    currentPassword: string;
    code?: string;
    recoveryCode?: string;
  }) => Promise<AuthUser | null>;
  /** Temporarily suppress background session checks (e.g. while showing one-time recovery codes). */
  pauseSessionChecks: (active: boolean) => void;
}

export const AuthContext = createContext<AuthContextType | null>(null);
