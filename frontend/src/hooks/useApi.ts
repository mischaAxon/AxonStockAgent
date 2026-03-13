import { useQuery, useMutation, useQueryClient, useQueries } from '@tanstack/react-query';
import { useMemo } from 'react';
import { api } from '../services/api';
import type { ApiResponse, PaginatedResponse, DashboardData, Signal, WatchlistItem, PortfolioItem, NewsArticle, SectorSentiment, TrendingSymbol, CompanyFundamentals, InsiderTransaction, AlgoSettingsResponse, ExchangeInfo, MarketSymbol, Quote, LatestSignalPerSymbol, MarketIndex, SentimentChange } from '../types';

// Dashboard
export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: () => api.get<ApiResponse<DashboardData>>('/v1/dashboard'),
  });
}

// Signals
export function useSignals(page = 1, limit = 20, symbol?: string, verdict?: string, since?: string) {
  const params = new URLSearchParams({ page: String(page), limit: String(limit) });
  if (symbol) params.set('symbol', symbol);
  if (verdict) params.set('verdict', verdict);
  if (since) params.set('since', since);

  return useQuery({
    queryKey: ['signals', page, limit, symbol, verdict, since],
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
    mutationFn: (data: { symbol: string; shares: number; avgBuyPrice?: number; notes?: string }) =>
      api.post('/v1/portfolio', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
  });
}

export function useDeletePortfolio() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (symbol: string) => api.delete(`/v1/portfolio/${symbol}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
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
    mutationFn: ({ name, ...data }: { name: string; isEnabled?: boolean; priority?: number; apiKey?: string; configJson?: string }) =>
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

// Fundamentals
export function useFundamentals(symbol: string) {
  return useQuery({
    queryKey: ['fundamentals', symbol],
    queryFn: () => api.get<ApiResponse<CompanyFundamentals>>(`/v1/fundamentals/${symbol}`),
    enabled: !!symbol,
    staleTime: 60 * 60 * 1000, // 1 uur client-side cache
  });
}

export function useInsiderTransactions(symbol: string) {
  return useQuery({
    queryKey: ['insiders', symbol],
    queryFn: () => api.get<ApiResponse<InsiderTransaction[]>>(`/v1/fundamentals/${symbol}/insiders`),
    enabled: !!symbol,
    staleTime: 60 * 60 * 1000,
  });
}

export function useRefreshAllFundamentals() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/v1/fundamentals/refresh-all', {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['fundamentals'] }),
  });
}

// Admin — Algo Settings
export function useAlgoSettings() {
  return useQuery({
    queryKey: ['admin', 'settings'],
    queryFn: () => api.get<ApiResponse<AlgoSettingsResponse>>('/v1/admin/settings'),
  });
}

export function useUpdateAlgoSetting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, value }: { id: number; value: string }) =>
      api.put(`/v1/admin/settings/${id}`, { value }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] }),
  });
}

export function useResetAlgoSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/v1/admin/settings/reset', {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] }),
  });
}

// Exchanges & Markets
export function useExchanges() {
  return useQuery({
    queryKey: ['exchanges'],
    queryFn: () => api.get<ApiResponse<ExchangeInfo[]>>('/v1/exchanges'),
    staleTime: 5 * 60 * 1000,
  });
}

export function useExchangeSymbols(exchange: string) {
  return useQuery({
    queryKey: ['exchanges', exchange, 'symbols'],
    queryFn: () => api.get<ApiResponse<MarketSymbol[]>>(`/v1/exchanges/${encodeURIComponent(exchange)}/symbols`),
    enabled: !!exchange,
  });
}

export function useAllSymbols(country?: string) {
  const params = country ? `?country=${country}` : '';
  return useQuery({
    queryKey: ['exchanges', 'all-symbols', country],
    queryFn: () => api.get<ApiResponse<MarketSymbol[]>>(`/v1/exchanges/all-symbols${params}`),
    staleTime: 5 * 60 * 1000,
  });
}

export function useLatestSignalsPerSymbol(days = 7) {
  return useQuery({
    queryKey: ['signals', 'latest-per-symbol', days],
    queryFn: () => api.get<ApiResponse<LatestSignalPerSymbol[]>>(`/v1/signals/latest-per-symbol?days=${days}`),
    staleTime: 60_000,
    refetchInterval: 60_000,
  });
}

export function useBatchQuotes(symbols: string[]) {
  // Split in chunks van max 50 voor de batch API
  const chunks = useMemo(() => {
    const result: string[][] = [];
    for (let i = 0; i < symbols.length; i += 50) {
      result.push(symbols.slice(i, i + 50));
    }
    return result;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [symbols.join(',')]);

  const queries = useQueries({
    queries: chunks.map((chunk, index) => ({
      queryKey: ['quotes', 'batch', index, chunk.join(',')],
      queryFn: () => api.get<ApiResponse<Record<string, Quote>>>(`/v1/quotes/batch?symbols=${chunk.join(',')}`),
      enabled: chunk.length > 0,
      refetchInterval: 30_000,
      staleTime: 15_000,
    })),
  });

  const data = useMemo(() => {
    const merged: Record<string, Quote> = {};
    for (const query of queries) {
      if (query.data?.data) {
        Object.assign(merged, query.data.data);
      }
    }
    return { data: merged };
  }, [queries]);

  const isLoading = queries.some(q => q.isLoading);

  return { data, isLoading };
}

export function useIndicesWithSymbols() {
  return useQuery({
    queryKey: ['exchanges', 'indices-with-symbols'],
    queryFn: () => api.get<ApiResponse<MarketIndex[]>>('/v1/exchanges/indices-with-symbols'),
    staleTime: 5 * 60 * 1000,
  });
}

// Favorites
export function useFavorites() {
  return useQuery({
    queryKey: ['favorites'],
    queryFn: () => api.get<ApiResponse<string[]>>('/v1/favorites'),
    staleTime: 30_000,
  });
}

export function useToggleFavorite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (symbol: string) =>
      api.post<{ data: { symbol: string; isFavorite: boolean } }>(`/v1/favorites/${symbol}`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['favorites'] }),
  });
}

// Sentiment Changes
export function useSentimentChanges(days = 7) {
  return useQuery({
    queryKey: ['signals', 'sentiment-changes', days],
    queryFn: () => api.get<ApiResponse<SentimentChange[]>>(`/v1/signals/sentiment-changes?days=${days}`),
    staleTime: 60_000,
    refetchInterval: 60_000,
  });
}
