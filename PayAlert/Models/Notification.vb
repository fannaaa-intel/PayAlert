Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>An in-app reminder (table notifications), with the payment's name and due date joined in.</summary>
    Public NotInheritable Class Notification
        Public Const TypeDueSoon As String = "due_soon"
        Public Const TypeDueTomorrow As String = "due_tomorrow"
        Public Const TypeDueToday As String = "due_today"
        Public Const TypeOverdue As String = "overdue"

        Public Property NotifId As Integer
        Public Property UserId As Integer
        Public Property PaymentId As Integer
        ''' <summary>One of the Type* constants.</summary>
        Public Property Type As String = TypeDueSoon
        Public Property Message As String = ""
        ''' <summary>The day the reminder is for (one of each type per payment per day).</summary>
        Public Property NotifDate As Date = Date.Today
        Public Property IsRead As Boolean
        Public Property CreatedAt As DateTime

        ' Joined from payments.
        Public Property PaymentName As String = ""
        Public Property DueDate As Date

        ''' <summary>"Overdue", "Due Today", "Due Tomorrow" or "Due in 3 Days" (as of the reminder's day).</summary>
        Public ReadOnly Property Title As String
            Get
                Select Case Type
                    Case TypeOverdue : Return "Overdue"
                    Case TypeDueToday : Return "Due Today"
                    Case TypeDueTomorrow : Return "Due Tomorrow"
                    Case Else
                        Dim days = CInt((DueDate - NotifDate).TotalDays)
                        Return If(days <= 1, "Due Soon", "Due in " & days & " Days")
                End Select
            End Get
        End Property
    End Class

End Namespace
