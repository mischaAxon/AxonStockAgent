-- ============================================================================
-- Cleanup: verwijder gestapelde signalen, behoud alleen het meest recente
-- per symbool + verdict combinatie per dag.
--
-- GEBRUIK:
--   psql -h localhost -U axon -d axonstockagent -f scripts/cleanup-duplicate-signals.sql
--
-- WAT DOET DIT SCRIPT:
-- 1. Toont eerst hoeveel duplicaten er zijn (dry run)
-- 2. Verwijdert alle behalve het nieuwste signaal per symbool+verdict per dag
-- 3. Voegt een composiet-index toe voor snelle upsert lookups
-- 4. Seed de signal_dedup_minutes AlgoSetting
-- ============================================================================

BEGIN;

-- ── Stap 1: Dry run — toon wat verwijderd gaat worden ──
SELECT
    'DUPLICATEN GEVONDEN' AS status,
    COUNT(*) AS aantal_te_verwijderen
FROM signals s
WHERE s.id NOT IN (
    SELECT DISTINCT ON (symbol, "FinalVerdict", DATE("CreatedAt"))
        id
    FROM signals
    ORDER BY symbol, "FinalVerdict", DATE("CreatedAt"), "CreatedAt" DESC
);

-- ── Stap 2: Verwijder duplicaten ──
-- Behoudt per symbool + verdict + dag alleen het meest recente signaal
DELETE FROM signals
WHERE id NOT IN (
    SELECT DISTINCT ON (symbol, "FinalVerdict", DATE("CreatedAt"))
        id
    FROM signals
    ORDER BY symbol, "FinalVerdict", DATE("CreatedAt"), "CreatedAt" DESC
);

-- Toon resultaat
SELECT
    'CLEANUP VOLTOOID' AS status,
    COUNT(*) AS signalen_over
FROM signals;

-- ── Stap 3: Index voor snelle upsert lookups ──
-- De UpsertSignalAsync query zoekt op Symbol + FinalVerdict + CreatedAt
CREATE INDEX IF NOT EXISTS ix_signals_symbol_verdict_created
    ON signals ("Symbol", "FinalVerdict", "CreatedAt" DESC);

-- ── Stap 4: Seed de dedup setting als die nog niet bestaat ──
INSERT INTO algo_settings ("Category", "Key", "Value", "Description", "ValueType", "MinValue", "MaxValue", "UpdatedAt")
VALUES (
    'scan',
    'signal_dedup_minutes',
    '60',
    'Tijdswindow in minuten waarbinnen een bestaand signaal voor hetzelfde symbool+verdict wordt geüpdatet i.p.v. een nieuw signaal aangemaakt. Voorkomt gestapelde duplicaten.',
    'integer',
    5,
    1440,
    NOW()
)
ON CONFLICT ("Category", "Key") DO NOTHING;

COMMIT;

-- Klaar! Controleer met:
-- SELECT symbol, "FinalVerdict", COUNT(*), MIN("CreatedAt"), MAX("CreatedAt")
-- FROM signals
-- GROUP BY symbol, "FinalVerdict"
-- ORDER BY symbol, "FinalVerdict";
