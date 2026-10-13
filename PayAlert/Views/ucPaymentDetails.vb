Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Payment details screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucPaymentDetails
        Inherits PAView

        Public Sub New()
            MyBase.New()
            Title = "Payment details"
            Subtitle = "Balance, due date and payment history"
            AddPlaceholder(IconKind.Payments, "Payment details arrive in Phase 5",
                           "The balance ring, due tag, info rows and this payment's history will show here. Edit opens one level deeper.",
                           "Open Edit Payment", Sub() Navigator.Push(New ucPaymentEditor() With {.Title = "Edit Payment"}))
        End Sub

    End Class

End Namespace
