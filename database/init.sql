-- AxonStockAgent Database Schema
-- PostgreSQL 16

-- Watchlist: aandelen die de gebruiker volgt
CREATE TABLE IF NOT EXISTS watchlist (
    id              SERIAL PRIMARY KEY,
    symbol          VARCHAR(20)  NOT NULL UNIQUE,
    exchange        VARCHAR(50),
    name            VARCHAR(200),
    sector          VARCHAR(100),
    industry        VARCHAR(100),
    country         VARCHAR(10),
    market_cap      BIGINT,
    logo            TEXT,
    web_url         TEXT,
    sector_source   VARCHAR(20),
    is_active       BOOLEAN DEFAULT true,
    added_at        TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Migreer bestaande databases: voeg nieuwe kolommen toe als ze nog niet bestaan
ALTER TABLE watchlist ADD COLUMN IF NOT EXISTS industry      VARCHAR(100);
ALTER TABLE watchlist ADD COLUMN IF NOT EXISTS country       VARCHAR(10);
ALTER TABLE watchlist ADD COLUMN IF NOT EXISTS market_cap    BIGINT;
ALTER TABLE watchlist ADD COLUMN IF NOT EXISTS logo          TEXT;
ALTER TABLE watchlist ADD COLUMN IF NOT EXISTS web_url       TEXT;
ALTER TABLE watchlist ADD COLUMN IF NOT EXISTS sector_source VARCHAR(20);

CREATE INDEX IF NOT EXISTS idx_watchlist_sector ON watchlist(sector);

-- Signalen: alle gegenereerde handelssignalen
CREATE TABLE IF NOT EXISTS signals (
    id                SERIAL PRIMARY KEY,
    symbol            VARCHAR(20) NOT NULL,
    direction         VARCHAR(10) NOT NULL,
    tech_score        DOUBLE PRECISION NOT NULL,
    ml_probability    REAL,
    sentiment_score   DOUBLE PRECISION,
    claude_confidence DOUBLE PRECISION,
    claude_direction  VARCHAR(10),
    claude_reasoning  TEXT,
    final_score       DOUBLE PRECISION NOT NULL,
    final_verdict     VARCHAR(10) NOT NULL,
    price_at_signal   DOUBLE PRECISION NOT NULL,
    trend_status      TEXT,
    momentum_status   TEXT,
    volatility_status TEXT,
    volume_status     TEXT,
    notified          BOOLEAN DEFAULT false,
    created_at        TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_signals_symbol  ON signals(symbol);
CREATE INDEX IF NOT EXISTS idx_signals_created ON signals(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_signals_verdict ON signals(final_verdict);

-- Outcome tracking kolommen (idempotent voor bestaande databases)
ALTER TABLE signals ADD COLUMN IF NOT EXISTS price_after_1d   DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS price_after_5d   DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS price_after_20d  DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS return_pct_1d    DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS return_pct_5d    DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS return_pct_20d   DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS outcome_correct  BOOLEAN;

-- Portfolio posities
CREATE TABLE IF NOT EXISTS portfolio (
    id            SERIAL PRIMARY KEY,
    symbol        VARCHAR(20) NOT NULL UNIQUE,
    shares        INTEGER NOT NULL DEFAULT 0,
    avg_buy_price DOUBLE PRECISION,
    notes         TEXT,
    added_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW()
);

-- Dividend data (gecached)
CREATE TABLE IF NOT EXISTS dividends (
    id         SERIAL PRIMARY KEY,
    symbol     VARCHAR(20) NOT NULL,
    ex_date    DATE NOT NULL,
    pay_date   DATE,
    amount     DOUBLE PRECISION NOT NULL,
    currency   VARCHAR(10) DEFAULT 'EUR',
    fetched_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(symbol, ex_date)
);

CREATE INDEX IF NOT EXISTS idx_dividends_symbol  ON dividends(symbol);
CREATE INDEX IF NOT EXISTS idx_dividends_ex_date ON dividends(ex_date);

-- Candle cache
CREATE TABLE IF NOT EXISTS candle_cache (
    id          SERIAL PRIMARY KEY,
    symbol      VARCHAR(20) NOT NULL,
    timeframe   VARCHAR(5)  NOT NULL,
    candle_time TIMESTAMPTZ NOT NULL,
    open_price  DOUBLE PRECISION NOT NULL,
    high_price  DOUBLE PRECISION NOT NULL,
    low_price   DOUBLE PRECISION NOT NULL,
    close_price DOUBLE PRECISION NOT NULL,
    volume      BIGINT NOT NULL,
    fetched_at  TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(symbol, timeframe, candle_time)
);

CREATE INDEX IF NOT EXISTS idx_candles_symbol_tf ON candle_cache(symbol, timeframe);

-- App settings
CREATE TABLE IF NOT EXISTS app_settings (
    key        VARCHAR(100) PRIMARY KEY,
    value      TEXT NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Audit log
CREATE TABLE IF NOT EXISTS audit_log (
    id          SERIAL PRIMARY KEY,
    action      VARCHAR(50) NOT NULL,
    entity_type VARCHAR(50),
    entity_id   VARCHAR(50),
    details     JSONB,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- Users
CREATE TABLE IF NOT EXISTS users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email         VARCHAR(200) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    display_name  VARCHAR(100),
    role          VARCHAR(20)  NOT NULL DEFAULT 'user',
    is_active     BOOLEAN DEFAULT true,
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    last_login_at TIMESTAMPTZ
);

-- Refresh tokens
CREATE TABLE IF NOT EXISTS refresh_tokens (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token      TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    is_revoked BOOLEAN DEFAULT false
);

CREATE INDEX IF NOT EXISTS idx_refresh_tokens_token ON refresh_tokens(token);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user  ON refresh_tokens(user_id);

-- Data providers
CREATE TABLE IF NOT EXISTS data_providers (
    id                    SERIAL PRIMARY KEY,
    name                  VARCHAR(50)   NOT NULL UNIQUE,
    display_name          VARCHAR(100)  NOT NULL,
    provider_type         VARCHAR(20)   NOT NULL DEFAULT 'market_data',
    is_enabled            BOOLEAN       DEFAULT false,
    api_key_encrypted     TEXT,
    config_json           JSONB,
    rate_limit_per_minute INTEGER       DEFAULT 60,
    supports_eu           BOOLEAN       DEFAULT false,
    supports_us           BOOLEAN       DEFAULT true,
    is_free               BOOLEAN       DEFAULT false,
    monthly_cost          DECIMAL(10,2) DEFAULT 0,
    health_status         VARCHAR(20)   DEFAULT 'unknown',
    last_health_check     TIMESTAMPTZ,
    created_at            TIMESTAMPTZ   DEFAULT NOW(),
    updated_at            TIMESTAMPTZ   DEFAULT NOW()
);

-- Seed providers
INSERT INTO data_providers
    (name, display_name, provider_type, rate_limit_per_minute, supports_eu, supports_us, is_free, monthly_cost)
VALUES
    ('finnhub',       'Finnhub',                'all',          60,  true,  true,  true,  0),
    ('eodhd',         'EODHD Historical Data',  'market_data',  100, true,  true,  false, 19.99),
    ('alpha_vantage', 'Alpha Vantage',          'market_data',  5,   true,  true,  true,  0),
    ('twelve_data',   'Twelve Data',            'market_data',  8,   true,  true,  true,  0),
    ('fmp',           'Financial Modeling Prep','fundamentals', 300, false, true,  true,  0),
    ('stockgeist',    'StockGeist',             'news',         60,  false, true,  false, 49),
    ('newsdata',      'NewsData.io',            'news',         200, true,  true,  true,  0),
    ('saxo',          'Saxo Bank OpenAPI',      'market_data',  120, true,  false, false, 0)
ON CONFLICT (name) DO NOTHING;

-- Update EODHD provider type naar 'all' (ondersteunt market data + news + fundamentals)
UPDATE data_providers SET provider_type = 'all' WHERE name = 'eodhd';

-- Algo settings: configureerbare gewichten en thresholds per categorie
CREATE TABLE IF NOT EXISTS algo_settings (
    id          SERIAL PRIMARY KEY,
    category    VARCHAR(50)  NOT NULL,
    key         VARCHAR(100) NOT NULL,
    value       TEXT         NOT NULL,
    description TEXT,
    value_type  VARCHAR(20)  NOT NULL DEFAULT 'decimal',
    min_value   DOUBLE PRECISION,
    max_value   DOUBLE PRECISION,
    updated_at  TIMESTAMPTZ  DEFAULT NOW(),
    UNIQUE(category, key)
);

-- Seed algo settings: gewichten (moeten optellen tot 1.0 per groep)
INSERT INTO algo_settings (category, key, value, description, value_type, min_value, max_value) VALUES
    -- Score gewichten (moeten optellen tot 1.0)
    ('weights', 'technical_weight',  '0.30', 'Gewicht technische analyse in eindscore',    'decimal', 0.0, 1.0),
    ('weights', 'ml_weight',         '0.25', 'Gewicht ML-voorspelling in eindscore',       'decimal', 0.0, 1.0),
    ('weights', 'sentiment_weight',  '0.20', 'Gewicht nieuwssentiment in eindscore',       'decimal', 0.0, 1.0),
    ('weights', 'claude_weight',     '0.15', 'Gewicht Claude AI-analyse in eindscore',     'decimal', 0.0, 1.0),
    ('weights', 'fundamental_weight','0.10', 'Gewicht fundamentele analyse in eindscore',  'decimal', 0.0, 1.0),
    -- Thresholds
    ('thresholds', 'buy_threshold',     '0.65', 'Minimum score voor BUY signaal',       'decimal', 0.0, 1.0),
    ('thresholds', 'sell_threshold',    '0.35', 'Maximum score voor SELL signaal',       'decimal', 0.0, 1.0),
    ('thresholds', 'squeeze_threshold', '0.80', 'Minimum score voor SQUEEZE signaal',   'decimal', 0.0, 1.0),
    -- Scan instellingen
    ('scan', 'realtime_mode',               'false', 'Realtime scanmodus — scant elke N minuten tijdens markturen (standaard: EOD dagelijks 22:30 UTC)', 'boolean', null, null),
    ('scan', 'realtime_interval_minutes',   '30',    'Interval in minuten bij realtime scan (alleen actief als realtime_mode aan staat)', 'integer', 5, 360),
    ('scan', 'lookback_days',               '90',    'Aantal dagen historische data',          'integer', 30,  365),
    ('scan', 'min_volume',                  '100000','Minimum gemiddeld volume',               'integer', 0,   null),
    ('scan', 'normalize_missing_sources',   'true',  'Ontbrekende databronnen krijgen 0.5 (neutraal) zodat scores vergelijkbaar zijn over symbolen (false = legacy: alleen aanwezige bronnen)', 'boolean', null, null),
    ('scan', 'signal_dedup_minutes',        '60',    'Deduplicatie-window in minuten: geen nieuw signaal voor hetzelfde symbool+verdict binnen dit interval', 'integer', 0, 1440),
    -- Notificatie instellingen
    ('notifications', 'notify_buy',     'true',  'Notificeer bij BUY signalen',     'boolean', null, null),
    ('notifications', 'notify_sell',    'true',  'Notificeer bij SELL signalen',    'boolean', null, null),
    ('notifications', 'notify_squeeze', 'true',  'Notificeer bij SQUEEZE signalen', 'boolean', null, null)
ON CONFLICT (category, key) DO NOTHING;

-- Seed default watchlist
INSERT INTO watchlist (symbol, exchange, name) VALUES
    ('ASML.AS',  'Euronext AMS', 'ASML Holding'),
    ('ADYEN.AS', 'Euronext AMS', 'Adyen'),
    ('HEIA.AS',  'Euronext AMS', 'Heineken'),
    ('AAPL',     'NASDAQ',       'Apple'),
    ('NVDA',     'NASDAQ',       'NVIDIA'),
    ('AMD',      'NASDAQ',       'AMD'),
    ('MSFT',     'NASDAQ',       'Microsoft')
ON CONFLICT (symbol) DO NOTHING;

-- Seed sector data voor bekende symbolen
UPDATE watchlist SET sector='Technology',       industry='Semiconductors',      country='NL' WHERE symbol='ASML.AS';
UPDATE watchlist SET sector='Technology',       industry='Payment Processing',   country='NL' WHERE symbol='ADYEN.AS';
UPDATE watchlist SET sector='Consumer Staples', industry='Beverages',            country='NL' WHERE symbol='HEIA.AS';
UPDATE watchlist SET sector='Technology',       industry='Consumer Electronics', country='US' WHERE symbol='AAPL';
UPDATE watchlist SET sector='Technology',       industry='Semiconductors',       country='US' WHERE symbol='NVDA';
UPDATE watchlist SET sector='Technology',       industry='Semiconductors',       country='US' WHERE symbol='AMD';
UPDATE watchlist SET sector='Technology',       industry='Software',             country='US' WHERE symbol='MSFT';

-- News articles
CREATE TABLE IF NOT EXISTS news_articles (
    id              SERIAL PRIMARY KEY,
    source          VARCHAR(50) NOT NULL,
    headline        TEXT NOT NULL,
    summary         TEXT,
    url             TEXT,
    symbol          VARCHAR(20),
    sector          VARCHAR(100),
    sentiment_score DOUBLE PRECISION NOT NULL DEFAULT 0,
    published_at    TIMESTAMP WITH TIME ZONE NOT NULL,
    fetched_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_news_symbol    ON news_articles(symbol);
CREATE INDEX IF NOT EXISTS idx_news_sector    ON news_articles(sector);
CREATE INDEX IF NOT EXISTS idx_news_published ON news_articles(published_at DESC);

-- Sector sentiment snapshots
CREATE TABLE IF NOT EXISTS sector_sentiment (
    id            SERIAL PRIMARY KEY,
    sector        VARCHAR(100) NOT NULL,
    avg_sentiment DOUBLE PRECISION NOT NULL,
    article_count INTEGER NOT NULL,
    period_start  TIMESTAMP WITH TIME ZONE NOT NULL,
    period_end    TIMESTAMP WITH TIME ZONE NOT NULL,
    calculated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_sector_sentiment_sector_calc ON sector_sentiment(sector, calculated_at);

-- Company fundamentals (gecacht, 1 rij per symbool)
CREATE TABLE IF NOT EXISTS company_fundamentals (
    id                    SERIAL PRIMARY KEY,
    symbol                VARCHAR(20) NOT NULL UNIQUE,
    pe_ratio              DOUBLE PRECISION,
    forward_pe            DOUBLE PRECISION,
    pb_ratio              DOUBLE PRECISION,
    ps_ratio              DOUBLE PRECISION,
    ev_to_ebitda          DOUBLE PRECISION,
    profit_margin         DOUBLE PRECISION,
    operating_margin      DOUBLE PRECISION,
    return_on_equity      DOUBLE PRECISION,
    return_on_assets      DOUBLE PRECISION,
    revenue_growth_yoy    DOUBLE PRECISION,
    earnings_growth_yoy   DOUBLE PRECISION,
    debt_to_equity        DOUBLE PRECISION,
    current_ratio         DOUBLE PRECISION,
    quick_ratio           DOUBLE PRECISION,
    dividend_yield        DOUBLE PRECISION,
    payout_ratio          DOUBLE PRECISION,
    market_cap            DOUBLE PRECISION,
    revenue               DOUBLE PRECISION,
    net_income            DOUBLE PRECISION,
    shares_outstanding    BIGINT,
    analyst_buy           INTEGER,
    analyst_hold          INTEGER,
    analyst_sell          INTEGER,
    analyst_strong_buy    INTEGER,
    analyst_strong_sell   INTEGER,
    target_price_high     DOUBLE PRECISION,
    target_price_low      DOUBLE PRECISION,
    target_price_mean     DOUBLE PRECISION,
    target_price_median   DOUBLE PRECISION,
    fetched_at            TIMESTAMPTZ DEFAULT NOW(),
    updated_at            TIMESTAMPTZ DEFAULT NOW()
);

-- Insider transactions
CREATE TABLE IF NOT EXISTS insider_transactions (
    id                SERIAL PRIMARY KEY,
    symbol            VARCHAR(20) NOT NULL,
    name              VARCHAR(200) NOT NULL,
    relation          VARCHAR(100),
    transaction_type  VARCHAR(20) NOT NULL,
    transaction_date  DATE NOT NULL,
    shares            BIGINT NOT NULL,
    price_per_share   DOUBLE PRECISION,
    total_value       DOUBLE PRECISION,
    fetched_at        TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(symbol, name, transaction_date)
);

CREATE INDEX IF NOT EXISTS idx_insider_symbol ON insider_transactions(symbol);
CREATE INDEX IF NOT EXISTS idx_insider_date   ON insider_transactions(transaction_date DESC);

-- EF Core migrations history: mark all migrations as applied so EF doesn't re-run them
-- UseSnakeCaseNamingConvention() converts column names to snake_case
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id    character varying(150) NOT NULL,
    product_version character varying(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY (migration_id)
);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260310192814_AddCompanyFundamentalsAndInsiderTransactions', '8.0.4')
ON CONFLICT DO NOTHING;
