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
  fundamentalsScore: number | null;
  newsScore: number | null;
  finalScore: number;
  finalVerdict: string;
  priceAtSignal: number;
  trendStatus: string | null;
  momentumStatus: string | null;
  volatilityStatus: string | null;
  volumeStatus: string | null;
  notified: boolean;
  createdAt: string;
  // Outcome tracking
  priceAfter1d: number | null;
  priceAfter5d: number | null;
  priceAfter20d: number | null;
  returnPct1d: number | null;
  returnPct5d: number | null;
  returnPct20d: number | null;
  outcomeCorrect: boolean | null;
}


export interface PortfolioItem {
  id: number;
  symbol: string;
  shares: number;
  avgBuyPrice: number | null;
  notes: string | null;
  addedAt: string;
  updatedAt: string;
}

export interface DashboardData {
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

// Algo Settings
export interface AlgoSetting {
  id: number;
  category: string;
  key: string;
  value: string;
  description: string | null;
  valueType: string;
  minValue: number | null;
  maxValue: number | null;
  updatedAt: string;
}

export type AlgoSettingsResponse = Record<string, AlgoSetting[]>;

// Exchange & Market data
export interface ExchangeInfo {
  exchange: string;
  country: string;
  symbolCount: number;
}

export interface MarketSymbol {
  symbol: string;
  name: string | null;
  exchange: string | null;
  sector: string | null;
  industry: string | null;
  country: string | null;
  logo: string | null;
  marketCap: number | null;
}

export interface Quote {
  currentPrice: number;
  previousClose: number;
  change: number;
  changePercent: number;
  high: number;
  low: number;
  open: number;
  volume: number;
  timestamp: string;
}

export interface MarketIndex {
  id: number;
  indexSymbol: string;
  displayName: string;
  exchangeCode: string;
  country: string;
  symbolCount: number;
  symbols: MarketSymbol[];
}

export interface LatestSignalPerSymbol {
  symbol: string;
  finalVerdict: string;
  finalScore: number;
  direction: string;
  createdAt: string;
  trendStatus: string | null;
  momentumStatus: string | null;
  volatilityStatus: string | null;
  volumeStatus: string | null;
  techScore: number;
  sentimentScore: number | null;
  claudeConfidence: number | null;
  claudeDirection: string | null;
  fundamentalsScore: number | null;
  newsScore: number | null;
}

export interface SentimentChange {
  symbol: string;
  sentimentChange: number | null;
  currentSentiment: number | null;
  dataPoints: number;
}
