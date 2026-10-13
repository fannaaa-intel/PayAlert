Option Strict On
Option Explicit On

Imports PayAlert.Data
Imports PayAlert.Models

Namespace Services

    ''' <summary>
    ''' In-app reminders (§7 ReminderService). Runs at login and every 5 minutes while the app is open, when the user's
    ''' notifications are on. Each unpaid payment gets at most one reminder a day, the most specific that applies:
    ''' overdue (past due), due today, due tomorrow, or due soon (within its "remind me" days). The unique key on
    ''' (payment, type, day) blocks duplicates, so running it again is always safe.
    ''' </summary>
    Public NotInheritable Class ReminderService

        Private Sub New()
        End Sub

        ''' <summary>The reminder type for a payment today, or Nothing when none is due.</summary>
        Public Shared Function TypeFor(p As PaymentSummary) As String
            If p.Balance <= 0D Then Return Nothing
            Select Case p.DaysUntilDue
                Case Is < 0 : Return Notification.TypeOverdue
                Case 0 : Return Notification.TypeDueToday
                Case 1 : Return Notification.TypeDueTomorrow
                Case Is <= p.ReminderDays : Return Notification.TypeDueSoon
                Case Else : Return Nothing
            End Select
        End Function

        ''' <summary>"BNPL Installment payment of ₱1,500.00 is due tomorrow." (the amount is what's left).</summary>
        Public Shared Function MessageFor(p As PaymentSummary, type As String) As String
            Dim subject = p.Name & If(p.IsReceivable, " collection of ", " payment of ") & Format.Money(p.Balance)
            Select Case type
                Case Notification.TypeOverdue
                    Dim late = -p.DaysUntilDue
                    Return subject & " is " & late & If(late = 1, " day", " days") & " overdue."
                Case Notification.TypeDueToday
                    Return subject & " is due today."
                Case Notification.TypeDueTomorrow
                    Return subject & " is due tomorrow."
                Case Else
                    Return subject & " is due in " & p.DaysUntilDue & " days (" & Format.ShortDate(p.DueDate) & ")."
            End Select
        End Function

        ''' <summary>Creates today's reminders for the user. Returns how many were new (0 when notifications are off).</summary>
        Public Shared Function Run(userId As Integer) As Integer
            If Not SettingsRepo.Get(userId).NotificationsEnabled Then Return 0
            Dim created = 0
            For Each p In PaymentRepo.GetSummaries(userId)
                Dim type = TypeFor(p)
                If type Is Nothing Then Continue For
                Dim reminder As New Notification With {
                    .UserId = userId, .PaymentId = p.PaymentId, .Type = type, .Message = MessageFor(p, type), .NotifDate = Date.Today}
                If NotificationRepo.AddIfNew(reminder) Then created += 1
            Next
            Return created
        End Function

    End Class

End Namespace
