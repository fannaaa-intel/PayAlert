Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' Dashboard summary card: a soft card with a short accent bar, a caption, a big number and a
    ''' tinted icon tile. Clickable (mouse, Enter or Space); lifts on hover and shows its hint.
    ''' </summary>
    <DefaultEvent("Click")>
    Public Class SummaryCard
        Inherits PAControl

        Private _caption As String = "Caption"
        Private _value As String = "0"
        Private _hint As String = "View payments"
        Private _accent As Color = Theme.Brand
        Private _valueColor As Color = Theme.TextPrimary
        Private _icon As IconKind = IconKind.Calendar
        Private _hover As Boolean

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(240, 116)
        End Sub

        <Category("PayAlert")>
        Public Property Caption As String
            Get
                Return _caption
            End Get
            Set(value As String)
                _caption = If(value, "")
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Value As String
            Get
                Return _value
            End Get
            Set(value As String)
                _value = If(value, "")
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Hint As String
            Get
                Return _hint
            End Get
            Set(value As String)
                _hint = If(value, "")
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property AccentColor As Color
            Get
                Return _accent
            End Get
            Set(value As Color)
                _accent = value
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property ValueColor As Color
            Get
                Return _valueColor
            End Get
            Set(value As Color)
                _valueColor = value
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(GetType(IconKind), "Calendar")>
        Public Property Icon As IconKind
            Get
                Return _icon
            End Get
            Set(value As IconKind)
                _icon = value
                Invalidate()
            End Set
        End Property

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If (keyData = Keys.Enter OrElse keyData = Keys.Space) AndAlso Focused Then
                OnClick(EventArgs.Empty)
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Space OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Space Then OnClick(EventArgs.Empty)
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

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
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

            Dim shadow = Px(3)
            Dim lift = If(_hover, Px(1), 0.0F)
            Dim face As New RectangleF(Px(1), Px(1) - lift, Width - Px(2) - 1, Height - Px(2) - shadow)
            Dim radius = Px(Theme.CardRadius)
            Dim active = _hover OrElse (Focused AndAlso ShowFocusCues)

            Gfx.SoftShadow(g, face, radius, shadow, If(_hover, 2, 1))
            Gfx.FillRounded(g, face, radius, Theme.Surface)
            Gfx.DrawRounded(g, face, radius, If(active, Theme.Blend(Theme.CardBorder, _accent, 0.45), Theme.CardBorder),
                            If(Focused AndAlso ShowFocusCues, Px(1.5F), 1.0F))

            Dim pad = Px(Theme.CardPadding)
            ' Short accent bar, echoing the page-title underline.
            Gfx.FillRounded(g, New RectangleF(face.X + pad, face.Y + pad, Px(24), Px(4)), Px(2), _accent)

            ' Icon tile at the top right.
            Dim tile = Px(36)
            Dim tileRect As New RectangleF(face.Right - pad - tile, face.Y + pad - Px(2), tile, tile)
            Gfx.FillRounded(g, tileRect, Px(10), Theme.Blend(Theme.Surface, _accent, 0.13))
            Icons.Draw(g, _icon, RectangleF.Inflate(tileRect, -Px(8), -Px(8)), _accent, Px(1.8F))

            Dim textLeft = CInt(face.X + pad)
            Dim textWidth = CInt(tileRect.X - textLeft - Px(8))
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            Dim captionTop = CInt(face.Y + pad + Px(14))
            TextRenderer.DrawText(g, _caption, Theme.FontCaptionBold,
                                  New Rectangle(textLeft, captionTop, textWidth, PxI(18)), Theme.TextMuted, flags)

            Dim valueTop = captionTop + PxI(20)
            Dim valueHeight = TextRenderer.MeasureText(g, "0", Theme.FontBigNumber, Size.Empty, flags).Height
            TextRenderer.DrawText(g, _value, Theme.FontBigNumber,
                                  New Rectangle(textLeft - PxI(1), valueTop, CInt(face.Right - pad) - textLeft, valueHeight), _valueColor, flags)

            ' Hint appears on hover / focus, bottom right, with a small arrow.
            If active AndAlso _hint.Length > 0 Then
                Dim hintSize = TextRenderer.MeasureText(g, _hint, Theme.FontCaptionBold, Size.Empty, flags)
                Dim arrow = Px(14)
                Dim hintRight = face.Right - pad
                Dim hintTop = CInt(face.Bottom - pad - hintSize.Height + Px(2))
                Icons.Draw(g, IconKind.ArrowOut, New RectangleF(hintRight - arrow, hintTop + (hintSize.Height - arrow) / 2.0F, arrow, arrow), _accent, Px(1.6F))
                TextRenderer.DrawText(g, _hint, Theme.FontCaptionBold,
                                      New Rectangle(CInt(hintRight - arrow - Px(4) - hintSize.Width), hintTop, hintSize.Width + 2, hintSize.Height), _accent, flags)
            End If
        End Sub

    End Class

End Namespace
