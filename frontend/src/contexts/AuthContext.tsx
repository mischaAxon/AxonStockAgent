import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authService from '../services/auth';
import type { UserResponse } from '../services/auth';

interface AuthContextValue {
  user: UserResponse | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  isAdmin: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    async function tryRestore() {
      if (authService.hasRefreshToken()) {
        try {
          const token = await authService.getAccessToken();
          if (token) {
            const me = await authService.getMe(token);
            setUser(me);
          }
        } catch {
          authService.clearTokens();
        }
      }
      setIsLoading(false);
    }
    tryRestore();
  }, []);

  async function login(email: string, password: string) {
    const response = await authService.login(email, password);
    authService.setTokens(response.accessToken, response.refreshToken, response.expiresAt);
    setUser(response.user);
  }

  async function register(email: string, password: string, displayName: string) {
    const response = await authService.register(email, password, displayName);
    authService.setTokens(response.accessToken, response.refreshToken, response.expiresAt);
    setUser(response.user);
  }

  async function logout() {
    await authService.logoutCurrent();
    setUser(null);
    navigate('/login');
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAuthenticated: user !== null,
        isAdmin: user?.role === 'admin',
        login,
        register,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
