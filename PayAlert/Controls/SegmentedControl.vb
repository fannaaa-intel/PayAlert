Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' A row of equal-width options in a rounded track (I Owe / Owed to Me, 1 / 3 / 7 days, No / Weekly / Monthly).
    ''' The chosen option sits on a white raised segment with Brand text. Left/Right arrows change it.
    ''' </summary>
    <DefaultEvent("SelectedIndexChanged")>
    Public Class SegmentedControl
        Inherits PAControl

        Private ReadOnly _items As New List(Of String)
        Private _selectedIndex As Integer = -1
        Private _hoverIndex As Integer = -1

        Public Event SelectedIndexChanged As EventHandler

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(260, Theme.FieldHeight)
        End Sub

        Public Sub SetItems(ParamArray items As String())
            _items.Clear()
            _items.AddRange(items)
            _selectedIndex = If(_items.Count > 0, 0, -1)
            Invalidate()
        End Sub

        <Browsable(False)>
        Public ReadOnly Property Items As IReadOnlyList(Of String)
            Get
                Return _items
            End Get
        End Property

        <Category("PayAlert"), DefaultValue(-1)>
        Public Property SelectedIndex As Integer
            Get
                Return _selectedIndex
            End Get
            Set(value As Integer)
                Dim clamped = Math.Max(-1, Math.Min(_items.Count - 1, value))
                If clamped = _selectedIndex Then Return
                _selectedIndex = clamped
                Invalidate()
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Function SegmentRect(index As Integer) As RectangleF
            Dim inset = Px(3)
            Dim w = (Width - 1 - inset * 2) / Math.Max(1, _items.Count)
            Return New RectangleF(inset + index * w, inset, w, Height - 1 - inset * 2)
        End Function

        Private Function IndexAt(p As Point) As Integer
            For i = 0 To _items.Count - 1
                If SegmentRect(i).Contains(p) Then Return i
            Next
            Return -1
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim i = IndexAt(e.Location)
            If i <> _hoverIndex Then
                _hoverIndex = i
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
            If e.KeyCode = Keys.Left AndAlso _selectedIndex > 0 Then SelectedIndex -= 1 : e.Handled = True
            If e.KeyCode = Keys.Right AndAlso _selectedIndex < _items.Count - 1 Then SelectedIndex += 1 : e.Handled = True
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim track As New RectangleF(0, 0, Width - 1, Height - 1)
            Dim radius = Px(10)
            Gfx.FillRounded(g, track, radius, Theme.AppBackground)
            Gfx.DrawRounded(g, track, radius, If(Focused AndAlso ShowFocusCues, Theme.Brand, Theme.CardBorder))

            Dim flags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or
                        TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            For i = 0 To _items.Count - 1
                Dim r = SegmentRect(i)
                Dim selected = i = _selectedIndex
                If selected Then
                    Dim lifted = r
                    lifted.Offset(0, Px(1))
                    Gfx.FillRounded(g, lifted, radius - Px(3), Color.FromArgb(18, Theme.Overlay))
                    Gfx.FillRounded(g, r, radius - Px(3), Theme.Surface)
                    Gfx.DrawRounded(g, r, radius - Px(3), Theme.CardBorder)
                ElseIf i = _hoverIndex AndAlso Enabled Then
                    Gfx.FillRounded(g, r, radius - Px(3), Theme.NeutralSoft)
                End If
                Dim textColor = If(Not Enabled, Theme.Blend(Theme.TextMuted, Theme.AppBackground, 0.4),
                               If(selected, Theme.BrandDark, If(i = _hoverIndex, Theme.TextPrimary, Theme.TextMuted)))
                TextRenderer.DrawText(g, _items(i), If(selected, Theme.FontBodyBold, Theme.FontBody), Rectangle.Round(r), textColor, flags)
            Next
        End Sub

    End Class

End Namespace
