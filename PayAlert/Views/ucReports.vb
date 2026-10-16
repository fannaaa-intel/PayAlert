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
    ''' Payment Summary report (2026 revision): how much was paid and collected with each payment method (Cash, GCash,
    ''' Maya, Bank Transfer, ...) in a date range. Filters: a quick range or From/To, and direction. A totals strip on top,
    ''' then one bar per method, biggest first. "Save as CSV" writes the same table for Excel.
    ''' </summary>
    Public Class ucReports
        Inherits PAView

        Private Shared ReadOnly RangeNames As String() = {"This month", "Last month", "This year", "All time"}

        Private ReadOnly _range As New SegmentedControl()
        Private ReadOnly _from As New PADatePicker()
        Private ReadOnly _to As New PADatePicker()
        Private ReadOnly _direction As New SegmentedControl()
        Private ReadOnly _countLabel As New Label()
        Private ReadOnly _totals As New ReportTotals()
        Private ReadOnly _breakdown As New MethodBreakdown()
        Private ReadOnly _empty As New EmptyState()
        Private ReadOnly _export As New SecondaryButton()
        Private _all As New List(Of PaymentTransaction)
        Private _report As New MethodReport()
        Private _applying As Boolean

        Public Sub New()
            MyBase.New()
            SidebarKey = "reports"
            Title = "Payment Summary"
            Subtitle = "Totals by payment method, like Cash, GCash, Maya and bank transfer"

            _export.Text = "Save as CSV"
            _export.Icon = IconKind.ArrowOut
            _export.Size = New Size(150, Theme.ButtonHeight)
            AddHeaderAction(_export)
            AddHandler _export.Click, AddressOf ExportClicked

            ' Row 1: quick ranges and how many records are counted.
            Dim large = Theme.IsLargeText
            _range.SetItems(RangeNames)
            _range.Dock = DockStyle.Left
            _range.Width = 470
            _countLabel.Dock = DockStyle.Fill
            _countLabel.TextAlign = ContentAlignment.MiddleRight
            _countLabel.AutoEllipsis = True
            _countLabel.Font = Theme.FontCaption
            _countLabel.ForeColor = Theme.TextMuted
            Dim row1 As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            row1.Controls.Add(_countLabel)
            row1.Controls.Add(_range)

            ' Row 2: From / To and direction.
            _from.Dock = DockStyle.Left
            _from.Width = If(large, 276, 256)
            _to.Dock = DockStyle.Left
            _to.Width = If(large, 248, 240)
            _direction.SetItems("All", "I Owe", "Owed to Me")
            _direction.Dock = DockStyle.Left
            _direction.Width = If(large, 312, 280)
            Dim row2 As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            ' Docked left-to-right: the last added docks first.
            row2.Controls.Add(_direction)
            row2.Controls.Add(New Panel() With {.Dock = DockStyle.Left, .Width = 16})
            row2.Controls.Add(_to)
            row2.Controls.Add(FieldCaption("To"))
            row2.Controls.Add(New Panel() With {.Dock = DockStyle.Left, .Width = 12})
            row2.Controls.Add(_from)
            row2.Controls.Add(FieldCaption("From"))

            Dim totalsCard As New SoftCard() With {.Dock = DockStyle.Top, .Height = If(large, 96, 86), .Padding = New Padding(20, 8, 20, 8)}
            _totals.Dock = DockStyle.Fill
            totalsCard.Controls.Add(_totals)

            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Padding = New Padding(20, 12, 20, 12)}
            Dim heading As New Label() With {
                .Text = "By payment method", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 34, .TextAlign = ContentAlignment.MiddleLeft}
            _breakdown.Dock = DockStyle.Fill
            _empty.Dock = DockStyle.Fill
            _empty.Compact = True
            _empty.Visible = False
            card.Controls.Add(_breakdown)
            card.Controls.Add(_empty)
            card.Controls.Add(heading)

            Body.Controls.Add(card)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 16})
            Body.Controls.Add(totalsCard)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 16})
            Body.Controls.Add(row2)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 12})
            Body.Controls.Add(row1)

            SetRange(0)
            AddHandler _range.SelectedIndexChanged, Sub(s, e) If _range.SelectedIndex >= 0 Then SetRange(_range.SelectedIndex)
            AddHandler _from.ValueChanged, AddressOf DatesChanged
            AddHandler _to.ValueChanged, AddressOf DatesChanged
            AddHandler _direction.SelectedIndexChanged, Sub(s, e) ApplyFilters()
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
                Debug.WriteLine("[ucReports] " & ex.ToString())
                PADialog.Error(Me, "Can't load the summary", Db.FriendlyMessageFor(ex))
                _all = New List(Of PaymentTransaction)
            End Try
            ' "All time" starts at the oldest record, which may have changed.
            If _range.SelectedIndex = 3 Then SetRange(3) Else ApplyFilters()
        End Sub

        ''' <summary>The report as shown (for tests).</summary>
        Public ReadOnly Property Report As MethodReport
            Get
                Return _report
            End Get
        End Property

        ' ---------------------------------------------------------------- filters

        ''' <summary>Sets From/To for a quick range: 0 this month, 1 last month, 2 this year, 3 all time.</summary>
        Private Sub SetRange(index As Integer)
            Dim today = Date.Today
            Dim monthStart = New Date(today.Year, today.Month, 1)
            Dim fromDate As Date
            Dim toDate = today
            Select Case index
                Case 1
                    fromDate = monthStart.AddMonths(-1)
                    toDate = monthStart.AddDays(-1)
                Case 2 : fromDate = New Date(today.Year, 1, 1)
                Case 3
                    fromDate = If(_all.Count = 0, today, _all.Min(Function(t) t.PaidOn))
                    If _all.Count > 0 Then toDate = If(_all.Max(Function(t) t.PaidOn) > today, _all.Max(Function(t) t.PaidOn), today)
                Case Else : fromDate = monthStart
            End Select
            _applying = True
            _from.Value = fromDate
            _to.Value = toDate
            _applying = False
            ApplyFilters()
        End Sub

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

        Private Sub ApplyFilters()
            If _applying Then Return
            _from.HasError = Not DatesValid
            _to.HasError = Not DatesValid
            Dim records = _all.Where(Function(t) _direction.SelectedIndex = 0 OrElse t.IsReceivable = (_direction.SelectedIndex = 2))
            _report = If(DatesValid, ReportService.ByMethod(records, _from.Value, _to.Value),
                         New MethodReport With {.FromDate = _from.Value, .ToDate = _to.Value})
            _breakdown.Report = _report
            _totals.SetReport(_report, _direction.SelectedIndex)

            If Not DatesValid Then
                _countLabel.Text = """From"" is after ""To""."
                _countLabel.ForeColor = Theme.OverdueText
            Else
                _countLabel.Text = _report.Records & If(_report.Records = 1, " record", " records") & " · " &
                                   _report.Methods.Count & If(_report.Methods.Count = 1, " method", " methods")
                _countLabel.ForeColor = Theme.TextMuted
            End If
            SetHeaderActionVisible(_export, _report.Methods.Count > 0)

            Dim empty = _report.Methods.Count = 0
            _breakdown.Visible = Not empty
            _empty.Visible = empty
            If Not empty Then Return
            If _all.Count = 0 Then
                SetEmpty(IconKind.Chart, "Nothing recorded yet", "Record a payment and choose how it was paid. The totals show up here.", "Go to Payments", IconKind.Payments)
            ElseIf Not DatesValid Then
                SetEmpty(IconKind.Calendar, "Check the dates", "The From date must be on or before the To date.", "Show this month", IconKind.Calendar)
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
            _direction.SelectedIndex = 0
            _range.SelectedIndex = If(DatesValid, 3, 0)
            _applying = False
            SetRange(_range.SelectedIndex)
        End Sub

        ' ---------------------------------------------------------------- export

        Private Sub ExportClicked(sender As Object, e As EventArgs)
            If _report.Methods.Count = 0 Then Return
            Using dialog As New SaveFileDialog() With {
                .Title = "Save the payment summary",
                .Filter = "CSV file for Excel (*.csv)|*.csv",
                .FileName = "PayAlert summary " & _report.FromDate.ToString("yyyy-MM-dd") & " to " & _report.ToDate.ToString("yyyy-MM-dd") & ".csv",
                .OverwritePrompt = True}
                If dialog.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                Try
                    ReportService.WriteCsv(_report, dialog.FileName)
                Catch ex As Exception
                    Debug.WriteLine("[ucReports] " & ex.ToString())
                    PADialog.Error(Me, "Couldn't save the file", "Check that the file isn't open in Excel, then try again.")
                    Return
                End Try
            End Using
            PAToast.Notify(FindForm(), "Summary saved", Tone.Success)
        End Sub

        ' ---------------------------------------------------------------- totals strip

        ''' <summary>
        ''' Three numbers across the top: the total for the range (with its record count), then what was paid (I Owe)
        ''' and collected (Owed to Me). With a direction filter, the other side shows "—".
        ''' </summary>
        Private NotInheritable Class ReportTotals
            Inherits PAControl

            Private _report As New MethodReport()
            Private _direction As Integer

            Public Sub New()
                MyBase.New()
                SetStyle(ControlStyles.Selectable, False)
                TabStop = False
            End Sub

            Public Sub SetReport(report As MethodReport, direction As Integer)
                _report = report
                _direction = direction
                Invalidate()
            End Sub

            Protected Overrides Sub OnPaint(e As PaintEventArgs)
                Dim g = e.Graphics
                Gfx.HighQuality(g)
                Dim third = Width \ 3
                DrawBlock(g, 0, third, "TOTAL", Format.Money(_report.Total), _report.Records & If(_report.Records = 1, " record", " records"), IconKind.Chart, True)
                DrawBlock(g, third, third, "PAID  (I OWE)", If(_direction = 2, "—", Format.Money(_report.TotalPaid)), "", IconKind.ArrowOut, False)
                DrawBlock(g, third * 2, Width - third * 2, "COLLECTED  (OWED TO ME)", If(_direction = 1, "—", Format.Money(_report.TotalCollected)), "", IconKind.ArrowIn, False)
                Using rule As New Pen(Theme.Border)
                    g.DrawLine(rule, third, PxI(8), third, Height - PxI(8))
                    g.DrawLine(rule, third * 2, PxI(8), third * 2, Height - PxI(8))
                End Using
            End Sub

            Private Sub DrawBlock(g As Graphics, x As Integer, width As Integer, caption As String, value As String, detail As String,
                                  icon As IconKind, brand As Boolean)
                Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
                Dim measure = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
                Dim left = x + If(x = 0, 0, PxI(20))
                Dim disc = PxI(36)
                Dim discRect As New RectangleF(left, (Height - disc) / 2.0F, disc, disc)
                Using brush As New SolidBrush(If(brand, Theme.BrandSoft, Theme.NeutralSoft))
                    g.FillEllipse(brush, discRect)
                End Using
                Icons.Draw(g, icon, RectangleF.Inflate(discRect, -Px(9), -Px(9)), If(brand, Theme.BrandDark, Theme.TextMuted), Px(1.7F))

                Dim textX = CInt(discRect.Right + Px(12))
                Dim textWidth = x + width - textX - PxI(8)
                Dim captionHeight = TextRenderer.MeasureText(g, "AG", Theme.FontSmallCaps, Size.Empty, measure).Height
                Dim valueHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontSection, Size.Empty, measure).Height
                Dim top = (Height - captionHeight - PxI(4) - valueHeight) \ 2
                TextRenderer.DrawText(g, caption, Theme.FontSmallCaps, New Rectangle(textX, top, textWidth, captionHeight), Theme.TextMuted, flags)
                If detail.Length > 0 Then
                    Dim captionWidth = TextRenderer.MeasureText(g, caption, Theme.FontSmallCaps, Size.Empty, measure).Width
                    TextRenderer.DrawText(g, detail, Theme.FontCaption, New Rectangle(textX + captionWidth + PxI(8), top - PxI(1), Math.Max(0, textWidth - captionWidth - PxI(8)), captionHeight + PxI(4)),
                                          Theme.TextMuted, flags)
                End If
                TextRenderer.DrawText(g, value, Theme.FontSection, New Rectangle(textX, top + captionHeight + PxI(4), textWidth, valueHeight), Theme.TextPrimary, flags)
            End Sub

        End Class

    End Class

End Namespace
