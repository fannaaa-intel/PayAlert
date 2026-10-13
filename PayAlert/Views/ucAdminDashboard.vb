Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Admin screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucAdminDashboard
        Inherits PAView

        Public Sub New()
            MyBase.New()
            SidebarKey = "admin"
            Title = "Admin"
            Subtitle = "Users and payments across PayAlert"
            AddPlaceholder(IconKind.Admin, "The admin dashboard arrives in Phase 10",
                           "Total users, active users, total payments and overdue payments. Open user management to try an inner screen.",
                           "Manage users", Sub() Navigator.Push(New ucAdminUsers()))
        End Sub

    End Class

End Namespace
