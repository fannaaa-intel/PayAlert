Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' The dashboard's "Upcoming Payments": a short list of rows, each with the compact due tag (its stub is the status
    ''' edge), the name with direction and person, the due phrase, the balance "of" the total, and the status pill.
    ''' Click a row, or move with Up/Down and press Enter, to open it.
    ''' </summary>
    Public Class UpcomingList
        Inherits PAControl

        Private _items As New List(Of PaymentSummary)
        Private _hover As Integer = -1
        Private _focusIndex As Integer = -1

        Public Event PaymentClicked As EventHandler(Of PaymentSummary)

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            BackColor = Theme.Surface
            Size = New Size(560, 300)
        End Sub

        Public Sub SetItems(items As IEnumerable(Of PaymentSummary))
            _items = New List(Of PaymentSummary)(items)
            _hover = -1
            _focusIndex = -1
            Cursor = Cursors.Default
            Invalidate()
        End Sub

        Public ReadOnly Property Items As IReadOnlyList(Of PaymentSummary)
            Get
                Return _items
            End Get
        End Property

        Private ReadOnly Property RowHeight As Integer
            Get
                Return PxI(58)
            End Get
        End Property

        Private Function IndexAt(y As Integer) As Integer
            Dim i = y \ RowHeight
            Return If(y >= 0 AndAlso i < _items.Count, i, -1)
        End Function

        ' ---------------------------------------------------------------- input

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim i = IndexAt(e.Y)
            If i <> _hover Then
                _hover = i
                Cursor = If(i >= 0, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = -1
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            Dim i = IndexAt(e.Y)
            If i >= 0 Then RaiseEvent PaymentClicked(Me, _items(i))
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Up OrElse keyData = Keys.Down OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If _items.Count = 0 Then Return
            If e.KeyCode = Keys.Down Then _focusIndex = Math.Min(_items.Count - 1, _focusIndex + 1) : Invalidate()
            If e.KeyCode = Keys.Up Then _focusIndex = Math.Max(0, _focusIndex - 1) : Invalidate()
        End Sub

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If keyData = Keys.Enter AndAlso Focused AndAlso _items.Count > 0 Then
                RaiseEvent PaymentClicked(Me, _items(Math.Max(0, _focusIndex)))
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            If _focusIndex < 0 AndAlso _items.Count > 0 Then _focusIndex = 0
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        ' ---------------------------------------------------------------- painting

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            ' Two-line blocks are top-aligned in boxes with a little headroom, so commas and descenders never clip.
            Dim topFlags = TextFormatFlags.NoPadding Or TextFormatFlags.Top Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            For i = 0 To _items.Count - 1
                Dim p = _items(i)
                Dim row As New Rectangle(0, i * RowHeight, Width, RowHeight)
                Dim keyboardFocus = Focused AndAlso ShowFocusCues AndAlso i = _focusIndex
                If i = _hover OrElse keyboardFocus Then
                    Gfx.FillRounded(g, New RectangleF(0, row.Y + Px(3), Width - 1, row.Height - Px(6)), Px(10),
                                    If(keyboardFocus, Theme.BrandSoft, Theme.Blend(Theme.Surface, Theme.AppBackground, 0.8)))
                End If
                If i < _items.Count - 1 Then
                    Using rule As New Pen(Theme.Border)
                        g.DrawLine(rule, Px(8), row.Bottom - 0.5F, Width - Px(8), row.Bottom - 0.5F)
                    End Using
                End If

                Dim x = Px(10)
                RowPainter.PaintDueTag(g, x, row, p.DueDate, p.Status, S)
                x += RowPainter.DueTagWidth(S) + Px(16)

                ' Right side, right to left: status pill, then balance "of" total.
                Dim pillWidth = Px(132)
                Dim pillX = Width - Px(10) - pillWidth
                RowPainter.PaintStatusPill(g, New Rectangle(CInt(pillX + pillWidth - RowPainter.StatusPillWidth(g, p.Status, S)), row.Y, CInt(pillWidth), row.Height), p.Status, S)
                Dim amountWidth = Px(124)
                Dim amountRight = pillX - Px(14)
                Dim amountRect As New Rectangle(CInt(amountRight - amountWidth), row.Y, CInt(amountWidth), row.Height)
                Dim lineHeight = TextRenderer.MeasureText(g, "Ag,", Theme.FontBodyBold, Size.Empty, topFlags).Height
                Dim captionHeight = TextRenderer.MeasureText(g, "Ag,", Theme.FontCaption, Size.Empty, topFlags).Height
                Dim gap = PxI(2)
                Dim top = row.Y + (row.Height - lineHeight - gap - captionHeight) \ 2
                TextRenderer.DrawText(g, Format.Money(p.Balance), Theme.FontBodyBold, New Rectangle(amountRect.X, top, amountRect.Width, lineHeight + PxI(4)),
                                      Theme.TextPrimary, topFlags Or TextFormatFlags.Right)
                TextRenderer.DrawText(g, "of " & Format.Money(p.TotalAmount), Theme.FontCaption,
                                      New Rectangle(amountRect.X, top + lineHeight + gap, amountRect.Width, captionHeight + PxI(4)), Theme.TextMuted, topFlags Or TextFormatFlags.Right)

                ' Middle: name over "I Owe · Landlord", and the due phrase.
                Dim nameRight = amountRect.X - Px(12)
                Dim nameRect As New Rectangle(CInt(x), top, CInt(nameRight - x), lineHeight + PxI(4))
                TextRenderer.DrawText(g, p.Name, Theme.FontBodyBold, nameRect, Theme.TextPrimary, topFlags)
                Dim arrow = 12 * S
                Dim lineTop = top + lineHeight + gap
                Icons.Draw(g, If(p.IsReceivable, IconKind.ArrowIn, IconKind.ArrowOut), New RectangleF(x, lineTop + (captionHeight - arrow) / 2.0F, arrow, arrow), Theme.TextMuted, 1.5F * S)
                Dim phrase = Format.DuePhrase(p.DaysUntilDue)
                Dim detail = p.DirectionLabel & If(p.Counterparty.Length > 0, "  ·  " & p.Counterparty, "")
                Dim detailX = CInt(x + arrow + Px(4))
                Dim phraseWidth = TextRenderer.MeasureText(g, phrase, Theme.FontCaptionBold, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width
                ' Urgent phrases are darker, not colored: the stub and the pill already carry the status color.
                Dim phraseColor = If(p.DaysUntilDue <= 1, Theme.TextPrimary, Theme.TextMuted)
                TextRenderer.DrawText(g, phrase, Theme.FontCaptionBold, New Rectangle(detailX, lineTop, phraseWidth + 2, captionHeight + PxI(4)), phraseColor, topFlags)
                TextRenderer.DrawText(g, "  ·  " & detail, Theme.FontCaption,
                                      New Rectangle(detailX + phraseWidth, lineTop, CInt(nameRight) - detailX - phraseWidth, captionHeight + PxI(4)), Theme.TextMuted, topFlags)
            Next
        End Sub

    End Class

End Namespace
