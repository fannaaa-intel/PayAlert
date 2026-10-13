Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>History screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucHistory
        Inherits PAView

        Public Sub New()
            MyBase.New()
            SidebarKey = "history"
            Title = "History"
            Subtitle = "Every payment you've recorded"
            AddPlaceholder(IconKind.History, "Payment history arrives in Phase 6",
                           "All recorded payments and collections, with date and direction filters and Total Paid / Total Collected.")
        End Sub

    End Class

End Namespace
