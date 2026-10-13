Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' A small inline banner for form feedback ("Wrong email or password.", "Account created. Please log in.").
    ''' Soft tone background, a tone status edge, an icon and one or two lines of text. Hidden when empty.
    ''' </summary>
    Public Class InlineMessage
        Inherits PAControl

        Private _tone As Tone = Tone.Danger
        Private _message As String = ""

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            SetStyle(ControlStyles.CacheText, True)
            TabStop = False
            Font = Theme.FontBodySemibold
            Size = New Size(320, 42)
            Visible = False
        End Sub

        <Category("PayAlert"), DefaultValue(GetType(Tone), "Danger")>
        Public Property Tone As Tone
            Get
                Return _tone
            End Get
            Set(value As Tone)
                _tone = value
                Invalidate()
            End Set
        End Property

        ''' <summary>The message currently shown (kept in a field so painting never depends on the window text).</summary>
        Public ReadOnly Property Message As String
            Get
                Return _message
            End Get
        End Property

        Public Sub ShowMessage(message As String, tone As Tone)
            _tone = tone
            _message = If(message, "")
            Text = _message
            Visible = _message.Length > 0
            Invalidate()
        End Sub

        Public Sub Clear()
            _message = ""
            Text = ""
            Visible = False
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim r As New RectangleF(0, 0, Width - 1, Height - 1)
            Dim toneColor = Theme.ToneColor(_tone)
            Gfx.FillRounded(g, r, Px(9), Theme.ToneSoft(_tone))
            Gfx.FillRounded(g, New RectangleF(Px(6), Px(9), Px(Theme.StatusEdgeWidth), Height - Px(18)), Px(2), toneColor)

            Dim iconSize = Px(18)
            Dim icon = If(_tone = Tone.Success, IconKind.Check, If(_tone = Tone.Danger, IconKind.Alert, IconKind.Info))
            Icons.Draw(g, icon, New RectangleF(Px(18), (Height - iconSize) / 2.0F, iconSize, iconSize), toneColor, Px(2))

            Dim textLeft = PxI(18 + 18 + 10)
            TextRenderer.DrawText(g, _message, Font, New Rectangle(textLeft, 0, Width - textLeft - PxI(12), Height), toneColor,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.WordBreak Or TextFormatFlags.EndEllipsis)
        End Sub

    End Class

End Namespace
