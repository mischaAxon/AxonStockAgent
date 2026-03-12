// ─── Fundamentals: Waardering ────────────────────────────────────────────────
export const TOOLTIPS = {
  // Valuation
  peRatio: 'Price-to-Earnings ratio. Aandeelprijs gedeeld door winst per aandeel. Lager dan 15 = goedkoop (groen), 15–25 = redelijk (geel), boven 25 = duur (rood).',
  forwardPe: 'Forward P/E. Zoals P/E, maar gebaseerd op verwachte toekomstige winst. Lager is over het algemeen beter.',
  pbRatio: 'Price-to-Book ratio. Aandeelprijs gedeeld door boekwaarde per aandeel. Onder 3 is doorgaans aantrekkelijk.',
  psRatio: 'Price-to-Sales ratio. Marktkapitalisatie gedeeld door omzet. Onder 2 is doorgaans aantrekkelijk.',
  evToEbitda: 'Enterprise Value / EBITDA. Totale bedrijfswaarde gedeeld door operationele kasstroom. Onder 15 is doorgaans aantrekkelijk.',

  // Profitability
  profitMargin: 'Nettomarge. Percentage van de omzet dat overblijft als nettowinst. Hoger is beter.',
  operatingMargin: 'Operationele marge. Percentage van de omzet dat overblijft na operationele kosten, vóór belasting en rente.',
  returnOnEquity: 'Return on Equity (ROE). Hoeveel winst het bedrijf maakt per euro eigen vermogen. Boven 15% is sterk.',
  returnOnAssets: 'Return on Assets (ROA). Hoeveel winst het bedrijf genereert per euro totaal vermogen.',
  revenueGrowthYoy: 'Omzetgroei jaar-op-jaar. Procentuele stijging/daling van de omzet t.o.v. vorig jaar.',
  earningsGrowthYoy: 'Winstgroei jaar-op-jaar. Procentuele stijging/daling van de nettowinst t.o.v. vorig jaar.',

  // Balance sheet
  debtToEquity: 'Schuld/Eigen vermogen. Totale schuld gedeeld door eigen vermogen. Onder 1 = conservatief (groen), 1–2 = matig (geel), boven 2 = hoge hefboom (rood).',
  currentRatio: 'Current Ratio. Vlottende activa gedeeld door kortlopende schulden. Boven 1.5 = gezond (groen), 1–1.5 = acceptabel (geel), onder 1 = risicovol (rood).',
  quickRatio: 'Quick Ratio. Zoals Current Ratio, maar zonder voorraden. Boven 1.5 = gezond, onder 1 = risicovol.',

  // Dividends
  dividendYield: 'Dividendrendement. Jaarlijks dividend als percentage van de aandeelprijs.',
  payoutRatio: 'Uitkeringsratio. Percentage van de winst dat als dividend wordt uitgekeerd. Onder 60% is duurzaam (groen), 60–80% = hoog (geel), boven 80% = risicovol (rood).',

  // Size
  marketCap: 'Marktkapitalisatie. Totale beurswaarde: aandeelprijs × aantal uitstaande aandelen.',
  revenue: 'Omzet (Trailing Twelve Months). Totale omzet over de afgelopen 12 maanden.',
  netIncome: 'Nettoresultaat. Winst (of verlies) na alle kosten, belastingen en rente.',

  // Analyst
  analystConsensus: 'Verdeling van analistenaanbevelingen: Strong Buy, Buy, Hold, Sell, Strong Sell. Gebaseerd op recente broker-rapporten.',
  targetPrice: 'Koersdoel van analisten. Het verwachte prijsniveau over 12 maanden, op basis van gemiddelden van meerdere analisten.',

  // ─── Signal & Score ──────────────────────────────────────────────────────
  finalScore: 'Gecombineerde score (0–100%). Gewogen gemiddelde van alle deelscores (technisch, sentiment, AI, ML, fundamentals). Boven 60% = BUY zone (groen), 30–60% = neutraal (geel), onder 30% = SELL zone (rood).',
  avgScore: 'Gemiddelde score over alle signalen voor dit symbool. Geeft een beeld van de historische signaalsterkte.',
  totalSignals: 'Totaal aantal signalen dat de scanner voor dit symbool heeft gegenereerd.',
  verdict: 'Signaaltype. BUY = koopsignaal, SELL = verkoopsignaal, SQUEEZE = verwachte sterke koersbeweging (hoge volatiliteit, lage Bollinger Band-breedte).',

  // Score breakdown
  techScore: 'Technische score. Gebaseerd op technische indicatoren: RSI, MACD, Bollinger Bands, voortschrijdende gemiddelden en volume-analyse. Genormaliseerd naar 0–100%.',
  sentimentScore: 'Sentimentscore. Afgeleid van recente nieuwsartikelen en hun sentimentanalyse. 0% = zeer negatief, 50% = neutraal, 100% = zeer positief.',
  claudeAI: 'Claude AI-score. Anthropic\'s Claude analyseert alle beschikbare data en geeft een eigen beoordeling met confidence-percentage en richting (BUY/SELL).',
  mlScore: 'Machine Learning score. Voorspelling van een ML-model getraind op historische signaaldata. Geeft de waarschijnlijkheid van een succesvolle trade.',
  fundamentalsScore: 'Fundamentals score. Beoordeling op basis van financiële kengetallen (P/E, groei, marges, schuld). Nog in ontwikkeling.',

  // Status indicators
  trendStatus: 'Trendstatus. Richting van het voortschrijdend gemiddelde: UPTREND, DOWNTREND of SIDEWAYS.',
  momentumStatus: 'Momentum. Kracht van de koersbeweging op basis van RSI en MACD: STRONG, MODERATE of WEAK.',
  volatilityStatus: 'Volatiliteit. Mate van koersschommelingen op basis van Bollinger Bands: HIGH, NORMAL of LOW.',
  volumeStatus: 'Volume. Handelsvolume t.o.v. het gemiddelde: ABOVE AVERAGE, NORMAL of BELOW AVERAGE.',

  // ─── Score Trend Chart ───────────────────────────────────────────────────
  scoreTrend: 'Score Trend. Laat de ontwikkeling van de gecombineerde score zien over tijd. Stippellijn boven (65%) = BUY-drempel. Stippellijn onder (35%) = SELL-drempel.',

  // ─── Live Price ──────────────────────────────────────────────────────────
  livePrice: 'Huidige koers van het aandeel, opgehaald via de actieve data-provider. Wordt elke 30 seconden ververst.',
  changePercent: 'Koersverandering vandaag als percentage t.o.v. de slotkoers van gisteren.',
  open: 'Openingskoers. De prijs bij de eerste transactie van de handelsdag.',
  high: 'Dagkoers hoog. De hoogste prijs bereikt tijdens de huidige handelsdag.',
  low: 'Dagkoers laag. De laagste prijs bereikt tijdens de huidige handelsdag.',
  prevClose: 'Vorige slotkoers. De slotkoers van de vorige handelsdag.',
  volume: 'Handelsvolume. Aantal verhandelde aandelen vandaag.',

  // ─── Dashboard ───────────────────────────────────────────────────────────
  watchlistCount: 'Aantal symbolen op je watchlist die actief worden gemonitord door de scanner.',
  portfolioPositions: 'Aantal unieke posities in je portfolio.',
  weekBuys: 'Aantal BUY-signalen gegenereerd in de afgelopen 7 dagen.',
  weekSells: 'Aantal SELL-signalen gegenereerd in de afgelopen 7 dagen.',
  weekSqueezes: 'Aantal SQUEEZE-signalen in de afgelopen 7 dagen. Een squeeze duidt op verwachte sterke koersbeweging.',
  sectorSentiment: 'Gemiddeld sentiment per sector. Berekend uit recente nieuwsartikelen. Schaal: -1 (zeer negatief) tot +1 (zeer positief). Boven +0.1 = positief (groen), onder -0.1 = negatief (rood).',
  trending: 'Symbolen die het vaakst in het nieuws voorkomen. De gekleurde stip geeft het gemiddelde sentiment aan.',

  // ─── Markets ─────────────────────────────────────────────────────────────
  signalBadge: 'Meest recente signaal voor dit symbool (afgelopen 7 dagen). Toont het verdict (BUY/SELL/SQUEEZE) en de gecombineerde score.',

  // ─── Watchlist ───────────────────────────────────────────────────────────
  watchlistMarketCap: 'Marktkapitalisatie. Totale beurswaarde van het bedrijf.',

  // ─── Portfolio ───────────────────────────────────────────────────────────
  portfolioValue: 'Geschatte portefeuillewaarde op basis van je gemiddelde aankoopprijs × aantal aandelen. Geen live koersen.',
  allocation: 'Allocatie. Procentueel aandeel van elke positie in je totale portefeuille, gebaseerd op aankoopwaarde.',
} as const;
