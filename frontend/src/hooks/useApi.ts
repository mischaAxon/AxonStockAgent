import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import type { ApiResponse, PaginatedResponse, DashboardData, Signal, WatchlistItem, PortfolioItem, NewsArticle, SectorSentiment, TrendingSymbol } from '../types';

// Dashboard
export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: () => api.get<ApiResponse<DashboardData>>('/v1/dashboard'),
  });
}

// Signals
export function useSignals(page = 1, limit = 20, symbol?: string, verdict?: string) {
  const params = new URLSearchParams({ page: String(page), limit: String(limit) });
  if (symbol) params.set('symbol', symbol);
  if (verdict) params.set('verdict', verdict);

  return useQuery({
    queryKey: ['signals', page, limit, symbol, verdict],
    queryFn: () => api.get<PaginatedResponse<Signal>>(`/v1/signals?${params}`),
  });
}

export function useLatestSignals(count = 10) {
  return useQuery({
    queryKey: ['signals', 'latest', count],
    queryFn: () => api.get<ApiResponse<Signal[]>>(`/v1/signals/latest?count=${count}`),
  });
}

// Watchlist
export function useWatchlist() {
  return useQuery({
    queryKey: ['watchlist'],
    queryFn: () => api.get<ApiResponse<WatchlistItem[]>>('/v1/watchlist'),
  });
}

export function useAddToWatchlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { symbol: string; exchange?: string; name?: string }) =>
      api.post('/v1/watchlist', data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['watchlist'] }),
  });
}

export function useRemoveFromWatchlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (symbol: string) => api.delete(`/v1/watchlist/${symbol}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['watchlist'] }),
  });
}

// Portfolio
export function usePortfolio() {
  return useQuery({
    queryKey: ['portfolio'],
    queryFn: () => api.get<ApiResponse<PortfolioItem[]>>('/v1/portfolio'),
  });
}

export function useUpsertPortfolio() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { symbol: string; shares: number; avgBuyPrice?: number }) =>
      api.post('/v1/portfolio', data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['portfolio'] }),
  });
}

// Sectors
export function useSectors() {
  return useQuery({
    queryKey: ['sectors'],
    queryFn: () => api.get<ApiResponse<{ sector: string; count: number }[]>>('/v1/sectors'),
  });
}

export function useEnrichWatchlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/v1/sectors/enrich', {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['watchlist'] });
      queryClient.invalidateQueries({ queryKey: ['sectors'] });
    },
  });
}

// News
export function useLatestNews(limit = 20) {
  return useQuery({
    queryKey: ['news', 'latest', limit],
    queryFn: () => api.get<NewsArticle[]>(`/v1/news/latest?limit=${limit}`),
    refetchInterval: 60_000,
  });
}

export function useNewsBySymbol(symbol: string) {
  return useQuery({
    queryKey: ['news', 'symbol', symbol],
    queryFn: () => api.get<NewsArticle[]>(`/v1/news/symbol/${symbol}`),
    enabled: !!symbol,
  });
}

export function useSectorSentiment() {
  return useQuery({
    queryKey: ['news', 'sector-sentiment'],
    queryFn: () => api.get<SectorSentiment[]>('/v1/news/sector-sentiment'),
    refetchInterval: 60_000,
  });
}

export function useTrending() {
  return useQuery({
    queryKey: ['news', 'trending'],
    queryFn: () => api.get<TrendingSymbol[]>('/v1/news/trending'),
    refetchInterval: 60_000,
  });
}

// Admin — Users
export function useAdminUsers() {
  return useQuery({
    queryKey: ['admin', 'users'],
    queryFn: () => api.get<ApiResponse<unknown[]>>('/v1/admin/users'),
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: { id: string; role?: string; isActive?: boolean }) =>
      api.put(`/v1/admin/users/${id}`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'users'] }),
  });
}

// Admin — Providers
export function useProviders() {
  return useQuery({
    queryKey: ['admin', 'providers'],
    queryFn: () => api.get<ApiResponse<unknown[]>>('/v1/admin/providers'),
  });
}

export function useUpdateProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ name, ...data }: { name: string; isEnabled?: boolean; apiKey?: string; configJson?: string }) =>
      api.put(`/v1/admin/providers/${name}`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'providers'] }),
  });
}

export function useTestProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) =>
      api.post(`/v1/admin/providers/${name}/test`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'providers'] }),
  });
}

// Admin — Algo Settings
export function useAlgoSettings() {
  return useQuery({
    queryKey: ['algo-settings'],
    queryFn: () => api.get<Record<string, unknown>>('/v1/admin/settings'),
  });
}

export function useUpdateAlgoSetting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ key, value }: { key: string; value: unknown }) =>
      api.put(`/v1/admin/settings/${key}`, value),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['algo-settings'] }),
  });
}

export function useResetAlgoSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/v1/admin/settings/reset', {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['algo-settings'] }),
  });
}
