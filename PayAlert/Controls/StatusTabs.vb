Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' Pill tabs with counts (All 9 · Upcoming 4 · Partial 2 · Paid 1 · Overdue 2). The chosen tab is a BrandSoft
    ''' pill with Brand text; each tab's count sits in a small badge. Left/Right arrows switch tabs.
    ''' </summary>
    <DefaultEvent("SelectedIndexChanged")>
    Public Class StatusTabs
        Inherits PAControl

        Private ReadOnly _names As New List(Of String)
        Private ReadOnly _counts As New List(Of Integer)
        Private _selectedIndex As Integer
        Private _hoverIndex As Integer = -1

        Public Event SelectedIndexChanged As EventHandler

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(520, Theme.FieldHeight)
        End Sub

        Public Sub SetTabs(ParamArray names As String())
            _names.Clear()
            _counts.Clear()
            _names.AddRange(names)
            For i = 1 To names.Length
                _counts.Add(0)
            Next
            _selectedIndex = 0
            Invalidate()
        End Sub

        Public Sub SetCount(index As Integer, count As Integer)
            If index < 0 OrElse index >= _counts.Count Then Return
            _counts(index) = count
            Invalidate()
        End Sub

        <Category("PayAlert"), DefaultValue(0)>
        Public Property SelectedIndex As Integer
            Get
                Return _selectedIndex
            End Get
            Set(value As Integer)
                Dim clamped = Math.Max(0, Math.Min(_names.Count - 1, value))
                If clamped = _selectedIndex Then Return
                _selectedIndex = clamped
                Invalidate()
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Width the tabs need, so the parent can place things beside them.</summary>
        Public Function PreferredWidth() As Integer
            Dim rects = TabRects()
            Return If(rects.Count = 0, 0, CInt(Math.Ceiling(rects(rects.Count - 1).Right)) + 2)
        End Function

        Private Function TabRects() As List(Of RectangleF)
            Dim list As New List(Of RectangleF)
            Dim x = 0.0F
            For i = 0 To _names.Count - 1
                Dim textWidth = TextRenderer.MeasureText(_names(i), Theme.FontBodyBold, Size.Empty, TextFormatFlags.NoPadding).Width
                Dim bw = BadgeWidth(_counts(i))
                Dim w = Px(14) + textWidth + Px(8) + bw + Px(10)
                list.Add(New RectangleF(x, 0, w, Height - 1))
                x += w + Px(6)
            Next
            Return list
        End Function

        Private Function BadgeWidth(count As Integer) As Single
            Dim w = TextRenderer.MeasureText(count.ToString(), Theme.FontCaptionBold, Size.Empty, TextFormatFlags.NoPadding).Width
            Return Math.Max(Px(22), w + Px(12))
        End Function

        Private Function IndexAt(p As Point) As Integer
            Dim rects = TabRects()
            For i = 0 To rects.Count - 1
                If rects(i).Contains(p) Then Return i
            Next
            Return -1
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim i = IndexAt(e.Location)
            If i <> _hoverIndex Then
                _hoverIndex = i
                Cursor = If(i >= 0, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hoverIndex = -1
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            Dim i = IndexAt(e.Location)
            If i >= 0 Then SelectedIndex = i
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Left OrElse keyData = Keys.Right OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Left AndAlso _selectedIndex > 0 Then SelectedIndex -= 1
            If e.KeyCode = Keys.Right AndAlso _selectedIndex < _names.Count - 1 Then SelectedIndex += 1
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
            Gfx.HighQuality(g)
            Dim rects = TabRects()
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine
            For i = 0 To rects.Count - 1
                Dim r = rects(i)
                Dim selected = i = _selectedIndex
                Dim radius = r.Height / 2.0F
                If selected Then
                    Gfx.FillRounded(g, r, radius, Theme.BrandSoft)
                    If Focused AndAlso ShowFocusCues Then Gfx.DrawRounded(g, r, radius, Theme.Brand, Px(1.25F))
                ElseIf i = _hoverIndex Then
                    Gfx.FillRounded(g, r, radius, Theme.NeutralSoft)
                End If

                Dim textColor = If(selected, Theme.BrandDark, If(i = _hoverIndex, Theme.TextPrimary, Theme.TextMuted))
                Dim textWidth = TextRenderer.MeasureText(_names(i), Theme.FontBodyBold, Size.Empty, TextFormatFlags.NoPadding).Width
                TextRenderer.DrawText(g, _names(i), If(selected, Theme.FontBodyBold, Theme.FontBody),
                                      New Rectangle(CInt(r.X + Px(14)), CInt(r.Y), textWidth + 4, CInt(r.Height)), textColor, flags)

                Dim bw = BadgeWidth(_counts(i))
                Dim badge As New RectangleF(r.X + Px(14) + textWidth + Px(8), r.Y + (r.Height - Px(20)) / 2.0F, bw, Px(20))
                Gfx.FillRounded(g, badge, badge.Height / 2.0F, If(selected, Theme.Brand, Theme.NeutralSoft))
                TextRenderer.DrawText(g, _counts(i).ToString(), Theme.FontCaptionBold, Rectangle.Round(badge),
                                      If(selected, Theme.TextOnBrand, Theme.TextMuted),
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Next
        End Sub

    End Class

End Namespace
