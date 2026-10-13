# PayAlert

**General Payment Reminder and Tracking System.** A Windows desktop app for recording payments you owe or are owed, tracking what's paid and what's left, getting in-app reminders before due dates, and reviewing payment history.

This is an academic prototype. It uses demo data and does not process real money or connect to banks or e-wallets.

- **Stack:** VB.NET, Windows Forms, .NET Framework 4.8
- **Database:** MySQL / MariaDB on XAMPP (`localhost:3306`, database `payalert_db`)
- **Build plan:** see [PLAN.md](PLAN.md)

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

## Project status

| Phase | Status |
|---|---|
| 1. Solution and database | Done: database scripts, connection test and demo data seeder |
| 2. Theme, signature controls and dialogs | Done |
| 3. Login and registration | Done: after logging in, a temporary component preview stands in for the main window (Phase 4) |
| 4–11 | Not started yet |
