Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Notifications screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucNotifications
        Inherits PAView

        Public Sub New()
            MyBase.New()
            SidebarKey = "notifications"
            Title = "Notifications"
            Subtitle = "Reminders before your due dates"
            AddPlaceholder(IconKind.Bell, "Reminders arrive in Phase 8",
                           "Due soon, due tomorrow, due today and overdue alerts, newest first, with Mark all as read.")
        End Sub

    End Class

End Namespace
