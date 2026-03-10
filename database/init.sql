-- AxonStockAgent Database Schema
-- PostgreSQL 16

-- Watchlist: aandelen die de gebruiker volgt
CREATE TABLE IF NOT EXISTS watchlist (
    id              SERIAL PRIMARY KEY,
    symbol          VARCHAR(20) NOT NULL UNIQUE,
    exchange        VARCHAR(50),
    name            VARCHAR(200),
    sector          VARCHAR(100),
    is_active       BOOLEAN DEFAULT true,
    added_at        TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Signalen: alle gegenereerde handelssignalen
CREATE TABLE IF NOT EXISTS signals (
    id              SERIAL PRIMARY KEY,
    symbol          VARCHAR(20) NOT NULL,
    direction       VARCHAR(10) NOT NULL,          -- BUY / SELL / SQUEEZE
    tech_score      DOUBLE PRECISION NOT NULL,     -- technische score -1..+1
    ml_probability  REAL,                          -- ML model kans 0..1
    sentiment_score DOUBLE PRECISION,              -- nieuws sentiment -1..+1
    claude_confidence DOUBLE PRECISION,            -- Claude AI zekerheid 0..1
    claude_direction VARCHAR(10),                  -- BUY / SELL / NEUTRAL / AVOID
    claude_reasoning TEXT,
    final_score     DOUBLE PRECISION NOT NULL,     -- gewogen eindscore
    final_verdict   VARCHAR(10) NOT NULL,          -- BUY / SELL / SQUEEZE / SKIP
    price_at_signal DOUBLE PRECISION NOT NULL,
    trend_status    TEXT,
    momentum_status TEXT,
    volatility_status TEXT,
    volume_status   TEXT,
    notified        BOOLEAN DEFAULT false,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_signals_symbol ON signals(symbol);
CREATE INDEX idx_signals_created ON signals(created_at DESC);
CREATE INDEX idx_signals_verdict ON signals(final_verdict);

-- Portfolio posities
CREATE TABLE IF NOT EXISTS portfolio (
    id              SERIAL PRIMARY KEY,
    symbol          VARCHAR(20) NOT NULL UNIQUE,
    shares          INTEGER NOT NULL DEFAULT 0,
    avg_buy_price   DOUBLE PRECISION,
    notes           TEXT,
    added_at        TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Dividend data (gecached)
CREATE TABLE IF NOT EXISTS dividends (
    id              SERIAL PRIMARY KEY,
    symbol          VARCHAR(20) NOT NULL,
    ex_date         DATE NOT NULL,
    pay_date        DATE,
    amount          DOUBLE PRECISION NOT NULL,
    currency        VARCHAR(10) DEFAULT 'EUR',
    fetched_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(symbol, ex_date)
);

CREATE INDEX idx_dividends_symbol ON dividends(symbol);
CREATE INDEX idx_dividends_ex_date ON dividends(ex_date);

-- Candle cache (voorkomt onnodige API calls)
CREATE TABLE IF NOT EXISTS candle_cache (
    id              SERIAL PRIMARY KEY,
    symbol          VARCHAR(20) NOT NULL,
    timeframe       VARCHAR(5) NOT NULL,           -- D, 60, W
    candle_time     TIMESTAMPTZ NOT NULL,
    open_price      DOUBLE PRECISION NOT NULL,
    high_price      DOUBLE PRECISION NOT NULL,
    low_price       DOUBLE PRECISION NOT NULL,
    close_price     DOUBLE PRECISION NOT NULL,
    volume          BIGINT NOT NULL,
    fetched_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(symbol, timeframe, candle_time)
);

CREATE INDEX idx_candles_symbol_tf ON candle_cache(symbol, timeframe);

-- App settings (key-value voor runtime configuratie)
CREATE TABLE IF NOT EXISTS app_settings (
    key             VARCHAR(100) PRIMARY KEY,
    value           TEXT NOT NULL,
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Audit log
CREATE TABLE IF NOT EXISTS audit_log (
    id              SERIAL PRIMARY KEY,
    action          VARCHAR(50) NOT NULL,
    entity_type     VARCHAR(50),
    entity_id       VARCHAR(50),
    details         JSONB,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Users: authenticatie en autorisatie
CREATE TABLE IF NOT EXISTS users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email           VARCHAR(200) NOT NULL UNIQUE,
    password_hash   TEXT NOT NULL,
    display_name    VARCHAR(100),
    role            VARCHAR(20) NOT NULL DEFAULT 'user',
    is_active       BOOLEAN DEFAULT true,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    last_login_at   TIMESTAMPTZ
);

-- Refresh tokens
CREATE TABLE IF NOT EXISTS refresh_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token           TEXT NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    is_revoked      BOOLEAN DEFAULT false
);

CREATE INDEX idx_refresh_tokens_token ON refresh_tokens(token);
CREATE INDEX idx_refresh_tokens_user ON refresh_tokens(user_id);

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
