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

-- Proof of payment: a photo or screenshot of the receipt, stored once and shared
-- by the records it belongs to (an advance payment can be split over several).
CREATE TABLE IF NOT EXISTS payment_proofs (
    proof_id      INT           NOT NULL AUTO_INCREMENT,
    user_id       INT           NOT NULL,
    file_name     VARCHAR(255)  NOT NULL,   -- the name of the file the user picked
    content_type  VARCHAR(50)   NOT NULL,   -- image/jpeg (resized to stay well under 1 MB)
    data          MEDIUMBLOB    NOT NULL,
    created_at    DATETIME      DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (proof_id),
    CONSTRAINT fk_proof_user FOREIGN KEY (user_id)
        REFERENCES users (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Payment reference: reference number, who paid, and the proof.
ALTER TABLE payment_transactions
    ADD COLUMN IF NOT EXISTS reference_no VARCHAR(50)  NULL AFTER method,
    ADD COLUMN IF NOT EXISTS payer_name   VARCHAR(100) NULL AFTER reference_no,
    ADD COLUMN IF NOT EXISTS proof_id     INT          NULL AFTER payer_name;

-- Advance payment: 1 = money paid ahead on an earlier payment of a repeating
-- chain (more than its balance) and carried over to this later one.
ALTER TABLE payment_transactions
    ADD COLUMN IF NOT EXISTS is_advance TINYINT(1) NOT NULL DEFAULT 0 AFTER proof_id;

ALTER TABLE payment_transactions
    ADD CONSTRAINT fk_txn_proof FOREIGN KEY IF NOT EXISTS (proof_id)
        REFERENCES payment_proofs (proof_id);
