Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    Public Enum ButtonKind
        Primary
        Secondary
        Danger
        DangerText
        Ghost
    End Enum

    ''' <summary>
    ''' PayAlert button: flat, rounded, 36px tall, optional line icon, hover/pressed/focus states.
    ''' Implements IButtonControl so forms can use it as Accept/Cancel button; Enter and Space click it.
    ''' </summary>
    <DefaultEvent("Click")>
    Public Class PAButton
        Inherits PAControl
        Implements IButtonControl

        Private _kind As ButtonKind = ButtonKind.Primary
        Private _icon As IconKind = IconKind.None
        Private _hover As Boolean
        Private _pressed As Boolean
        Private _dialogResult As DialogResult = DialogResult.None

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            SetStyle(ControlStyles.StandardDoubleClick, False)
            TabStop = True
            Cursor = Cursors.Hand
            Font = Theme.FontButton
            Size = New Size(120, Theme.ButtonHeight)
        End Sub

        <Category("PayAlert"), DefaultValue(GetType(ButtonKind), "Primary")>
        Public Property Kind As ButtonKind
            Get
                Return _kind
            End Get
            Set(value As ButtonKind)
                _kind = value
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

        <Category("Behavior"), DefaultValue(GetType(DialogResult), "None")>
        Public Property DialogResult As DialogResult Implements IButtonControl.DialogResult
            Get
                Return _dialogResult
            End Get
            Set(value As DialogResult)
                _dialogResult = value
            End Set
        End Property

        Public Sub NotifyDefault(value As Boolean) Implements IButtonControl.NotifyDefault
        End Sub

        Public Sub PerformClick() Implements IButtonControl.PerformClick
            If Enabled AndAlso Visible Then OnClick(EventArgs.Empty)
        End Sub

        ''' <summary>Width that fits the text and icon with comfortable side padding.</summary>
        Public Function PreferredWidth() As Integer
            Dim textWidth = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width
            Dim iconWidth = If(_icon = IconKind.None, 0, PxI(16 + 8))
            Return Math.Max(PxI(88), textWidth + iconWidth + PxI(40))
        End Function

        Protected Overrides Sub OnClick(e As EventArgs)
            If _dialogResult <> DialogResult.None Then
                Dim form = FindForm()
                If form IsNot Nothing Then form.DialogResult = _dialogResult
            End If
            MyBase.OnClick(e)
        End Sub

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If keyData = Keys.Enter AndAlso Focused Then
                PerformClick()
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Space Then
                _pressed = True
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
            MyBase.OnKeyUp(e)
            If e.KeyCode = Keys.Space AndAlso _pressed Then
                _pressed = False
                Invalidate()
                PerformClick()
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
            _pressed = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then
                _pressed = True
                Focus()
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressed = False
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

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Cursor = If(Enabled, Cursors.Hand, Cursors.Default)
            Invalidate()
        End Sub

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            Invalidate()
        End Sub

        Private Sub GetColors(ByRef fill As Color, ByRef border As Color, ByRef textColor As Color, ByRef ring As Color)
            Dim active = _hover OrElse _pressed
            border = Color.Empty
            Select Case _kind
                Case ButtonKind.Primary
                    fill = If(Not Enabled, Theme.Blend(Theme.Brand, Theme.Surface, 0.45), If(active, Theme.BrandDark, Theme.Brand))
                    textColor = Theme.TextOnBrand
                    ring = Theme.Brand
                Case ButtonKind.Secondary
                    fill = If(active AndAlso Enabled, Theme.AppBackground, Theme.Surface)
                    border = If(active AndAlso Enabled, Theme.BorderStrong, Theme.Border)
                    textColor = If(Enabled, Theme.TextPrimary, Theme.TextMuted)
                    ring = Theme.Brand
                Case ButtonKind.Danger
                    fill = If(Not Enabled, Theme.Blend(Theme.OverdueText, Theme.Surface, 0.45), If(active, Theme.DangerDark, Theme.OverdueText))
                    textColor = Theme.TextOnBrand
                    ring = Theme.OverdueText
                Case ButtonKind.DangerText
                    fill = If(active AndAlso Enabled, Theme.OverdueSoft, Color.Transparent)
                    textColor = If(Enabled, Theme.OverdueText, Theme.Blend(Theme.OverdueText, Theme.Surface, 0.45))
                    ring = Theme.OverdueText
                Case Else ' Ghost
                    fill = If(active AndAlso Enabled, Theme.NeutralSoft, Color.Transparent)
                    textColor = If(Enabled, Theme.TextPrimary, Theme.TextMuted)
                    ring = Theme.Brand
            End Select
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)

            Dim fill, border, textColor, ring As Color
            GetColors(fill, border, textColor, ring)

            Dim inset = Px(2)
            Dim r As New RectangleF(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1)
            If _pressed Then r.Offset(0, Px(1))
            Dim radius = Px(Theme.ButtonRadius)

            ' Focus ring sits in the 2px margin around the button (keyboard focus only).
            If Focused AndAlso ShowFocusCues Then
                Dim ringRect = RectangleF.Inflate(r, Px(1.5F), Px(1.5F))
                Gfx.DrawRounded(g, ringRect, radius + Px(1.5F), Color.FromArgb(120, ring), Px(2))
            End If

            If fill.A > 0 Then Gfx.FillRounded(g, r, radius, fill)
            If Not border.IsEmpty Then Gfx.DrawRounded(g, r, radius, border)

            ' Content: optional icon + text, centered as one group.
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.VerticalCenter
            Dim textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags)
            Dim iconSize = Px(16)
            Dim gap = If(_icon = IconKind.None OrElse Text.Length = 0, 0.0F, Px(8))
            Dim groupWidth = textSize.Width + If(_icon = IconKind.None, 0.0F, iconSize + gap)
            Dim x = r.X + (r.Width - groupWidth) / 2.0F
            If _icon <> IconKind.None Then
                Icons.Draw(g, _icon, New RectangleF(x, r.Y + (r.Height - iconSize) / 2.0F, iconSize, iconSize), textColor, Px(1.9F))
                x += iconSize + gap
            End If
            Dim textRect As New Rectangle(CInt(Math.Round(x)), CInt(r.Y), textSize.Width + 2, CInt(r.Height))
            TextRenderer.DrawText(g, Text, Font, textRect, textColor, flags)
        End Sub

    End Class

    ''' <summary>Brand fill, white text.</summary>
    Public Class PrimaryButton
        Inherits PAButton
        Public Sub New()
            MyBase.New()
            Kind = ButtonKind.Primary
        End Sub
    End Class

    ''' <summary>White with a border.</summary>
    Public Class SecondaryButton
        Inherits PAButton
        Public Sub New()
            MyBase.New()
            Kind = ButtonKind.Secondary
        End Sub
    End Class

    ''' <summary>Danger action: red fill by default, or red text when Filled = False.</summary>
    Public Class DangerButton
        Inherits PAButton

        Public Sub New()
            MyBase.New()
            Kind = ButtonKind.Danger
        End Sub

        <Category("PayAlert"), DefaultValue(True)>
        Public Property Filled As Boolean
            Get
                Return Kind = ButtonKind.Danger
            End Get
            Set(value As Boolean)
                Kind = If(value, ButtonKind.Danger, ButtonKind.DangerText)
            End Set
        End Property
    End Class

End Namespace
