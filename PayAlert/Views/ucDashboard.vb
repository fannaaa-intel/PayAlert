Option Strict On
Option Explicit On

Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>Dashboard screen. Placeholder until its phase is built (see PLAN.md §9).</summary>
    Public Class ucDashboard
        Inherits PAView

        Public Sub New()
            MyBase.New()
            SidebarKey = "dashboard"
            Title = "Dashboard"
            Subtitle = Greeting()
            Dim action As New PrimaryButton() With {.Text = "Add Payment", .Icon = IconKind.Plus, .Size = New Drawing.Size(150, Theme.ButtonHeight)}
            AddHandler action.Click, Sub(s, e) Navigator.Push(New ucPaymentEditor())
            AddHeaderAction(action)
            AddPlaceholder(IconKind.Dashboard, "Your dashboard arrives in Phase 7",
                           "Due-soon and overdue counts, what you owe and what you're owed, and your next five payments will show here.",
                           "Go to Payments", Sub() Navigator.ShowRoot(New ucPayments()))
        End Sub

        ''' <summary>"Good morning, Juan!" plus today's date.</summary>
        Private Shared Function Greeting() As String
            Dim hour = Date.Now.Hour
            Dim part = If(hour < 12, "morning", If(hour < 18, "afternoon", "evening"))
            Dim name = If(Session.CurrentUser?.FirstName, "")
            Return "Good " & part & If(name.Length > 0, ", " & name, "") & "!  ·  " & Date.Today.ToString("dddd, MMM d, yyyy", Globalization.CultureInfo.InvariantCulture)
        End Function

    End Class

End Namespace
