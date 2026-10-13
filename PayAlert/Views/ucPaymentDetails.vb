Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Payment Details (§7), loaded by payment_id: the balance ring with the due tag and status, the info rows,
    ''' and actions to record a payment (hidden once paid), edit, or delete (red confirm). Reloads whenever shown.
    ''' </summary>
    Public Class ucPaymentDetails
        Inherits PAView

        Private ReadOnly _paymentId As Integer
        Private _payment As PaymentSummary

        Private ReadOnly _record As New PrimaryButton()
        Private ReadOnly _edit As New SecondaryButton()
        Private ReadOnly _delete As New DangerButton()

        Private ReadOnly _ring As New BalanceRing()
        Private ReadOnly _tag As New DueTag()
        Private ReadOnly _pill As New StatusPill()
        Private ReadOnly _duePhrase As New Label()
        Private ReadOnly _paidLine As New Label()
        Private ReadOnly _info As New TableLayoutPanel()

        Public Sub New(paymentId As Integer)
            MyBase.New()
            _paymentId = paymentId
            SidebarKey = "payments"
            Title = "Payment details"

            _record.Text = "Record Payment"
            _record.Icon = IconKind.Plus
            _record.Size = New Size(170, Theme.ButtonHeight)
            _edit.Text = "Edit"
            _edit.Icon = IconKind.Edit
            _edit.Size = New Size(96, Theme.ButtonHeight)
            _delete.Text = "Delete"
            _delete.Filled = False
            _delete.Icon = IconKind.Trash
            _delete.Size = New Size(104, Theme.ButtonHeight)
            AddHeaderAction(_record)
            AddHeaderAction(_edit)
            AddHeaderAction(_delete)
            AddHandler _record.Click, AddressOf RecordClicked
            AddHandler _edit.Click, Sub(s, e) Navigator.Push(New ucPaymentEditor(_paymentId))
            AddHandler _delete.Click, AddressOf DeleteClicked

            Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2, .BackColor = Theme.AppBackground}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 38))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 62))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 66))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 34))
            layout.Controls.Add(BuildBalanceCard(), 0, 0)
            layout.Controls.Add(BuildInfoCard(), 1, 0)
            Dim history = BuildHistoryCard()
            layout.Controls.Add(history, 0, 1)
            layout.SetColumnSpan(history, 2)
            Body.Controls.Add(layout)
        End Sub

        ' ---------------------------------------------------------------- layout

        Private Function BuildBalanceCard() As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 16, 16)}
            _ring.Size = New Size(176, 176)
            _ring.Anchor = AnchorStyles.Top
            Dim ringHost As New Panel() With {.Dock = DockStyle.Top, .Height = 184, .BackColor = Color.Transparent}
            ringHost.Controls.Add(_ring)
            AddHandler ringHost.Layout, Sub(s, e) _ring.Left = (ringHost.ClientSize.Width - _ring.Width) \ 2

            ' Due tag on the left; the status pill with "Due in 3 days" under it on the right. Centered under the ring.
            Dim strip As New FlowLayoutPanel() With {.AutoSize = True, .WrapContents = False, .BackColor = Color.Transparent, .Margin = New Padding(0)}
            _tag.Margin = New Padding(0, 0, 14, 0)
            Dim stack As New FlowLayoutPanel() With {
                .AutoSize = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .BackColor = Color.Transparent,
                .Margin = New Padding(0, 4, 0, 0)}
            _pill.Margin = New Padding(0, 0, 0, 4)
            _duePhrase.AutoSize = True
            _duePhrase.Font = Theme.FontBodySemibold
            _duePhrase.ForeColor = Theme.TextMuted
            _duePhrase.BackColor = Color.Transparent
            _duePhrase.Margin = New Padding(2, 0, 0, 0)
            stack.Controls.Add(_pill)
            stack.Controls.Add(_duePhrase)
            strip.Controls.Add(_tag)
            strip.Controls.Add(stack)
            Dim stripHost As New Panel() With {.Dock = DockStyle.Top, .Height = 66, .BackColor = Color.Transparent}
            stripHost.Controls.Add(strip)
            AddHandler stripHost.Layout, Sub(s, e) strip.Left = Math.Max(0, (stripHost.ClientSize.Width - strip.Width) \ 2)
            AddHandler strip.SizeChanged, Sub(s, e) strip.Left = Math.Max(0, (stripHost.ClientSize.Width - strip.Width) \ 2)

            _paidLine.Dock = DockStyle.Top
            _paidLine.Height = 22
            _paidLine.TextAlign = ContentAlignment.MiddleCenter
            _paidLine.Font = Theme.FontCaption
            _paidLine.ForeColor = Theme.TextMuted
            _paidLine.BackColor = Color.Transparent

            card.Controls.Add(_paidLine)
            card.Controls.Add(stripHost)
            card.Controls.Add(ringHost)
            Return card
        End Function

        Private Function BuildInfoCard() As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 0, 16), .Padding = New Padding(20, 14, 20, 12)}
            _info.Dock = DockStyle.Fill
            _info.ColumnCount = 2
            _info.BackColor = Color.Transparent
            _info.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150))
            _info.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Dim heading As New Label() With {
                .Text = "Details", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 30}
            card.Controls.Add(_info)
            card.Controls.Add(heading)
            Return card
        End Function

        Private Function BuildHistoryCard() As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Margin = New Padding(0), .Padding = New Padding(20, 14, 20, 12)}
            Dim note As New Label() With {
                .Text = "Each recorded payment for this record (date, amount, method and note) will be listed here once Phase 6 adds Record Payment.",
                .Font = Theme.FontBody, .ForeColor = Theme.TextMuted, .BackColor = Color.Transparent, .Dock = DockStyle.Fill}
            Dim heading As New Label() With {
                .Text = "Payment History", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 30}
            card.Controls.Add(note)
            card.Controls.Add(heading)
            Return card
        End Function

        ' ---------------------------------------------------------------- data

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Try
                _payment = PaymentRepo.GetSummary(_paymentId, Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucPaymentDetails] " & ex.ToString())
                PADialog.Error(Me, "Can't load this payment", Db.FriendlyMessageFor(ex))
                Return
            End Try
            If _payment Is Nothing Then
                PADialog.Info(Me, "This payment isn't available", "It may have been deleted.", Tone.Warning)
                Navigator.GoBack()
                Return
            End If
            Fill(_payment)
        End Sub

        Private Sub Fill(p As PaymentSummary)
            Title = p.Name
            Subtitle = p.DirectionLabel & "  ·  " & p.CategoryName & If(p.Counterparty.Length > 0, "  ·  " & p.Counterparty, "")
            _record.Text = If(p.IsReceivable, "Record Collection", "Record Payment")
            SetHeaderActionVisible(_record, Not p.IsPaid)

            _ring.SetValues(p.TotalAmount, p.AmountPaid, animate:=False)
            _ring.Replay()
            _tag.DueDate = p.DueDate
            _tag.Status = p.Status
            _pill.Status = p.Status
            _duePhrase.Text = If(p.IsPaid, "Fully paid", Format.DuePhrase(p.DaysUntilDue))
            _duePhrase.ForeColor = If(p.Status = Theme.StatusOverdue, Theme.OverdueText, Theme.TextMuted)
            _paidLine.Text = Format.Money(p.AmountPaid) & If(p.IsReceivable, " collected", " paid") & "  ·  " &
                             CInt(Math.Round(p.Progress * 100)) & "% of " & Format.Money(p.TotalAmount)

            _info.SuspendLayout()
            _info.Controls.Clear()
            _info.RowStyles.Clear()
            _info.RowCount = 0
            AddInfo("Direction", p.DirectionLabel)
            AddInfo(If(p.IsReceivable, "Amount collected", "Amount paid"), Format.Money(p.AmountPaid))
            AddInfo("Total amount", Format.Money(p.TotalAmount))
            AddInfo("Due date", Format.ShortDate(p.DueDate) & "  ·  " & Format.RelativeDay(p.DaysUntilDue))
            AddInfo("Category", p.CategoryName)
            AddInfo("Person / Company", If(p.Counterparty.Length = 0, "—", p.Counterparty))
            AddInfo("Reminder", p.ReminderDays & If(p.ReminderDays = 1, " day", " days") & " before")
            AddInfo("Repeats", If(p.IsRecurring, If(p.Recurrence = Payment.RecurrenceWeekly, "Every week", "Every month"), "No"))
            AddInfo("Notes", If(p.Notes.Length = 0, "—", p.Notes), wrap:=True)
            _info.ResumeLayout()
        End Sub

        Private Sub AddInfo(label As String, value As String, Optional wrap As Boolean = False)
            Dim row = _info.RowCount
            _info.RowCount = row + 1
            _info.RowStyles.Add(If(wrap, New RowStyle(SizeType.Percent, 100), New RowStyle(SizeType.Absolute, 29)))
            _info.Controls.Add(New Label() With {
                .Text = label, .Font = Theme.FontBody, .ForeColor = Theme.TextMuted, .BackColor = Color.Transparent,
                .Dock = DockStyle.Fill, .TextAlign = If(wrap, ContentAlignment.TopLeft, ContentAlignment.MiddleLeft),
                .Padding = New Padding(0, If(wrap, 6, 0), 0, 0)}, 0, row)
            _info.Controls.Add(New Label() With {
                .Text = value, .Font = Theme.FontBodySemibold, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Fill, .AutoEllipsis = Not wrap,
                .TextAlign = If(wrap, ContentAlignment.TopLeft, ContentAlignment.MiddleLeft),
                .Padding = New Padding(0, If(wrap, 6, 0), 0, 0)}, 1, row)
        End Sub

        ' ---------------------------------------------------------------- actions

        Private Sub RecordClicked(sender As Object, e As EventArgs)
            PADialog.Info(Me, If(_payment?.IsReceivable, "Recording collections comes next", "Recording payments comes next"),
                          "Phase 6 adds the Record Payment window: partial or full amounts, the date and method, and the history list below.",
                          Tone.Brand)
        End Sub

        Private Sub DeleteClicked(sender As Object, e As EventArgs)
            If _payment Is Nothing Then Return
            Dim name = _payment.Name
            If Not PADialog.Confirm(Me, "Delete Payment?",
                                    "Are you sure you want to delete """ & name & """? This can't be undone.",
                                    "Yes, Delete", "Cancel", Tone.Danger) Then Return
            Try
                PaymentRepo.SoftDelete(_paymentId, Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucPaymentDetails] " & ex.ToString())
                PADialog.Error(Me, "Couldn't delete the payment", Db.FriendlyMessageFor(ex))
                Return
            End Try
            Dim host = FindForm()
            Navigator.GoBack()
            PAToast.Notify(host, """" & name & """ deleted", Tone.Danger)
        End Sub

    End Class

End Namespace
