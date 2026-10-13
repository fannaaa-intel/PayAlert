-- =====================================================================
-- PayAlert  -  02_views.sql
-- v_payment_summary: every list, card and detail screen reads from here.
-- Run after 01_schema.sql. Safe to run again (CREATE OR REPLACE).
--
-- Status rules (checked in this order):
--   Paid            balance = 0
--   Overdue         due_date < today and balance > 0
--   Partially Paid  amount_paid > 0 and balance > 0
--   Upcoming        everything else
-- =====================================================================

USE payalert_db;

CREATE OR REPLACE VIEW v_payment_summary AS
SELECT
    p.payment_id,
    p.user_id,
    p.parent_payment_id,
    p.name,
    p.direction,
    p.counterparty,
    p.category_id,
    c.name                                          AS category_name,
    p.total_amount,
    p.due_date,
    p.reminder_days,
    p.is_recurring,
    p.recurrence,
    p.notes,
    p.is_deleted,
    p.created_at,
    p.updated_at,
    COALESCE(SUM(t.amount), 0)                      AS amount_paid,
    p.total_amount - COALESCE(SUM(t.amount), 0)     AS balance,
    CASE
        WHEN p.total_amount - COALESCE(SUM(t.amount), 0) <= 0 THEN 'Paid'
        WHEN p.due_date < CURDATE()                          THEN 'Overdue'
        WHEN COALESCE(SUM(t.amount), 0) > 0                  THEN 'Partially Paid'
        ELSE 'Upcoming'
    END                                             AS status,
    DATEDIFF(p.due_date, CURDATE())                 AS days_until_due
FROM payments p
JOIN categories c
    ON c.category_id = p.category_id
LEFT JOIN payment_transactions t
    ON t.payment_id = p.payment_id
WHERE p.is_deleted = 0
GROUP BY p.payment_id, c.name;
