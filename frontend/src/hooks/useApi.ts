import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import type { ApiResponse, PaginatedResponse, DashboardData, Signal, WatchlistItem, PortfolioItem } from '../types';

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
