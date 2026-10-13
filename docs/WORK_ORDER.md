# PayAlert: Work Order

The 11 build phases from [PLAN.md](../PLAN.md) §9, with status and a test checklist for each.
Rules: one phase at a time. After each phase, build, test, then commit as `Phase N: <summary>` once the user confirms. **Push only when the user says "push".**

_Last updated: 2026-10-14 (session 2: Phases 6 and 7 done)_

| # | Work order | Status | Commit |
|---|---|---|---|
| 1 | Solution and database | ✅ Done | `b8a09ba` |
| 2 | Theme, signature controls and dialogs | ✅ Done | `71a6f4a` |
| 3 | Auth (login, register) | ✅ Done | `416eca9` |
| 4 | Main window and navigation | ✅ Done | `9b649a6` |
| 5 | Payments (list, add/edit, details, delete) | ✅ Done | `77cbf80` |
| 6 | Transactions, history and recurrence | ✅ Done | `005b9a7` |
| 7 | Dashboard | ✅ Done | `e4ab477` |
| 8 | Notifications | ⏳ **Next** | |
| 9 | Settings and profile | ⏳ | |
| 10 | Admin | ⏳ | |
| 11 | Polish | ⏳ | |

Nothing has been pushed to GitHub yet.

---

## 1. Solution and database ✅
Done when: builds; SQL runs in phpMyAdmin; app starts, connects and seeds demo data.
- [x] `Database/01_schema.sql`, `02_views.sql`, `03_seed.sql` run in order, and can be run again safely
- [x] First start creates `admin@payalert.com` / `Admin@123` and `demo@payalert.com` / `Demo@123` with 9 demo payments
- [x] Every demo payment shows the status from PLAN.md §8
- [x] Friendly message when MySQL isn't running

## 2. Theme, signature controls and dialogs ✅
Done when: every control and dialog tone looks right, anti-aliased, no flicker, no default WinForms look.
- [x] Theme.vb holds every color, font and size (only exception: `CardBorder #DADDE3`, added so cards stand off the page)
- [x] SoftCard, DueTag, BalanceRing, status edge, StatusPill, SummaryCard, SidebarButton, PesoTextBox, EmptyState, buttons
- [x] PADialog (Confirm, Info, Success, Error) over a dim overlay with fade/slide; Danger focuses Cancel
- [x] PAToast bottom-right, 3 s, hover pauses
- [x] Own GDI+ line icons (Theme/Icons.vb), no icon packs

## 3. Auth ✅
Done when: register, log in, wrong-password message and deactivated-account block all work.
- [x] Borderless login/register windows with the ticket illustration
- [x] "Wrong email or password." for both a wrong password and an unknown email
- [x] "This account is deactivated." (only after the password is correct)
- [x] Register validates every field inline and creates the `user_settings` row
- [x] Back to Login with the email filled in and "Account created. Please log in."

## 4. Main window ✅
Done when: each menu item switches screens (placeholders are fine).
- [x] Sidebar: Dashboard, Payments, History, Notifications (badge), Settings, Admin (admins only), Log out
- [x] Navigator with a back stack; inner screens get one "Back" button; Alt+Left goes back
- [x] Log out asks for confirmation and returns to Login with the email filled in

## 5. Payments ✅
Done when: full add/view/edit/delete works and the status pills are correct.
- [x] Tabs with counts (All · Upcoming · Partial · Paid · Overdue), I Owe / Owed to Me, category, live search
- [x] Sort by clicking a column header; double-click or Enter opens details
- [x] Its own empty state for each tab and filter
- [x] Add/Edit: inline validation; Save disabled until valid; amount can't go below what's already paid
- [x] Success dialog with View Payment / Back to Dashboard
- [x] Details: balance ring, due tag, status, info rows; Delete with red confirm (soft delete)

## 6. Transactions, history and recurrence ✅
Done when: partial and full payments update the balance and status; recurring payments create the next one; history totals are correct.
- [x] `TransactionRepo`: add a transaction, list one payment's transactions, list all of a user's transactions with filters
- [x] `dlgRecordPayment` (styled like PADialog): Amount (defaults to the full balance; > 0 and ≤ balance), Date paid (defaults to today), Method (editable dropdown: Cash, E-wallet, Bank Transfer, Card, Other), Note. Title "Record Collection" for receivables.
- [x] Wire the Record Payment button on ucPaymentDetails (it currently shows a "comes next" message)
- [x] Payment History list on ucPaymentDetails (replaces the placeholder card): Date, Amount, Method, Note
- [x] `RecurrenceService`: when a recurring payment becomes fully Paid, create the next one (due + 1 week or + 1 month) with `parent_payment_id`
- [x] Success dialog "Payment recorded"
- [x] `ucHistory`: all transactions (Date, Payment, Direction, Amount, Method, Note); filters From/To, direction and search; footer **Total Paid** (payables) and **Total Collected** (receivables)
- [x] Test: record ₱1,000 on Apartment Rent (status stays Partially Paid), then the rest (status Paid, Record button hidden); record the Streaming Plan in full and check that next month's appears; check that the History totals add up

## 7. Dashboard ✅
Done when: numbers match the Payments screen.
- [x] Greeting with first name and date (already in the placeholder)
- [x] Four SummaryCards: Due in 7 days, Due Tomorrow, Overdue (red), To Collect; clicking opens Payments filtered (`New ucPayments(status, direction)` already accepts filters)
- [x] "Total I Owe: ₱X · Total Owed to Me: ₱Y"; BalanceRing on the totals card (§6A)
- [x] Upcoming Payments: next 5 unpaid by due date, with DueTag and StatusPill; click opens details
- [x] Empty state "No payments yet. Add your first one."

## 8. Notifications ⏳ NEXT
Done when: demo data produces the expected alerts, with no duplicates.
- [ ] `NotificationRepo` + `ReminderService`: run at login and every 5 minutes when `notifications_enabled = 1`; due_soon / due_tomorrow / due_today / overdue; duplicates blocked by the unique key (use INSERT IGNORE)
- [ ] ucNotifications: cards newest first, colored dot, title, message, relative time; unread cards BrandSoft; click marks read and opens the payment; Mark all as read (toast)
- [ ] Sidebar badge: `frmMain.SetUnreadCount(n)` already exists

## 9. Settings and profile ⏳
Done when: all settings save and reload.
- [ ] `SettingsRepo`; ucSettings cards: Account (Profile, Change Password), Notifications (toggle, default reminder 1/3/7), Preferences (Currency PHP, read-only), Log out
- [ ] ucProfile: edit full name and email (email stays unique); refresh the sidebar user block
- [ ] dlgChangePassword: current, new (8+ characters), confirm
- [ ] Payment editor uses the user's default reminder (it currently starts at 3 days)

## 10. Admin ⏳
Done when: the admin can deactivate or reset a user, and a regular user can't see the Admin menu.
- [ ] `AdminRepo`; ucAdminDashboard cards: Total Users, Active Users, Total Payments, Overdue Payments
- [ ] ucAdminUsers grid: Name, Email, Role, Status, Created; Activate/Deactivate (not yourself) with a confirm; Reset Password shows a temporary password once

## 11. Polish ⏳
Done when: full walkthrough with no crashes; every screen matches §6, §6A and §6B; no `MessageBox`; no brand names.
- [ ] Empty states, tab order, Enter/Esc everywhere, validation messages, consistent spacing
- [ ] Check at 150% display scaling (the client PC); fix anything cut off
- [ ] Final README; exe icon file; replace the placeholder logo if the client sends theirs
