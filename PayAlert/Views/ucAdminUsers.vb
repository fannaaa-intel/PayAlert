Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Manage users screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucAdminUsers
        Inherits PAView

        Public Sub New()
            MyBase.New()
            Title = "Manage users"
            Subtitle = "Activate, deactivate and reset passwords"
            AddPlaceholder(IconKind.User, "User management arrives in Phase 10",
                           "A list of every account with Activate/Deactivate and Reset Password.")
        End Sub

    End Class

End Namespace
