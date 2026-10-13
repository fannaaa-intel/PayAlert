Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Settings screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucSettings
        Inherits PAView

        Public Sub New()
            MyBase.New()
            SidebarKey = "settings"
            Title = "Settings"
            Subtitle = "Your account and reminder preferences"
            AddPlaceholder(IconKind.Settings, "Settings arrive in Phase 9",
                           "Profile, password, notifications and your default reminder. Open your profile to try an inner screen.",
                           "Open profile", Sub() Navigator.Push(New ucProfile()))
        End Sub

    End Class

End Namespace
