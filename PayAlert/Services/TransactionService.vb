Option Strict On
Option Explicit On

Imports MySql.Data.MySqlClient
Imports PayAlert.Data
Imports PayAlert.Models

Namespace Services

    Public Enum RecordStatus
        Recorded
        ''' <summary>The payment was deleted or doesn't belong to the user.</summary>
        NotFound
        ''' <summary>The amount is more than the balance (it changed since the window opened, e.g. in another copy of the app).</summary>
        MoreThanBalance
        ''' <summary>The payment is already fully paid.</summary>
        AlreadyPaid
    End Enum

    Public NotInheritable Class RecordResult
        Public Property Status As RecordStatus
        ''' <summary>The transaction as saved (with its new id), when Recorded.</summary>
        Public Property Transaction As PaymentTransaction
        ''' <summary>The payment after recording (amounts and status recalculated), when Recorded.</summary>
        Public Property Payment As PaymentSummary
        ''' <summary>The balance the database had when the amount was checked.</summary>
        Public Property BalanceBefore As Decimal
        ''' <summary>The next payment of a recurring chain, when this one became fully paid.</summary>
        Public Property NextPayment As Payment
    End Class

    ''' <summary>
    ''' Records a full or partial payment (dlgRecordPayment). In one database transaction: lock the payment, check the
    ''' amount against the live balance, add the transaction, and let RecurrenceService create the next one when it's now Paid.
    ''' </summary>
    Public NotInheritable Class TransactionService

        Private Sub New()
        End Sub

        Public Shared Function Record(txn As PaymentTransaction, userId As Integer) As RecordResult
            If txn Is Nothing Then Throw New ArgumentNullException(NameOf(txn))
            If txn.Amount <= 0D Then Throw New ArgumentOutOfRangeException(NameOf(txn), "The amount must be more than zero.")

            Dim result As New RecordResult()
            Db.InTransaction(
                Sub(conn As MySqlConnection, tx As MySqlTransaction)
                    Dim balance = TransactionRepo.LockBalance(conn, tx, txn.PaymentId, userId)
                    If Not balance.HasValue Then
                        result.Status = RecordStatus.NotFound
                        Return
                    End If
                    result.BalanceBefore = balance.Value
                    If balance.Value <= 0D Then
                        result.Status = RecordStatus.AlreadyPaid
                        Return
                    End If
                    If txn.Amount > balance.Value Then
                        result.Status = RecordStatus.MoreThanBalance
                        Return
                    End If

                    txn.TxnId = TransactionRepo.Add(conn, tx, txn)
                    result.Transaction = txn
                    result.NextPayment = RecurrenceService.CreateNextIfPaid(conn, tx, txn.PaymentId, userId)
                    result.Payment = PaymentRepo.GetSummary(conn, tx, txn.PaymentId, userId)
                    result.Status = RecordStatus.Recorded
                End Sub)
            Return result
        End Function

    End Class

End Namespace
