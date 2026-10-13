Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>A row of v_payment_summary: the payment plus its calculated amounts and status.</summary>
    Public NotInheritable Class PaymentSummary
        Inherits Payment

        Public Property CategoryName As String = ""
        Public Property AmountPaid As Decimal
        Public Property Balance As Decimal
        ''' <summary>"Paid", "Overdue", "Partially Paid" or "Upcoming" (Theme.Status* constants).</summary>
        Public Property Status As String = ""
        Public Property DaysUntilDue As Integer
        Public Property CreatedAt As DateTime

        Public ReadOnly Property IsPaid As Boolean
            Get
                Return Status = Theme.StatusPaid
            End Get
        End Property

        ''' <summary>Paid ÷ total, 0..1.</summary>
        Public ReadOnly Property Progress As Double
            Get
                Return If(TotalAmount <= 0D, 0.0, CDbl(Math.Min(1D, AmountPaid / TotalAmount)))
            End Get
        End Property
    End Class

End Namespace
