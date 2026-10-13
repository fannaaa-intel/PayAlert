Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Add Payment / Edit Payment (§7) in one screen. Edit mode comes pre-filled and won't let the amount drop below
    ''' what's already paid. Save stays disabled until the required fields are valid; problems show inline.
    ''' </summary>
    Public Class ucPaymentEditor
        Inherits PAView

        Private Const CardWidth As Integer = 860
        Private Const MaxCardWidth As Integer = 1120
        Private Shared ReadOnly ReminderChoices As Integer() = {1, 3, 7}
        Private Shared ReadOnly RepeatChoices As String() = {Nothing, Payment.RecurrenceWeekly, Payment.RecurrenceMonthly}

        Private ReadOnly _paymentId As Integer
        Private _existing As PaymentSummary
        Private _categories As New List(Of Category)
        Private _loaded As Boolean

        Private ReadOnly _direction As New SegmentedControl()
        Private ReadOnly _reminder As New SegmentedControl()
        Private ReadOnly _name As New PATextBox()
        Private ReadOnly _person As New PATextBox()
        Private ReadOnly _amount As New PesoTextBox()
        Private ReadOnly _due As New PADatePicker()
        Private ReadOnly _category As New PASelect()
        Private ReadOnly _repeat As New SegmentedControl()
        Private ReadOnly _notes As New PATextBox()
        Private ReadOnly _nameNote As New Label()
        Private ReadOnly _personNote As New Label()
        Private ReadOnly _amountNote As New Label()
        Private ReadOnly _dueNote As New Label()
        Private ReadOnly _categoryNote As New Label()
        Private ReadOnly _repeatNote As New Label()
        Private ReadOnly _directionNote As New Label()
        Private ReadOnly _reminderNote As New Label()
        Private ReadOnly _notesNote As New Label()
        Private ReadOnly _save As New PrimaryButton()
        Private ReadOnly _cancel As New SecondaryButton()
        Private ReadOnly _touched As New HashSet(Of Control)

        ''' <param name="paymentId">0 to add a new payment; otherwise the payment to edit.</param>
        Public Sub New(Optional paymentId As Integer = 0)
            MyBase.New()
            _paymentId = paymentId
            Title = If(IsEdit, "Edit Payment", "Add Payment")
            Subtitle = If(IsEdit, "Change the details and save. Fields marked * are required.", "Fill in the details. Fields marked * are required.")

            _direction.SetItems("I Owe", "Owed to Me")
            _reminder.SetItems("1 day", "3 days", "7 days")
            _reminder.SelectedIndex = 1
            _repeat.SetItems("No", "Weekly", "Monthly")
            _name.Icon = IconKind.Payments
            _name.PlaceholderText = "e.g. Electricity Bill"
            _name.MaxLength = 100
            _person.Icon = IconKind.User
            _person.PlaceholderText = "Who you pay or who pays you"
            _person.MaxLength = 100
            _category.PlaceholderText = "Choose a category"
            _notes.Multiline = True
            _notes.MaxLength = 500
            _notes.PlaceholderText = "Anything to remember about this payment (optional)"

            Dim grid As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 4, .BackColor = Theme.Surface}
            For i = 0 To 2
                grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.33F))
            Next
            For i = 0 To 2
                grid.RowStyles.Add(New RowStyle(SizeType.Absolute, 82))
            Next
            grid.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            grid.Controls.Add(FieldCell("Direction *", _direction, _directionNote, "Money you pay, or money owed to you."), 0, 0)
            grid.Controls.Add(FieldCell("Payment name *", _name, _nameNote, ""), 1, 0)
            grid.Controls.Add(FieldCell("Person / Company", _person, _personNote, ""), 2, 0)
            grid.Controls.Add(FieldCell("Amount *", _amount, _amountNote, ""), 0, 1)
            grid.Controls.Add(FieldCell("Due date *", _due, _dueNote, ""), 1, 1)
            grid.Controls.Add(FieldCell("Category *", _category, _categoryNote, ""), 2, 1)
            grid.Controls.Add(FieldCell("Remind me before it's due", _reminder, _reminderNote, "The reminder shows inside PayAlert."), 0, 2)
            grid.Controls.Add(FieldCell("Repeats", _repeat, _repeatNote, "The next one is created once this is paid."), 1, 2)
            Dim notesCell = FieldCell("Notes", _notes, _notesNote, "0 / 500")
            _notes.Height = 52
            grid.Controls.Add(notesCell, 0, 3)
            grid.SetColumnSpan(notesCell, 3)

            ' Footer: Cancel and Save, right-aligned.
            _save.Text = "Save Payment"
            _save.Size = New Size(150, Theme.ButtonHeight)
            _save.Enabled = False
            _cancel.Text = "Cancel"
            _cancel.Size = New Size(110, Theme.ButtonHeight)
            Dim footer As New FlowLayoutPanel() With {
                .Dock = DockStyle.Bottom, .Height = 46, .FlowDirection = FlowDirection.RightToLeft, .BackColor = Theme.Surface,
                .Padding = New Padding(0, 8, 0, 0), .WrapContents = False}
            _save.Margin = New Padding(0)
            _cancel.Margin = New Padding(0, 0, 10, 0)
            footer.Controls.Add(_save)
            footer.Controls.Add(_cancel)

            Dim card As New SoftCard() With {
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Bottom, .Location = New Point(0, 0),
                .Size = New Size(CardWidth, ViewHeight - 44 - 76 - 20 - Theme.ContentPadding * 2),
                .Padding = New Padding(22, 18, 22, 16)}
            card.Controls.Add(grid)
            card.Controls.Add(footer)
            Body.Controls.Add(card)
            AddHandler Body.Layout,
                Sub(s, e)
                    card.Width = Math.Min(Body.ClientSize.Width, CInt(MaxCardWidth * DeviceDpi / 96.0F))
                    card.Height = Body.ClientSize.Height
                End Sub

            AddHandler _save.Click, AddressOf SaveClicked
            AddHandler _cancel.Click, Sub(s, e) Navigator.GoBack()
            For Each input As Control In New Control() {_name, _person, _amount, _due, _category, _notes}
                AddHandler input.Leave, Sub(s, e) Touch(DirectCast(s, Control))
            Next
            AddHandler _name.TextChanged, Sub(s, e) CheckForm()
            AddHandler _amount.ValueChanged, Sub(s, e) CheckForm()
            AddHandler _category.SelectedIndexChanged, Sub(s, e) Touch(_category)
            AddHandler _due.ValueChanged, Sub(s, e) CheckForm()
            AddHandler _direction.SelectedIndexChanged, Sub(s, e) CheckForm()
            AddHandler _notes.TextChanged, Sub(s, e) CheckForm()
        End Sub

        Private ReadOnly Property IsEdit As Boolean
            Get
                Return _paymentId > 0
            End Get
        End Property

        ''' <summary>A form cell: label on top, the control, and a one-line note for hints or inline errors.</summary>
        Private Shared Function FieldCell(label As String, input As Control, note As Label, hint As String) As Panel
            Dim cell As New Panel() With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 18, 0), .BackColor = Theme.Surface}
            note.Text = hint
            note.Tag = hint
            note.Font = Theme.FontCaption
            note.ForeColor = Theme.TextMuted
            note.BackColor = Theme.Surface
            note.Dock = DockStyle.Top
            note.Height = 18
            note.AutoEllipsis = True
            input.Dock = DockStyle.Top
            If TypeOf input IsNot PATextBox OrElse Not DirectCast(input, PATextBox).Multiline Then input.Height = 42
            Dim caption As New Label() With {
                .Text = label, .Font = Theme.FontBodySemibold, .ForeColor = Theme.TextPrimary, .BackColor = Theme.Surface,
                .Dock = DockStyle.Top, .Height = 22}
            cell.Controls.Add(note)
            cell.Controls.Add(input)
            cell.Controls.Add(caption)
            Return cell
        End Function

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            If _loaded Then Return
            _loaded = True
            Try
                _categories = CategoryRepo.GetAll()
                _category.SetItems(_categories.Cast(Of Object)())
                If IsEdit Then
                    _existing = PaymentRepo.GetSummary(_paymentId, Session.CurrentUser.UserId)
                    If _existing Is Nothing Then
                        PADialog.Info(Me, "This payment isn't available", "It may have been deleted.", Tone.Warning)
                        Navigator.GoBack()
                        Return
                    End If
                    Prefill(_existing)
                Else
                    _due.Value = Date.Today.AddDays(7)
                End If
            Catch ex As Exception
                Debug.WriteLine("[ucPaymentEditor] " & ex.ToString())
                PADialog.Error(Me, "Can't open the payment form", Db.FriendlyMessageFor(ex))
            End Try
            CheckForm()
            _name.Focus()
        End Sub

        Private Sub Prefill(p As PaymentSummary)
            _direction.SelectedIndex = If(p.IsReceivable, 1, 0)
            _name.Text = p.Name
            _person.Text = p.Counterparty
            _amount.Value = p.TotalAmount
            _due.Value = p.DueDate
            _category.SelectedIndex = _categories.FindIndex(Function(c) c.CategoryId = p.CategoryId)
            _reminder.SelectedIndex = Math.Max(0, Array.IndexOf(ReminderChoices, p.ReminderDays))
            _repeat.SelectedIndex = Math.Max(0, Array.IndexOf(RepeatChoices, p.Recurrence))
            _notes.Text = p.Notes
        End Sub

        ' ---------------------------------------------------------------- validation

        Private Sub Touch(field As Control)
            _touched.Add(field)
            CheckForm()
        End Sub

        ''' <summary>Checks every field, shows errors on fields the user has already left, and enables Save when all is well.</summary>
        Private Function CheckForm() As Boolean
            Dim nameError = If(_name.Text.Trim().Length = 0, "Enter a payment name.", Nothing)

            Dim amountError As String = Nothing
            Dim amount = _amount.Value
            If Not amount.HasValue OrElse amount.Value <= 0D Then
                amountError = "Enter an amount more than ₱0.00."
            ElseIf IsEdit AndAlso _existing IsNot Nothing AndAlso amount.Value < _existing.AmountPaid Then
                amountError = "Can't be lower than the " & Format.Money(_existing.AmountPaid) & " already paid."
            End If

            Dim categoryError = If(_category.SelectedIndex < 0, "Choose a category.", Nothing)

            ShowNote(_name, _nameNote, nameError)
            ShowNote(_amount, _amountNote, amountError)
            ShowNote(_category, _categoryNote, categoryError)

            ' Hints that follow the inputs.
            Dim days = CInt((_due.Value - Date.Today).TotalDays)
            _dueNote.Text = If(days < 0, "This date has passed, so the payment will show as overdue.", Format.DuePhrase(days) & ".")
            _dueNote.ForeColor = If(days < 0, Theme.UpcomingText, Theme.TextMuted)
            _notesNote.Text = _notes.Text.Length & " / 500"
            If IsEdit AndAlso _existing IsNot Nothing AndAlso amountError Is Nothing AndAlso _existing.AmountPaid > 0D Then
                _amountNote.Text = Format.Money(_existing.AmountPaid) & " already paid."
            End If

            Dim valid = nameError Is Nothing AndAlso amountError Is Nothing AndAlso categoryError Is Nothing
            _save.Enabled = valid
            Return valid
        End Function

        Private Sub ShowNote(field As Control, note As Label, message As String)
            Dim show = message IsNot Nothing AndAlso _touched.Contains(field)
            note.Text = If(show, message, CStr(note.Tag))
            note.ForeColor = If(show, Theme.OverdueText, Theme.TextMuted)
            If TypeOf field Is PATextBox Then DirectCast(field, PATextBox).HasError = show
            If TypeOf field Is PesoTextBox Then DirectCast(field, PesoTextBox).HasError = show
            If TypeOf field Is PASelect Then DirectCast(field, PASelect).HasError = show
        End Sub

        ' ---------------------------------------------------------------- save

        Private Function BuildPayment() As Payment
            Return New Payment With {
                .PaymentId = _paymentId,
                .UserId = Session.CurrentUser.UserId,
                .ParentPaymentId = If(_existing?.ParentPaymentId, Nothing),
                .Name = _name.Text.Trim(),
                .Direction = If(_direction.SelectedIndex = 1, Payment.DirectionReceivable, Payment.DirectionPayable),
                .Counterparty = _person.Text.Trim(),
                .CategoryId = DirectCast(_category.SelectedItem, Category).CategoryId,
                .TotalAmount = _amount.Value.Value,
                .DueDate = _due.Value,
                .ReminderDays = ReminderChoices(Math.Max(0, _reminder.SelectedIndex)),
                .Recurrence = RepeatChoices(Math.Max(0, _repeat.SelectedIndex)),
                .Notes = _notes.Text.Trim()}
        End Function

        Private Sub SaveClicked(sender As Object, e As EventArgs)
            For Each field As Control In New Control() {_name, _amount, _category}
                _touched.Add(field)
            Next
            If Not CheckForm() Then Return

            Dim payment = BuildPayment()
            Dim savedId = _paymentId
            Try
                If IsEdit Then
                    If Not PaymentRepo.Update(payment) Then
                        PADialog.Info(Me, "This payment isn't available", "It may have been deleted, so the changes weren't saved.", Tone.Warning)
                        Navigator.GoBack()
                        Return
                    End If
                Else
                    savedId = PaymentRepo.Insert(payment)
                End If
            Catch ex As Exception
                Debug.WriteLine("[ucPaymentEditor] " & ex.ToString())
                PADialog.Error(Me, "Couldn't save the payment", Db.FriendlyMessageFor(ex))
                Return
            End Try

            Dim nav = Navigator
            Dim choice = PADialog.Success(Me, If(IsEdit, "Payment updated", "Payment added successfully!"),
                                          {"Payment: " & payment.Name,
                                           "Amount: " & Format.Money(payment.TotalAmount),
                                           "Due date: " & Format.ShortDate(payment.DueDate)},
                                          "View Payment", "Back to Dashboard")
            If choice = DialogChoice.Secondary Then
                nav.ShowRoot(New ucDashboard())
            ElseIf IsEdit Then
                nav.GoBack()                ' back to the details screen, which reloads
            Else
                nav.GoBack()
                nav.Push(New ucPaymentDetails(savedId))
            End If
        End Sub

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            ' Enter saves (except inside Notes, where it starts a new line).
            If keyData = Keys.Enter AndAlso Not _notes.InnerTextBox.Focused AndAlso
               TypeOf ActiveControl IsNot PAButton AndAlso _save.Enabled Then
                SaveClicked(Me, EventArgs.Empty)
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

    End Class

End Namespace
