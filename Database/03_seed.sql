-- =====================================================================
-- PayAlert  -  03_seed.sql
-- Categories only. Demo users and payments are created by the app the
-- first time it starts with an empty users table.
-- Safe to run again (INSERT IGNORE skips names that already exist).
-- =====================================================================

USE payalert_db;

INSERT IGNORE INTO categories (name) VALUES
    ('Loan'),
    ('Bills'),
    ('Buy Now Pay Later'),
    ('Rent'),
    ('Subscription'),
    ('Tuition'),
    ('Others');
