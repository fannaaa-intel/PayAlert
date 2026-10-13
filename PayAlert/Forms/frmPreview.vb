Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq
Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Dialogs
Imports PayAlert.Services

Namespace Forms

    ''' <summary>
    ''' TEMPORARY (Phases 2-3): shows every shared control and every dialog/toast tone, and stands in for the
    ''' main window after login (signed-in user + Log out). Replaced by frmMain in Phase 4.
    ''' </summary>
    Public Class frmPreview
        Inherits Form

        Private ReadOnly _sidebar As New Panel()
        Private ReadOnly _header As New PageHeader()
        Private ReadOnly _pageHost As New Panel()
        Private ReadOnly _pages As New Dictionary(Of SidebarButton, Panel)
        Private ReadOnly _pageTitles As New Dictionary(Of SidebarButton, String())
        Private ReadOnly _rings As New List(Of BalanceRing)
        Private _logoutButton As SidebarButton

        ''' <summary>Raised after the user confirms Log out.</summary>
        Public Event LoggedOut As EventHandler

        Public Sub New()
            SuspendLayout()
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            Text = "PayAlert - Component preview"
            Size = New Size(1280, 760)
            MinimumSize = New Size(1100, 680)
            StartPosition = FormStartPosition.CenterScreen
            WindowState = FormWindowState.Maximized
            BackColor = Theme.AppBackground
            ForeColor = Theme.TextPrimary
            Font = Theme.FontBody
            DoubleBuffered = True

            BuildContent()
            BuildSidebar()
            ResumeLayout(False)
            PerformLayout()
        End Sub

        ' ================================================================ shell

        Private Sub BuildSidebar()
            _sidebar.Dock = DockStyle.Left
            _sidebar.Width = Theme.SidebarWidth
            _sidebar.BackColor = Theme.Surface
            AddHandler _sidebar.Paint, AddressOf PaintSidebar

            Dim items = {
                Tuple.Create("Controls", IconKind.Dashboard, 0),
                Tuple.Create("Cards and rows", IconKind.Payments, 0),
                Tuple.Create("Dialogs", IconKind.Bell, 4),
                Tuple.Create("Empty state", IconKind.Inbox, 0)}
            Dim pages = {BuildControlsPage(), BuildCardsPage(), BuildDialogsPage(), BuildEmptyPage()}
            Dim subtitles = {
                "Buttons, fields, status pills, due tags, the balance ring and summary cards",
                "Payment rows (the due tag's colored stub is the status edge) and notification cards",
                "Every PADialog tone over the dim overlay, plus toasts",
                "What a screen shows when there's nothing to list yet"}

            Dim first As SidebarButton = Nothing
            For i = 0 To items.Length - 1
                Dim button As New SidebarButton() With {
                    .Text = items(i).Item1, .Icon = items(i).Item2, .BadgeCount = items(i).Item3,
                    .Location = New Point(12, 84 + i * 48), .Size = New Size(196, 44), .TabIndex = i}
                AddHandler button.Click, AddressOf SidebarClicked
                _sidebar.Controls.Add(button)
                _pages(button) = pages(i)
                _pageTitles(button) = {items(i).Item1, subtitles(i)}
                If first Is Nothing Then first = button
            Next

            _logoutButton = New SidebarButton() With {
                .Text = "Log out", .Icon = IconKind.Logout,
                .Location = New Point(12, 760 - 40 - 44 - 24), .Size = New Size(196, 44),
                .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left, .TabIndex = items.Length}
            AddHandler _logoutButton.Click,
                Sub(s, e)
                    If PADialog.Confirm(Me, "Log out of PayAlert?", "You'll need your email and password to log in again.",
                                        "Log out", "Stay", Tone.Neutral, IconKind.Logout) Then
                        RaiseEvent LoggedOut(Me, EventArgs.Empty)
                    End If
                End Sub
            _sidebar.Controls.Add(_logoutButton)
            AddHandler _sidebar.Resize, Sub(s, e) _sidebar.Invalidate()

            Controls.Add(_sidebar)
            ShowPage(first)
        End Sub

        Private Sub PaintSidebar(sender As Object, e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim s = DeviceDpi / 96.0F
            Using pen As New Pen(Theme.Border)
                g.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height)
            End Using
            Dim mark As New RectangleF(22 * s, 24 * s, 30 * s, 30 * s)
            Gfx.DrawLogoMark(g, mark)
            Dim x = CInt(mark.Right + 10 * s)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim paySize = TextRenderer.MeasureText(g, "Pay", Theme.FontWordmark, Size.Empty, flags)
            Dim y = CInt(mark.Y + (mark.Height - paySize.Height) / 2.0F)
            TextRenderer.DrawText(g, "Pay", Theme.FontWordmark, New Point(x, y), Theme.TextPrimary, flags)
            TextRenderer.DrawText(g, "Alert", Theme.FontWordmark, New Point(x + paySize.Width, y), Theme.Brand, flags)

            ' Signed-in user, just above Log out: initials disc, name and role.
            Dim user = Session.CurrentUser
            If user Is Nothing OrElse _logoutButton Is Nothing Then Return
            Dim top = _logoutButton.Top - CInt(64 * s)
            Using rule As New Pen(Theme.Border)
                g.DrawLine(rule, CInt(20 * s), top - CInt(10 * s), _sidebar.Width - CInt(20 * s), top - CInt(10 * s))
            End Using
            Dim disc As New RectangleF(20 * s, top + 6 * s, 36 * s, 36 * s)
            Using fill As New SolidBrush(Theme.BrandSoft)
                g.FillEllipse(fill, disc)
            End Using
            Dim initials = String.Concat(user.FullName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries).
                                         Take(2).Select(Function(part) Char.ToUpperInvariant(part(0))))
            TextRenderer.DrawText(g, initials, Theme.FontCaptionBold, Rectangle.Round(disc), Theme.BrandDark,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Dim textX = CInt(disc.Right + 10 * s)
            Dim textWidth = _sidebar.Width - textX - CInt(16 * s)
            TextRenderer.DrawText(g, user.FullName, Theme.FontBodyBold, New Rectangle(textX, CInt(top + 6 * s), textWidth, CInt(20 * s)),
                                  Theme.TextPrimary, flags Or TextFormatFlags.EndEllipsis)
            TextRenderer.DrawText(g, If(user.IsAdmin, "Administrator", "User") & "  ·  " & user.Email, Theme.FontCaption,
                                  New Rectangle(textX, CInt(top + 26 * s), textWidth, CInt(18 * s)), Theme.TextMuted, flags Or TextFormatFlags.EndEllipsis)
        End Sub

        Private Sub BuildContent()
            Dim content As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(Theme.ContentPadding), .BackColor = Theme.AppBackground}

            _header.Dock = DockStyle.Top
            _header.Height = 76
            _header.Width = 1280 - Theme.SidebarWidth - Theme.ContentPadding * 2

            _pageHost.Dock = DockStyle.Fill
            Dim spacer As New Panel() With {.Dock = DockStyle.Top, .Height = 20}
            content.Controls.Add(_pageHost)
            content.Controls.Add(spacer)
            content.Controls.Add(_header)
            Controls.Add(content)
        End Sub

        Private Sub SidebarClicked(sender As Object, e As EventArgs)
            ShowPage(DirectCast(sender, SidebarButton))
        End Sub

        Private Sub ShowPage(button As SidebarButton)
            For Each pair In _pages
                pair.Key.IsActive = pair.Key Is button
                pair.Value.Visible = pair.Key Is button
            Next
            _header.Title = _pageTitles(button)(0)
            _header.Subtitle = _pageTitles(button)(1)
            For Each ring In _rings
                If _pages(button).Contains(ring) Then ring.Replay()
            Next
        End Sub

        Private Function NewPage() As Panel
            Dim page As New Panel() With {.Dock = DockStyle.Fill, .Visible = False}
            _pageHost.Controls.Add(page)
            Return page
        End Function

        ' ================================================================ helpers

        Private Shared Function MakeCard(title As String, ByRef body As FlowLayoutPanel, Optional rightMargin As Integer = 16) As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, rightMargin, 0)}
            body = New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent, .WrapContents = True, .Margin = New Padding(0)}
            Dim heading As New Label() With {
                .Text = title, .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 34}
            card.Controls.Add(body)
            card.Controls.Add(heading)
            Return card
        End Function

        Private Shared Function Caption(text As String, Optional width As Integer = 240, Optional color As Color = Nothing) As Label
            ' AutoSize with a maximum width: long captions wrap instead of being cut off.
            Return New Label() With {
                .Text = text, .Font = Theme.FontCaption, .ForeColor = If(color.IsEmpty, Theme.TextMuted, color),
                .BackColor = Color.Transparent, .AutoSize = True, .MaximumSize = New Size(width, 0), .MinimumSize = New Size(width, 0),
                .Margin = New Padding(0, 4, 0, 4)}
        End Function

        Private Shared Function Breaker() As Panel
            Return New Panel() With {.Size = New Size(1, 1), .Margin = New Padding(0)}
        End Function

        Private Sub Toast(message As String, Optional tone As Tone = Tone.Neutral)
            PAToast.Notify(Me, message, tone)
        End Sub

        ' ================================================================ page 1: controls

        Private Function BuildControlsPage() As Panel
            Dim page = NewPage()
            Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 4, .BackColor = Color.Transparent}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 136))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 212))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 220))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            ' Row 1: summary cards.
            Dim summary As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 1, .Margin = New Padding(0, 0, 0, 16), .BackColor = Color.Transparent}
            For i = 0 To 3
                summary.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            Next
            Dim cards = {
                New SummaryCard() With {.Caption = "DUE IN 7 DAYS", .Value = "4", .Icon = IconKind.Calendar, .AccentColor = Theme.Brand},
                New SummaryCard() With {.Caption = "DUE TOMORROW", .Value = "1", .Icon = IconKind.Clock, .AccentColor = Theme.UpcomingText},
                New SummaryCard() With {.Caption = "OVERDUE", .Value = "2", .Icon = IconKind.Alert, .AccentColor = Theme.OverdueText, .ValueColor = Theme.OverdueText},
                New SummaryCard() With {.Caption = "TO COLLECT", .Value = Format.Money(2800D), .Icon = IconKind.ArrowIn, .AccentColor = Theme.Brand, .Hint = "View receivables"}}
            For i = 0 To cards.Length - 1
                Dim card = cards(i)
                card.Dock = DockStyle.Fill
                card.Margin = New Padding(0, 0, If(i < cards.Length - 1, 16, 0), 0)
                AddHandler card.Click, Sub(s, e) Toast("This card will open Payments filtered to """ & DirectCast(s, SummaryCard).Caption.ToLowerInvariant() & """.")
                summary.Controls.Add(card, i, 0)
            Next
            layout.Controls.Add(summary, 0, 0)
            layout.SetColumnSpan(summary, 2)

            Dim flow As FlowLayoutPanel = Nothing

            ' Row 2 left: buttons, each style named above its sample.
            Dim buttonsCard = MakeCard("Buttons", flow)
            buttonsCard.Margin = New Padding(0, 0, 16, 16)
            Dim samples As Tuple(Of String, PAButton)() = {
                Tuple.Create("Primary", CType(New PrimaryButton() With {.Text = "Add Payment", .Icon = IconKind.Plus}, PAButton)),
                Tuple.Create("Secondary", CType(New SecondaryButton() With {.Text = "Cancel"}, PAButton)),
                Tuple.Create("Danger", CType(New DangerButton() With {.Text = "Yes, Delete"}, PAButton)),
                Tuple.Create("Danger text", CType(New DangerButton() With {.Text = "Delete", .Filled = False, .Icon = IconKind.Trash}, PAButton)),
                Tuple.Create("Ghost", New PAButton() With {.Text = "Back", .Kind = ButtonKind.Ghost, .Icon = IconKind.Back}),
                Tuple.Create("Disabled", CType(New PrimaryButton() With {.Text = "Save Payment", .Enabled = False}, PAButton))}
            For Each sample In samples
                Dim b = sample.Item2
                b.Size = New Size(150, Theme.ButtonHeight)
                Dim label = b.Text
                AddHandler b.Click, Sub(s, e) Toast("""" & label & """ clicked.")
                flow.Controls.Add(Spec(sample.Item1, b, 166))
            Next
            ' Always three per row, so the grid stays 3 x 2 on any window width.
            flow.SetFlowBreak(flow.Controls(2), True)
            layout.Controls.Add(buttonsCard, 0, 1)

            ' Row 2 right: the amount field, normal and with an inline error.
            Dim fieldsCard = MakeCard("Amount field", flow, 0)
            fieldsCard.Margin = New Padding(0, 0, 0, 16)
            Dim amount As New PesoTextBox() With {.Size = New Size(166, Theme.FieldHeight)}
            amount.Value = 1500D
            Dim normal = Spec("Normal", amount, 176, "You typed " & Format.Money(1500D))
            Dim helper = DirectCast(normal.Controls(normal.Controls.Count - 1), Label)
            AddHandler amount.ValueChanged,
                Sub(s, e) helper.Text = If(amount.Value.HasValue, "You typed " & Format.Money(amount.Value.Value), "Empty")
            flow.Controls.Add(normal)
            Dim bad As New PesoTextBox() With {.Size = New Size(166, Theme.FieldHeight), .HasError = True}
            flow.Controls.Add(Spec("With an error", bad, 176, "Must be more than ₱0.00.", Theme.OverdueText))
            layout.Controls.Add(fieldsCard, 1, 1)

            ' Row 3 left: one column per status. Pill on top, its due tag under it, and what it means.
            Dim statusCard As New SoftCard() With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 16, 0)}
            Dim table As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 3, .BackColor = Color.Transparent}
            For i = 0 To 3
                table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            Next
            table.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            table.RowStyles.Add(New RowStyle(SizeType.Absolute, 66))
            table.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
            Dim today = Date.Today
            Dim examples = {
                Tuple.Create(Theme.StatusUpcoming, 1, "Due tomorrow"),
                Tuple.Create(Theme.StatusPartial, 6, "Part paid, due in 6 days"),
                Tuple.Create(Theme.StatusPaid, -2, "Fully paid"),
                Tuple.Create(Theme.StatusOverdue, -4, "4 days overdue")}
            For i = 0 To examples.Length - 1
                Dim example = examples(i)
                table.Controls.Add(New StatusPill() With {.Status = example.Item1, .Anchor = AnchorStyles.None}, i, 0)
                table.Controls.Add(New DueTag() With {.DueDate = today.AddDays(example.Item2), .Status = example.Item1, .Anchor = AnchorStyles.None}, i, 1)
                table.Controls.Add(New Label() With {
                    .Text = example.Item3, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .BackColor = Color.Transparent,
                    .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .AutoEllipsis = True, .Margin = New Padding(0)}, i, 2)
            Next
            Dim statusNote As New Label() With {
                .Text = "Each status has one color. A due tag's colored stub matches its status.",
                .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 22, .AutoEllipsis = True}
            Dim statusHeading As New Label() With {
                .Text = "Status", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 30}
            statusCard.Controls.Add(table)
            statusCard.Controls.Add(statusNote)
            statusCard.Controls.Add(statusHeading)
            layout.Controls.Add(statusCard, 0, 2)

            ' Row 3 right: balance ring with the numbers spelled out beside it.
            Dim ringCard = MakeCard("Balance ring", flow, 0)
            ringCard.Margin = New Padding(0)
            Dim ring As New BalanceRing() With {.Size = New Size(150, 150), .Margin = New Padding(0, 0, 18, 0)}
            ring.SetValues(5000D, 2000D, False)
            _rings.Add(ring)
            flow.Controls.Add(ring)
            Dim side As New FlowLayoutPanel() With {.FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Size = New Size(200, 150),
                                                     .BackColor = Color.Transparent, .Margin = New Padding(0, 14, 0, 0)}
            side.Controls.Add(New Label() With {.Text = "Apartment Rent", .Font = Theme.FontBodyBold, .ForeColor = Theme.TextPrimary,
                                                .AutoSize = True, .BackColor = Color.Transparent, .Margin = New Padding(0, 0, 0, 2)})
            Dim paidLine = Caption("Paid " & Format.Money(2000D) & " of " & Format.Money(5000D), 200)
            side.Controls.Add(paidLine)
            Dim record As New PAButton() With {.Text = "Record ₱1,000", .Kind = ButtonKind.Secondary, .Size = New Size(150, Theme.ButtonHeight),
                                               .Margin = New Padding(0, 14, 0, 0)}
            AddHandler record.Click,
                Sub(s, e)
                    Dim paid = If(ring.Paid >= ring.Total, 0D, ring.Paid + 1000D)
                    ring.SetValues(ring.Total, paid)
                    paidLine.Text = "Paid " & Format.Money(paid) & " of " & Format.Money(ring.Total)
                    If paid >= ring.Total Then Toast("Apartment Rent is now fully paid.", Tone.Success)
                End Sub
            side.Controls.Add(record)
            flow.Controls.Add(side)
            layout.Controls.Add(ringCard, 1, 2)

            page.Controls.Add(layout)
            Return page
        End Function

        ''' <summary>A labelled sample: small caption on top, the control, and an optional helper line under it.</summary>
        Private Shared Function Spec(label As String, sample As Control, width As Integer,
                                     Optional helper As String = Nothing, Optional helperColor As Color = Nothing) As Panel
            Dim cell As New Panel() With {.BackColor = Color.Transparent, .Margin = New Padding(0, 0, 8, 8)}
            Dim y = 0
            If label.Length > 0 Then
                cell.Controls.Add(New Label() With {.Text = label.ToUpperInvariant(), .Font = Theme.FontSmallCaps, .ForeColor = Theme.TextMuted,
                                                    .BackColor = Color.Transparent, .Location = New Point(0, 0), .Size = New Size(width, 16)})
                y = 18
            End If
            sample.Location = New Point(0, y)
            sample.Margin = New Padding(0)
            cell.Controls.Add(sample)
            y += sample.Height
            If helper IsNot Nothing Then
                cell.Controls.Add(New Label() With {.Text = helper, .Font = Theme.FontCaption,
                                                    .ForeColor = If(helperColor.IsEmpty, Theme.TextMuted, helperColor),
                                                    .BackColor = Color.Transparent, .Location = New Point(0, y + 4), .Size = New Size(width, 20)})
                y += 24
            End If
            cell.Size = New Size(width, y)
            Return cell
        End Function

        ' ================================================================ page 2: cards and rows

        Private Function BuildCardsPage() As Panel
            Dim page = NewPage()
            Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Color.Transparent}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40))

            Dim left As New Panel() With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 20, 0)}
            Dim today = Date.Today
            Dim rows = {
                MakePaymentRow("BNPL Installment", "Buy Now Pay Later  ·  Online Store  ·  I Owe", Theme.StatusUpcoming, today.AddDays(1), 1500D, 1500D),
                MakePaymentRow("Apartment Rent", "Rent  ·  Landlord  ·  I Owe", Theme.StatusPartial, today.AddDays(6), 3000D, 5000D),
                MakePaymentRow("Water Bill", "Bills  ·  Water Utility  ·  I Owe", Theme.StatusPaid, today.AddDays(-2), 0D, 500D),
                MakePaymentRow("Loan to Pedro", "Loan  ·  Pedro Reyes  ·  Owed to Me", Theme.StatusOverdue, today.AddDays(-1), 800D, 800D)}
            ' Docked top-down: add in reverse so the first row ends up on top.
            For i = rows.Length - 1 To 0 Step -1
                left.Controls.Add(rows(i))
                If i > 0 Then left.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 12})
            Next
            left.Controls.Add(New Label() With {
                .Text = "Payment rows", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary,
                .Dock = DockStyle.Top, .Height = 34, .BackColor = Color.Transparent})
            layout.Controls.Add(left, 0, 0)

            Dim right As New Panel() With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
            Dim notes = {
                MakeNotificationCard("Due Tomorrow", "BNPL Installment payment of " & Format.Money(1500D) & " is due tomorrow.", "10 min ago", Theme.Brand, True),
                MakeNotificationCard("Overdue", "Cash Loan payment of " & Format.Money(1000D) & " is 4 days overdue.", "1 hr ago", Theme.OverdueText, True),
                MakeNotificationCard("Due in 3 Days", "Home Internet payment of " & Format.Money(1699D) & " is due in 3 days.", "Yesterday", Theme.UpcomingText, False)}
            For i = notes.Length - 1 To 0 Step -1
                right.Controls.Add(notes(i))
                If i > 0 Then right.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 12})
            Next
            right.Controls.Add(New Label() With {
                .Text = "Notification cards (unread, unread, read)", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary,
                .Dock = DockStyle.Top, .Height = 34, .BackColor = Color.Transparent})
            layout.Controls.Add(right, 1, 0)

            page.Controls.Add(layout)
            Return page
        End Function

        Private Function MakePaymentRow(name As String, meta As String, status As String, due As Date, balance As Decimal, total As Decimal) As SoftCard
            Dim row As New SoftCard() With {.Dock = DockStyle.Top, .Height = 80, .Hoverable = True,
                                            .Padding = New Padding(14, 10, 18, 10), .Width = 640}
            Dim faceHeight = 80 - 20 - 3

            Dim amounts As New Panel() With {.Dock = DockStyle.Right, .Width = 150, .BackColor = Color.Transparent, .Padding = New Padding(0, 8, 0, 0)}
            amounts.Controls.Add(New Label() With {
                .Text = "of " & Format.Money(total), .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .Dock = DockStyle.Top,
                .Height = 20, .TextAlign = ContentAlignment.TopRight, .BackColor = Color.Transparent})
            amounts.Controls.Add(New Label() With {
                .Text = Format.Money(balance), .Font = Theme.FontBodyBold, .ForeColor = Theme.TextPrimary, .Dock = DockStyle.Top,
                .Height = 22, .TextAlign = ContentAlignment.BottomRight, .BackColor = Color.Transparent})

            Dim pillHolder As New Panel() With {.Dock = DockStyle.Right, .Width = 130, .Height = faceHeight, .BackColor = Color.Transparent}
            Dim pill As New StatusPill() With {.Status = status}
            pill.Location = New Point(0, (faceHeight - pill.Height) \ 2)
            pill.Anchor = AnchorStyles.Left
            pillHolder.Controls.Add(pill)

            Dim middle As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent, .Padding = New Padding(14, 6, 0, 0)}
            middle.Controls.Add(New Label() With {
                .Text = meta, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .Dock = DockStyle.Top, .Height = 20,
                .AutoEllipsis = True, .BackColor = Color.Transparent})
            middle.Controls.Add(New Label() With {
                .Text = name, .Font = Theme.FontBodyBold, .ForeColor = Theme.TextPrimary, .Dock = DockStyle.Top, .Height = 24,
                .AutoEllipsis = True, .BackColor = Color.Transparent})

            Dim tag As New DueTag() With {.DueDate = due, .Status = status, .Dock = DockStyle.Left, .Width = 54}

            row.Controls.Add(middle)
            row.Controls.Add(pillHolder)
            row.Controls.Add(amounts)
            row.Controls.Add(tag)
            AddHandler row.Click, Sub(s, e) Toast("This row will open the details for """ & name & """.")
            Return row
        End Function

        Private Function MakeNotificationCard(title As String, message As String, time As String, dotColor As Color, unread As Boolean) As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Top, .Height = 88, .Hoverable = True, .Width = 400,
                                             .FillColor = If(unread, Theme.BrandSoft, Theme.Surface),
                                             .BorderColor = If(unread, Theme.Blend(Theme.Border, Theme.Brand, 0.25), Theme.Border),
                                             .Padding = New Padding(16, 14, 16, 12)}
            Dim dot As New Panel() With {.Dock = DockStyle.Left, .Width = 22, .BackColor = Color.Transparent}
            AddHandler dot.Paint,
                Sub(s, e)
                    Gfx.HighQuality(e.Graphics)
                    Dim d = 10 * dot.DeviceDpi / 96.0F
                    Using brush As New SolidBrush(dotColor)
                        e.Graphics.FillEllipse(brush, 0, 5 * dot.DeviceDpi / 96.0F, d, d)
                    End Using
                End Sub
            Dim body As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent}
            body.Controls.Add(New Label() With {
                .Text = message, .Font = Theme.FontBody, .ForeColor = Theme.TextMuted, .Dock = DockStyle.Fill, .BackColor = Color.Transparent})
            Dim top As New Panel() With {.Dock = DockStyle.Top, .Height = 24, .BackColor = Color.Transparent}
            top.Controls.Add(New Label() With {
                .Text = title, .Font = Theme.FontBodyBold, .ForeColor = Theme.TextPrimary, .Dock = DockStyle.Fill, .BackColor = Color.Transparent})
            top.Controls.Add(New Label() With {
                .Text = time, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .Dock = DockStyle.Right, .Width = 90,
                .TextAlign = ContentAlignment.TopRight, .BackColor = Color.Transparent})
            body.Controls.Add(top)
            card.Controls.Add(body)
            card.Controls.Add(dot)
            AddHandler card.Click, Sub(s, e) Toast("Marked """ & title & """ as read.", Tone.Success)
            Return card
        End Function

        ' ================================================================ page 3: dialogs and toasts

        Private Function BuildDialogsPage() As Panel
            Dim page = NewPage()
            Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 2, .BackColor = Color.Transparent}
            For i = 0 To 2
                layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3F))
            Next
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 50))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 50))

            Dim flow As FlowLayoutPanel = Nothing

            Dim confirmCard = MakeCard("Confirm", flow)
            confirmCard.Margin = New Padding(0, 0, 16, 16)
            flow.Controls.Add(Caption("Danger starts focus on Cancel, so Enter can't delete by accident.", 250))
            AddDemo(flow, "Delete payment", ButtonKind.Danger,
                    Sub()
                        Dim yes = PADialog.Confirm(Me, "Delete Payment?",
                                                   "Are you sure you want to delete ""Electricity Bill""? This can't be undone.",
                                                   "Yes, Delete", "Cancel", Tone.Danger)
                        Toast(If(yes, """Electricity Bill"" deleted.", "Nothing was deleted."), If(yes, Tone.Danger, Tone.Neutral))
                    End Sub)
            AddDemo(flow, "Log out", ButtonKind.Secondary,
                    Sub()
                        Dim yes = PADialog.Confirm(Me, "Log out of PayAlert?", "You'll need your email and password to sign in again.",
                                                   "Log out", "Stay signed in", Tone.Neutral, IconKind.Logout)
                        Toast(If(yes, "You chose to log out.", "You're still signed in."))
                    End Sub)
            AddDemo(flow, "Brand confirm", ButtonKind.Secondary,
                    Sub()
                        Dim yes = PADialog.Confirm(Me, "Make this payment recurring?",
                                                   "PayAlert will create next month's ""Streaming Plan"" automatically once this one is paid.",
                                                   "Make recurring", "Not now", Tone.Brand)
                        Toast(If(yes, "Streaming Plan is now recurring.", "No change made."), If(yes, Tone.Success, Tone.Neutral))
                    End Sub)
            layout.Controls.Add(confirmCard, 0, 0)

            Dim infoCard = MakeCard("Info (all tones)", flow)
            infoCard.Margin = New Padding(0, 0, 16, 16)
            flow.Controls.Add(Caption("One OK button. Enter or Esc closes.", 250))
            AddDemo(flow, "Neutral", ButtonKind.Secondary,
                    Sub() PADialog.Info(Me, "Reminders run in the app", "PayAlert checks your due dates when you log in and every 5 minutes while it's open.", Tone.Neutral))
            AddDemo(flow, "Warning", ButtonKind.Secondary,
                    Sub() PADialog.Info(Me, "Home Internet is due in 3 days", "Its balance is " & Format.Money(1699D) & ". Record a payment once you've paid it.", Tone.Warning))
            AddDemo(flow, "Brand", ButtonKind.Secondary,
                    Sub() PADialog.Info(Me, "Welcome to PayAlert", "Add the payments you owe or are owed, and PayAlert will remind you before each due date.", Tone.Brand))
            AddDemo(flow, "Success", ButtonKind.Secondary,
                    Sub() PADialog.Info(Me, "All caught up", "You have no overdue payments right now.", Tone.Success))
            layout.Controls.Add(infoCard, 1, 0)

            Dim successCard = MakeCard("Success", flow, 0)
            successCard.Margin = New Padding(0, 0, 0, 16)
            flow.Controls.Add(Caption("A short receipt, with View Payment / Back to Dashboard.", 250))
            AddDemo(flow, "Payment added", ButtonKind.Primary,
                    Sub()
                        Dim choice = PADialog.Success(Me, "Payment added successfully!",
                                                      {"Payment: Electricity Bill", "Amount: " & Format.Money(2300D), "Due date: " & Format.ShortDate(Date.Today.AddDays(10))},
                                                      "View Payment", "Back to Dashboard")
                        Toast("You chose: " & If(choice = DialogChoice.Primary, "View Payment", If(choice = DialogChoice.Secondary, "Back to Dashboard", "closed with Esc")))
                    End Sub)
            AddDemo(flow, "Payment recorded", ButtonKind.Secondary,
                    Sub()
                        PADialog.Success(Me, "Payment recorded",
                                         {"Payment: Apartment Rent", "Amount paid: " & Format.Money(1000D), "Balance left: " & Format.Money(2000D)},
                                         "View Payment", "Back to Dashboard")
                    End Sub)
            layout.Controls.Add(successCard, 2, 0)

            Dim errorCard = MakeCard("Error", flow)
            flow.Controls.Add(Caption("Friendly words only, never codes or exception text.", 250))
            AddDemo(flow, "Database down", ButtonKind.Secondary,
                    Sub() PADialog.Error(Me, "Can't reach the database", Data.Db.MsgCannotConnect))
            AddDemo(flow, "Unexpected", ButtonKind.Secondary,
                    Sub() PADialog.Error(Me, "Something went wrong", "PayAlert couldn't finish that. Please try again."))
            layout.Controls.Add(errorCard, 0, 1)

            Dim toastCard = MakeCard("Toasts", flow)
            flow.Controls.Add(Caption("Bottom-right, 3 seconds; hovering pauses it.", 250))
            AddDemo(flow, "Settings saved", ButtonKind.Secondary, Sub() PAToast.Notify(Me, "Settings saved", Tone.Success))
            AddDemo(flow, "With View link", ButtonKind.Secondary,
                    Sub() PAToast.Notify(Me, "Payment updated", Tone.Brand, "View", Sub() PADialog.Info(Me, "View clicked", "This would open the payment's details.", Tone.Brand)))
            AddDemo(flow, "Warning", ButtonKind.Secondary, Sub() PAToast.Notify(Me, "2 payments are due tomorrow", Tone.Warning, "View"))
            AddDemo(flow, "Danger", ButtonKind.Secondary, Sub() PAToast.Notify(Me, "Payment deleted", Tone.Danger))
            AddDemo(flow, "Neutral", ButtonKind.Secondary, Sub() PAToast.Notify(Me, "Marked all as read", Tone.Neutral))
            layout.Controls.Add(toastCard, 1, 1)

            Dim keysCard = MakeCard("Keys", flow, 0)
            For Each line In {"Enter  —  primary action (or the focused button)", "Esc  —  cancel / close", "Tab  —  move between buttons",
                              "Drag the title area to move a dialog"}
                flow.Controls.Add(Caption(line, 250, Theme.TextPrimary))
            Next
            layout.Controls.Add(keysCard, 2, 1)

            page.Controls.Add(layout)
            Return page
        End Function

        Private Shared Sub AddDemo(flow As FlowLayoutPanel, text As String, kind As ButtonKind, action As Action)
            Dim button As New PAButton() With {.Text = text, .Kind = kind, .Size = New Size(124, Theme.ButtonHeight), .Margin = New Padding(0, 6, 8, 4)}
            AddHandler button.Click, Sub(s, e) action()
            flow.Controls.Add(button)
        End Sub

        ' ================================================================ page 4: empty state

        Private Function BuildEmptyPage() As Panel
            Dim page = NewPage()
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill}
            Dim empty As New EmptyState() With {
                .Dock = DockStyle.Fill, .Icon = IconKind.Payments, .Title = "No payments yet",
                .Message = "Add your first one. PayAlert will remind you before it's due.", .ActionText = "Add Payment"}
            AddHandler empty.ActionClick, Sub(s, e) Toast("This button will open Add Payment.", Tone.Brand)
            card.Controls.Add(empty)
            page.Controls.Add(card)
            Return page
        End Function

    End Class

End Namespace
