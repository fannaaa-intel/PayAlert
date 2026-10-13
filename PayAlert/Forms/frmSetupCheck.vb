Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports PayAlert.Services

Namespace Forms

    ''' <summary>
    ''' TEMPORARY (Phase 1 only). Shows whether the app can reach payalert_db, whether the
    ''' demo data was seeded, and what status the view gives each demo payment.
    ''' Fully custom-drawn so no default WinForms chrome is visible. Deleted in Phase 3.
    ''' Keys: Enter / Esc close, F5 runs the checks again.
    ''' </summary>
    Public Class frmSetupCheck
        Inherits Form

        Private Enum CheckState
            Running
            Passed
            Failed
            Skipped
        End Enum

        Private Const LogicalWidth As Integer = 800
        Private Const LogicalHeight As Integer = 680
        Private Const HeaderHeight As Integer = 104
        Private Const CheckRowHeight As Integer = 34
        Private Const TableHeaderHeight As Integer = 30
        Private Const TableRowHeight As Integer = 26

        Private _report As SetupReport
        Private _running As Boolean
        Private _hoverButton As String = ""
        Private _closeRect As Rectangle
        Private _retryRect As Rectangle

        Private ReadOnly _wordmarkFont As New Font(Theme.FontFamilyName, 11.0F, FontStyle.Bold)
        Private ReadOnly _titleFont As Font = Theme.PageTitleFont()
        Private ReadOnly _sectionFont As Font = Theme.SectionFont()
        Private ReadOnly _bodyFont As Font = Theme.BodyFont()
        Private ReadOnly _bodyBold As Font = Theme.BodyFont(FontStyle.Bold)
        Private ReadOnly _captionFont As Font = Theme.CaptionFont()
        Private ReadOnly _captionBold As Font = Theme.CaptionFont(FontStyle.Bold)

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
            DoubleBuffered = True
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            FormBorderStyle = FormBorderStyle.None
            StartPosition = FormStartPosition.CenterScreen
            ClientSize = New Size(LogicalWidth, LogicalHeight)
            BackColor = Theme.AppBackground
            ForeColor = Theme.TextPrimary
            Font = _bodyFont
            KeyPreview = True
            Text = "PayAlert - Setup check"
        End Sub

        ' Soft system drop shadow around the borderless window.
        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Const CS_DROPSHADOW As Integer = &H20000
                Dim cp = MyBase.CreateParams
                cp.ClassStyle = cp.ClassStyle Or CS_DROPSHADOW
                Return cp
            End Get
        End Property

        Private ReadOnly Property S As Single
            Get
                Return DeviceDpi / 96.0F
            End Get
        End Property

        Private Function Px(logical As Integer) As Integer
            Return CInt(Math.Round(logical * S))
        End Function

        Protected Overrides Async Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            Await RunChecksAsync()
        End Sub

        Private Async Function RunChecksAsync() As Task
            If _running Then Return
            _running = True
            _report = Nothing
            Invalidate()
            Try
                _report = Await Task.Run(Function() SetupCheck.Run())
            Catch ex As Exception
                Debug.WriteLine("[frmSetupCheck] " & ex.ToString())
                _report = New SetupReport With {
                    .Connection = New Data.DbCheckResult With {.Success = False, .FriendlyMessage = Data.Db.MsgUnexpected}}
            Finally
                _running = False
            End Try
            Invalidate()
        End Function

        ' ---------------------------------------------------------------- painting

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.PixelOffsetMode = PixelOffsetMode.HighQuality

            Dim pad = Px(Theme.ContentPadding)
            Dim contentWidth = ClientSize.Width - pad * 2

            ' Window frame: 1px border line.
            Using borderPen As New Pen(Theme.Border)
                g.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1)
            End Using

            DrawHeader(g, pad)

            Dim y = Px(HeaderHeight)
            Dim checksHeight = Px(Theme.Space12) * 2 + Px(CheckRowHeight) * 4
            Dim checksCard As New Rectangle(pad, y, contentWidth, checksHeight)
            DrawCard(g, checksCard)
            DrawChecks(g, checksCard)

            y = checksCard.Bottom + Px(Theme.Space16)
            Dim footerTop = ClientSize.Height - pad - Px(Theme.ButtonHeight)
            Dim tableCard As New Rectangle(pad, y, contentWidth, footerTop - Px(Theme.Space16) - y)
            DrawCard(g, tableCard)
            If _report IsNot Nothing AndAlso _report.Payments.Count > 0 Then
                DrawPaymentsTable(g, tableCard)
            Else
                DrawHelp(g, tableCard)
            End If

            DrawFooter(g, pad, footerTop)
        End Sub

        Private Sub DrawHeader(g As Graphics, pad As Integer)
            Dim x = pad
            Dim top = Px(20)

            ' PayAlert wordmark: a small Brand bell-in-ticket mark plus the name.
            Dim mark As New Rectangle(x, top, Px(22), Px(22))
            DrawMark(g, mark)
            Dim nameX = mark.Right + Px(Theme.Space8)
            TextRenderer.DrawText(g, "Pay", _wordmarkFont, New Point(nameX, top + Px(1)), Theme.TextPrimary, TextFormatFlags.NoPadding)
            Dim payWidth = TextRenderer.MeasureText(g, "Pay", _wordmarkFont, Size.Empty, TextFormatFlags.NoPadding).Width
            TextRenderer.DrawText(g, "Alert", _wordmarkFont, New Point(nameX + payWidth, top + Px(1)), Theme.Brand, TextFormatFlags.NoPadding)

            TextRenderer.DrawText(g, "PHASE 1  ·  SETUP CHECK", _captionBold,
                                  New Rectangle(x, top, ClientSize.Width - pad * 2, Px(22)), Theme.TextMuted,
                                  TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

            ' Page title on its header band with the short Brand underline accent.
            Dim titleTop = top + Px(32)
            TextRenderer.DrawText(g, "Database and demo data", _titleFont, New Point(x - Px(2), titleTop), Theme.TextPrimary, TextFormatFlags.NoPadding)
            Dim titleHeight = TextRenderer.MeasureText(g, "Ag", _titleFont, Size.Empty, TextFormatFlags.NoPadding).Height
            Using accent As New SolidBrush(Theme.Brand)
                g.FillRectangle(accent, x, titleTop + titleHeight + Px(4), Px(Theme.TitleAccentWidth), Px(Theme.TitleAccentHeight))
            End Using
        End Sub

        ''' <summary>The PayAlert mark: a rounded Brand tile with a white bell outline and a small clapper.</summary>
        Private Sub DrawMark(g As Graphics, r As Rectangle)
            Using tile = RoundedRect(r, Px(6))
                Using fill As New SolidBrush(Theme.Brand)
                    g.FillPath(fill, tile)
                End Using
            End Using
            Dim cx = r.X + r.Width / 2.0F
            Dim w = r.Width * 0.5F
            Dim topY = r.Y + r.Height * 0.24F
            Dim bottomY = r.Y + r.Height * 0.68F
            Using bell As New GraphicsPath()
                bell.AddArc(cx - w / 2, topY, w, w, 180, 180)
                bell.AddLine(cx + w / 2, topY + w / 2, cx + w / 2 + r.Width * 0.06F, bottomY)
                bell.AddLine(cx - w / 2 - r.Width * 0.06F, bottomY, cx - w / 2, topY + w / 2)
                bell.CloseFigure()
                Using pen As New Pen(Theme.TextOnBrand, Math.Max(1.5F, 1.6F * S))
                    pen.LineJoin = LineJoin.Round
                    g.DrawPath(pen, bell)
                End Using
            End Using
            Dim d = r.Width * 0.16F
            Using clapper As New SolidBrush(Theme.TextOnBrand)
                g.FillEllipse(clapper, cx - d / 2, bottomY + r.Height * 0.04F, d, d)
            End Using
        End Sub

        Private Sub DrawCard(g As Graphics, r As Rectangle)
            Dim radius = Px(Theme.CardRadius)
            ' Very soft shadow: two low-alpha passes offset downwards.
            For i = 1 To 2
                Dim shadowRect = New Rectangle(r.X, r.Y + Px(i), r.Width, r.Height)
                Using shadowPath = RoundedRect(shadowRect, radius)
                    Using shadow As New SolidBrush(Color.FromArgb(10, Theme.TextPrimary))
                        g.FillPath(shadow, shadowPath)
                    End Using
                End Using
            Next
            Using path = RoundedRect(r, radius)
                Using fill As New SolidBrush(Theme.Surface)
                    g.FillPath(fill, path)
                End Using
                Using pen As New Pen(Theme.Border)
                    g.DrawPath(pen, path)
                End Using
            End Using
        End Sub

        Private Sub DrawChecks(g As Graphics, card As Rectangle)
            Dim rows = BuildCheckRows()
            Dim y = card.Y + Px(Theme.Space12)
            For Each row In rows
                Dim rowRect As New Rectangle(card.X + Px(Theme.CardPadding), y, card.Width - Px(Theme.CardPadding) * 2, Px(CheckRowHeight))
                Dim badgeSize = Px(22)
                Dim badge As New Rectangle(rowRect.X, rowRect.Y + (rowRect.Height - badgeSize) \ 2, badgeSize, badgeSize)
                DrawBadge(g, badge, row.Item3)

                Dim labelX = badge.Right + Px(Theme.Space12)
                Dim labelWidth = Px(150)
                TextRenderer.DrawText(g, row.Item1, _bodyBold, New Rectangle(labelX, rowRect.Y, labelWidth, rowRect.Height),
                                      Theme.TextPrimary, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                Dim detailColor = If(row.Item3 = CheckState.Failed, Theme.OverdueText, Theme.TextMuted)
                TextRenderer.DrawText(g, row.Item2, _bodyFont,
                                      New Rectangle(labelX + labelWidth, rowRect.Y, rowRect.Right - labelX - labelWidth, rowRect.Height),
                                      detailColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
                y += Px(CheckRowHeight)
            Next
        End Sub

        Private Function BuildCheckRows() As List(Of Tuple(Of String, String, CheckState))
            Dim rows As New List(Of Tuple(Of String, String, CheckState))
            If _report Is Nothing Then
                rows.Add(Tuple.Create("Connection", "Checking the database...", CheckState.Running))
                rows.Add(Tuple.Create("Categories", "Waiting", CheckState.Running))
                rows.Add(Tuple.Create("Demo data", "Waiting", CheckState.Running))
                rows.Add(Tuple.Create("Statuses", "Waiting", CheckState.Running))
                Return rows
            End If

            Dim conn = _report.Connection
            If Not conn.Success Then
                rows.Add(Tuple.Create("Connection", conn.FriendlyMessage, CheckState.Failed))
                rows.Add(Tuple.Create("Categories", "Skipped until the database is reachable", CheckState.Skipped))
                rows.Add(Tuple.Create("Demo data", "Skipped until the database is reachable", CheckState.Skipped))
                rows.Add(Tuple.Create("Statuses", "Skipped until the database is reachable", CheckState.Skipped))
                Return rows
            End If

            rows.Add(Tuple.Create("Connection", "Connected to payalert_db  ·  server " & conn.ServerVersion, CheckState.Passed))

            If _report.CategoryCount >= 7 Then
                rows.Add(Tuple.Create("Categories", _report.CategoryCount & " categories found", CheckState.Passed))
            Else
                rows.Add(Tuple.Create("Categories", "Only " & _report.CategoryCount & " found. Run 03_seed.sql in phpMyAdmin.", CheckState.Failed))
            End If

            If _report.SeedError.Length > 0 Then
                rows.Add(Tuple.Create("Demo data", _report.SeedError, CheckState.Failed))
            ElseIf _report.Seed IsNot Nothing Then
                rows.Add(Tuple.Create("Demo data", _report.Seed.Message & "  (" & _report.UserCount & " users)", CheckState.Passed))
            Else
                rows.Add(Tuple.Create("Demo data", "Not checked", CheckState.Skipped))
            End If

            Dim total = _report.Payments.Count
            If total = 0 Then
                rows.Add(Tuple.Create("Statuses", "No demo payments found for " & DemoDataSeeder.DemoEmail, CheckState.Failed))
            Else
                Dim mismatched = _report.Payments.FindAll(Function(p) Not p.MatchesExpected).Count
                If mismatched = 0 Then
                    rows.Add(Tuple.Create("Statuses", "All " & total & " demo payments show the expected status", CheckState.Passed))
                Else
                    rows.Add(Tuple.Create("Statuses", mismatched & " of " & total & " payments differ from the expected status", CheckState.Failed))
                End If
            End If
            Return rows
        End Function

        Private Sub DrawBadge(g As Graphics, r As Rectangle, state As CheckState)
            Dim fore As Color
            Dim soft As Color
            Select Case state
                Case CheckState.Passed : fore = Theme.PaidText : soft = Theme.PaidSoft
                Case CheckState.Failed : fore = Theme.OverdueText : soft = Theme.OverdueSoft
                Case CheckState.Running : fore = Theme.Brand : soft = Theme.BrandSoft
                Case Else : fore = Theme.TextMuted : soft = Theme.AppBackground
            End Select

            Using fill As New SolidBrush(soft)
                g.FillEllipse(fill, r)
            End Using
            Using pen As New Pen(fore, Math.Max(1.6F, 1.8F * S))
                pen.StartCap = LineCap.Round
                pen.EndCap = LineCap.Round
                pen.LineJoin = LineJoin.Round
                Dim cx = r.X + r.Width / 2.0F
                Dim cy = r.Y + r.Height / 2.0F
                Dim u = r.Width / 22.0F
                Select Case state
                    Case CheckState.Passed
                        g.DrawLines(pen, {New PointF(cx - 5 * u, cy), New PointF(cx - 1.5F * u, cy + 3.5F * u), New PointF(cx + 5 * u, cy - 3.5F * u)})
                    Case CheckState.Failed
                        g.DrawLine(pen, cx - 4 * u, cy - 4 * u, cx + 4 * u, cy + 4 * u)
                        g.DrawLine(pen, cx + 4 * u, cy - 4 * u, cx - 4 * u, cy + 4 * u)
                    Case CheckState.Running
                        g.DrawArc(pen, cx - 5 * u, cy - 5 * u, 10 * u, 10 * u, -90, 270)
                    Case Else
                        g.DrawLine(pen, cx - 4 * u, cy, cx + 4 * u, cy)
                End Select
            End Using
        End Sub

        Private Sub DrawPaymentsTable(g As Graphics, card As Rectangle)
            Dim inner = Rectangle.Inflate(card, -Px(Theme.CardPadding), -Px(Theme.Space12))
            TextRenderer.DrawText(g, "Demo payments for Juan Dela Cruz, read from v_payment_summary", _sectionFont,
                                  New Rectangle(inner.X, inner.Y, inner.Width, Px(24)), Theme.TextPrimary,
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

            ' Column layout: Name | Direction | Total | Balance | Due | Status
            Dim widths = {0.27F, 0.15F, 0.14F, 0.14F, 0.14F, 0.16F}
            Dim headers = {"Payment", "Direction", "Total", "Balance", "Due", "Status"}
            Dim rightAligned = {False, False, True, True, False, False}
            Dim edgeSpace = Px(Theme.Space12)
            Dim tableX = inner.X + edgeSpace
            Dim tableWidth = inner.Width - edgeSpace
            Dim colX(widths.Length) As Integer
            colX(0) = tableX
            For i = 0 To widths.Length - 1
                colX(i + 1) = colX(i) + CInt(tableWidth * widths(i))
            Next
            Dim gap = Px(Theme.Space12)

            Dim y = inner.Y + Px(30)
            For i = 0 To headers.Length - 1
                Dim flags = TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or If(rightAligned(i), TextFormatFlags.Right, TextFormatFlags.Left)
                TextRenderer.DrawText(g, headers(i).ToUpperInvariant(), _captionBold,
                                      New Rectangle(colX(i), y, colX(i + 1) - colX(i) - gap, Px(TableHeaderHeight)), Theme.TextMuted, flags)
            Next
            y += Px(TableHeaderHeight)
            Using divider As New Pen(Theme.Border)
                g.DrawLine(divider, inner.X, y, inner.Right, y)
            End Using

            Dim rowHeight = Px(TableRowHeight)
            For Each item In _report.Payments
                If y + rowHeight > inner.Bottom + Px(4) Then Exit For

                ' Status edge: 4px rounded strip in the status color.
                Dim edge As New Rectangle(inner.X, y + Px(5), Px(Theme.StatusEdgeWidth), rowHeight - Px(10))
                Using edgePath = RoundedRect(edge, Px(2))
                    Using edgeBrush As New SolidBrush(Theme.StatusText(item.Status))
                        g.FillPath(edgeBrush, edgePath)
                    End Using
                End Using

                Dim cells = {item.Name, item.Direction, Format.Money(item.Total), Format.Money(item.Balance), Format.ShortDate(item.DueDate)}
                For i = 0 To cells.Length - 1
                    Dim flags = TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or
                                If(rightAligned(i), TextFormatFlags.Right, TextFormatFlags.Left)
                    TextRenderer.DrawText(g, cells(i), If(i = 0, _bodyBold, _bodyFont),
                                          New Rectangle(colX(i), y, colX(i + 1) - colX(i) - gap, rowHeight), Theme.TextPrimary, flags)
                Next
                DrawStatusPill(g, item, New Rectangle(colX(5), y, colX(6) - colX(5), rowHeight))

                y += rowHeight
                Using divider As New Pen(Color.FromArgb(140, Theme.Border))
                    g.DrawLine(divider, inner.X + edgeSpace, y, inner.Right, y)
                End Using
            Next
        End Sub

        Private Sub DrawStatusPill(g As Graphics, item As SetupCheckRow, cell As Rectangle)
            Dim textSize = TextRenderer.MeasureText(g, item.Status, _captionBold, Size.Empty, TextFormatFlags.NoPadding)
            Dim pill As New Rectangle(cell.X, cell.Y + (cell.Height - Px(20)) \ 2, textSize.Width + Px(16), Px(20))
            Using path = RoundedRect(pill, pill.Height \ 2)
                Using fill As New SolidBrush(Theme.StatusSoft(item.Status))
                    g.FillPath(fill, path)
                End Using
            End Using
            TextRenderer.DrawText(g, item.Status, _captionBold, pill, Theme.StatusText(item.Status),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            If Not item.MatchesExpected Then
                TextRenderer.DrawText(g, "expected " & item.ExpectedStatus, _captionFont,
                                      New Rectangle(pill.Right + Px(6), cell.Y, cell.Right - pill.Right, cell.Height), Theme.OverdueText,
                                      TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
            End If
        End Sub

        Private Sub DrawHelp(g As Graphics, card As Rectangle)
            Dim inner = Rectangle.Inflate(card, -Px(Theme.CardPadding), -Px(Theme.CardPadding))
            Dim heading = If(_report Is Nothing, "Running the checks...", "How to fix this")
            TextRenderer.DrawText(g, heading, _sectionFont, New Rectangle(inner.X, inner.Y, inner.Width, Px(24)), Theme.TextPrimary,
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            If _report Is Nothing Then Return

            Dim steps = {
                "Open the XAMPP Control Panel and click Start next to MySQL.",
                "Open http://localhost/phpmyadmin, then run Database\01_schema.sql, 02_views.sql and 03_seed.sql in the SQL tab, in that order.",
                "If MySQL won't start, another MySQL service may be using port 3306. Stop it in Windows Services first.",
                "Press F5 or click Run again."}
            Dim y = inner.Y + Px(36)
            For i = 0 To steps.Length - 1
                Dim dot As New Rectangle(inner.X, y, Px(22), Px(22))
                Using fill As New SolidBrush(Theme.BrandSoft)
                    g.FillEllipse(fill, dot)
                End Using
                TextRenderer.DrawText(g, (i + 1).ToString(), _captionBold, dot, Theme.BrandDark,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                Dim textWidth = inner.Right - dot.Right - Px(Theme.Space12)
                Dim flags = TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding
                Dim textHeight = TextRenderer.MeasureText(g, steps(i), _bodyFont, New Size(textWidth, Integer.MaxValue), flags).Height
                Dim textRect As New Rectangle(dot.Right + Px(Theme.Space12), y + Px(2), textWidth, textHeight)
                TextRenderer.DrawText(g, steps(i), _bodyFont, textRect, Theme.TextPrimary, flags)
                y += Math.Max(dot.Height, textHeight + Px(2)) + Px(Theme.Space12)
            Next
        End Sub

        Private Sub DrawFooter(g As Graphics, pad As Integer, top As Integer)
            Dim h = Px(Theme.ButtonHeight)
            Dim closeWidth = Px(112)
            Dim retryWidth = Px(112)
            _closeRect = New Rectangle(ClientSize.Width - pad - closeWidth, top, closeWidth, h)
            _retryRect = New Rectangle(_closeRect.X - Px(Theme.Space12) - retryWidth, top, retryWidth, h)

            Dim note = If(_report IsNot Nothing AndAlso _report.AllPassed,
                          "Phase 1 checks passed. Sign in arrives in Phase 3.",
                          "Enter or Esc closes  ·  F5 runs the checks again")
            TextRenderer.DrawText(g, note, _captionFont, New Rectangle(pad, top, _retryRect.X - pad - Px(Theme.Space12), h), Theme.TextMuted,
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

            ' Secondary button: white with a border.
            Dim retryEnabled = Not _running
            Using path = RoundedRect(_retryRect, Px(8))
                Using fill As New SolidBrush(If(_hoverButton = "retry" AndAlso retryEnabled, Theme.AppBackground, Theme.Surface))
                    g.FillPath(fill, path)
                End Using
                Using pen As New Pen(Theme.Border)
                    g.DrawPath(pen, path)
                End Using
            End Using
            TextRenderer.DrawText(g, If(_running, "Checking...", "Run again"), _bodyBold, _retryRect,
                                  If(retryEnabled, Theme.TextPrimary, Theme.TextMuted),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

            ' Primary button: Brand fill, white text.
            Using path = RoundedRect(_closeRect, Px(8))
                Using fill As New SolidBrush(If(_hoverButton = "close", Theme.BrandDark, Theme.Brand))
                    g.FillPath(fill, path)
                End Using
            End Using
            TextRenderer.DrawText(g, "Close", _bodyBold, _closeRect, Theme.TextOnBrand,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End Sub

        Private Shared Function RoundedRect(r As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)))
            path.AddArc(r.X, r.Y, d, d, 180, 90)
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            path.CloseFigure()
            Return path
        End Function

        ' ---------------------------------------------------------------- input

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim hover = If(_closeRect.Contains(e.Location), "close", If(_retryRect.Contains(e.Location), "retry", ""))
            If hover <> _hoverButton Then
                _hoverButton = hover
                Cursor = If(hover.Length > 0, Cursors.Hand, Cursors.Default)
                Invalidate(Rectangle.Union(_closeRect, _retryRect))
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _hoverButton.Length > 0 Then
                _hoverButton = ""
                Cursor = Cursors.Default
                Invalidate()
            End If
        End Sub

        Protected Overrides Async Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            If _closeRect.Contains(e.Location) Then
                Close()
            ElseIf _retryRect.Contains(e.Location) Then
                Await RunChecksAsync()
            ElseIf e.Y < Px(HeaderHeight) Then
                ' Drag the borderless window by its header.
                NativeMethods.ReleaseCapture()
                NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, New IntPtr(NativeMethods.HTCAPTION), IntPtr.Zero)
            End If
        End Sub

        Protected Overrides Async Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Select Case e.KeyCode
                Case Keys.Enter, Keys.Escape
                    e.Handled = True
                    Close()
                Case Keys.F5
                    e.Handled = True
                    Await RunChecksAsync()
            End Select
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _wordmarkFont.Dispose()
                _titleFont.Dispose()
                _sectionFont.Dispose()
                _bodyFont.Dispose()
                _bodyBold.Dispose()
                _captionFont.Dispose()
                _captionBold.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

        Private NotInheritable Class NativeMethods
            Public Const WM_NCLBUTTONDOWN As Integer = &HA1
            Public Const HTCAPTION As Integer = 2

            <DllImport("user32.dll")>
            Public Shared Function ReleaseCapture() As Boolean
            End Function

            <DllImport("user32.dll")>
            Public Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
            End Function
        End Class

    End Class

End Namespace
