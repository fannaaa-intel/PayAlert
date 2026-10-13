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
    ''' Dashboard (§7, manuscript Figures 2 and 8): the greeting, four summary cards that open Payments filtered to them,
    ''' a totals card with the balance ring (Total I Owe · Total Owed to Me), and the next five unpaid payments.
    ''' Every number comes from the same v_payment_summary rows as the Payments screen, with the same rules.
    ''' </summary>
    Public Class ucDashboard
        Inherits PAView

        Private Const UpcomingCount As Integer = 5

        Private ReadOnly _dueSoon As New SummaryCard()
        Private ReadOnly _dueTomorrow As New SummaryCard()
        Private ReadOnly _overdue As New SummaryCard()
        Private ReadOnly _toCollect As New SummaryCard()
        Private ReadOnly _ring As New BalanceRing()
        Private ReadOnly _ringCaption As New Label()
        Private ReadOnly _owe As New TotalLine()
        Private ReadOnly _owed As New TotalLine()
        Private ReadOnly _upcoming As New UpcomingList()
        Private ReadOnly _upcomingEmpty As New EmptyState()
        Private ReadOnly _upcomingNote As New Label()
        Private ReadOnly _layout As New TableLayoutPanel()
        Private ReadOnly _emptyCard As New SoftCard()
        Private ReadOnly _empty As New EmptyState()
        Private _all As New List(Of PaymentSummary)

        Public Sub New()
            MyBase.New()
            SidebarKey = "dashboard"
            Title = "Dashboard"
            Subtitle = Greeting()
            Dim action As New PrimaryButton() With {.Text = "Add Payment", .Icon = IconKind.Plus, .Size = New Size(150, Theme.ButtonHeight)}
            AddHandler action.Click, Sub(s, e) Navigator.Push(New ucPaymentEditor())
            AddHeaderAction(action)

            ' Row 1: four summary cards.
            SetupCard(_dueSoon, "DUE IN 7 DAYS", IconKind.Calendar, Theme.Brand, "View them")
            SetupCard(_dueTomorrow, "DUE TOMORROW", IconKind.Clock, Theme.Brand, "View them")
            SetupCard(_overdue, "OVERDUE", IconKind.Alert, Theme.OverdueText, "View overdue")
            SetupCard(_toCollect, "TO COLLECT", IconKind.ArrowIn, Theme.Brand, "View Owed to Me")
            Dim cards As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 1, .Margin = New Padding(0, 0, 0, 16)}
            For i = 0 To 3
                cards.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            Next
            cards.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim cardList = {_dueSoon, _dueTomorrow, _overdue, _toCollect}
            For i = 0 To 3
                cardList(i).Dock = DockStyle.Fill
                cardList(i).Margin = New Padding(0, 0, If(i < 3, 16, 0), 0)
                cardList(i).TabIndex = i
                cards.Controls.Add(cardList(i), i, 0)
            Next
            AddHandler _dueSoon.Click, Sub(s, e) Navigator.ShowRoot(New ucPayments(window:=DueWindow.Next7Days))
            AddHandler _dueTomorrow.Click, Sub(s, e) Navigator.ShowRoot(New ucPayments(window:=DueWindow.Tomorrow))
            AddHandler _overdue.Click, Sub(s, e) Navigator.ShowRoot(New ucPayments(Theme.StatusOverdue))
            AddHandler _toCollect.Click, Sub(s, e) Navigator.ShowRoot(New ucPayments(direction:=Payment.DirectionReceivable))

            ' Row 2: totals card (ring) and upcoming payments.
            _layout.Dock = DockStyle.Fill
            _layout.ColumnCount = 2
            _layout.RowCount = 2
            _layout.BackColor = Theme.AppBackground
            _layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 34))
            _layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 66))
            _layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 132))
            _layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            _layout.Controls.Add(cards, 0, 0)
            _layout.SetColumnSpan(cards, 2)
            _layout.Controls.Add(BuildTotalsCard(), 0, 1)
            _layout.Controls.Add(BuildUpcomingCard(), 1, 1)
            Body.Controls.Add(_layout)

            ' No payments at all: one friendly empty state instead.
            _emptyCard.Dock = DockStyle.Fill
            _emptyCard.Visible = False
            _empty.Dock = DockStyle.Fill
            _empty.Icon = IconKind.Payments
            _empty.Title = "No payments yet"
            _empty.Message = "Add your first one. PayAlert will remind you before it's due."
            _empty.ActionText = "Add Payment"
            _empty.ActionIcon = IconKind.Plus
            AddHandler _empty.ActionClick, Sub(s, e) Navigator.Push(New ucPaymentEditor())
            _emptyCard.Controls.Add(_empty)
            Body.Controls.Add(_emptyCard)
        End Sub

        Private Shared Sub SetupCard(card As SummaryCard, caption As String, icon As IconKind, accent As Color, hint As String)
            card.Caption = caption
            card.Icon = icon
            card.AccentColor = accent
            card.Hint = hint
            card.Value = "0"
        End Sub

        Private Function BuildTotalsCard() As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 16, 0), .Padding = New Padding(20, 14, 20, 14)}
            Dim heading As New Label() With {
                .Text = "Totals", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 30}

            _ring.Size = New Size(150, 150)
            Dim ringHost As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent, .MinimumSize = New Size(0, 150)}
            ringHost.Controls.Add(_ring)
            ringHost.Controls.Add(_ringCaption)
            AddHandler ringHost.Layout,
                Sub(s, e)
                    ' The ring takes what height is left (up to 230px) and stays centered.
                    Dim side = Math.Max(CInt(110 * DeviceDpi / 96.0F), Math.Min(Math.Min(ringHost.ClientSize.Width, ringHost.ClientSize.Height - CInt(32 * DeviceDpi / 96.0F)),
                                                                                   CInt(230 * DeviceDpi / 96.0F)))
                    Dim captionHeight = CInt(24 * DeviceDpi / 96.0F)
                    Dim top = Math.Max(0, (ringHost.ClientSize.Height - side - captionHeight) \ 2)
                    _ring.Size = New Size(side, side)
                    _ring.Location = New Point((ringHost.ClientSize.Width - side) \ 2, top)
                    _ringCaption.SetBounds(0, _ring.Bottom + CInt(4 * DeviceDpi / 96.0F), ringHost.ClientSize.Width, captionHeight)
                End Sub

            _ringCaption.TextAlign = ContentAlignment.MiddleCenter
            _ringCaption.Font = Theme.FontCaption
            _ringCaption.ForeColor = Theme.TextMuted
            _ringCaption.BackColor = Color.Transparent

            _owe.Dock = DockStyle.Bottom
            _owe.Height = 46
            _owe.Icon = IconKind.ArrowOut
            _owe.Caption = "Total I Owe"
            _owed.Dock = DockStyle.Bottom
            _owed.Height = 46
            _owed.Icon = IconKind.ArrowIn
            _owed.Caption = "Total Owed to Me"
            _owed.ShowRule = False

            ' Docked: heading on top, the two totals at the bottom, the ring in what's left.
            card.Controls.Add(ringHost)
            card.Controls.Add(_owe)
            card.Controls.Add(_owed)
            card.Controls.Add(heading)
            Return card
        End Function

        Private Function BuildUpcomingCard() As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Margin = New Padding(0), .Padding = New Padding(16, 14, 16, 10)}
            Dim heading As New Label() With {
                .Text = "Upcoming Payments", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Left, .Width = 220, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(4, 0, 0, 0)}
            Dim viewAll As New PAButton() With {.Kind = ButtonKind.Ghost, .Text = "View all", .Dock = DockStyle.Right}
            viewAll.Width = viewAll.PreferredWidth()
            AddHandler viewAll.Click, Sub(s, e) Navigator.ShowRoot(New ucPayments())
            _upcomingNote.Dock = DockStyle.Fill
            _upcomingNote.TextAlign = ContentAlignment.MiddleLeft
            _upcomingNote.Font = Theme.FontCaption
            _upcomingNote.ForeColor = Theme.TextMuted
            _upcomingNote.BackColor = Color.Transparent
            Dim headRow As New Panel() With {.Dock = DockStyle.Top, .Height = 36, .BackColor = Color.Transparent}
            headRow.Controls.Add(_upcomingNote)
            headRow.Controls.Add(viewAll)
            headRow.Controls.Add(heading)

            _upcoming.Dock = DockStyle.Fill
            AddHandler _upcoming.PaymentClicked, Sub(s, p) Navigator.Push(New ucPaymentDetails(p.PaymentId))
            _upcomingEmpty.Dock = DockStyle.Fill
            _upcomingEmpty.Compact = True
            _upcomingEmpty.Icon = IconKind.Check
            _upcomingEmpty.Title = "All caught up"
            _upcomingEmpty.Message = "Nothing left to pay or collect right now."
            _upcomingEmpty.Visible = False
            card.Controls.Add(_upcoming)
            card.Controls.Add(_upcomingEmpty)
            card.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 6, .BackColor = Color.Transparent})
            card.Controls.Add(headRow)
            Return card
        End Function

        ''' <summary>"Good morning, Juan!" plus today's date.</summary>
        Private Shared Function Greeting() As String
            Dim hour = Date.Now.Hour
            Dim part = If(hour < 12, "morning", If(hour < 18, "afternoon", "evening"))
            Dim name = If(Session.CurrentUser?.FirstName, "")
            Return "Good " & part & If(name.Length > 0, ", " & name, "") & "!  ·  " & Date.Today.ToString("dddd, MMM d, yyyy", Globalization.CultureInfo.InvariantCulture)
        End Function

        ' ---------------------------------------------------------------- data

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Subtitle = Greeting()
            Try
                _all = PaymentRepo.GetSummaries(Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucDashboard] " & ex.ToString())
                PADialog.Error(Me, "Can't load your dashboard", Db.FriendlyMessageFor(ex))
                _all = New List(Of PaymentSummary)
            End Try
            Fill()
        End Sub

        Private Sub Fill()
            Dim hasAny = _all.Count > 0
            _layout.Visible = hasAny
            _emptyCard.Visible = Not hasAny
            If Not hasAny Then Return

            Dim unpaid = _all.Where(Function(p) p.Balance > 0D).ToList()
            Dim payables = unpaid.Where(Function(p) Not p.IsReceivable).ToList()
            Dim receivables = unpaid.Where(Function(p) p.IsReceivable).ToList()
            Dim dueSoon = _all.Where(Function(p) ucPayments.InWindow(p, DueWindow.Next7Days)).Count()
            Dim dueTomorrow = _all.Where(Function(p) ucPayments.InWindow(p, DueWindow.Tomorrow)).Count()
            Dim overdue = _all.Where(Function(p) p.Status = Theme.StatusOverdue).Count()
            Dim totalOwe = payables.Sum(Function(p) p.Balance)
            Dim totalOwed = receivables.Sum(Function(p) p.Balance)

            _dueSoon.Value = dueSoon.ToString()
            _dueTomorrow.Value = dueTomorrow.ToString()
            _overdue.Value = overdue.ToString()
            _overdue.ValueColor = If(overdue > 0, Theme.OverdueText, Theme.TextPrimary)
            _toCollect.Value = Format.Money(totalOwed)

            ' The ring follows what you owe: paid so far on your open payables, with the amount left in the middle.
            Dim oweTotal = payables.Sum(Function(p) p.TotalAmount)
            Dim owePaid = payables.Sum(Function(p) p.AmountPaid)
            _ring.SetValues(oweTotal, owePaid, animate:=False)
            _ring.Replay()
            _ringCaption.Text = If(payables.Count = 0, "Nothing left to pay",
                                   Format.Money(owePaid) & " paid on " & payables.Count & If(payables.Count = 1, " open bill", " open bills"))
            _owe.Amount = totalOwe
            _owe.Detail = payables.Count & If(payables.Count = 1, " payment", " payments")
            _owed.Amount = totalOwed
            _owed.Detail = receivables.Count & If(receivables.Count = 1, " payment", " payments")

            Dim nextFive = unpaid.OrderBy(Function(p) p.DueDate).ThenBy(Function(p) p.Name).Take(UpcomingCount).ToList()
            _upcoming.SetItems(nextFive)
            _upcoming.Visible = nextFive.Count > 0
            _upcomingEmpty.Visible = nextFive.Count = 0
            _upcomingNote.Text = If(unpaid.Count > UpcomingCount, "Next " & UpcomingCount & " of " & unpaid.Count & " unpaid", "")
        End Sub

        ' ---------------------------------------------------------------- totals line

        ''' <summary>One total in the totals card: a direction arrow on a soft disc, the label with a count, and the amount.</summary>
        Private NotInheritable Class TotalLine
            Inherits PAControl

            Public Sub New()
                MyBase.New()
                ' Display only: never a Tab stop.
                SetStyle(ControlStyles.Selectable, False)
                TabStop = False
            End Sub

            Private _amount As Decimal
            Private _caption As String = ""
            Private _detail As String = ""

            Public Property Icon As IconKind = IconKind.ArrowOut
            Public Property ShowRule As Boolean = True

            Public Property Caption As String
                Get
                    Return _caption
                End Get
                Set(value As String)
                    _caption = If(value, "")
                    Invalidate()
                End Set
            End Property

            Public Property Detail As String
                Get
                    Return _detail
                End Get
                Set(value As String)
                    _detail = If(value, "")
                    Invalidate()
                End Set
            End Property

            Public Property Amount As Decimal
                Get
                    Return _amount
                End Get
                Set(value As Decimal)
                    _amount = value
                    Invalidate()
                End Set
            End Property

            ''' <summary>"Total I Owe: ₱X", for tests and accessibility.</summary>
            Public Overrides Property Text As String
                Get
                    Return _caption & ": " & Format.Money(_amount)
                End Get
                Set(value As String)
                End Set
            End Property

            Protected Overrides Sub OnPaint(e As PaintEventArgs)
                Dim g = e.Graphics
                Gfx.HighQuality(g)
                If ShowRule Then
                    Using rule As New Pen(Theme.Border)
                        g.DrawLine(rule, 0, Height - 0.5F, Width, Height - 0.5F)
                    End Using
                End If
                Dim disc = Px(30)
                Dim discRect As New RectangleF(0, (Height - disc) / 2.0F, disc, disc)
                Using brush As New SolidBrush(Theme.NeutralSoft)
                    g.FillEllipse(brush, discRect)
                End Using
                Icons.Draw(g, Icon, RectangleF.Inflate(discRect, -Px(8), -Px(8)), Theme.TextMuted, Px(1.6F))

                Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
                Dim value = Format.Money(_amount)
                Dim valueSize = TextRenderer.MeasureText(g, value, Theme.FontSection, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
                TextRenderer.DrawText(g, value, Theme.FontSection, New Rectangle(Width - valueSize.Width - 1, (Height - valueSize.Height) \ 2, valueSize.Width + 1, valueSize.Height),
                                      Theme.TextPrimary, flags)
                Dim textX = CInt(disc + Px(12))
                Dim textWidth = Width - textX - valueSize.Width - PxI(8)
                Dim captionHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontBodySemibold, Size.Empty, flags).Height
                Dim top = (Height - captionHeight - PxI(16)) \ 2
                TextRenderer.DrawText(g, _caption, Theme.FontBodySemibold, New Rectangle(textX, top, textWidth, captionHeight), Theme.TextPrimary, flags)
                TextRenderer.DrawText(g, _detail, Theme.FontCaption, New Rectangle(textX, top + captionHeight, textWidth, PxI(16)), Theme.TextMuted, flags)
            End Sub
        End Class

    End Class

End Namespace
