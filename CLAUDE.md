# PayAlert

VB.NET Windows Forms (.NET Framework 4.8) payment reminder app with a MySQL/MariaDB database on XAMPP.

**At the start of every session, read these first:**
1. [PLAN.md](PLAN.md): the source of truth (stack, rules, design system, screens, phases)
2. [docs/WORK_ORDER.md](docs/WORK_ORDER.md): which phase is done, which is next, and the checklist for each
3. [docs/HANDOFF.md](docs/HANDOFF.md): where the last session stopped, architecture, gotchas, how to test

Working rules:
- One phase at a time. Build with `dotnet build` (0 errors), test, show the user, and commit `Phase N: <summary>` only after they confirm. Then update docs/WORK_ORDER.md.
- **Never push** unless the user says "push". Never touch the parent git repo in the user profile.
- No `MessageBox`. Use `PADialog` and `PAToast`. Validation errors go inline.
- No SQL outside `PayAlert/Data/` (parameterized only), `Option Strict On` + `Option Explicit On` in every file, colors only in `Theme/Theme.vb`, no third-party brand names.
- Test against a throwaway MariaDB on port 3307 and render screens offscreen. Don't take over the user's desktop.
