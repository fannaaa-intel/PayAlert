Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>
    ''' One recorded payment or collection (table payment_transactions). The sum of a payment's transactions is its
    ''' amount paid. Each one can carry a reference number, the name of who paid, and a proof photo (payment_proofs).
    ''' History lists also fill in the payment's name and direction.
    ''' </summary>
    Public NotInheritable Class PaymentTransaction
        Public Const MethodCash As String = "Cash"
        ' The two e-wallets the client's users pay with, named at the client's request (2026 revision).
        Public Const MethodGCash As String = "GCash"
        Public Const MethodMaya As String = "Maya"
        Public Const MethodBank As String = "Bank Transfer"
        Public Const MethodCard As String = "Card"
        ''' <summary>The generic e-wallet choice of the first release; older records still show and group under it.</summary>
        Public Const MethodEWallet As String = "E-wallet"
        Public Const MethodOther As String = "Other"

        ''' <summary>The choices offered by Record Payment; "Other" lets the user type their own.</summary>
        Public Shared ReadOnly Methods As String() = {MethodCash, MethodGCash, MethodMaya, MethodBank, MethodCard, MethodOther}

        Public Property TxnId As Integer
        Public Property PaymentId As Integer
        Public Property Amount As Decimal
        Public Property PaidOn As Date = Date.Today
        Public Property Method As String = ""
        Public Property Note As String = ""
        ''' <summary>The receipt or confirmation number, to find and check the payment later.</summary>
        Public Property ReferenceNo As String = ""
        ''' <summary>Who paid: the user for "I Owe", the other person for "Owed to Me" (editable).</summary>
        Public Property PayerName As String = ""
        ''' <summary>The proof photo (payment_proofs), if one was attached.</summary>
        Public Property ProofId As Integer?
        Public Property CreatedAt As DateTime

        Public ReadOnly Property HasProof As Boolean
            Get
                Return ProofId.HasValue
            End Get
        End Property

        ' Filled by history queries (joined from payments).
        Public Property PaymentName As String = ""
        Public Property Direction As String = Payment.DirectionPayable
        ''' <summary>True when the payment was deleted later; its history is kept but it can't be opened.</summary>
        Public Property PaymentDeleted As Boolean

        Public ReadOnly Property IsReceivable As Boolean
            Get
                Return Direction = Payment.DirectionReceivable
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
