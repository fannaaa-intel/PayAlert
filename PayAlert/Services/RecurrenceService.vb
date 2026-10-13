Option Strict On
Option Explicit On

Imports MySql.Data.MySqlClient
Imports PayAlert.Data
Imports PayAlert.Models

Namespace Services

    ''' <summary>
    ''' Recurring payments (PLAN.md §3.8): when one becomes fully Paid, the next one is created with the due date
    ''' one week or one month later, linked to it by parent_payment_id. Runs inside the transaction that recorded the payment.
    ''' </summary>
    Public NotInheritable Class RecurrenceService

        Private Sub New()
        End Sub

        ''' <summary>The due date after <paramref name="due"/>: +7 days, or +1 month (Jan 31 becomes Feb 28/29).</summary>
        Public Shared Function NextDueDate(due As Date, recurrence As String) As Date
            Return If(recurrence = Payment.RecurrenceWeekly, due.AddDays(7), due.AddMonths(1))
        End Function

        ''' <summary>
        ''' If the payment is recurring, fully paid, and has no next payment yet, creates the next one and returns it
        ''' (with its new id). Otherwise returns Nothing.
        ''' </summary>
        Public Shared Function CreateNextIfPaid(conn As MySqlConnection, tx As MySqlTransaction, paymentId As Integer, userId As Integer) As Payment
            Dim current = PaymentRepo.GetSummary(conn, tx, paymentId, userId)
            If current Is Nothing OrElse Not current.IsRecurring OrElse current.Balance > 0D Then Return Nothing
            If PaymentRepo.GetNextInChain(conn, tx, paymentId).HasValue Then Return Nothing

            Dim nextPayment As New Payment With {
                .UserId = userId,
                .ParentPaymentId = current.PaymentId,
                .Name = current.Name,
                .Direction = current.Direction,
                .Counterparty = current.Counterparty,
                .CategoryId = current.CategoryId,
                .TotalAmount = current.TotalAmount,
                .DueDate = NextDueDate(current.DueDate, current.Recurrence),
                .ReminderDays = current.ReminderDays,
                .Recurrence = current.Recurrence,
                .Notes = current.Notes}
            nextPayment.PaymentId = PaymentRepo.Insert(conn, tx, nextPayment)
            Return nextPayment
        End Function

    End Class

End Namespace
