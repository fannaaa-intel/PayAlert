Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports MySql.Data.MySqlClient
Imports PayAlert.Data
Imports PayAlert.Models

Namespace Services

    Public Enum RecordStatus
        Recorded
        ''' <summary>The payment was deleted or doesn't belong to the user.</summary>
        NotFound
        ''' <summary>
        ''' The amount is more than the payment can take: its balance for a one-time payment, or the balance plus
        ''' <see cref="TransactionService.MaxAdvancePeriods"/> periods ahead for a repeating one (it may have changed since
        ''' the window opened, e.g. in another copy of the app). See RecordResult.MaxAmount.
        ''' </summary>
        MoreThanBalance
        ''' <summary>The payment is already fully paid.</summary>
        AlreadyPaid
        ''' <summary>An advance couldn't be applied because a later payment of the chain was deleted. Nothing was saved.</summary>
        AdvanceBlocked
    End Enum

    ''' <summary>One later payment of a repeating chain that an advance paid (in full or in part).</summary>
    Public NotInheritable Class AdvanceShare
        Public Property PaymentId As Integer
        Public Property DueDate As Date
        Public Property Amount As Decimal
        ''' <summary>What is still left on that payment after the advance.</summary>
        Public Property BalanceAfter As Decimal
    End Class

    Public NotInheritable Class RecordResult
        Public Property Status As RecordStatus
        ''' <summary>The transaction for this payment as saved (with its new id), when Recorded.</summary>
        Public Property Transaction As PaymentTransaction
        ''' <summary>The payment after recording (amounts and status recalculated), when Recorded.</summary>
        Public Property Payment As PaymentSummary
        ''' <summary>The balance the database had when the amount was checked.</summary>
        Public Property BalanceBefore As Decimal
        ''' <summary>The most the payment could take when the amount was checked (balance, plus periods ahead if it repeats).</summary>
        Public Property MaxAmount As Decimal
        ''' <summary>The next payment of a recurring chain, when this one became fully paid.</summary>
        Public Property NextPayment As Payment
        ''' <summary>The later payments an advance went to, in due-date order (empty when nothing was paid ahead).</summary>
        Public Property AdvanceShares As New List(Of AdvanceShare)
        ''' <summary>The first payment of the chain that still has a balance after recording, if it repeats.</summary>
        Public Property NextDue As PaymentSummary

        ''' <summary>The whole amount entered: this payment's part plus any advance.</summary>
        Public ReadOnly Property TotalRecorded As Decimal
            Get
                Dim sum = If(Transaction Is Nothing, 0D, Transaction.Amount)
                For Each share In AdvanceShares
                    sum += share.Amount
                Next
                Return sum
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Records a full or partial payment (dlgRecordPayment). In one database transaction: lock the payment, check the
    ''' amount against the live balance, save the proof picture (if any), add the transaction, and let RecurrenceService
    ''' create the next one when it's now Paid.
    ''' Advance payment: on a repeating payment the amount may be more than the balance. The balance is paid first, and
    ''' the rest goes to the next payments of the chain in order (created when needed), each as its own record marked
    ''' "advance" with the same method, reference, payer and proof, until it runs out. At most MaxAdvancePeriods ahead.
    ''' </summary>
    Public NotInheritable Class TransactionService

        ''' <summary>How many later periods (months or weeks) one advance may cover.</summary>
        Public Const MaxAdvancePeriods As Integer = 12

        Private Sub New()
        End Sub

        ''' <summary>The most that can be recorded on <paramref name="payment"/> right now (the balance, plus periods ahead if it repeats).</summary>
        Public Shared Function MaxAmountFor(payment As PaymentSummary) As Decimal
            Return MaxAmountFor(payment.Balance, payment.IsRecurring, payment.TotalAmount)
        End Function

        Private Shared Function MaxAmountFor(balance As Decimal, recurring As Boolean, total As Decimal) As Decimal
            Return If(recurring, balance + total * MaxAdvancePeriods, balance)
        End Function

        ''' <summary>
        ''' How an amount would be shared over a repeating chain, without saving: this payment's part first, then whole
        ''' periods of <paramref name="periodAmount"/>. Used by Record Payment to explain an advance as it is typed.
        ''' Returns the amounts in order: (0) this payment, then one entry per later period.
        ''' </summary>
        Public Shared Function SplitAdvance(amount As Decimal, balance As Decimal, periodAmount As Decimal) As List(Of Decimal)
            Dim parts As New List(Of Decimal) From {Math.Min(amount, balance)}
            Dim rest = amount - parts(0)
            While rest > 0D AndAlso parts.Count <= MaxAdvancePeriods
                Dim part = Math.Min(rest, periodAmount)
                parts.Add(part)
                rest -= part
            End While
            Return parts
        End Function

        Public Shared Function Record(txn As PaymentTransaction, userId As Integer, Optional proof As PreparedProof = Nothing) As RecordResult
            If txn Is Nothing Then Throw New ArgumentNullException(NameOf(txn))
            If txn.Amount <= 0D Then Throw New ArgumentOutOfRangeException(NameOf(txn), "The amount must be more than zero.")

            Dim result As New RecordResult()
            Try
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
                        Dim current = PaymentRepo.GetSummary(conn, tx, txn.PaymentId, userId)
                        result.MaxAmount = MaxAmountFor(balance.Value, current.IsRecurring, current.TotalAmount)
                        If txn.Amount > result.MaxAmount Then
                            result.Status = RecordStatus.MoreThanBalance
                            Return
                        End If

                        If proof IsNot Nothing Then
                            txn.ProofId = ProofRepo.Insert(conn, tx, userId, proof.FileName, proof.ContentType, proof.Data)
                        End If
                        Dim entered = txn.Amount
                        txn.Amount = Math.Min(entered, balance.Value)
                        txn.TxnId = TransactionRepo.Add(conn, tx, txn)
                        result.Transaction = txn
                        result.NextPayment = RecurrenceService.CreateNextIfPaid(conn, tx, txn.PaymentId, userId)

                        Dim rest = entered - txn.Amount
                        If rest > 0D Then PayAhead(conn, tx, txn, rest, userId, result)

                        result.Payment = PaymentRepo.GetSummary(conn, tx, txn.PaymentId, userId)
                        If result.Payment.IsRecurring Then result.NextDue = FirstUnpaidAfter(conn, tx, txn.PaymentId, userId)
                        result.Status = RecordStatus.Recorded
                    End Sub)
            Catch ex As AdvanceBlockedException
                ' Rolled back: nothing was saved.
                Return New RecordResult With {.Status = RecordStatus.AdvanceBlocked, .BalanceBefore = result.BalanceBefore}
            End Try
            Return result
        End Function

        ''' <summary>Puts <paramref name="rest"/> on the payments after this one, in order, creating them as they get paid.</summary>
        Private Shared Sub PayAhead(conn As MySqlConnection, tx As MySqlTransaction, first As PaymentTransaction, rest As Decimal,
                                    userId As Integer, result As RecordResult)
            Dim currentId = first.PaymentId
            Dim periods = 0
            While rest > 0D
                periods += 1
                If periods > MaxAdvancePeriods Then Throw New InvalidOperationException("The advance is more than " & MaxAdvancePeriods & " periods.")
                ' The next payment exists once the current one is fully paid (RecurrenceService made it).
                Dim nextId = PaymentRepo.GetNextInChain(conn, tx, currentId)
                If Not nextId.HasValue Then
                    ' An older chain may have a paid payment without its next one yet: create it now.
                    RecurrenceService.CreateNextIfPaid(conn, tx, currentId, userId)
                    nextId = PaymentRepo.GetNextInChain(conn, tx, currentId)
                    If Not nextId.HasValue Then Throw New AdvanceBlockedException()
                End If
                Dim nextBalance = TransactionRepo.LockBalance(conn, tx, nextId.Value, userId)
                ' A later payment the user deleted stops the chain; refuse the whole advance rather than half of it.
                If Not nextBalance.HasValue Then Throw New AdvanceBlockedException()

                If nextBalance.Value > 0D Then
                    Dim part = Math.Min(rest, nextBalance.Value)
                    Dim advance As New PaymentTransaction With {
                        .PaymentId = nextId.Value, .Amount = part, .PaidOn = first.PaidOn, .Method = first.Method,
                        .ReferenceNo = first.ReferenceNo, .PayerName = first.PayerName, .ProofId = first.ProofId,
                        .Note = first.Note, .IsAdvance = True}
                    advance.TxnId = TransactionRepo.Add(conn, tx, advance)
                    RecurrenceService.CreateNextIfPaid(conn, tx, nextId.Value, userId)
                    rest -= part
                    Dim after = PaymentRepo.GetSummary(conn, tx, nextId.Value, userId)
                    result.AdvanceShares.Add(New AdvanceShare With {
                        .PaymentId = nextId.Value, .DueDate = after.DueDate, .Amount = part, .BalanceAfter = after.Balance})
                End If
                currentId = nextId.Value
            End While
        End Sub

        ''' <summary>The first payment from <paramref name="paymentId"/> on (following the chain) that still has a balance.</summary>
        Private Shared Function FirstUnpaidAfter(conn As MySqlConnection, tx As MySqlTransaction, paymentId As Integer, userId As Integer) As PaymentSummary
            Dim id As Integer? = paymentId
            For i = 0 To MaxAdvancePeriods + 1
                If Not id.HasValue Then Return Nothing
                Dim p = PaymentRepo.GetSummary(conn, tx, id.Value, userId)
                If p Is Nothing Then Return Nothing
                If p.Balance > 0D Then Return p
                id = PaymentRepo.GetNextInChain(conn, tx, id.Value)
            Next
            Return Nothing
        End Function

        Private NotInheritable Class AdvanceBlockedException
            Inherits Exception
        End Class

    End Class

End Namespace
