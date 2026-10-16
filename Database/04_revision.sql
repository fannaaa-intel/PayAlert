-- =====================================================================
-- PayAlert  -  04_revision.sql
-- Client revision after Phase 11: text size, payment reference and proof,
-- advance payments, and the payment summary report.
--
-- Run after 01_schema.sql, 02_views.sql and 03_seed.sql. Safe to run again.
-- The app also applies these changes by itself when it starts
-- (Data/SchemaUpgrade.vb), so this file is only needed for a manual setup.
-- =====================================================================

USE payalert_db;

-- Settings -> Text size: 0 = Normal, 1 = Large (easier to read).
ALTER TABLE user_settings
    ADD COLUMN IF NOT EXISTS large_text TINYINT(1) NOT NULL DEFAULT 0;
