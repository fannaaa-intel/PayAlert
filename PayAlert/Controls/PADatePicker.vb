Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Globalization
Imports System.Windows.Forms
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' PayAlert date field: shows "Oct 15, 2026 · in 2 days" and opens PayAlert's own month calendar.
    ''' Replaces DateTimePicker. Up/Down move a day while closed; Enter, Space, F4 or Alt+Down open the calendar.
    ''' </summary>
    <DefaultEvent("ValueChanged")>
    Public Class PADatePicker
        Inherits PAControl

        Private _value As Date = Date.Today
        Private _hasError As Boolean
        Private _hover As Boolean
        Private _open As Boolean

        Public Event ValueChanged As EventHandler

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(220, 42)
        End Sub

        <Category("PayAlert")>
        Public Property Value As Date
            Get
                Return _value
            End Get
            Set(v As Date)
                If v.Date = _value Then Return
                _value = v.Date
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(False)>
        Public Property HasError As Boolean
            Get
                Return _hasError
            End Get
            Set(v As Boolean)
                _hasError = v
                Invalidate()
            End Set
        End Property

        Public Sub OpenCalendar()
            If _open OrElse Not Enabled Then Return
            Dim calendar As New CalendarPanel(_value)
            Dim popup As New PopupHost(calendar)
            AddHandler calendar.Picked,
                Sub(s, picked)
                    Value = picked
                    popup.Close()
                End Sub
            AddHandler popup.Closed,
                Sub(s, e)
                    _open = False
                    Invalidate()
                    Focus()
                    BeginInvoke(New MethodInvoker(AddressOf popup.Dispose))
                End Sub
            _open = True
            Invalidate()
            popup.ShowBelow(Me)
        End Sub

        Protected Overrides Sub OnClick(e As EventArgs)
            MyBase.OnClick(e)
            OpenCalendar()
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Select Case keyData
                Case Keys.Up, Keys.Down, Keys.Space, Keys.F4, Keys.Alt Or Keys.Down
                    Return True
            End Select
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyData = Keys.Space OrElse e.KeyData = Keys.F4 OrElse e.KeyData = (Keys.Alt Or Keys.Down) Then
                OpenCalendar()
            ElseIf e.KeyCode = Keys.Up Then
                Value = _value.AddDays(1)
            ElseIf e.KeyCode = Keys.Down Then
                Value = _value.AddDays(-1)
            Else
                Return
            End If
            e.Handled = True
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Dim active = Focused OrElse _open
            Dim field = FieldPainter.Paint(g, Me, S, _hasError, active, _hover, Enabled)
            Dim iconSize = Px(18)
            Dim x = field.X + Px(14)
            Icons.Draw(g, IconKind.Calendar, New RectangleF(x, (Height - iconSize) / 2.0F, iconSize, iconSize),
                       If(_hasError, Theme.OverdueText, If(active, Theme.Brand, Theme.TextMuted)), Px(1.6F))
            x += iconSize + Px(10)

            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine
            Dim dateText = Format.ShortDate(_value)
            Dim dateWidth = TextRenderer.MeasureText(g, dateText, Theme.FontBody, Size.Empty, flags).Width
            TextRenderer.DrawText(g, dateText, Theme.FontBody, New Rectangle(CInt(x), 0, dateWidth + 2, Height), Theme.TextPrimary, flags)
            Dim chevron = Px(16)
            Dim relative = "  ·  " & Format.RelativeDay(CInt((_value - Date.Today).TotalDays))
            TextRenderer.DrawText(g, relative, Theme.FontCaption,
                                  New Rectangle(CInt(x) + dateWidth, 0, CInt(field.Right - Px(14) - chevron - Px(6) - x - dateWidth), Height),
                                  Theme.TextMuted, flags Or TextFormatFlags.EndEllipsis)
            Icons.Draw(g, IconKind.ChevronDown, New RectangleF(field.Right - Px(14) - chevron, (Height - chevron) / 2.0F, chevron, chevron),
                       If(active, Theme.Brand, Theme.TextMuted), Px(1.7F))
        End Sub

    End Class

    ''' <summary>
    ''' PayAlert's month calendar: month title with previous/next, a Sunday-first grid, today ringed in Brand,
    ''' the chosen day filled in Brand, and a "Today" shortcut. Arrow keys, PageUp/PageDown and Enter work.
    ''' </summary>
    Friend NotInheritable Class CalendarPanel
        Inherits PAControl

        Private Const Cell As Integer = 38
        Private Const Pad As Integer = 12
        Private Const HeaderHeight As Integer = 42
        Private Const WeekdayHeight As Integer = 26
        Private Const FooterHeight As Integer = 42

        Private _month As Date
        Private _focusDay As Date
        Private ReadOnly _selected As Date
        Private _hoverDay As Date?
        Private _hoverZone As String = ""

        Public Event Picked As EventHandler(Of Date)

        Public Sub New(selected As Date)
            MyBase.New()
            _selected = selected.Date
            _focusDay = _selected
            _month = New Date(_selected.Year, _selected.Month, 1)
            SetStyle(ControlStyles.Selectable, True)
            BackColor = Theme.Surface
            Size = New Size(PxI(Pad * 2 + Cell * 7), PxI(Pad + HeaderHeight + WeekdayHeight + Cell * 6 + FooterHeight))
        End Sub

        Private ReadOnly Property GridTop As Single
            Get
                Return Px(Pad + HeaderHeight + WeekdayHeight)
            End Get
        End Property

        Private Function FirstShownDay() As Date
            Return _month.AddDays(-CInt(_month.DayOfWeek))
        End Function

        Private Function DayRect(index As Integer) As RectangleF
            Return New RectangleF(Px(Pad) + (index Mod 7) * Px(Cell), GridTop + (index \ 7) * Px(Cell), Px(Cell), Px(Cell))
        End Function

        Private ReadOnly Property PrevRect As RectangleF
            Get
                Return New RectangleF(Width - Px(Pad) - Px(64), Px(Pad + 7), Px(28), Px(28))
            End Get
        End Property

        Private ReadOnly Property NextRect As RectangleF
            Get
                Return New RectangleF(Width - Px(Pad) - Px(30), Px(Pad + 7), Px(28), Px(28))
            End Get
        End Property

        Private ReadOnly Property TodayRect As RectangleF
            Get
                Return New RectangleF(Px(Pad), Height - Px(FooterHeight) + Px(4), Px(70), Px(30))
            End Get
        End Property

        Private Function DayAt(p As Point) As Date?
            Dim first = FirstShownDay()
            For i = 0 To 41
                If DayRect(i).Contains(p) Then Return first.AddDays(i)
            Next
            Return Nothing
        End Function

        Private Sub ShowMonthOf(d As Date)
            _month = New Date(d.Year, d.Month, 1)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim zone = If(PrevRect.Contains(e.Location), "prev", If(NextRect.Contains(e.Location), "next", If(TodayRect.Contains(e.Location), "today", "")))
            Dim day = DayAt(e.Location)
            If zone <> _hoverZone OrElse Not Nullable.Equals(day, _hoverDay) Then
                _hoverZone = zone
                _hoverDay = day
                Cursor = If(zone.Length > 0 OrElse day.HasValue, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hoverDay = Nothing
            _hoverZone = ""
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If PrevRect.Contains(e.Location) Then
                ShowMonthOf(_month.AddMonths(-1))
            ElseIf NextRect.Contains(e.Location) Then
                ShowMonthOf(_month.AddMonths(1))
            ElseIf TodayRect.Contains(e.Location) Then
                RaiseEvent Picked(Me, Date.Today)
            Else
                Dim day = DayAt(e.Location)
                If day.HasValue Then RaiseEvent Picked(Me, day.Value)
            End If
        End Sub

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            ShowMonthOf(_month.AddMonths(If(e.Delta > 0, -1, 1)))
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Select Case keyData
                Case Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Enter, Keys.PageUp, Keys.PageDown, Keys.Home
                    Return True
            End Select
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Select Case e.KeyCode
                Case Keys.Left : _focusDay = _focusDay.AddDays(-1)
                Case Keys.Right : _focusDay = _focusDay.AddDays(1)
                Case Keys.Up : _focusDay = _focusDay.AddDays(-7)
                Case Keys.Down : _focusDay = _focusDay.AddDays(7)
                Case Keys.PageUp : _focusDay = _focusDay.AddMonths(-1)
                Case Keys.PageDown : _focusDay = _focusDay.AddMonths(1)
                Case Keys.Home : _focusDay = Date.Today
                Case Keys.Enter
                    RaiseEvent Picked(Me, _focusDay)
                    Return
                Case Else
                    Return
            End Select
            ShowMonthOf(_focusDay)
            e.Handled = True
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            g.Clear(Theme.Surface)
            Dim flags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine

            ' Month title and arrows.
            Dim title = _month.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            TextRenderer.DrawText(g, title, Theme.FontSection, New Rectangle(PxI(Pad + 4), PxI(Pad), Width, PxI(HeaderHeight)), Theme.TextPrimary,
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            For Each arrow In {Tuple.Create(PrevRect, IconKind.Back, "prev"), Tuple.Create(NextRect, IconKind.ChevronRight, "next")}
                If _hoverZone = arrow.Item3 Then Gfx.FillRounded(g, arrow.Item1, Px(8), Theme.NeutralSoft)
                Icons.Draw(g, arrow.Item2, RectangleF.Inflate(arrow.Item1, -Px(6), -Px(6)), Theme.TextPrimary, Px(1.8F))
            Next

            ' Weekday initials.
            Dim names = {"Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"}
            For i = 0 To 6
                TextRenderer.DrawText(g, names(i), Theme.FontCaptionBold,
                                      New Rectangle(PxI(Pad) + i * PxI(Cell), PxI(Pad + HeaderHeight), PxI(Cell), PxI(WeekdayHeight)),
                                      Theme.TextMuted, flags)
            Next

            ' Days.
            Dim first = FirstShownDay()
            Dim today = Date.Today
            For i = 0 To 41
                Dim day = first.AddDays(i)
                Dim r = RectangleF.Inflate(DayRect(i), -Px(3), -Px(3))
                Dim inMonth = day.Month = _month.Month
                Dim isSelected = day = _selected
                Dim isToday = day = today
                Dim isFocus = Focused AndAlso day = _focusDay AndAlso Not isSelected
                If isSelected Then
                    Using b As New SolidBrush(Theme.Brand)
                        g.FillEllipse(b, r)
                    End Using
                ElseIf isFocus Then
                    Using b As New SolidBrush(Theme.BrandSoft)
                        g.FillEllipse(b, r)
                    End Using
                ElseIf _hoverDay.HasValue AndAlso _hoverDay.Value = day Then
                    Using b As New SolidBrush(Theme.NeutralSoft)
                        g.FillEllipse(b, r)
                    End Using
                End If
                If isToday AndAlso Not isSelected Then
                    Using pen As New Pen(Theme.Brand, Px(1.5F))
                        g.DrawEllipse(pen, r)
                    End Using
                End If
                Dim color = If(isSelected, Theme.TextOnBrand, If(isToday, Theme.BrandDark,
                               If(inMonth, Theme.TextPrimary, Theme.Blend(Theme.TextMuted, Theme.Surface, 0.45))))
                TextRenderer.DrawText(g, day.Day.ToString(CultureInfo.InvariantCulture),
                                      If(isSelected OrElse isToday, Theme.FontBodyBold, Theme.FontBody), Rectangle.Round(r), color, flags)
            Next

            ' Footer: Today shortcut and today's date.
            Using rule As New Pen(Theme.Border)
                g.DrawLine(rule, Px(Pad), Height - Px(FooterHeight), Width - Px(Pad), Height - Px(FooterHeight))
            End Using
            Dim footerToday = TodayRect
            If _hoverZone = "today" Then Gfx.FillRounded(g, footerToday, Px(8), Theme.BrandSoft)
            TextRenderer.DrawText(g, "Today", Theme.FontBodyBold, Rectangle.Round(footerToday), Theme.BrandDark, flags)
            TextRenderer.DrawText(g, today.ToString("ddd, MMM d", CultureInfo.InvariantCulture), Theme.FontCaption,
                                  New Rectangle(0, CInt(footerToday.Y), Width - PxI(Pad + 4), CInt(footerToday.Height)), Theme.TextMuted,
                                  TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End Sub

    End Class

End Namespace
