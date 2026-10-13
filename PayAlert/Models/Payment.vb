Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>The editable fields of a payment (table payments). Status is never stored; see PaymentSummary.</summary>
    Public Class Payment
        Public Const DirectionPayable As String = "payable"
        Public Const DirectionReceivable As String = "receivable"
        Public Const RecurrenceWeekly As String = "weekly"
        Public Const RecurrenceMonthly As String = "monthly"

        Public Property PaymentId As Integer
        Public Property UserId As Integer
        Public Property ParentPaymentId As Integer?
        Public Property Name As String = ""
        Public Property Direction As String = DirectionPayable
        Public Property Counterparty As String = ""
        Public Property CategoryId As Integer
        Public Property TotalAmount As Decimal
        Public Property DueDate As Date = Date.Today
        Public Property ReminderDays As Integer = 3
        ''' <summary>Nothing for one-time payments, otherwise "weekly" or "monthly".</summary>
        Public Property Recurrence As String
        Public Property Notes As String = ""

        Public ReadOnly Property IsRecurring As Boolean
            Get
                Return Not String.IsNullOrEmpty(Recurrence)
            End Get
        End Property

        Public ReadOnly Property IsReceivable As Boolean
            Get
                Return Direction = DirectionReceivable
            End Get
        End Property

        ''' <summary>"I Owe" or "Owed to Me".</summary>
        Public ReadOnly Property DirectionLabel As String
            Get
                Return If(IsReceivable, "Owed to Me", "I Owe")
            End Get
        End Property
    End Class

End Namespace
