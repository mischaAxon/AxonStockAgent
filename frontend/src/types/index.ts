export interface Signal {
  id: number;
  symbol: string;
  direction: string;
  techScore: number;
  mlProbability: number | null;
  sentimentScore: number | null;
  claudeConfidence: number | null;
  claudeDirection: string | null;
  claudeReasoning: string | null;
  finalScore: number;
  finalVerdict: string;
  priceAtSignal: number;
  trendStatus: string | null;
  momentumStatus: string | null;
  volatilityStatus: string | null;
  volumeStatus: string | null;
  notified: boolean;
  createdAt: string;
}

export interface WatchlistItem {
  id: number;
  symbol: string;
  exchange: string | null;
  name: string | null;
  sector: string | null;
  industry: string | null;
  country: string | null;
  marketCap: number | null;
  logo: string | null;
  webUrl: string | null;
  sectorSource: string | null;
  isActive: boolean;
  addedAt: string;
}

export interface PortfolioItem {
  id: number;
  symbol: string;
  shares: number;
  avgBuyPrice: number | null;
  notes: string | null;
  addedAt: string;
}

export interface DashboardData {
  watchlistCount: number;
  portfolioPositions: number;
  portfolioEstimatedValue: number;
  signals: {
    weekBuys: number;
    weekSells: number;
    weekSqueezes: number;
  };
  recentSignals: Signal[];
  upcomingDividends: Array<{
    symbol: string;
    exDate: string;
    amount: number;
    currency: string;
  }>;
}

export interface ApiResponse<T> {
  data: T;
}

export interface PaginatedResponse<T> {
  data: T[];
  meta: { page: number; limit: number; total: number };
}

export interface NewsArticle {
  id: number;
  source: string;
  headline: string;
  summary?: string;
  url?: string;
  symbol?: string;
  sector?: string;
  sentimentScore: number;
  publishedAt: string;
  fetchedAt: string;
}

export interface SectorSentiment {
  sector: string;
  avgSentiment: number;
  articleCount: number;
  calculatedAt: string;
}

export interface TrendingSymbol {
  symbol: string;
  articleCount: number;
  avgSentiment: number;
}

// Algo Settings types
export interface WeightsConfig {
  technical: number;
  ml: number;
  sentiment: number;
  claude: number;
}

export interface TechnicalWeightsConfig {
  trend: number;
  momentum: number;
  volatility: number;
  volume: number;
}

export interface ThresholdsConfig {
  bull: number;
  bear: number;
}

export interface ScanConfig {
  intervalMinutes: number;
  cooldownMinutes: number;
  candleHistory: number;
  timeframe: string;
}

export interface FeatureFlagsConfig {
  enableMl: boolean;
  enableClaude: boolean;
  enableSentiment: boolean;
  enableNewsFetcher: boolean;
}

export interface AlgoSettingsResponse {
  weights?: WeightsConfig;
  technical_weights?: TechnicalWeightsConfig;
  thresholds?: ThresholdsConfig;
  scan?: ScanConfig;
  features?: FeatureFlagsConfig;
}
