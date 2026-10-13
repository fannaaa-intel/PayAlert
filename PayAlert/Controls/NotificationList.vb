Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Windows.Forms
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' Notification cards (§7 ucNotifications), newest day first under "Today" / "Yesterday" / date headings.
    ''' Each card: a colored dot (Overdue red, Due Today/Tomorrow orange, Due Soon yellow), a bold title, the message and
    ''' a relative time. Unread cards sit on BrandSoft. Click (or Up/Down and Enter) opens one. No visible scrollbar:
    ''' the wheel scrolls and the bottom fades when more is below.
    ''' </summary>
    Public Class NotificationList
        Inherits PAControl

        Private Const HeaderHeight As Integer = 34
        Private Const CardHeight As Integer = 76
        Private Const CardGap As Integer = 10

        Private NotInheritable Class Slot
            Public Bounds As Rectangle         ' in content coordinates (before scrolling)
            Public Index As Integer = -1       ' -1 for a day heading
            Public Heading As String
        End Class

        Private _items As New List(Of Notification)
        Private ReadOnly _slots As New List(Of Slot)
        Private _contentHeight As Integer
        Private _offset As Integer
        Private _hover As Integer = -1
        Private _focusIndex As Integer = -1

        Public Event NotificationClicked As EventHandler(Of Notification)

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            BackColor = Theme.AppBackground
            Size = New Size(600, 400)
        End Sub

        Public Sub SetItems(items As IEnumerable(Of Notification))
            _items = New List(Of Notification)(items)
            _offset = 0
            _hover = -1
            _focusIndex = -1
            BuildSlots()
            Invalidate()
        End Sub

        Public ReadOnly Property Items As IReadOnlyList(Of Notification)
            Get
                Return _items
            End Get
        End Property

        ' ---------------------------------------------------------------- layout

        Private Sub BuildSlots()
            _slots.Clear()
            Dim y = 0
            Dim lastDay As Date? = Nothing
            For i = 0 To _items.Count - 1
                Dim day = _items(i).NotifDate
                If Not lastDay.HasValue OrElse lastDay.Value <> day Then
                    If lastDay.HasValue Then y += PxI(8)
                    _slots.Add(New Slot With {.Bounds = New Rectangle(0, y, Width, PxI(HeaderHeight)), .Heading = DayHeading(day)})
                    y += PxI(HeaderHeight)
                    lastDay = day
                End If
                _slots.Add(New Slot With {.Bounds = New Rectangle(0, y, Width, PxI(CardHeight)), .Index = i})
                y += PxI(CardHeight) + PxI(CardGap)
            Next
            _contentHeight = y
            ClampOffset()
        End Sub

        Private Shared Function DayHeading(day As Date) As String
            If day = Date.Today Then Return "TODAY"
            If day = Date.Today.AddDays(-1) Then Return "YESTERDAY"
            Return day.ToString("ddd, MMM d", CultureInfo.InvariantCulture).ToUpperInvariant()
        End Function

        Private Sub ClampOffset()
            _offset = Math.Max(0, Math.Min(_offset, _contentHeight - Height))
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            BuildSlots()
            Invalidate()
        End Sub

        Private Function IndexAt(pt As Point) As Integer
            Dim y = pt.Y + _offset
            For Each slot In _slots
                If slot.Index >= 0 AndAlso slot.Bounds.Contains(pt.X, y) Then Return slot.Index
            Next
            Return -1
        End Function

        ''' <summary>Scrolls so the given card is fully visible.</summary>
        Private Sub EnsureVisible(index As Integer)
            For Each slot In _slots
                If slot.Index <> index Then Continue For
                If slot.Bounds.Top < _offset Then _offset = slot.Bounds.Top - PxI(HeaderHeight)
                If slot.Bounds.Bottom > _offset + Height Then _offset = slot.Bounds.Bottom - Height + PxI(4)
                ClampOffset()
            Next
        End Sub

        ' ---------------------------------------------------------------- input

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            _offset -= Math.Sign(e.Delta) * PxI(CardHeight + CardGap)
            ClampOffset()
            _hover = IndexAt(e.Location)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim i = IndexAt(e.Location)
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

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            Dim i = IndexAt(e.Location)
            If i >= 0 Then RaiseEvent NotificationClicked(Me, _items(i))
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Up OrElse keyData = Keys.Down OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If _items.Count = 0 Then Return
            If e.KeyCode = Keys.Down Then _focusIndex = Math.Min(_items.Count - 1, _focusIndex + 1)
            If e.KeyCode = Keys.Up Then _focusIndex = Math.Max(0, _focusIndex - 1)
            If e.KeyCode = Keys.Down OrElse e.KeyCode = Keys.Up Then
                EnsureVisible(_focusIndex)
                Invalidate()
            End If
        End Sub

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If keyData = Keys.Enter AndAlso Focused AndAlso _items.Count > 0 Then
                RaiseEvent NotificationClicked(Me, _items(Math.Max(0, _focusIndex)))
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

        ''' <summary>Dot color per type (§7): Overdue red, Due Today/Tomorrow orange, Due Soon yellow.</summary>
        Public Shared Function DotColor(type As String) As Color
            Select Case type
                Case Notification.TypeOverdue : Return Theme.OverdueText
                Case Notification.TypeDueToday, Notification.TypeDueTomorrow : Return Theme.Brand
                Case Else : Return Theme.UpcomingText
            End Select
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            g.Clear(Theme.AppBackground)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.Top Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis

            For Each slot In _slots
                Dim r = slot.Bounds
                r.Offset(0, -_offset)
                If r.Bottom < 0 OrElse r.Top > Height Then Continue For

                If slot.Index < 0 Then
                    ' Day heading with a hairline to the right.
                    Dim headingSize = TextRenderer.MeasureText(g, slot.Heading, Theme.FontCaptionBold, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
                    Dim textY = r.Y + (r.Height - headingSize.Height) \ 2
                    TextRenderer.DrawText(g, slot.Heading, Theme.FontCaptionBold, New Point(PxI(4), textY), Theme.TextMuted, TextFormatFlags.NoPadding)
                    Using rule As New Pen(Theme.Border)
                        Dim lineY = r.Y + r.Height / 2.0F
                        g.DrawLine(rule, PxI(4) + headingSize.Width + Px(12), lineY, Width - Px(4), lineY)
                    End Using
                    Continue For
                End If

                Dim n = _items(slot.Index)
                Dim hover = slot.Index = _hover
                Dim keyboardFocus = Focused AndAlso ShowFocusCues AndAlso slot.Index = _focusIndex
                Dim face As New RectangleF(Px(1), r.Y + Px(1), Width - Px(2) - 1, r.Height - Px(4))
                Dim radius = Px(Theme.CardRadius)
                Gfx.SoftShadow(g, face, radius, Px(2), If(hover, 2, 1))
                Gfx.FillRounded(g, face, radius, If(n.IsRead, Theme.Surface, Theme.BrandSoft))
                Dim border = If(keyboardFocus OrElse hover, Theme.Blend(Theme.CardBorder, Theme.Brand, If(keyboardFocus, 0.7, 0.35)),
                                If(n.IsRead, Theme.CardBorder, Theme.Blend(Theme.CardBorder, Theme.Brand, 0.25)))
                Gfx.DrawRounded(g, face, radius, border, If(keyboardFocus, Px(1.5F), 1.0F))

                ' Colored dot with a soft halo.
                Dim dot = Px(10)
                Dim dotX = face.X + Px(20)
                Dim dotY = face.Y + Px(19)
                Using halo As New SolidBrush(Color.FromArgb(46, DotColor(n.Type)))
                    g.FillEllipse(halo, dotX - Px(4), dotY - Px(4), dot + Px(8), dot + Px(8))
                End Using
                Using brush As New SolidBrush(DotColor(n.Type))
                    g.FillEllipse(brush, dotX, dotY, dot, dot)
                End Using

                ' Relative time on the right, then title and message.
                Dim timeText = Format.Relative(n.CreatedAt)
                Dim timeSize = TextRenderer.MeasureText(g, timeText, Theme.FontCaption, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
                Dim right = CInt(face.Right - Px(18))
                TextRenderer.DrawText(g, timeText, Theme.FontCaption, New Point(right - timeSize.Width, CInt(face.Y + Px(16))), Theme.TextMuted, TextFormatFlags.NoPadding)
                If Not n.IsRead Then
                    ' "New" marker under the time.
                    Dim mark = Px(7)
                    Using brush As New SolidBrush(Theme.Brand)
                        g.FillEllipse(brush, right - mark, face.Y + Px(40), mark, mark)
                    End Using
                End If

                Dim textX = CInt(dotX + dot + Px(14))
                Dim textWidth = right - timeSize.Width - PxI(16) - textX
                Dim titleFont = If(n.IsRead, Theme.FontBodySemibold, Theme.FontBodyBold)
                Dim titleHeight = TextRenderer.MeasureText(g, "Ag", titleFont, Size.Empty, flags).Height
                TextRenderer.DrawText(g, n.Title, titleFont, New Rectangle(textX, CInt(face.Y + Px(14)), textWidth, titleHeight + PxI(4)), Theme.TextPrimary, flags)
                TextRenderer.DrawText(g, n.Message, Theme.FontBody, New Rectangle(textX, CInt(face.Y + Px(16)) + titleHeight, right - PxI(24) - textX, PxI(24)),
                                      If(n.IsRead, Theme.TextMuted, Theme.TextPrimary), flags)
            Next

            ' More below and no scrollbar: fade the bottom edge and show a small chevron.
            If _offset + Height < _contentHeight - PxI(CardGap) Then
                Dim h = PxI(40)
                Dim band As New Rectangle(0, Height - h, Width, h)
                Using fade As New Drawing2D.LinearGradientBrush(New Rectangle(band.X, band.Y - 1, band.Width, band.Height + 2),
                                                                Color.FromArgb(0, Theme.AppBackground), Theme.AppBackground, Drawing2D.LinearGradientMode.Vertical)
                    g.FillRectangle(fade, band)
                End Using
                Dim size = Px(16)
                Icons.Draw(g, IconKind.ChevronDown, New RectangleF((Width - size) / 2.0F, Height - size - Px(6), size, size), Theme.TextMuted, Px(1.8F))
            End If
        End Sub

    End Class

End Namespace
