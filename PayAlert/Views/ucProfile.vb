Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Profile screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucProfile
        Inherits PAView

        Public Sub New()
            MyBase.New()
            Title = "Profile"
            Subtitle = "Your name and email"
            AddPlaceholder(IconKind.User, "Profile editing arrives in Phase 9",
                           "Edit your full name and email here. Use Back to return to Settings.")
        End Sub

    End Class

End Namespace
