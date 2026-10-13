Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Dialogs

    ''' <summary>
    ''' Record Payment / Record Collection (§7), shown inside a PADialog: Amount (defaults to the full balance,
    ''' more than 0 and no more than the balance), Date paid (today, not in the future), Method (Cash, E-wallet,
    ''' Bank Transfer, Card, or Other with its own name) and a Note. Problems show under each field; Save stays
    ''' disabled until everything is valid. Use <see cref="Run"/>.
    ''' </summary>
    Public NotInheritable Class dlgRecordPayment
        Inherits Panel
        Implements IDialogBody

        Private Const ColumnGap As Integer = 16
        Private Const RowGap As Integer = 10
        Private Const CaptionHeight As Integer = 22
        Private Const FieldHeight As Integer = 42
        Private Const NoteHeight As Integer = 20
        Private Const NoteMax As Integer = 255
        Private Const MethodMax As Integer = 50

        Private ReadOnly _payment As PaymentSummary
        Private _balance As Decimal

        Private ReadOnly _amount As New PesoTextBox()
        Private ReadOnly _date As New PADatePicker()
        Private ReadOnly _method As New PASelect()
        Private ReadOnly _otherMethod As New PATextBox()
        Private ReadOnly _note As New PATextBox()
        Private ReadOnly _amountNote As New Label()
        Private ReadOnly _dateNote As New Label()
        Private ReadOnly _methodNote As New Label()
        Private ReadOnly _otherNote As New Label()
        Private ReadOnly _noteNote As New Label()
        Private ReadOnly _otherCaption As New Label()
        Private _otherTouched As Boolean
        Private _canSubmit As Boolean

        Public Event CanSubmitChanged As EventHandler Implements IDialogBody.CanSubmitChanged

        ''' <summary>What happened when the user saved; Nothing until then.</summary>
        Public Property Result As RecordResult

        ''' <summary>
        ''' Opens the window for <paramref name="payment"/>. Returns the result when the user saved, or Nothing when they
        ''' cancelled. Database problems are shown here; the caller only handles the outcome.
        ''' </summary>
        Public Shared Function Run(owner As Control, payment As PaymentSummary) As RecordResult
            Dim body As New dlgRecordPayment(payment)
            Dim title = If(payment.IsReceivable, "Record Collection", "Record Payment")
            Dim message = """" & payment.Name & """  ·  " & Format.Money(payment.Balance) & " left of " & Format.Money(payment.TotalAmount)
            Dim choice = PADialog.ShowForm(owner, Tone.Brand, IconKind.Payments, title, message, body,
                                           If(payment.IsReceivable, "Save Collection", "Save Payment"))
            Return If(choice = DialogChoice.Primary, body.Result, Nothing)
        End Function

        Private Sub New(payment As PaymentSummary)
            _payment = payment
            _balance = payment.Balance
            BackColor = Theme.Surface
            Font = Theme.FontBody

            _amount.Value = _balance
            _date.Value = Date.Today
            _method.SetItems(PaymentTransaction.Methods, 0)
            _method.Icon = IconKind.Payments
            _otherMethod.PlaceholderText = "e.g. Check"
            _otherMethod.MaxLength = MethodMax
            _note.PlaceholderText = "Reference number or anything to remember (optional)"
            _note.MaxLength = NoteMax

            Dim colWidth = (Theme.Px(540 - 48) - Theme.Px(ColumnGap)) \ 2
            Dim rightX = colWidth + Theme.Px(ColumnGap)
            Dim fullWidth = colWidth * 2 + Theme.Px(ColumnGap)
            Dim y = 0
            AddField(If(payment.IsReceivable, "Amount received *", "Amount paid *"), _amount, _amountNote, 0, y, colWidth)
            AddField(If(payment.IsReceivable, "Date received *", "Date paid *"), _date, _dateNote, rightX, y, colWidth)
            y += RowHeight + Theme.Px(RowGap)
            AddField("Method", _method, _methodNote, 0, y, colWidth)
            AddField("Method name *", _otherMethod, _otherNote, rightX, y, colWidth, _otherCaption)
            y += RowHeight + Theme.Px(RowGap)
            AddField("Note", _note, _noteNote, 0, y, fullWidth)
            y += RowHeight
            Size = New Size(fullWidth, y)

            _amount.TabIndex = 0
            _date.TabIndex = 1
            _method.TabIndex = 2
            _otherMethod.TabIndex = 3
            _note.TabIndex = 4
            SetOtherVisible(False)

            AddHandler _amount.ValueChanged, Sub(s, e) CheckForm()
            AddHandler _date.ValueChanged, Sub(s, e) CheckForm()
            AddHandler _method.SelectedIndexChanged, AddressOf MethodChanged
            AddHandler _otherMethod.TextChanged, Sub(s, e) CheckForm()
            AddHandler _otherMethod.Leave,
                Sub(s, e)
                    _otherTouched = True
                    CheckForm()
                End Sub
            AddHandler _note.TextChanged, Sub(s, e) CheckForm()
            CheckForm()
        End Sub

        Private Shared ReadOnly Property RowHeight As Integer
            Get
                Return Theme.Px(CaptionHeight + FieldHeight + NoteHeight)
            End Get
        End Property

        ''' <summary>Caption on top, the field, and a one-line note for hints or inline errors.</summary>
        Private Sub AddField(caption As String, input As Control, note As Label, x As Integer, y As Integer, width As Integer,
                             Optional captionLabel As Label = Nothing)
            Dim label = If(captionLabel, New Label())
            label.Text = caption
            label.Font = Theme.FontBodySemibold
            label.ForeColor = Theme.TextPrimary
            label.BackColor = Theme.Surface
            label.SetBounds(x, y, width, Theme.Px(CaptionHeight))
            input.SetBounds(x, y + Theme.Px(CaptionHeight), width, Theme.Px(FieldHeight))
            note.Font = Theme.FontCaption
            note.ForeColor = Theme.TextMuted
            note.BackColor = Theme.Surface
            note.AutoEllipsis = True
            note.SetBounds(x, input.Bottom + Theme.Px(2), width, Theme.Px(NoteHeight) - Theme.Px(2))
            Controls.Add(label)
            Controls.Add(input)
            Controls.Add(note)
        End Sub

        Private ReadOnly Property IsOtherMethod As Boolean
            Get
                Return CStr(_method.SelectedItem) = PaymentTransaction.MethodOther
            End Get
        End Property

        Private Sub MethodChanged(sender As Object, e As EventArgs)
            SetOtherVisible(IsOtherMethod)
            If IsOtherMethod Then
                _otherTouched = False
                _otherMethod.Focus()
            End If
            CheckForm()
        End Sub

        Private Sub SetOtherVisible(visible As Boolean)
            _otherCaption.Visible = visible
            _otherMethod.Visible = visible
            _otherNote.Visible = visible
        End Sub

        ' ---------------------------------------------------------------- validation

        ''' <summary>Checks every field, shows hints and errors under them, and enables Save when all is well.</summary>
        Private Function CheckForm() As Boolean
            Dim paidWord = If(_payment.IsReceivable, "collects", "pays")

            Dim amountError As String = Nothing
            Dim amount = _amount.Value
            If Not amount.HasValue OrElse amount.Value <= 0D Then
                amountError = "Enter an amount more than ₱0.00."
            ElseIf amount.Value > _balance Then
                amountError = "Can't be more than the " & Format.Money(_balance) & " balance."
            End If
            If amountError IsNot Nothing Then
                ShowNote(_amountNote, amountError, True)
            ElseIf amount.Value = _balance Then
                ShowNote(_amountNote, "This " & paidWord & " it in full.", False)
            Else
                ShowNote(_amountNote, Format.Money(_balance - amount.Value) & " will be left.", False)
            End If
            _amount.HasError = amountError IsNot Nothing

            Dim daysAgo = CInt((Date.Today - _date.Value).TotalDays)
            Dim dateError = If(daysAgo < 0, "Can't be in the future.", Nothing)
            ShowNote(_dateNote, If(dateError, If(_payment.IsReceivable, "The day you received it.", "The day you paid it.")), dateError IsNot Nothing)
            _date.HasError = dateError IsNot Nothing

            ShowNote(_methodNote, If(IsOtherMethod, "Type the method on the right.", "Or choose Other to type your own."), False)

            Dim otherError As String = Nothing
            If IsOtherMethod AndAlso _otherMethod.Text.Trim().Length = 0 Then otherError = "Type the method, or pick one from the list."
            Dim showOther = otherError IsNot Nothing AndAlso _otherTouched
            ShowNote(_otherNote, If(showOther, otherError, "Up to " & MethodMax & " characters."), showOther)
            _otherMethod.HasError = showOther

            ShowNote(_noteNote, _note.Text.Length & " / " & NoteMax, False)

            SetCanSubmit(amountError Is Nothing AndAlso dateError Is Nothing AndAlso otherError Is Nothing)
            Return _canSubmit
        End Function

        Private Shared Sub ShowNote(note As Label, message As String, isError As Boolean)
            note.Text = message
            note.ForeColor = If(isError, Theme.OverdueText, Theme.TextMuted)
        End Sub

        Private Sub SetCanSubmit(value As Boolean)
            If value = _canSubmit Then Return
            _canSubmit = value
            RaiseEvent CanSubmitChanged(Me, EventArgs.Empty)
        End Sub

        ' ---------------------------------------------------------------- IDialogBody

        Public ReadOnly Property CanSubmit As Boolean Implements IDialogBody.CanSubmit
            Get
                Return _canSubmit
            End Get
        End Property

        Public Sub FocusFirstField() Implements IDialogBody.FocusFirstField
            _amount.InnerTextBox.Focus()
            _amount.InnerTextBox.SelectAll()
        End Sub

        Public Function Submit() As Boolean Implements IDialogBody.Submit
            _otherTouched = True
            If Not CheckForm() Then Return False

            Dim txn As New PaymentTransaction With {
                .PaymentId = _payment.PaymentId,
                .Amount = _amount.Value.Value,
                .PaidOn = _date.Value,
                .Method = If(IsOtherMethod, _otherMethod.Text.Trim(), CStr(_method.SelectedItem)),
                .Note = _note.Text.Trim()}
            Dim outcome As RecordResult
            Try
                outcome = TransactionService.Record(txn, Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[dlgRecordPayment] " & ex.ToString())
                PADialog.Error(Me, If(_payment.IsReceivable, "Couldn't record the collection", "Couldn't record the payment"),
                               Db.FriendlyMessageFor(ex))
                Return False
            End Try

            If outcome.Status = RecordStatus.MoreThanBalance Then
                ' Someone recorded part of it elsewhere since this window opened: keep the window open with the new limit.
                _balance = outcome.BalanceBefore
                CheckForm()
                Return False
            End If
            Result = outcome
            Return True
        End Function

    End Class

End Namespace
