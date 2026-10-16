Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' The Payment Summary list: one row per payment method, biggest first. Each row has the method name and how many
    ''' records, a bar for its share of the total (one brand color; the longest bar is the biggest method), the amount,
    ''' and its percent. Under the name, the split into paid (I Owe) and collected (Owed to Me) when both happened.
    ''' Display only. With more rows than fit, the mouse wheel scrolls and the bottom fades.
    ''' </summary>
    Public Class MethodBreakdown
        Inherits PAControl

        Private _report As New MethodReport()
        Private _scroll As Integer

        Public Sub New()
            MyBase.New()
            ' Selectable only so the mouse wheel reaches it (it takes focus while the pointer is over it); never a Tab stop.
            SetStyle(ControlStyles.Selectable, True)
            TabStop = False
        End Sub

        Public Property Report As MethodReport
            Get
                Return _report
            End Get
            Set(value As MethodReport)
                _report = If(value, New MethodReport())
                _scroll = 0
                Invalidate()
            End Set
        End Property

        ''' <summary>"GCash ₱1,000.00 (40%) · Cash ...", for tests and screen readers.</summary>
        Public Overrides Property Text As String
            Get
                Return String.Join(" · ", _report.Methods.ConvertAll(Function(m) m.Method & " " & Format.Money(m.Total) & " (" & Percent(m) & ")"))
            End Get
            Set(value As String)
            End Set
        End Property

        Private ReadOnly Property RowHeight As Integer
            Get
                Return PxI(If(Theme.IsLargeText, 66, 58))
            End Get
        End Property

        Private Function Percent(m As MethodTotal) As String
            Dim share = _report.Share(m) * 100
            Return If(share > 0 AndAlso share < 1, "<1%", Math.Round(share).ToString("0") & "%")
        End Function

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            Dim maxScroll = Math.Max(0, _report.Methods.Count * RowHeight - Height)
            _scroll = Math.Max(0, Math.Min(maxScroll, _scroll - Math.Sign(e.Delta) * RowHeight))
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            ' Take the wheel while the pointer is over a list that is taller than its space.
            If Not Focused AndAlso _report.Methods.Count * RowHeight > Height Then Focus()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            If _report.Methods.Count = 0 Then Return

            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            Dim measure = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim biggest = 0D
            For Each m In _report.Methods
                biggest = Math.Max(biggest, m.Total)
            Next

            ' Columns: name block | bar | amount | percent.
            ' Wide enough for "5 records · Paid ₱2,400.00 · Collected ₱1,000.00", also with Large text.
            Dim nameWidth = Math.Min(PxI(If(Theme.IsLargeText, 350, 280)), Width * 2 \ 5)
            Dim percentWidth = PxI(52)
            Dim amountWidth = TextRenderer.MeasureText(g, "₱000,000,000.00", Theme.FontSection, Size.Empty, measure).Width
            Dim barLeft = nameWidth + PxI(16)
            Dim barRight = Width - percentWidth - amountWidth - PxI(28)
            Dim nameHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontBodyBold, Size.Empty, measure).Height
            Dim detailHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontCaption, Size.Empty, measure).Height

            Dim y = -_scroll
            For i = 0 To _report.Methods.Count - 1
                Dim m = _report.Methods(i)
                Dim row As New Rectangle(0, y, Width, RowHeight)
                y += RowHeight
                If row.Bottom < 0 OrElse row.Top > Height Then Continue For

                If i > 0 Then
                    Using rule As New Pen(Theme.Border)
                        g.DrawLine(rule, 0, row.Top, Width, row.Top)
                    End Using
                End If

                ' Name, then "3 records" or the paid/collected split.
                Dim detail = m.Records & If(m.Records = 1, " record", " records")
                If m.Paid > 0D AndAlso m.Collected > 0D Then
                    detail &= "  ·  Paid " & Format.Money(m.Paid) & "  ·  Collected " & Format.Money(m.Collected)
                End If
                Dim top = row.Top + (row.Height - nameHeight - detailHeight - PxI(2)) \ 2
                TextRenderer.DrawText(g, m.Method, Theme.FontBodyBold, New Rectangle(0, top, nameWidth, nameHeight),
                                      If(m.Method = ReportService.NotSet, Theme.TextMuted, Theme.TextPrimary), flags)
                TextRenderer.DrawText(g, detail, Theme.FontCaption, New Rectangle(0, top + nameHeight + PxI(2), nameWidth, detailHeight), Theme.TextMuted, flags)

                ' Bar on a soft track; at least a sliver so small methods still show.
                If barRight > barLeft Then
                    Dim barHeight = Px(12)
                    Dim track As New RectangleF(barLeft, row.Top + (row.Height - barHeight) / 2.0F, barRight - barLeft, barHeight)
                    Gfx.FillRounded(g, track, barHeight / 2.0F, Theme.NeutralSoft)
                    If biggest > 0D AndAlso m.Total > 0D Then
                        Dim w = Math.Max(barHeight, CSng(track.Width * m.Total / biggest))
                        Gfx.FillRounded(g, New RectangleF(track.X, track.Y, w, barHeight), barHeight / 2.0F, Theme.Brand)
                    End If
                End If

                Dim amountRect As New Rectangle(Width - percentWidth - amountWidth - PxI(12), row.Top, amountWidth, row.Height)
                TextRenderer.DrawText(g, Format.Money(m.Total), Theme.FontSection, amountRect, Theme.TextPrimary,
                                      flags Or TextFormatFlags.Right Or TextFormatFlags.VerticalCenter)
                TextRenderer.DrawText(g, Percent(m), Theme.FontBodySemibold, New Rectangle(Width - percentWidth, row.Top, percentWidth, row.Height),
                                      Theme.TextMuted, flags Or TextFormatFlags.Right Or TextFormatFlags.VerticalCenter)
            Next

            ' More rows below: fade the bottom edge and show a small chevron, like the payment lists.
            If y > Height Then
                Dim h = PxI(32)
                Dim band As New Rectangle(0, Height - h, Width, h)
                Using fade As New Drawing2D.LinearGradientBrush(New Rectangle(band.X, band.Y - 1, band.Width, band.Height + 2),
                                                                Color.FromArgb(0, Theme.Surface), Theme.Surface, Drawing2D.LinearGradientMode.Vertical)
                    g.FillRectangle(fade, band)
                End Using
                Dim size = Px(16)
                Icons.Draw(g, IconKind.ChevronDown, New RectangleF((Width - size) / 2.0F, Height - size - Px(4), size, size), Theme.TextMuted, Px(1.8F))
            End If
        End Sub

    End Class

End Namespace
