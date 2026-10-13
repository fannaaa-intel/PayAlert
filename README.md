# PayAlert

**General Payment Reminder and Tracking System.** A Windows desktop app for recording payments you owe or are owed, tracking what's paid and what's left, getting in-app reminders before due dates, and reviewing payment history.

This is an academic prototype. It uses demo data and does not process real money or connect to banks or e-wallets.

- **Stack:** VB.NET, Windows Forms, .NET Framework 4.8
- **Database:** MySQL / MariaDB on XAMPP (`localhost:3306`, database `payalert_db`)
- **Build plan:** see [PLAN.md](PLAN.md)
- **Progress:** [docs/WORK_ORDER.md](docs/WORK_ORDER.md) (status and test checklists) and [docs/HANDOFF.md](docs/HANDOFF.md) (developer notes)

## Requirements

| Tool | Notes |
|---|---|
| Windows 10 or 11 | |
| .NET Framework 4.8 Developer Pack | Needed to build for `net48` |
| .NET SDK 8 (or newer) | Provides `dotnet build` |
| XAMPP | Uses its MySQL (MariaDB) and phpMyAdmin |
| Visual Studio 2022 (optional) | Open `PayAlert.sln` |

## 1. Start the database server

1. Open the **XAMPP Control Panel** and click **Start** next to **MySQL**.
2. If MySQL won't start or stops right away, another MySQL server is probably using port 3306 (for example, a separate *MySQL80* Windows service). Open **Services** (`services.msc`), stop that service, then start MySQL in XAMPP again.

## 2. Create the database (phpMyAdmin)

1. Click **Admin** next to MySQL in XAMPP, or open <http://localhost/phpmyadmin>.
2. Open the **SQL** tab. You don't need to select or create a database first.
3. Run the three files in the `Database/` folder **in this order**. For each one, paste its contents into the box (or use the **Import** tab and choose the file), then click **Go**:
   1. `Database/01_schema.sql`: creates `payalert_db` and its six tables
   2. `Database/02_views.sql`: creates the `v_payment_summary` view
   3. `Database/03_seed.sql`: adds the seven payment categories
4. Refresh the left panel. `payalert_db` should list 6 tables and 1 view.

All three scripts are safe to run again; they won't duplicate anything.

The app creates the demo accounts and payments itself, the first time it starts with an empty `users` table.

## 3. Build and run

```bash
dotnet build
```

Then run `PayAlert\bin\Debug\net48\PayAlert.exe`, or open `PayAlert.sln` in Visual Studio and press **F5**. The app opens on the login window.

The connection string is in `PayAlert/App.config` (`PayAlertDb`). It uses the XAMPP default: user `root` with no password. If your MySQL root user has a password, set `Pwd=` there.

## Demo logins

| Role | Email | Password |
|---|---|---|
| Admin | `admin@payalert.com` | `Admin@123` |
| User (Juan Dela Cruz) | `demo@payalert.com` | `Demo@123` |

The demo user's payments have due dates relative to the day the data was created, so every status appears: Upcoming, Partially Paid, Paid and Overdue.

To recreate the demo data, empty the tables in phpMyAdmin (or drop `payalert_db` and run the three scripts again), then start the app.

## What you can do

| Screen | What it does |
|---|---|
| **Login / Register** | Sign in, or create an account. Problems show next to each field. |
| **Dashboard** | Due in 7 days, Due tomorrow, Overdue and To Collect cards (each opens the matching list), your totals with the balance ring, and the next five unpaid payments. |
| **Payments** | Every payment with status tabs (All, Upcoming, Partial, Paid, Overdue), I Owe / Owed to Me, category and search. Add, view, edit and delete. |
| **Payment Details** | Balance ring, due date, details and the payment's history. **Record Payment** (or **Record Collection**) takes full or partial amounts. A recurring payment creates the next one when it's fully paid. |
| **History** | Every recorded payment and collection with date range, direction and search, plus **Total Paid** and **Total Collected**. |
| **Notifications** | Reminders before, on and after due dates, grouped by day. The sidebar badge shows how many are unread. Reminders are checked when you log in and every 5 minutes. |
| **Settings** | Profile (name and email), Change Password, reminders on/off, default reminder (1, 3 or 7 days), and Log out. |
| **Admin** (admins only) | Totals across PayAlert, and Manage Users: activate or deactivate accounts and reset passwords (a temporary password is shown once). |

**Keyboard:** Enter saves a form or opens the selected row, Esc closes a dialog, Tab moves through each screen top to bottom, and Alt+Left goes back.

## Display scaling

The app is DPI-aware and was checked at the size of a maximized window on a 1920×1080 screen at 150% scaling. Every screen fits without scrollbars; long lists scroll with the mouse wheel and fade at the bottom when there is more.

## Troubleshooting

| Message | What to do |
|---|---|
| "Can't reach the database. Make sure MySQL is running in XAMPP." | Start MySQL in the XAMPP Control Panel. If it won't start, stop the separate *MySQL80* service (see step 1). |
| "The PayAlert database wasn't found…" | Run the three SQL files (step 2). |
| "MySQL refused the login…" | Set the right password in `PayAlert/App.config`. |
| "This account is deactivated." | Ask an administrator to activate it in Admin → Manage Users. |

## Project status

All 11 build phases from [PLAN.md](PLAN.md) are done. See [docs/WORK_ORDER.md](docs/WORK_ORDER.md) for each phase's checklist.
