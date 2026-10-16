# PayAlert — Build Plan

> **For Claude Code:** This is the source of truth for the project. Read the whole file before writing code. Work **one phase at a time** (Section 9). Stop at the end of each phase, summarize what you built, explain how to test it, and wait for the user to confirm before starting the next phase. If something here is unclear or impossible, ask instead of guessing.

---

## 1. Project summary

**PayAlert: General Payment Reminder and Tracking System**: a desktop app that helps people record payments they owe or are owed, track how much is paid and how much is left, get in-app reminders before due dates, and review payment history.

This is an academic prototype (Prototyping SDLC model). It uses demo data. It does **not** process real money, connect to banks or e-wallets, or send SMS or push notifications.

## Repository

- **GitHub:** https://github.com/fannaaa-intel/PayAlert.git
- **Local folder:** `PayAlert/`, the repository root. This file lives at the root.
- **Setup (Phase 1):**
  - PayAlert must be **its own repository**. If `git rev-parse --show-toplevel` shows a folder above PayAlert (for example the user profile), run `git init -b main` inside PayAlert and set the remote: `git remote add origin https://github.com/fannaaa-intel/PayAlert.git`.
  - **Do not modify, move or delete any parent repository or its `.git` folder.**
  - If the remote already has commits, run `git pull origin main --allow-unrelated-histories` first.
  - Use branch `main`.
- **`.gitignore`:** the standard Visual Studio / .NET ignore list. Must include `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo` and `packages/`.
- **Commits:**
  - Commit at the end of every phase, after the build passes and the user confirms.
  - Message format: `Phase N: <short summary>`, e.g. `Phase 1: solution, database scripts, demo seeder`.
  - **Push only when the user says to.**
- **Never commit** real passwords or personal data. The `App.config` default (`root` with no password) is fine for XAMPP.

## 2. Tech stack (fixed)

| Item | Choice |
|---|---|
| Language | **VB.NET** only (no C#) |
| UI | **Windows Forms** |
| Framework | **.NET Framework 4.8** |
| Project format | SDK-style `.vbproj` with `<TargetFramework>net48</TargetFramework>` and `<UseWindowsForms>true</UseWindowsForms>`, so it builds with `dotnet build` and opens in Visual Studio 2022 |
| Database | **MySQL / MariaDB via XAMPP** (localhost:3306), database `payalert_db` |
| DB driver | NuGet **MySql.Data** (a version that supports net48) |
| Password hashing | NuGet **BCrypt.Net-Next** |
| Config | Connection string in `App.config` (`PayAlertDb`). XAMPP default: user `root`, empty password |

## 3. Key design decisions

1. **One account type** for regular users. Each payment has a **direction**:
   - `payable`: shown as **"I Owe"** (borrower or bill payer)
   - `receivable`: shown as **"Owed to Me"** (lender or record owner)

   This covers both the borrower and lender modules without accounts sharing records.
2. **Admin role** (`role = 'admin'`) adds an Admin section for managing users.
3. **Status is calculated, never typed in.** It comes from the amounts and dates (see the view in §4).
   - **Paid**: balance = 0
   - **Overdue**: due date < today and balance > 0
   - **Partially Paid**: amount paid > 0 and balance > 0
   - **Upcoming**: everything else
4. **No "Pending confirmation" status** (dropped from the manuscript).
5. **Partial payments** are recorded as transactions. The sum of transactions is the amount paid. The transactions table is the Payment History.
6. **Soft delete** for payments (`is_deleted = 1`), so their history is kept.
7. **Reminders are in-app only.** Alerts are created when the user logs in and checked every 5 minutes by a timer while the app is open.
8. **Recurring payments**: when a recurring payment becomes fully Paid, automatically create the next one (due date + 1 week or + 1 month), linked by `parent_payment_id`.

## 4. Database (`Database/` folder)

Create three scripts the user runs in phpMyAdmin, in order:

- `01_schema.sql`: `CREATE DATABASE IF NOT EXISTS payalert_db`, plus all tables (InnoDB, utf8mb4)
- `02_views.sql`: `v_payment_summary`
- `03_seed.sql`: categories only. Demo users and payments are created by the app (see Phase 1).

### Tables

```
users
  user_id INT PK AUTO_INCREMENT
  full_name VARCHAR(100) NOT NULL
  email VARCHAR(150) NOT NULL UNIQUE
  password_hash VARCHAR(100) NOT NULL
  role ENUM('user','admin') NOT NULL DEFAULT 'user'
  is_active TINYINT(1) NOT NULL DEFAULT 1
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP

user_settings
  user_id INT PK, FK -> users ON DELETE CASCADE
  notifications_enabled TINYINT(1) NOT NULL DEFAULT 1
  default_reminder_days TINYINT NOT NULL DEFAULT 3
  currency CHAR(3) NOT NULL DEFAULT 'PHP'

categories
  category_id INT PK AUTO_INCREMENT
  name VARCHAR(50) NOT NULL UNIQUE
  -- seed: Loan, Bills, Buy Now Pay Later, Rent, Subscription, Tuition, Others

payments
  payment_id INT PK AUTO_INCREMENT
  user_id INT NOT NULL FK -> users
  parent_payment_id INT NULL FK -> payments   -- recurring chain
  name VARCHAR(100) NOT NULL
  direction ENUM('payable','receivable') NOT NULL DEFAULT 'payable'
  counterparty VARCHAR(100) NULL              -- person/company
  category_id INT NOT NULL FK -> categories
  total_amount DECIMAL(12,2) NOT NULL CHECK (total_amount > 0)
  due_date DATE NOT NULL
  reminder_days TINYINT NOT NULL DEFAULT 3    -- 1, 3 or 7
  is_recurring TINYINT(1) NOT NULL DEFAULT 0
  recurrence ENUM('weekly','monthly') NULL
  notes VARCHAR(500) NULL
  is_deleted TINYINT(1) NOT NULL DEFAULT 0
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP
  updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
  INDEX (user_id, due_date)

payment_transactions
  txn_id INT PK AUTO_INCREMENT
  payment_id INT NOT NULL FK -> payments
  amount DECIMAL(12,2) NOT NULL CHECK (amount > 0)
  paid_on DATE NOT NULL
  method VARCHAR(50) NULL        -- Cash, GCash, Bank, etc. (free text)
  note VARCHAR(255) NULL
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP

notifications
  notif_id INT PK AUTO_INCREMENT
  user_id INT NOT NULL FK -> users
  payment_id INT NOT NULL FK -> payments
  type ENUM('due_soon','due_tomorrow','due_today','overdue') NOT NULL
  message VARCHAR(255) NOT NULL
  notif_date DATE NOT NULL
  is_read TINYINT(1) NOT NULL DEFAULT 0
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP
  UNIQUE (payment_id, type, notif_date)    -- no duplicate alerts per day
```

### View `v_payment_summary`
For each payment where `is_deleted = 0`, return all payment columns plus:
- `category_name`
- `amount_paid` = `COALESCE(SUM(t.amount), 0)`
- `balance` = `total_amount - amount_paid`
- `status` as `'Paid' | 'Overdue' | 'Partially Paid' | 'Upcoming'`, using the rules in §3
- `days_until_due` = `DATEDIFF(due_date, CURDATE())`

**Every list, card and detail screen reads from this view.**

## 5. Solution structure

```
PayAlert/                    (repo root: github.com/fannaaa-intel/PayAlert)
├─ .gitignore
├─ PLAN.md
├─ README.md                 (setup: XAMPP, run SQL, build, demo logins)
├─ PayAlert.sln
├─ Database/
│   ├─ 01_schema.sql
│   ├─ 02_views.sql
│   └─ 03_seed.sql
└─ PayAlert/
    ├─ PayAlert.vbproj
    ├─ App.config
    ├─ Program.vb / ApplicationEvents (startup → frmLogin)
    ├─ Forms/        frmLogin, frmRegister, frmMain (main window: sidebar + content area)
    ├─ Views/        UserControls hosted inside frmMain:
    │                ucDashboard, ucPayments, ucPaymentDetails, ucPaymentEditor (Add + Edit),
    │                ucHistory, ucNotifications, ucSettings, ucProfile,
    │                ucAdminDashboard, ucAdminUsers
    ├─ Dialogs/      PADialog (Confirm/Info/Success/Error), PAToast, DimOverlay,
    │                dlgRecordPayment, dlgChangePassword (both styled like PADialog)
    ├─ Controls/     SoftCard, DueTag, BalanceRing, StatusPill, SummaryCard,
    │                SidebarButton, PesoTextBox, EmptyState,
    │                PrimaryButton/SecondaryButton/DangerButton
    ├─ Models/       User, Payment, PaymentSummary, PaymentTransaction,
    │                Notification, UserSettings, Category
    ├─ Data/         Db.vb (connection factory + helpers),
    │                UserRepo, PaymentRepo, TransactionRepo, NotificationRepo,
    │                SettingsRepo, CategoryRepo, AdminRepo
    ├─ Services/     Session, AuthService, ReminderService, RecurrenceService,
    │                Navigator, DemoDataSeeder, Format (money/date helpers)
    └─ Theme/        Theme.vb (colors, fonts, spacing, control styling helpers)
```

### Code rules
- Forms and UserControls **never contain SQL**. They call repositories or services.
- **Use parameterized queries only.** Never build SQL by concatenating strings.
- Use `Using` blocks for every connection, command and reader.
- Use `Decimal` for money and `Date` for dates. Never use `Double` for money.
- Show friendly error messages to the user and write the details to `Debug`. Never show raw exceptions.
- **Build UI in code or in `.Designer.vb` files that compile cleanly**, so they also open in the Visual Studio designer.
- `Option Strict On` and `Option Explicit On` in every file.
- **`MessageBox.Show` is forbidden everywhere.** All messages go through the PayAlert dialog system (§6B).
- The project must build with `dotnet build` with zero errors after every phase.

## 6. Visual system (`Theme.vb`)

All colors, fonts and sizes live in `Theme.vb`. No hard-coded colors anywhere else.

### Colors
| Token | Hex | Use |
|---|---|---|
| Brand | `#F7931E` | Primary buttons, active sidebar item, accents |
| BrandDark | `#D9740F` | Primary button hover/pressed |
| BrandSoft | `#FFF1E0` | Active sidebar background, subtle highlights |
| AppBackground | `#F5F6F8` | Content area background |
| Surface | `#FFFFFF` | Cards, panels, sidebar, dialogs |
| Border | `#E5E7EB` | Card borders, dividers, grid lines |
| TextPrimary | `#1F2937` | Main text |
| TextMuted | `#6B7280` | Labels, captions, timestamps |
| Upcoming | `#B58100` text on `#FFF6D6` | Upcoming status |
| Partial | `#2F6FD6` text on `#E3EEFD` | Partially Paid status |
| Paid | `#1E8E4E` text on `#E3F5EA` | Paid status, success banners |
| Overdue | `#D93036` text on `#FDE7E8` | Overdue status, delete/danger |

**Status colors are used only to show status. Red is also the delete/danger color.** Screens are not tinted with different colors; every screen uses AppBackground with white cards.

### Type and spacing
- Font: **Segoe UI**
  - Page title: 18 bold
  - Section heading: 12 semibold
  - Body: 10
  - Caption: 9 (muted)
  - Big numbers: 20 bold
- Spacing scale: 4, 8, 12, 16, 24. Content padding: 24. Card padding: 16.
- Buttons: height 36, flat style, no 3D borders.
  - **Primary**: Brand fill, white text
  - **Secondary**: white with border
  - **Danger**: Overdue red text or fill
- DataGridView styling:
  - Header row: white background, muted bold text
  - Row height: 36
  - Gridlines: horizontal only
  - Selection: full row in BrandSoft
  - No row headers, read-only
- Window: starts at 1280×760, minimum 1100×680, centered. Sidebar is fixed at 220px; the content area scrolls.

### Formatting
- Money: `₱1,500.00` (culture `en-PH`)
- Dates: `Oct 15, 2026`
- Relative time in notifications: "10 min ago", "Yesterday", then the date

## 6A. Design identity: original to this client

This app is built for a specific client and **must not look like a template or like any other product**. Rules:

- **No recognizable patterns:**
  - Don't copy or imitate the look of known apps, brands or design systems: banking or e-wallet apps, Material Design, Bootstrap/AdminLTE dashboards, Windows Settings, Office.
  - Don't use stock "admin template" layouts.
- **No trademarks:**
  - No third-party brand names, logos, product icons or brand colors in the UI, code, sample data or screenshots.
  - **One exception (client revision, §12):** the payment methods **GCash** and **Maya** appear as plain text in the method list, the demo data and the Payment Summary report, because the client asked for them by name. Never their logos or brand colors.
  - The only logo is the client's own PayAlert logo.
- **Icons:** draw them yourself with GDI+ (simple geometric line icons) or use plain Segoe MDL2 Assets glyphs. No icon packs copied from elsewhere.
- **Default WinForms look is not acceptable for visible UI:**
  - no gray 3D buttons, default borders or standard title-bar message boxes
  - every visible control is styled through `Theme.vb`

### PayAlert signature elements (use them consistently)
These give the app its own identity. Implement them as reusable controls:

1. **Due Tag** (`DueTag` control)
   - A small rounded "ticket" shape with a notched left edge.
   - Shows the day number large and the month small, e.g. `15 / OCT`.
   - Its notch is tinted with the status color.
   - Used in the payment lists, the dashboard upcoming list and the details header.
2. **Balance Ring** (`BalanceRing` control)
   - A thick circular progress ring (GDI+, anti-aliased) showing paid ÷ total.
   - The center shows the remaining balance, with "left of ₱Total" under it.
   - Used in Payment Details and on the dashboard total card.
3. **Status edge**
   - Payment rows and cards have a 4px rounded colored strip on the left edge in the status color.
   - It is the main status signal, together with the StatusPill text.
4. **Soft cards**
   - 12px corner radius, 1px Border line and a very soft shadow (2–3px, low alpha), drawn with GDI+ on a `Panel` subclass (`SoftCard`).
5. **Header band**
   - Each page title sits on a thin band with a short Brand-colored underline accent (40px × 3px) under the title.
6. **Sidebar**
   - A white surface with a rounded BrandSoft "capsule" behind the active item and a small Brand dot at its left.
   - No full-width colored blocks.

All six must look polished: anti-aliased drawing, correct DPI scaling (`AutoScaleMode.Dpi`), and no flicker (double-buffered controls).

## 6B. PayAlert dialog system (replaces all message boxes)

Every message the user sees uses custom, designed dialogs. They are animated and use the same identity as the rest of the app. Never use a plain or static message box.

**`PADialog`** (one borderless form class, created through a static helper)
```
PADialog.Confirm(owner, title, message, confirmText, cancelText, tone) As Boolean
PADialog.Info(owner, title, message, tone)
PADialog.Success(owner, title, detailsLines, primaryText, secondaryText) As DialogChoice
PADialog.Error(owner, title, friendlyMessage)
```
- **Tones:** `Neutral`, `Success` (Paid green), `Warning` (Upcoming amber), `Danger` (Overdue red), `Brand` (orange).
- **Layout** (about 420px wide):
  - 14px rounded corners (window `Region` from a GraphicsPath) and a soft drop shadow
  - a 4px tone-colored strip across the top
  - a **circular icon badge**, 48px, drawn with GDI+ in the tone color on its soft background:
    - check for Success
    - "!" for Warning
    - trash or "×" for Danger
    - "i" for Info
  - a bold 14pt title and a muted 10pt message that wraps
  - buttons right-aligned, using the Theme button styles
- **Behavior:**
  - **Dim overlay:** while the dialog is open, a semi-transparent dark layer covers the main window.
  - **Motion:** 150ms fade-in plus a slight upward slide; fade-out on close. Use a `Timer` on Opacity and Top. No external libraries.
  - **Keys:**
    - Enter = primary action
    - Esc = cancel
    - In **Danger** dialogs the focus starts on **Cancel**, so Enter can't delete by accident.
  - Centered on the owner window. Can be dragged by the title area.
- **Copy:** friendly, specific messages that name the item, e.g. "Delete "Electricity Bill"? This can't be undone." Never show error codes or exception text.

**`PAToast`** (non-blocking feedback)
- A small rounded card that slides in at the bottom-right of the main window.
- It has a tone-colored status edge, an icon, a one-line message and an optional "View" link.
- It closes automatically after 3 seconds; hovering pauses the timer.
- Use it for light confirmations: "Settings saved", "Marked all as read", "Payment updated".

**When to use which**
- **Confirm (Danger):** delete payment, deactivate user, log out (Neutral)
- **Success dialog:** payment added, payment recorded (with View Payment / Back to Dashboard)
- **Toast:** small saves and updates
- **Error:** database not reachable ("Can't reach the database. Make sure MySQL is running in XAMPP."), unexpected failures
- **Form validation:** never in a dialog. Show it inline next to the field.

## 7. Navigation and screens

### Navigation map
```
frmLogin ──► frmRegister ──► (back to Login, email prefilled)
   │
   └─► frmMain
        Sidebar: Dashboard · Payments · History · Notifications (unread badge)
                 · Settings · Admin (admins only) · Log out
        Payments ─► Payment Details ─► Edit Payment / Record Payment / Delete
        + Add Payment (Dashboard or Payments) ─► success ─► View Payment | Back to Dashboard
        Settings ─► Profile, Change Password
```

- **`Navigator`** swaps UserControls in frmMain's content area and keeps a back stack.
- Inner screens have **one "← Back" button** at the top-left. There is no extra "Back Dashboard" link.
- The active sidebar item uses the §6A style: a rounded BrandSoft capsule, a small Brand dot at its left, and bold text.
- Pressing **Esc** closes a dialog. Pressing **Enter** submits a form.
- Log out asks for confirmation, clears `Session`, and returns to frmLogin.

### Screens

**frmLogin**
- Two-panel layout: the PayAlert logo with "Never Miss a Payment" on the left, and the form on the right.
- Fields: Email and Password (with a show/hide toggle). Includes a LOGIN button and a "Create an account" link.
- Errors show inline in red: "Wrong email or password." / "This account is deactivated."
- Enter key logs in.

**frmRegister**
- Fields: Full Name, Email, Password, Confirm Password.
- Validation (shown next to each field):
  - all fields required
  - valid email format
  - email not already used
  - password at least 8 characters
  - Confirm Password matches
- Also creates the `user_settings` row.
- After success, go back to Login with the email filled in and the message "Account created. Please log in."

**ucDashboard**
- Greeting: "Good morning/afternoon/evening, {first name}!" plus today's date.
- Four SummaryCards (click one to open Payments filtered to it):
  - Due in 7 days (count)
  - Due Tomorrow (count)
  - Overdue (count, red)
  - To Collect (total balance of receivables)
- One line: **Total I Owe: ₱X · Total Owed to Me: ₱Y**
- "Upcoming Payments": the next 5 unpaid payments by due date.
  - Each row shows Name, Amount/Balance, Due date and a StatusPill.
  - Clicking a row opens its details.
- A **+ Add Payment** primary button sits at the top-right.
- When the user has no payments, show an empty state: "No payments yet. Add your first one."

**ucPayments** (the "All Payments" frames, combined into one screen)
- Header: "Payments: View and manage your reminders", with **+ Add Payment** on the right.
- Status tabs (pill buttons): **All · Upcoming · Partial · Paid · Overdue**
- Direction filter: All / I Owe / Owed to Me. Category filter dropdown.
- Live search box: filters by name or counterparty as the user types.
- Grid columns: Name, Category, Person, Total, Balance, Due, Status. Status is drawn as a colored pill.
- Clicking a column header sorts by it. Double-click or Enter opens Payment Details.
- Each tab or filter has its own empty state.

**ucPaymentEditor** (Add and Edit in one control; `Mode` = Add / Edit)
- Title: "Add Payment" or "Edit Payment". Edit mode comes **pre-filled**.
- Fields:
  - Direction (I Owe / Owed to Me segmented toggle)
  - Payment Name*
  - Person/Company
  - Amount* (PesoTextBox, > 0)
  - Due Date* (DateTimePicker)
  - Category* (dropdown from `categories`)
  - Remind me (1 / 3 / 7 days before; defaults to the user's default reminder setting)
  - Recurring (No / Yes, and Yes shows Weekly / Monthly)
  - Notes (multiline, max 500)
- Edit mode does not allow lowering Amount below what's already paid. Show an inline error if the user tries.
- Buttons: Cancel (secondary) and **Save Payment** (primary, disabled until required fields are valid).
- After saving: dlgSuccess with "✓ Payment added successfully!" (or "updated"), the name, amount and due date, and **View Payment** / **Back to Dashboard**.

**ucPaymentDetails** (one screen, loaded by `payment_id`)
- Header: name, StatusPill, direction label, and a large **Balance ₱X** with "of ₱Total".
- Info rows: Amount Paid, Due Date (with "in 3 days" / "2 days overdue"), Category, Person, Reminder, Recurring, Notes.
- A progress bar for paid ÷ total.
- "Payment History" mini-grid for this payment: Date, Amount, Method, Note.
- Actions:
  - **Record Payment** (primary; hidden when Paid)
  - Edit (secondary)
  - Delete (danger text)

**dlgRecordPayment** (replaces "Mark as Paid")
- Title: "Record Payment" or "Record Collection" for receivables.
- Fields:
  - Amount (defaults to the full balance; must be > 0 and ≤ balance)
  - Date paid (defaults to today)
  - Method (editable dropdown: Cash, E-wallet, Bank Transfer, Card, Other)
  - Note
- After saving: if the balance is now 0, the payment shows as Paid. If it is recurring, RecurrenceService creates the next one. Then show dlgSuccess with "✓ Payment recorded".

**Confirmations** (`PADialog.Confirm`, see §6B; dlgConfirm and dlgSuccess below mean PADialog)
- Shows a title, a message, and confirm/cancel text. A `IsDanger` flag makes the confirm button red.
- Used for:
  - **Delete:** "Delete Payment?" / "Are you sure you want to delete this payment? This can't be undone." / "Yes, Delete"
  - **Log out**

**ucHistory** (manuscript Figure 7)
- Grid of all transactions for the current user: Date, Payment, Direction, Amount, Method, Note.
- Filters: date range (From/To), direction, and payment search.
- Footer totals: **Total Paid** (payables) and **Total Collected** (receivables) for the selected range.

**ucNotifications**
- A list of cards, newest first. Each card has:
  - a colored dot (Overdue red, Due Today/Tomorrow orange, Due Soon yellow)
  - a bold title: "Due Tomorrow" / "Due in 3 Days" / "Overdue"
  - a message, e.g. "BNPL Installment payment of ₱1,500.00 is due tomorrow."
  - a relative time
- Unread cards have a BrandSoft background. Clicking a card marks it read and opens the payment.
- **Mark all as read** button. The sidebar badge shows the unread count.

**ReminderService**
- Runs at login and every 5 minutes (WinForms Timer) when `notifications_enabled = 1`.
- For each unpaid payment, create a notification when one of these applies:
  - `days_until_due <= reminder_days` (due_soon)
  - `= 1` (due_tomorrow)
  - `= 0` (due_today)
  - `< 0` (overdue, at most once per day)
- Duplicates are prevented by the unique key.

**ucSettings**
- Sections are cards:
  - **Account**: Profile → ucProfile; Change Password → dlgChangePassword
  - **Notifications**: Enable notifications (toggle); Default reminder (1/3/7 days)
  - **Preferences**: Currency PHP (₱), shown read-only
  - **Log out** button (danger, with confirm)

**ucProfile**
- Edit Full Name and Email (email must stay unique). Save button. Back.

**dlgChangePassword**
- Fields: Current password, New password (at least 8 characters), Confirm new password.

**ucAdminDashboard** (admins only)
- Cards: Total Users, Active Users, Total Payments, Overdue Payments.

**ucAdminUsers** (admins only)
- Grid: Name, Email, Role, Status, Created.
- Actions:
  - Activate/Deactivate (an admin can't deactivate themselves)
  - Reset Password, which sets a temporary password and shows it once

### Shared controls
- **StatusPill**: a rounded label in status colors.
- **SummaryCard**: white card with a caption, a big number and an optional colored accent bar. Clickable.
- **SidebarButton**: an icon (a GDI+ line icon or Segoe MDL2 Assets glyph, never emoji) plus text, with the §6A capsule-and-dot active state.
- **PesoTextBox**: shows a "₱" prefix, accepts digits and one decimal point, and formats on blur.
- **EmptyState**: a centered icon, message and optional action button.

## 8. Demo data (`DemoDataSeeder`)

On startup, if the `users` table is empty, create:
- **Admin**: `admin@payalert.com` / `Admin@123`
- **Demo user**: `demo@payalert.com` / `Demo@123`, named "Juan Dela Cruz"
- Payments for the demo user, with dates relative to today so the statuses always look right:

| Name | Direction | Category | Person | Total | Due | Paid so far |
|---|---|---|---|---|---|---|
| BNPL Installment | I Owe | Buy Now Pay Later | Online Store | 1,500 | today + 1 | 0 |
| Home Internet | I Owe | Bills | Internet Provider | 1,699 | today + 3 | 0 |
| Electricity Bill | I Owe | Bills | Power Utility | 2,300 | today + 10 | 0 |
| Cash Loan | I Owe | Loan | Lending App | 1,000 | today − 4 | 0 (Overdue) |
| Apartment Rent | I Owe | Rent | Landlord | 5,000 | today + 6 | 2,000 (Partial) |
| Water Bill | I Owe | Bills | Water Utility | 500 | today − 2 | 500 (Paid) |
| Streaming Plan | I Owe | Subscription | Streaming Service | 549 | today + 12 | 0 (recurring monthly) |
| Loan to Maria | Owed to Me | Loan | Maria Santos | 3,000 | today + 5 | 1,000 (Partial) |
| Loan to Pedro | Owed to Me | Loan | Pedro Reyes | 800 | today − 1 | 0 (Overdue) |

## 9. Build phases (one at a time, and stop after each)

| # | Phase | Done when |
|---|---|---|
| 1 | **Solution and database**: git setup and `.gitignore` (see Repository), solution, project, NuGet packages, App.config, `Database/*.sql`, `Db.vb` with a connection test, `DemoDataSeeder`, README setup steps | Builds. SQL runs in phpMyAdmin. App starts, connects, and seeds demo data. |
| 2 | **Theme, signature controls and dialogs**: Theme.vb, buttons, StatusPill, SummaryCard, SidebarButton, PesoTextBox, EmptyState, **SoftCard, DueTag, BalanceRing, status edge, PADialog (all tones, overlay, animation), PAToast** | A temporary preview form shows every control and every dialog tone. Everything is anti-aliased, has no flicker, and nothing uses the default WinForms look. |
| 3 | **Auth**: frmLogin, frmRegister, AuthService (BCrypt), Session | Register, log in, wrong-password message, and deactivated-account block all work. |
| 4 | **Main window**: frmMain, sidebar, Navigator, back stack, log out, admin-only menu | Clicking each menu item switches screens; placeholders are fine. |
| 5 | **Payments**: `v_payment_summary`, PaymentRepo, ucPayments (tabs, filters, search, sort), ucPaymentEditor (Add/Edit), ucPaymentDetails, Delete | Full add/view/edit/delete works, and status pills are correct. |
| 6 | **Transactions**: dlgRecordPayment, TransactionRepo, details history grid, ucHistory, RecurrenceService | Partial and full payments update balance and status. Recurring payments create the next one. History totals are correct. |
| 7 | **Dashboard**: ucDashboard cards, totals, upcoming list, card click filters | Numbers match the Payments screen. |
| 8 | **Notifications**: ReminderService, timer, ucNotifications, sidebar badge, mark read | Demo data produces the expected alerts, with no duplicates. |
| 9 | **Settings and profile**: ucSettings, ucProfile, dlgChangePassword, default reminder used by the editor | All settings save and reload. |
| 10 | **Admin**: ucAdminDashboard, ucAdminUsers | The admin can deactivate or reset a user. A regular user can't see the Admin menu. |
| 11 | **Polish**: empty states, tab order, Enter/Esc everywhere, validation messages, consistent spacing, README final | Full walkthrough with no crashes; every screen matches §6, §6A and §6B. A search for `MessageBox` finds nothing. No third-party brand names appear anywhere. |

## 10. Manuscript figure mapping (for screenshots)

| Figure | Screen |
|---|---|
| 1. Login Screen | frmLogin |
| 2. General User Dashboard | ucDashboard |
| 3. Payment / Debt List | ucPayments (All tab) |
| 4. Payment Details, Balance and Due Date | ucPaymentDetails |
| 5. Payment Reminder / Alert | ucNotifications |
| 6. Payment Status | ucPayments (status tabs) |
| 7. Payment History | ucHistory |
| 8. Lender / Record Owner Dashboard | ucDashboard / ucPayments filtered "Owed to Me" |
| 9. Payment Record Management | ucPaymentEditor + dlgRecordPayment |
| 10. Administrator Dashboard | ucAdminDashboard |

## 11. Out of scope
- Real payments or transaction verification
- Bank or e-wallet connections
- SMS, email or OS push notifications
- Biometric login
- Credit scoring or lending decisions
- Sharing records between accounts

## 12. Client revision (2026)

The client asked for four improvements after Phase 11 (original request in Taglish):

1. **User-friendly interface**, with older users in mind: simple, clear and easy screens.
2. **Advance payment**, with the remaining balance updating automatically.
3. **Payment reference**: reference number, the name of who paid, and proof of payment, to identify and verify each transaction.
4. **Payment summary report**: total payments by payment method (GCash, Maya, bank transfer, cash).

### Database (`Database/04_revision.sql`, also applied by the app at startup through `Data/SchemaUpgrade.vb`)
```
user_settings
  + large_text TINYINT(1) NOT NULL DEFAULT 0        -- Text size: Normal (0) or Large (1)

payment_proofs                                       -- new table
  proof_id INT PK AUTO_INCREMENT
  user_id INT NOT NULL FK -> users
  file_name VARCHAR(255) NOT NULL
  content_type VARCHAR(50) NOT NULL                  -- image/jpeg
  data MEDIUMBLOB NOT NULL                           -- resized to stay under 600 KB (XAMPP max_allowed_packet is 1 MB)
  created_at DATETIME

payment_transactions
  + reference_no VARCHAR(50) NULL
  + payer_name VARCHAR(100) NULL
  + proof_id INT NULL FK -> payment_proofs           -- shared by the records of one advance
  + is_advance TINYINT(1) NOT NULL DEFAULT 0         -- carried over from an earlier payment of the series
```

### Rules
- **Text size** (Settings → Preferences): Normal or Large (all fonts × 1.2), saved per user. Changing it reopens the main window on Settings in the new size. Login and Register stay Normal. Every screen must fit at 1280×688 in both sizes.
- **One click** opens a row in Payments, History and a payment's history (no double-click).
- **Record Payment** adds: Reference no. (optional; warns, without blocking, when the same number was already recorded, ignoring spaces, dashes and case), Paid by / Received from (defaults to the user for I Owe, the other person for Owed to Me), and Proof of payment (a JPG/PNG/BMP/GIF picture; turned upright, flattened on white, at most 1600 px and 600 KB). Methods: Cash, GCash, Maya, Bank Transfer, Card, Other (typed). Old "E-wallet" records still show.
- **Payment record** window (click a record): amount, date, method, reference, payer, note, Advance tag, and the proof picture with "View full size" (opens in the Windows picture viewer). From History it also offers "Open Payment".
- **Advance payment**: only for repeating payments. The amount may be up to the balance plus 12 periods. The balance is paid first; the rest goes to the next payments of the series in order (created as each becomes paid), as records marked `is_advance` with the same method, reference, payer and proof. One database transaction; if a later payment of the series was deleted, nothing is saved and the user is told why. One-time payments still can't take more than their balance.
- **Payment Summary** (sidebar **Summary**): date range (This month, Last month, This year, All time, or From/To) and direction. Totals strip (Total, Paid, Collected) and one bar per method, biggest first, "Not set" last; methods are grouped ignoring case and spaces. **Save as CSV** (UTF-8 with BOM, for Excel).

### Phases
| # | Phase | Done when |
|---|---|---|
| 12 | **Easier to use**: Text size setting (Normal/Large), one-click open, clearer hints, layouts checked in Large | Every screen fits and nothing important is cut off in both sizes; the setting saves and reloads. |
| 13 | **Payment reference**: reference no., payer, proof picture, record details window, search by reference | A recorded payment shows its reference, payer and proof; a reused reference warns; a bad file is refused inline. |
| 14 | **Advance payment**: pay ahead on repeating payments | Overpaying a repeating payment fills the next ones in order and their balances update; one-time payments refuse it. |
| 15 | **Payment Summary report** by method, with CSV | Totals per method match History for the same range; CSV opens in Excel. |
