const API_BASE = import.meta.env.VITE_API_URL || '/api';

export interface UserResponse {
  id: string;
  email: string;
  displayName: string;
  role: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserResponse;
}

// In-memory token storage (never localStorage)
let _accessToken: string | null = null;
let _refreshToken: string | null = null;
let _expiresAt: Date | null = null;

export function setTokens(accessToken: string, refreshToken: string, expiresAt: string) {
  _accessToken = accessToken;
  _refreshToken = refreshToken;
  _expiresAt = new Date(expiresAt);
}

export function clearTokens() {
  _accessToken = null;
  _refreshToken = null;
  _expiresAt = null;
}

export function hasRefreshToken(): boolean {
  return _refreshToken !== null;
}

function isTokenExpired(): boolean {
  if (!_expiresAt) return true;
  return new Date() >= _expiresAt;
}

export async function getAccessToken(): Promise<string | null> {
  if (_accessToken && !isTokenExpired()) {
    return _accessToken;
  }
  if (_refreshToken) {
    try {
      const response = await refreshToken(_refreshToken);
      setTokens(response.accessToken, response.refreshToken, response.expiresAt);
      return response.accessToken;
    } catch {
      clearTokens();
      return null;
    }
  }
  return null;
}

async function authFetch(endpoint: string, options: RequestInit = {}): Promise<Response> {
  return fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  });
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  const res = await authFetch('/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || 'Login mislukt');
  }
  const data = await res.json();
  return data.data;
}

export async function register(
  email: string,
  password: string,
  displayName: string
): Promise<AuthResponse> {
  const res = await authFetch('/v1/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password, displayName }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || 'Registratie mislukt');
  }
  const data = await res.json();
  return data.data;
}

export async function refreshToken(token: string): Promise<AuthResponse> {
  const res = await authFetch('/v1/auth/refresh', {
    method: 'POST',
    body: JSON.stringify({ refreshToken: token }),
  });
  if (!res.ok) throw new Error('Token vernieuwen mislukt');
  const data = await res.json();
  return data.data;
}

export async function logoutCurrent(): Promise<void> {
  if (_refreshToken) {
    await authFetch('/v1/auth/logout', {
      method: 'POST',
      body: JSON.stringify({ refreshToken: _refreshToken }),
    }).catch(() => {});
  }
  clearTokens();
}

export async function getMe(accessToken: string): Promise<UserResponse> {
  const res = await fetch(`${API_BASE}/v1/auth/me`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
    },
  });
  if (!res.ok) throw new Error('Gebruiker ophalen mislukt');
  const data = await res.json();
  return data.data;
}
