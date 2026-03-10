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

// Company Fundamentals
export interface CompanyFundamentals {
  id: number;
  symbol: string;
  // Valuation
  peRatio: number | null;
  forwardPe: number | null;
  pbRatio: number | null;
  psRatio: number | null;
  evToEbitda: number | null;
  // Profitability
  profitMargin: number | null;
  operatingMargin: number | null;
  returnOnEquity: number | null;
  returnOnAssets: number | null;
  // Growth
  revenueGrowthYoy: number | null;
  earningsGrowthYoy: number | null;
  // Balance sheet
  debtToEquity: number | null;
  currentRatio: number | null;
  quickRatio: number | null;
  // Dividends
  dividendYield: number | null;
  payoutRatio: number | null;
  // Size
  marketCap: number | null;
  revenue: number | null;
  netIncome: number | null;
  sharesOutstanding: number | null;
  // Analyst
  analystBuy: number | null;
  analystHold: number | null;
  analystSell: number | null;
  analystStrongBuy: number | null;
  analystStrongSell: number | null;
  targetPriceHigh: number | null;
  targetPriceLow: number | null;
  targetPriceMean: number | null;
  targetPriceMedian: number | null;
  // Meta
  fetchedAt: string;
  updatedAt: string;
}

export interface InsiderTransaction {
  id: number;
  symbol: string;
  name: string;
  relation: string;
  transactionType: string;
  transactionDate: string;
  shares: number;
  pricePerShare: number;
  totalValue: number;
  fetchedAt: string;
}
