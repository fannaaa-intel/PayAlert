Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports PayAlert.Models

Namespace Data

    ''' <summary>
    ''' Payments for the signed-in user. Reads always go through v_payment_summary (status is calculated there);
    ''' every statement also filters by user_id so one account can never touch another's records.
    ''' </summary>
    Public NotInheritable Class PaymentRepo

        Private Const SummaryColumns As String =
            "SELECT payment_id, user_id, parent_payment_id, name, direction, counterparty, category_id, category_name, " &
            "total_amount, due_date, reminder_days, recurrence, notes, created_at, amount_paid, balance, status, days_until_due " &
            "FROM v_payment_summary "

        Private Sub New()
        End Sub

        ''' <summary>All of a user's payments (not deleted), soonest due first.</summary>
        Public Shared Function GetSummaries(userId As Integer) As List(Of PaymentSummary)
            Dim table = Db.Query(SummaryColumns & "WHERE user_id = @user ORDER BY due_date, name", Db.P("@user", userId))
            Dim list As New List(Of PaymentSummary)(table.Rows.Count)
            For Each row As DataRow In table.Rows
                list.Add(Map(row))
            Next
            Return list
        End Function

        ''' <summary>One payment with its calculated amounts, or Nothing if it doesn't exist, was deleted, or isn't the user's.</summary>
        Public Shared Function GetSummary(paymentId As Integer, userId As Integer) As PaymentSummary
            Dim table = Db.Query(SummaryColumns & "WHERE payment_id = @id AND user_id = @user",
                                 Db.P("@id", paymentId), Db.P("@user", userId))
            Return If(table.Rows.Count = 0, Nothing, Map(table.Rows(0)))
        End Function

        ''' <summary>Creates a payment for payment.UserId and returns its new id.</summary>
        Public Shared Function Insert(payment As Payment) As Integer
            Using conn = Db.OpenConnection()
                Return Db.Insert(conn, Nothing,
                    "INSERT INTO payments (user_id, parent_payment_id, name, direction, counterparty, category_id, total_amount, " &
                    "due_date, reminder_days, is_recurring, recurrence, notes) " &
                    "VALUES (@user, @parent, @name, @direction, @person, @category, @total, @due, @remind, @recurring, @recurrence, @notes)",
                    ParametersFor(payment, includeUser:=True))
            End Using
        End Function

        ''' <summary>Saves the editable fields. Returns False if the payment no longer exists for this user.</summary>
        Public Shared Function Update(payment As Payment) As Boolean
            Dim changed = Db.ExecuteNonQuery(
                "UPDATE payments SET name = @name, direction = @direction, counterparty = @person, category_id = @category, " &
                "total_amount = @total, due_date = @due, reminder_days = @remind, is_recurring = @recurring, recurrence = @recurrence, " &
                "notes = @notes WHERE payment_id = @id AND user_id = @user AND is_deleted = 0",
                ParametersFor(payment, includeUser:=True, includeId:=True))
            Return changed > 0
        End Function

        ''' <summary>Soft delete: the payment disappears from every screen but its history stays in the database.</summary>
        Public Shared Function SoftDelete(paymentId As Integer, userId As Integer) As Boolean
            Return Db.ExecuteNonQuery("UPDATE payments SET is_deleted = 1 WHERE payment_id = @id AND user_id = @user AND is_deleted = 0",
                                      Db.P("@id", paymentId), Db.P("@user", userId)) > 0
        End Function

        Private Shared Function ParametersFor(p As Payment, includeUser As Boolean, Optional includeId As Boolean = False) As MySql.Data.MySqlClient.MySqlParameter()
            Dim list As New List(Of MySql.Data.MySqlClient.MySqlParameter) From {
                Db.P("@parent", If(p.ParentPaymentId.HasValue, CObj(p.ParentPaymentId.Value), Nothing)),
                Db.P("@name", p.Name.Trim()),
                Db.P("@direction", p.Direction),
                Db.P("@person", If(String.IsNullOrWhiteSpace(p.Counterparty), Nothing, p.Counterparty.Trim())),
                Db.P("@category", p.CategoryId),
                Db.P("@total", p.TotalAmount),
                Db.P("@due", p.DueDate.Date),
                Db.P("@remind", p.ReminderDays),
                Db.P("@recurring", If(p.IsRecurring, 1, 0)),
                Db.P("@recurrence", If(p.IsRecurring, p.Recurrence, Nothing)),
                Db.P("@notes", If(String.IsNullOrWhiteSpace(p.Notes), Nothing, p.Notes.Trim()))}
            If includeUser Then list.Add(Db.P("@user", p.UserId))
            If includeId Then list.Add(Db.P("@id", p.PaymentId))
            Return list.ToArray()
        End Function

        Private Shared Function Map(row As DataRow) As PaymentSummary
            Return New PaymentSummary With {
                .PaymentId = Convert.ToInt32(row("payment_id")),
                .UserId = Convert.ToInt32(row("user_id")),
                .ParentPaymentId = If(row.IsNull("parent_payment_id"), CType(Nothing, Integer?), Convert.ToInt32(row("parent_payment_id"))),
                .Name = CStr(row("name")),
                .Direction = CStr(row("direction")),
                .Counterparty = If(row.IsNull("counterparty"), "", CStr(row("counterparty"))),
                .CategoryId = Convert.ToInt32(row("category_id")),
                .CategoryName = CStr(row("category_name")),
                .TotalAmount = Convert.ToDecimal(row("total_amount")),
                .DueDate = Convert.ToDateTime(row("due_date")).Date,
                .ReminderDays = Convert.ToInt32(row("reminder_days")),
                .Recurrence = If(row.IsNull("recurrence"), Nothing, CStr(row("recurrence"))),
                .Notes = If(row.IsNull("notes"), "", CStr(row("notes"))),
                .CreatedAt = If(row.IsNull("created_at"), DateTime.MinValue, Convert.ToDateTime(row("created_at"))),
                .AmountPaid = Convert.ToDecimal(row("amount_paid")),
                .Balance = Convert.ToDecimal(row("balance")),
                .Status = CStr(row("status")),
                .DaysUntilDue = Convert.ToInt32(row("days_until_due"))}
        End Function

    End Class

End Namespace
