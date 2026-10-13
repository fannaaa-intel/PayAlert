Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Add Payment screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucPaymentEditor
        Inherits PAView

        Public Sub New()
            MyBase.New()
            Title = "Add Payment"
            Subtitle = "Fill in the details. Fields marked * are required."
            AddPlaceholder(IconKind.Edit, "The payment form arrives in Phase 5",
                           "Direction, name, person, amount, due date, category, reminder and recurrence. Use Back to return.")
        End Sub

    End Class

End Namespace
