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
    ''' History (§7, manuscript Figure 7): every recorded payment and collection. Filters: a quick range or From/To dates,
    ''' direction, and a payment search. The footer adds up Total Paid (I Owe) and Total Collected (Owed to Me) for
    ''' what is shown. Double-click or Enter opens the payment.
    ''' </summary>
    Public Class ucHistory
        Inherits PAView

        Private Shared ReadOnly RangeNames As String() = {"Last 30 days", "This month", "This year", "All time"}

        Private ReadOnly _range As New SegmentedControl()
        Private ReadOnly _search As New PATextBox()
        Private ReadOnly _from As New PADatePicker()
        Private ReadOnly _to As New PADatePicker()
        Private ReadOnly _direction As New SegmentedControl()
        Private ReadOnly _countLabel As New Label()
        Private ReadOnly _grid As New TransactionGrid()
        Private ReadOnly _empty As New EmptyState()
        Private ReadOnly _totals As New HistoryTotals()
        Private _all As New List(Of PaymentTransaction)
        Private _applying As Boolean

        Public Sub New()
            MyBase.New()
            SidebarKey = "history"
            Title = "History"
            Subtitle = "Every payment and collection you've recorded"

            ' Row 1: quick ranges, search on the right.
            _range.SetItems(RangeNames)
            _range.Dock = DockStyle.Left
            _range.Width = 470
            _search.Icon = IconKind.Search
            _search.PlaceholderText = "Search by payment, method or note"
            _search.Dock = DockStyle.Right
            _search.Width = 300
            Dim row1 As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            row1.Controls.Add(_range)
            row1.Controls.Add(_search)

            ' Row 2: From / To, direction, and how many are showing.
            _from.Dock = DockStyle.Left
            _from.Width = 256
            _to.Dock = DockStyle.Left
            _to.Width = 240
            _direction.SetItems("All", "I Owe", "Owed to Me")
            _direction.Dock = DockStyle.Left
            _direction.Width = 280
            _countLabel.Dock = DockStyle.Fill
            _countLabel.TextAlign = ContentAlignment.MiddleRight
            _countLabel.Font = Theme.FontCaption
            _countLabel.ForeColor = Theme.TextMuted
            Dim row2 As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            ' Docked left-to-right: the last added docks first.
            row2.Controls.Add(_countLabel)
            row2.Controls.Add(_direction)
            row2.Controls.Add(New Panel() With {.Dock = DockStyle.Left, .Width = 16})
            row2.Controls.Add(_to)
            row2.Controls.Add(FieldCaption("To"))
            row2.Controls.Add(New Panel() With {.Dock = DockStyle.Left, .Width = 12})
            row2.Controls.Add(_from)
            row2.Controls.Add(FieldCaption("From"))

            ' The list with its totals footer; an empty state is laid over the list when nothing matches.
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Padding = New Padding(6, 8, 6, 0)}
            _grid.Dock = DockStyle.Fill
            _empty.Dock = DockStyle.Fill
            _empty.Visible = False
            _totals.Dock = DockStyle.Bottom
            _totals.Height = 64
            card.Controls.Add(_grid)
            card.Controls.Add(_empty)
            card.Controls.Add(_totals)

            Body.Controls.Add(card)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 16})
            Body.Controls.Add(row2)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 12})
            Body.Controls.Add(row1)

            SetRange(0)
            AddHandler _range.SelectedIndexChanged, Sub(s, e) If _range.SelectedIndex >= 0 Then SetRange(_range.SelectedIndex)
            AddHandler _from.ValueChanged, AddressOf DatesChanged
            AddHandler _to.ValueChanged, AddressOf DatesChanged
            AddHandler _direction.SelectedIndexChanged, Sub(s, e) ApplyFilters()
            AddHandler _search.TextChanged, Sub(s, e) ApplyFilters()
            AddHandler _grid.TransactionActivated, Sub(s, t) Navigator.Push(New ucPaymentDetails(t.PaymentId))
            AddHandler _empty.ActionClick, AddressOf EmptyActionClicked
        End Sub

        Private Shared Function FieldCaption(text As String) As Label
            Return New Label() With {
                .Text = text, .Dock = DockStyle.Left, .Width = 48, .TextAlign = ContentAlignment.MiddleLeft,
                .Font = Theme.FontBodySemibold, .ForeColor = Theme.TextMuted}
        End Function

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Try
                _all = TransactionRepo.GetHistory(Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucHistory] " & ex.ToString())
                PADialog.Error(Me, "Can't load your history", Db.FriendlyMessageFor(ex))
                _all = New List(Of PaymentTransaction)
            End Try
            ' "All time" starts at the oldest record, which may have changed.
            If _range.SelectedIndex = 3 Then SetRange(3) Else ApplyFilters()
        End Sub

        ' ---------------------------------------------------------------- filters

        ''' <summary>Sets From/To for a quick range: 0 last 30 days, 1 this month, 2 this year, 3 all time.</summary>
        Private Sub SetRange(index As Integer)
            Dim today = Date.Today
            Dim fromDate As Date
            Select Case index
                Case 1 : fromDate = New Date(today.Year, today.Month, 1)
                Case 2 : fromDate = New Date(today.Year, 1, 1)
                Case 3 : fromDate = If(_all.Count = 0, today, _all.Min(Function(t) t.PaidOn))
                Case Else : fromDate = today.AddDays(-29)
            End Select
            _applying = True
            _from.Value = fromDate
            _to.Value = If(index = 3 AndAlso _all.Count > 0, Max(today, _all.Max(Function(t) t.PaidOn)), today)
            _applying = False
            ApplyFilters()
        End Sub

        Private Shared Function Max(a As Date, b As Date) As Date
            Return If(a > b, a, b)
        End Function

        Private Sub DatesChanged(sender As Object, e As EventArgs)
            If _applying Then Return
            _applying = True
            _range.SelectedIndex = -1          ' the user picked their own dates
            _applying = False
            ApplyFilters()
        End Sub

        Private ReadOnly Property DatesValid As Boolean
            Get
                Return _from.Value <= _to.Value
            End Get
        End Property

        Private Function MatchesFilters(t As PaymentTransaction) As Boolean
            If t.PaidOn < _from.Value OrElse t.PaidOn > _to.Value Then Return False
            Select Case _direction.SelectedIndex
                Case 1 : If t.IsReceivable Then Return False
                Case 2 : If Not t.IsReceivable Then Return False
            End Select
            Dim query = _search.Text.Trim()
            If query.Length > 0 AndAlso
               t.PaymentName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 AndAlso
               t.Method.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 AndAlso
               t.Note.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 Then Return False
            Return True
        End Function

        Private Sub ApplyFilters()
            If _applying Then Return
            _from.HasError = Not DatesValid
            _to.HasError = Not DatesValid
            Dim shown = If(DatesValid, _all.Where(AddressOf MatchesFilters).ToList(), New List(Of PaymentTransaction))
            _grid.SetRows(shown)

            If Not DatesValid Then
                _countLabel.Text = """From"" is after ""To"". Pick an earlier From date."
                _countLabel.ForeColor = Theme.OverdueText
            Else
                _countLabel.Text = If(_all.Count = 0, "", shown.Count & " of " & _all.Count & If(_all.Count = 1, " record", " records"))
                _countLabel.ForeColor = Theme.TextMuted
            End If

            _totals.SetTotals(shown.Where(Function(t) Not t.IsReceivable).Sum(Function(t) t.Amount),
                              shown.Where(Function(t) Not t.IsReceivable).Count(),
                              shown.Where(Function(t) t.IsReceivable).Sum(Function(t) t.Amount),
                              shown.Where(Function(t) t.IsReceivable).Count())
            UpdateEmptyState(shown.Count)
        End Sub

        Private Sub UpdateEmptyState(shownCount As Integer)
            Dim empty = shownCount = 0
            _grid.Visible = Not empty
            _empty.Visible = empty
            If Not empty Then Return

            Dim query = _search.Text.Trim()
            If _all.Count = 0 Then
                SetEmpty(IconKind.History, "Nothing recorded yet",
                         "Open a payment and use Record Payment each time you pay or receive money. It will be listed here.",
                         "Go to Payments", IconKind.Payments)
            ElseIf Not DatesValid Then
                SetEmpty(IconKind.Calendar, "Check the dates", "The From date must be on or before the To date.", "Show the last 30 days", IconKind.Calendar)
            ElseIf query.Length > 0 Then
                SetEmpty(IconKind.Search, "Nothing matches """ & query & """", "Try another payment name, method or note, or clear the filters.", "Clear filters", IconKind.Close)
            Else
                SetEmpty(IconKind.Calendar, "Nothing recorded in this range", "Try a longer range, like ""All time"".", "Show all time", IconKind.Calendar)
            End If
        End Sub

        Private Sub SetEmpty(icon As IconKind, title As String, message As String, actionText As String, actionIcon As IconKind)
            _empty.Icon = icon
            _empty.Title = title
            _empty.Message = message
            _empty.ActionIcon = actionIcon
            _empty.ActionText = actionText
        End Sub

        Private Sub EmptyActionClicked(sender As Object, e As EventArgs)
            If _all.Count = 0 Then
                Navigator.ShowRoot(New ucPayments())
                Return
            End If
            _applying = True
            _search.Text = ""
            _direction.SelectedIndex = 0
            _range.SelectedIndex = If(DatesValid, 3, 0)
            _applying = False
            SetRange(_range.SelectedIndex)
        End Sub

        ' ---------------------------------------------------------------- totals footer

        ''' <summary>
        ''' The footer under the list: a perforated rule (the ticket motif) and, on the right, Total Paid and Total Collected
        ''' for the rows shown, each with its direction arrow and how many records it adds up.
        ''' </summary>
        Private NotInheritable Class HistoryTotals
            Inherits PAControl

            Private _paid As Decimal
            Private _paidCount As Integer
            Private _collected As Decimal
            Private _collectedCount As Integer

            Public Sub SetTotals(paid As Decimal, paidCount As Integer, collected As Decimal, collectedCount As Integer)
                _paid = paid
                _paidCount = paidCount
                _collected = collected
                _collectedCount = collectedCount
                Invalidate()
            End Sub

            ''' <summary>The totals as shown, for tests: "Total Paid ₱X · Total Collected ₱Y".</summary>
            Public Overrides Property Text As String
                Get
                    Return "Total Paid " & Format.Money(_paid) & " · Total Collected " & Format.Money(_collected)
                End Get
                Set(value As String)
                End Set
            End Property

            Protected Overrides Sub OnPaint(e As PaintEventArgs)
                Dim g = e.Graphics
                Gfx.HighQuality(g)
                Using rule As New Pen(Theme.BorderStrong, Math.Max(1.0F, S))
                    rule.DashStyle = Drawing2D.DashStyle.Custom
                    rule.DashPattern = {3.0F, 3.0F}
                    g.DrawLine(rule, Px(8), 0.5F, Width - Px(8), 0.5F)
                End Using

                Dim caption = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.Left
                TextRenderer.DrawText(g, "Totals for the records shown", Theme.FontCaption,
                                      New Rectangle(PxI(16), 0, PxI(260), Height), Theme.TextMuted, caption Or TextFormatFlags.VerticalCenter)

                ' Right to left: Total Collected, then Total Paid.
                Dim right = Width - PxI(16)
                right = DrawTotal(g, right, "TOTAL COLLECTED", _collected, _collectedCount, IconKind.ArrowIn)
                right -= PxI(36)
                DrawTotal(g, right, "TOTAL PAID", _paid, _paidCount, IconKind.ArrowOut)
            End Sub

            ''' <summary>Draws one total ending at <paramref name="right"/> and returns its left edge.</summary>
            Private Function DrawTotal(g As Graphics, right As Integer, caption As String, amount As Decimal, count As Integer, icon As IconKind) As Integer
                Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
                Dim value = Format.Money(amount)
                Dim detail = count & If(count = 1, " record", " records")
                Dim valueSize = TextRenderer.MeasureText(g, value, Theme.FontSection, Size.Empty, flags)
                Dim captionSize = TextRenderer.MeasureText(g, caption, Theme.FontSmallCaps, Size.Empty, flags)
                Dim detailSize = TextRenderer.MeasureText(g, detail, Theme.FontCaption, Size.Empty, flags)
                Dim textWidth = Math.Max(valueSize.Width, captionSize.Width + PxI(8) + detailSize.Width)
                Dim left = right - textWidth
                Dim block = captionSize.Height + PxI(3) + valueSize.Height
                Dim top = (Height - block) \ 2

                TextRenderer.DrawText(g, caption, Theme.FontSmallCaps, New Point(left, top), Theme.TextMuted, flags)
                TextRenderer.DrawText(g, detail, Theme.FontCaption, New Point(right - detailSize.Width, top - PxI(1)), Theme.TextMuted, flags)
                TextRenderer.DrawText(g, value, Theme.FontSection, New Point(right - valueSize.Width, top + captionSize.Height + PxI(3)),
                                      Theme.TextPrimary, flags)

                ' Direction arrow on a soft disc, left of the text.
                Dim disc = Px(30)
                Dim discRect As New RectangleF(left - PxI(10) - disc, (Height - disc) / 2.0F, disc, disc)
                Using brush As New SolidBrush(Theme.NeutralSoft)
                    g.FillEllipse(brush, discRect)
                End Using
                Icons.Draw(g, icon, RectangleF.Inflate(discRect, -Px(8), -Px(8)), Theme.TextMuted, Px(1.6F))
                Return CInt(discRect.Left)
            End Function

        End Class

    End Class

End Namespace
