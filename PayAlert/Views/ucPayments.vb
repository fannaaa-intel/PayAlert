Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Payments screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucPayments
        Inherits PAView

        Public Sub New()
            MyBase.New()
            SidebarKey = "payments"
            Title = "Payments"
            Subtitle = "View and manage your reminders"
            Dim action As New PrimaryButton() With {.Text = "Add Payment", .Icon = IconKind.Plus, .Size = New Drawing.Size(150, Theme.ButtonHeight)}
            AddHandler action.Click, Sub(s, e) Navigator.Push(New ucPaymentEditor())
            AddHeaderAction(action)
            AddPlaceholder(IconKind.Payments, "Your payment list arrives in Phase 5",
                           "Every payment you owe or are owed, with status tabs, filters and search. Open a sample to try the back button.",
                           "Open a sample payment", Sub() Navigator.Push(New ucPaymentDetails()))
        End Sub

    End Class

End Namespace
