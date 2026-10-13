Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' PayAlert dropdown: looks like a text field with a chevron and opens a rounded list. Replaces ComboBox.
    ''' Up/Down change the choice while closed; Enter, Space, F4 or Alt+Down open the list.
    ''' </summary>
    <DefaultEvent("SelectedIndexChanged")>
    Public Class PASelect
        Inherits PAControl

        Private ReadOnly _items As New List(Of Object)
        Private _selectedIndex As Integer = -1
        Private _placeholder As String = "Select"
        Private _icon As IconKind = IconKind.None
        Private _hasError As Boolean
        Private _hover As Boolean
        Private _open As Boolean

        Public Event SelectedIndexChanged As EventHandler

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(220, 42)
        End Sub

        Public Sub SetItems(items As IEnumerable(Of Object), Optional selectIndex As Integer = -1)
            _items.Clear()
            _items.AddRange(items)
            _selectedIndex = Math.Min(selectIndex, _items.Count - 1)
            Invalidate()
        End Sub

        <Browsable(False)>
        Public ReadOnly Property Items As IReadOnlyList(Of Object)
            Get
                Return _items
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
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

        <Browsable(False)>
        Public ReadOnly Property SelectedItem As Object
            Get
                Return If(_selectedIndex < 0, Nothing, _items(_selectedIndex))
            End Get
        End Property

        <Category("PayAlert"), DefaultValue("Select")>
        Public Property PlaceholderText As String
            Get
                Return _placeholder
            End Get
            Set(value As String)
                _placeholder = If(value, "")
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(GetType(IconKind), "None")>
        Public Property Icon As IconKind
            Get
                Return _icon
            End Get
            Set(value As IconKind)
                _icon = value
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(False)>
        Public Property HasError As Boolean
            Get
                Return _hasError
            End Get
            Set(value As Boolean)
                _hasError = value
                Invalidate()
            End Set
        End Property

        ' ---------------------------------------------------------------- open the list

        Public Sub OpenList()
            If _open OrElse _items.Count = 0 OrElse Not Enabled Then Return
            Dim texts As New List(Of String)
            For Each item In _items
                texts.Add(If(item?.ToString(), ""))
            Next
            Dim list As New SelectList(texts, _selectedIndex, Width)
            Dim popup As New PopupHost(list)
            AddHandler list.Picked,
                Sub(s, index)
                    SelectedIndex = index
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
            OpenList()
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
                OpenList()
                e.Handled = True
            ElseIf e.KeyCode = Keys.Down AndAlso _selectedIndex < _items.Count - 1 Then
                SelectedIndex += 1
                e.Handled = True
            ElseIf e.KeyCode = Keys.Up AndAlso _selectedIndex > 0 Then
                SelectedIndex -= 1
                e.Handled = True
            End If
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
            Dim x = field.X + Px(14)
            If _icon <> IconKind.None Then
                Dim size = Px(18)
                Icons.Draw(g, _icon, New RectangleF(x, (Height - size) / 2.0F, size, size),
                           If(_hasError, Theme.OverdueText, If(active, Theme.Brand, Theme.TextMuted)), Px(1.6F))
                x += size + Px(10)
            End If
            Dim chevron = Px(16)
            Dim text = If(_selectedIndex >= 0, If(_items(_selectedIndex)?.ToString(), ""), _placeholder)
            TextRenderer.DrawText(g, text, Theme.FontBody,
                                  New Rectangle(CInt(x), 0, CInt(field.Right - Px(14) - chevron - Px(8) - x), Height),
                                  If(_selectedIndex >= 0, Theme.TextPrimary, Theme.TextMuted),
                                  TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
            Icons.Draw(g, IconKind.ChevronDown, New RectangleF(field.Right - Px(14) - chevron, (Height - chevron) / 2.0F, chevron, chevron),
                       If(active, Theme.Brand, Theme.TextMuted), Px(1.7F))
        End Sub

    End Class

    ''' <summary>Shared field frame for PASelect and PADatePicker: rounded box, focus/error ring, border.</summary>
    Friend NotInheritable Class FieldPainter
        Private Sub New()
        End Sub

        ''' <summary>Paints the frame and returns the inner field rectangle.</summary>
        Public Shared Function Paint(g As Graphics, control As Control, s As Single, hasError As Boolean, active As Boolean,
                                     hover As Boolean, enabled As Boolean) As RectangleF
            Gfx.HighQuality(g)
            Dim ring = 3 * s
            Dim field As New RectangleF(ring, ring, control.Width - ring * 2 - 1, control.Height - ring * 2 - 1)
            Dim radius = 9 * s
            If hasError OrElse active Then
                Gfx.FillRounded(g, RectangleF.Inflate(field, ring - 0.5F * s, ring - 0.5F * s), radius + ring,
                                If(hasError, Theme.OverdueSoft, Theme.BrandSoft))
            End If
            Gfx.FillRounded(g, field, radius, If(enabled, Theme.Surface, Theme.AppBackground))
            Dim border = If(hasError, Theme.OverdueText, If(active, Theme.Brand, If(hover, Theme.BorderStrong, Theme.CardBorder)))
            Gfx.DrawRounded(g, field, radius, border, If(active OrElse hasError, 1.25F * s, 1.0F))
            Return field
        End Function
    End Class

    ''' <summary>The list shown inside a PASelect popup.</summary>
    Friend NotInheritable Class SelectList
        Inherits PAControl

        Private ReadOnly _texts As List(Of String)
        Private ReadOnly _selected As Integer
        Private _hover As Integer

        Public Event Picked As EventHandler(Of Integer)

        Public Sub New(texts As List(Of String), selected As Integer, width As Integer)
            MyBase.New()
            _texts = texts
            _selected = selected
            _hover = Math.Max(0, selected)
            SetStyle(ControlStyles.Selectable, True)
            BackColor = Theme.Surface
            Cursor = Cursors.Hand
            Size = New Size(width, PxI(6) * 2 + RowHeight * texts.Count)
        End Sub

        Private ReadOnly Property RowHeight As Integer
            Get
                Return PxI(36)
            End Get
        End Property

        Private Function IndexAt(y As Integer) As Integer
            Dim i = (y - PxI(6)) \ RowHeight
            Return If(i >= 0 AndAlso i < _texts.Count, i, -1)
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim i = IndexAt(e.Y)
            If i >= 0 AndAlso i <> _hover Then
                _hover = i
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            Dim i = IndexAt(e.Y)
            If i >= 0 Then RaiseEvent Picked(Me, i)
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Up OrElse keyData = Keys.Down OrElse keyData = Keys.Enter OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Down AndAlso _hover < _texts.Count - 1 Then _hover += 1 : Invalidate()
            If e.KeyCode = Keys.Up AndAlso _hover > 0 Then _hover -= 1 : Invalidate()
            If e.KeyCode = Keys.Enter Then RaiseEvent Picked(Me, _hover)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            g.Clear(Theme.Surface)
            For i = 0 To _texts.Count - 1
                Dim row As New RectangleF(Px(6), PxI(6) + i * RowHeight, Width - Px(12), RowHeight)
                If i = _hover Then Gfx.FillRounded(g, row, Px(8), Theme.NeutralSoft)
                Dim selected = i = _selected
                TextRenderer.DrawText(g, _texts(i), If(selected, Theme.FontBodyBold, Theme.FontBody),
                                      New Rectangle(CInt(row.X + Px(12)), CInt(row.Y), CInt(row.Width - Px(44)), CInt(row.Height)),
                                      If(selected, Theme.BrandDark, Theme.TextPrimary),
                                      TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
                If selected Then
                    Dim size = Px(16)
                    Icons.Draw(g, IconKind.Check, New RectangleF(row.Right - Px(12) - size, row.Y + (row.Height - size) / 2.0F, size, size), Theme.Brand, Px(2))
                End If
            Next
        End Sub

    End Class

End Namespace
