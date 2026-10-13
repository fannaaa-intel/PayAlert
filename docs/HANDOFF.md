# PayAlert: Session Hand-off

_Updated in session 2 (2026-10-14), after Phase 7. Read this together with [PLAN.md](../PLAN.md) (source of truth) and [WORK_ORDER.md](WORK_ORDER.md) (status and checklists)._

## Where we stopped
- Phases 1–7 are done and committed on `main` (Phase 6 = `005b9a7`, Phase 7 = `e4ab477` "Phase 7: dashboard").
- **Nothing is pushed.** The remote `origin` is https://github.com/fannaaa-intel/PayAlert.git. Push only when the user says "push".
- **Next: Phase 8** (Notifications). The checklist is in WORK_ORDER.md.

## How the user works
- One phase at a time. Build, test, show the result, and wait for the user's OK before committing or starting the next phase.
- They sometimes say "read only, wait for my go". Do nothing until they say go.
- They like to see the app running: launch `PayAlert\bin\Debug\net48\PayAlert.exe` for them at the end of a phase.
- **Don't take over their screen for testing.** They use the PC while we work. Render screens offscreen instead (see *Testing* below).
- They care a lot about the look. Every visible control must be custom-styled (§6, §6A, §6B), with no repeated status colors and clearly labelled layouts. When they flag something as confusing, redesign it rather than patching it.

## Environment (this PC)
- .NET SDK 8.0.302 (`dotnet build`), .NET Framework 4.8 Developer Pack, Git 2.53, XAMPP MariaDB 10.4.32.
- A separate **MySQL80 Windows service** also wants port 3306. When it's running, XAMPP's MySQL can't start. The user stops it in `services.msc`.
- The PayAlert repo is nested inside the user-profile git repo (`C:\Users\DELL`). **Never touch the parent repo.**
- The display is at 100% here; the client PC is at 150%.

## Architecture
```
Program.vb           AppFlow (ApplicationContext): frmLogin <-> frmRegister -> frmMain; Log out -> frmLogin
Forms/               PAWindow (borderless base, rounded + layered shadow) -> AuthWindow -> frmLogin, frmRegister
                     frmMain (sidebar + content host driven by Navigator)
Views/               PAView base (header band, Body panel, Back button, AddHeaderAction/SetHeaderActionVisible)
                     ucDashboard, ucPayments (+ DueWindow chip filter; InWindow shared with the dashboard), ucPaymentEditor, ucPaymentDetails, ucHistory = real; the others are placeholders (AddPlaceholder)
Controls/            PAControl base; buttons; SoftCard; DueTag; BalanceRing; StatusPill; SummaryCard; SidebarButton;
                     PATextBox (icon, password eye, multiline); PesoTextBox; PASelect + SelectList; PADatePicker + CalendarPanel;
                     PopupHost (ToolStripDropDown); SegmentedControl; StatusTabs; PaymentGrid (custom-drawn DataGridView);
                     TransactionGrid (same style; Compact mode for the details card); UpcomingList; RowPainter (shared compact due tag + status pill); EmptyState (Compact mode); InlineMessage;
                     PageHeader; ChromeButton; AuthArt
Dialogs/             PADialog (Confirm/Info/Success/Error, and ShowForm to host a form body implementing IDialogBody),
                     dlgRecordPayment (an IDialogBody; reuse the same pattern for dlgChangePassword), PAToast.Notify,
                     DimOverlay, ShadowWindow, Native (Win32)
Data/                Db (parameterized helpers, FriendlyMessageFor), UserRepo, CategoryRepo, PaymentRepo, TransactionRepo
Services/            Session, AuthService (BCrypt), Navigator, DemoDataSeeder, Format (Money, ShortDate, DuePhrase, RelativeDay),
                     TransactionService.Record (one DB transaction: lock payment row, check balance, insert, then
                     RecurrenceService.CreateNextIfPaid), RecurrenceService (NextDueDate, no duplicate children)
Theme/               Theme (tokens, fonts, Px), Gfx (rounded/notched paths, shadow, logo mark), Icons (GDI+ line icons), AppIcon
Models/              User, Payment, PaymentSummary (row of v_payment_summary), Category, PaymentTransaction
```
- Navigation: `Navigator.ShowRoot(view)` for sidebar sections, `Push(view)` for inner screens, `GoBack()`, `BackToRoot()`. Views reload their data in `OnNavigatedTo()`. Screens left behind are disposed through BeginInvoke.
- All reads go through `v_payment_summary`, and every query filters by `Session.CurrentUser.UserId`.
- Messages: `PADialog.*` and `PAToast.Notify(...)` only. Never `MessageBox`. Form validation errors go inline next to the field.

## Design decisions made with the user
- **Ticket motif** gives PayAlert its identity: DueTag (notched ticket with a status stub), PADialog (tone strip, notched perforated footer), the payments icon, and the login illustration.
- In lists, **the DueTag's colored stub is the row's status edge**, and the month text is neutral. The user asked for each status color to appear only once per row.
- Cards use `Theme.CardBorder` (`#DADDE3`) and a stronger soft shadow, because the plan's `#E5E7EB` blended into the page.
- Main window opens maximized. There are no stock scrollbars: the payment grid scrolls with the wheel and fades at the bottom when more rows are below.
- Payment grid rows are 56px tall, not the plan's 36, so the due tag fits.

## Gotchas we hit (VB.NET / WinForms)
- **VB is case-insensitive.** Locals named `text`, `size`, `color`, `tag`, `layout`, `pad`, `name`, `sub`… collide with members and types (`Size.Empty` turns into `Single.Empty`). Use distinct names (`textColor`, `iconSize`…).
- `ReadOnly` is a keyword: write `Me.ReadOnly = True`. Don't name members after base members (`Sorted`, `Validate`, `Margin`, `Layout`).
- **Field initializers run after the base constructor.** The base sets `Font`, which triggers `OnLayout`, before child-control fields exist. Guard with `If Box Is Nothing Then Return`.
- A control anchored Bottom inside a panel that is docked later keeps the wrong distance. Position it in the panel's `Layout` event.
- `Format.Money` needs `Imports PayAlert.Services`; otherwise VB's own `Format()` function wins.
- `List.Count(predicate)` doesn't compile in VB. Use `.Where(...).Count()`.
- `TextRenderer.MeasureText` with `EndEllipsis` and `Size.Empty` measures almost nothing ("D…"). Measure without EndEllipsis.
- DataGridView row and header heights don't auto-scale. PaymentGrid sets them from `DeviceDpi` in `OnHandleCreated`.
- Views are created at runtime: PAView uses `AutoScaleMode.Dpi`. Dialogs and toasts don't auto-scale; they use `Theme.Px()`.

## Testing (how session 1 verified everything)
- **Throwaway database:** XAMPP's own binaries on port 3307, with a data folder in the scratchpad:
  `C:\xampp\mysql\bin\mysql_install_db.exe --datadir=<scratch>\mariadb\data --port=3307`, then
  `mysqld.exe --defaults-file=<scratch>\mariadb\data\my.ini` (run in the background), load the 3 SQL files with `mysql.exe -uroot --port=3307`,
  and shut it down afterwards with `mysqladmin --port=3307 shutdown`. The user's real database on 3306 is never touched.
- **Offscreen UI harness** (PowerShell, STA):
  - `[Reflection.Assembly]::LoadFrom(PayAlert.exe)`
  - hook `AssemblyResolve` with a small Add-Type C# class, because PowerShell has no binding redirects (a PowerShell scriptblock resolver stack-overflows on worker threads)
  - install `WindowsFormsSynchronizationContext` before invoking async methods
  - set `Db._connectionString` by reflection to the 3307 server
  - seed, then `AuthService.Login` + `Session.SignIn`
  - create forms at (-5000, -5000) and render them with `DrawToBitmap` (PrintWindow is unreliable offscreen)
  - answer PADialogs with a WinForms Timer that calls the private `Choose(DialogChoice)`; for form dialogs, set the body's
    fields first (the queued answer can be a scriptblock that gets the dialog and its `_body`). A form that refuses to
    close must be cancelled by the robot, or the harness hangs.
  - pass `.psobject.BaseObject` when giving objects to reflection calls
- Rule checks to run before finishing each phase: no `MessageBox`; no `New ComboBox/DateTimePicker/Button(`; no SQL outside `Data/` (and the seeder); `Option Strict On` + `Option Explicit On` in every file; no hard-coded colors outside Theme.vb; no third-party brand names.

## Next steps (Phase 8)
1. `NotificationRepo` (INSERT IGNORE against the unique key, list newest first, unread count, mark read / mark all read) and `Models/Notification`.
2. `ReminderService`: run at login and every 5 minutes (WinForms Timer in frmMain) when `notifications_enabled = 1`; types due_soon / due_tomorrow / due_today / overdue.
3. `ucNotifications` cards; sidebar badge through `frmMain.SetUnreadCount(n)`. Test no duplicates on repeated runs, show the user, commit after they confirm.
