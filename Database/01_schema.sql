-- =====================================================================
-- PayAlert: General Payment Reminder and Tracking System
-- 01_schema.sql  -  database and tables
--
-- Run in phpMyAdmin (SQL tab) in this order:
--   01_schema.sql  ->  02_views.sql  ->  03_seed.sql
-- Safe to run again: every object uses IF NOT EXISTS.
-- Demo users and payments are created by the app on first start.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS payalert_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE payalert_db;

-- ---------------------------------------------------------------------
-- users: regular users and administrators
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS users (
    user_id        INT           NOT NULL AUTO_INCREMENT,
    full_name      VARCHAR(100)  NOT NULL,
    email          VARCHAR(150)  NOT NULL,
    password_hash  VARCHAR(100)  NOT NULL,
    role           ENUM('user','admin') NOT NULL DEFAULT 'user',
    is_active      TINYINT(1)    NOT NULL DEFAULT 1,
    created_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id),
    UNIQUE KEY uq_users_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- user_settings: one row per user
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS user_settings (
    user_id                INT        NOT NULL,
    notifications_enabled  TINYINT(1) NOT NULL DEFAULT 1,
    default_reminder_days  TINYINT    NOT NULL DEFAULT 3,
    currency               CHAR(3)    NOT NULL DEFAULT 'PHP',
    PRIMARY KEY (user_id),
    CONSTRAINT fk_settings_user FOREIGN KEY (user_id)
        REFERENCES users (user_id) ON DELETE CASCADE,
    CONSTRAINT chk_settings_reminder CHECK (default_reminder_days IN (1, 3, 7))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- categories: lookup list (seeded by 03_seed.sql)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS categories (
    category_id  INT          NOT NULL AUTO_INCREMENT,
    name         VARCHAR(50)  NOT NULL,
    PRIMARY KEY (category_id),
    UNIQUE KEY uq_categories_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- payments: what a user owes (payable) or is owed (receivable).
-- Status is never stored; v_payment_summary calculates it.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS payments (
    payment_id         INT            NOT NULL AUTO_INCREMENT,
    user_id            INT            NOT NULL,
    parent_payment_id  INT            NULL,                -- recurring chain
    name               VARCHAR(100)   NOT NULL,
    direction          ENUM('payable','receivable') NOT NULL DEFAULT 'payable',
    counterparty       VARCHAR(100)   NULL,                -- person or company
    category_id        INT            NOT NULL,
    total_amount       DECIMAL(12,2)  NOT NULL,
    due_date           DATE           NOT NULL,
    reminder_days      TINYINT        NOT NULL DEFAULT 3,  -- 1, 3 or 7
    is_recurring       TINYINT(1)     NOT NULL DEFAULT 0,
    recurrence         ENUM('weekly','monthly') NULL,
    notes              VARCHAR(500)   NULL,
    is_deleted         TINYINT(1)     NOT NULL DEFAULT 0,  -- soft delete
    created_at         DATETIME       DEFAULT CURRENT_TIMESTAMP,
    updated_at         DATETIME       DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (payment_id),
    INDEX idx_payments_user_due (user_id, due_date),
    CONSTRAINT fk_payments_user FOREIGN KEY (user_id)
        REFERENCES users (user_id),
    CONSTRAINT fk_payments_parent FOREIGN KEY (parent_payment_id)
        REFERENCES payments (payment_id),
    CONSTRAINT fk_payments_category FOREIGN KEY (category_id)
        REFERENCES categories (category_id),
    CONSTRAINT chk_payments_total CHECK (total_amount > 0),
    CONSTRAINT chk_payments_reminder CHECK (reminder_days IN (1, 3, 7)),
    CONSTRAINT chk_payments_recurrence CHECK (
        (is_recurring = 0 AND recurrence IS NULL) OR
        (is_recurring = 1 AND recurrence IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- payment_transactions: each full or partial payment (Payment History)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS payment_transactions (
    txn_id      INT            NOT NULL AUTO_INCREMENT,
    payment_id  INT            NOT NULL,
    amount      DECIMAL(12,2)  NOT NULL,
    paid_on     DATE           NOT NULL,
    method      VARCHAR(50)    NULL,       -- Cash, E-wallet, Bank Transfer, Card, Other (free text)
    note        VARCHAR(255)   NULL,
    created_at  DATETIME       DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (txn_id),
    INDEX idx_txn_payment (payment_id),
    CONSTRAINT fk_txn_payment FOREIGN KEY (payment_id)
        REFERENCES payments (payment_id),
    CONSTRAINT chk_txn_amount CHECK (amount > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- notifications: in-app reminders; at most one of each type per day
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS notifications (
    notif_id    INT           NOT NULL AUTO_INCREMENT,
    user_id     INT           NOT NULL,
    payment_id  INT           NOT NULL,
    type        ENUM('due_soon','due_tomorrow','due_today','overdue') NOT NULL,
    message     VARCHAR(255)  NOT NULL,
    notif_date  DATE          NOT NULL,
    is_read     TINYINT(1)    NOT NULL DEFAULT 0,
    created_at  DATETIME      DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (notif_id),
    UNIQUE KEY uq_notif_once_per_day (payment_id, type, notif_date),
    INDEX idx_notif_user_read (user_id, is_read),
    CONSTRAINT fk_notif_user FOREIGN KEY (user_id)
        REFERENCES users (user_id),
    CONSTRAINT fk_notif_payment FOREIGN KEY (payment_id)
        REFERENCES payments (payment_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
