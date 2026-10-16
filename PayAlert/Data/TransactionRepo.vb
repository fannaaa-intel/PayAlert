Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports MySql.Data.MySqlClient
Imports PayAlert.Models

Namespace Data

    ''' <summary>
    ''' Recorded payments and collections (payment_transactions). Every read joins payments and filters by user_id,
    ''' so one account never sees another's history. Recording goes through TransactionService, which locks the payment.
    ''' </summary>
    Public NotInheritable Class TransactionRepo

        Private Const HistoryColumns As String =
            "SELECT t.txn_id, t.payment_id, t.amount, t.paid_on, t.method, t.reference_no, t.payer_name, t.proof_id, t.is_advance, t.note, t.created_at, " &
            "p.name AS payment_name, p.direction, p.is_deleted " &
            "FROM payment_transactions t JOIN payments p ON p.payment_id = t.payment_id "

        Private Sub New()
        End Sub

        ''' <summary>One payment's history, newest first. Empty if the payment isn't the user's.</summary>
        Public Shared Function GetForPayment(paymentId As Integer, userId As Integer) As List(Of PaymentTransaction)
            Dim table = Db.Query(HistoryColumns & "WHERE t.payment_id = @id AND p.user_id = @user " &
                                 "ORDER BY t.paid_on DESC, t.txn_id DESC",
                                 Db.P("@id", paymentId), Db.P("@user", userId))
            Return MapAll(table)
        End Function

        ''' <summary>
        ''' All of a user's transactions, newest first, including those of payments deleted later (the money still moved).
        ''' Optional filters: paid on or after <paramref name="fromDate"/>, on or before <paramref name="toDate"/>,
        ''' and a direction (Payment.DirectionPayable / DirectionReceivable).
        ''' </summary>
        Public Shared Function GetHistory(userId As Integer, Optional fromDate As Date? = Nothing, Optional toDate As Date? = Nothing,
                                          Optional direction As String = Nothing) As List(Of PaymentTransaction)
            Dim table = Db.Query(HistoryColumns &
                                 "WHERE p.user_id = @user " &
                                 "AND (@from IS NULL OR t.paid_on >= @from) " &
                                 "AND (@to IS NULL OR t.paid_on <= @to) " &
                                 "AND (@direction IS NULL OR p.direction = @direction) " &
                                 "ORDER BY t.paid_on DESC, t.txn_id DESC",
                                 Db.P("@user", userId),
                                 Db.P("@from", If(fromDate.HasValue, CObj(fromDate.Value.Date), Nothing)),
                                 Db.P("@to", If(toDate.HasValue, CObj(toDate.Value.Date), Nothing)),
                                 Db.P("@direction", If(String.IsNullOrEmpty(direction), Nothing, direction)))
            Return MapAll(table)
        End Function

        ''' <summary>
        ''' Locks the payment row for the rest of the transaction and returns its current balance,
        ''' or Nothing if it doesn't exist, was deleted, or isn't the user's.
        ''' </summary>
        Public Shared Function LockBalance(conn As MySqlConnection, tx As MySqlTransaction, paymentId As Integer, userId As Integer) As Decimal?
            Dim total = Db.ExecuteScalar(conn, tx,
                "SELECT total_amount FROM payments WHERE payment_id = @id AND user_id = @user AND is_deleted = 0 FOR UPDATE",
                Db.P("@id", paymentId), Db.P("@user", userId))
            If total Is Nothing OrElse TypeOf total Is DBNull Then Return Nothing
            Dim paid = Db.ExecuteScalar(conn, tx,
                "SELECT COALESCE(SUM(amount), 0) FROM payment_transactions WHERE payment_id = @id",
                Db.P("@id", paymentId))
            Return Convert.ToDecimal(total) - Convert.ToDecimal(paid)
        End Function

        ''' <summary>Inserts a transaction and returns its new id. The caller (TransactionService) has already checked the amount.</summary>
        Public Shared Function Add(conn As MySqlConnection, tx As MySqlTransaction, txn As PaymentTransaction) As Integer
            Return Db.Insert(conn, tx,
                "INSERT INTO payment_transactions (payment_id, amount, paid_on, method, reference_no, payer_name, proof_id, is_advance, note) " &
                "VALUES (@payment, @amount, @paidOn, @method, @reference, @payer, @proof, @advance, @note)",
                Db.P("@payment", txn.PaymentId),
                Db.P("@amount", txn.Amount),
                Db.P("@paidOn", txn.PaidOn.Date),
                Db.P("@method", Blank(txn.Method)),
                Db.P("@reference", Blank(txn.ReferenceNo)),
                Db.P("@payer", Blank(txn.PayerName)),
                Db.P("@proof", If(txn.ProofId.HasValue, CObj(txn.ProofId.Value), Nothing)),
                Db.P("@advance", If(txn.IsAdvance, 1, 0)),
                Db.P("@note", Blank(txn.Note)))
        End Function

        ''' <summary>
        ''' Earlier records of this user with the same reference number (ignoring spaces, dashes and case), newest first,
        ''' so Record Payment can warn about a receipt that was already used.
        ''' </summary>
        Public Shared Function FindByReference(userId As Integer, referenceNo As String) As List(Of PaymentTransaction)
            Dim wanted = NormalizeReference(referenceNo)
            If wanted.Length = 0 Then Return New List(Of PaymentTransaction)
            Dim table = Db.Query(HistoryColumns &
                                 "WHERE p.user_id = @user AND REPLACE(REPLACE(UPPER(t.reference_no), ' ', ''), '-', '') = @reference " &
                                 "ORDER BY t.paid_on DESC, t.txn_id DESC",
                                 Db.P("@user", userId), Db.P("@reference", wanted))
            Return MapAll(table)
        End Function

        ''' <summary>A reference number as compared: no spaces or dashes, upper case.</summary>
        Public Shared Function NormalizeReference(referenceNo As String) As String
            Return If(referenceNo, "").Replace(" ", "").Replace("-", "").ToUpperInvariant()
        End Function

        Private Shared Function Blank(value As String) As Object
            Return If(String.IsNullOrWhiteSpace(value), Nothing, CObj(value.Trim()))
        End Function

        Private Shared Function MapAll(table As DataTable) As List(Of PaymentTransaction)
            Dim list As New List(Of PaymentTransaction)(table.Rows.Count)
            For Each row As DataRow In table.Rows
                list.Add(New PaymentTransaction With {
                    .TxnId = Convert.ToInt32(row("txn_id")),
                    .PaymentId = Convert.ToInt32(row("payment_id")),
                    .Amount = Convert.ToDecimal(row("amount")),
                    .PaidOn = Convert.ToDateTime(row("paid_on")).Date,
                    .Method = If(row.IsNull("method"), "", CStr(row("method"))),
                    .Note = If(row.IsNull("note"), "", CStr(row("note"))),
                    .ReferenceNo = If(row.IsNull("reference_no"), "", CStr(row("reference_no"))),
                    .PayerName = If(row.IsNull("payer_name"), "", CStr(row("payer_name"))),
                    .ProofId = If(row.IsNull("proof_id"), CType(Nothing, Integer?), Convert.ToInt32(row("proof_id"))),
                    .IsAdvance = Convert.ToInt32(row("is_advance")) <> 0,
                    .CreatedAt = If(row.IsNull("created_at"), DateTime.MinValue, Convert.ToDateTime(row("created_at"))),
                    .PaymentName = CStr(row("payment_name")),
                    .Direction = CStr(row("direction")),
                    .PaymentDeleted = Convert.ToInt32(row("is_deleted")) <> 0})
            Next
            Return list
        End Function

    End Class

End Namespace
