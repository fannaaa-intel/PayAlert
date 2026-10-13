Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>Rounded status label in the status colors, with a small leading dot. Sizes itself to its text.</summary>
    Public Class StatusPill
        Inherits PAControl

        Private _status As String = Theme.StatusUpcoming

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            Font = Theme.FontCaptionBold
            AutoSize = True
        End Sub

        ''' <summary>One of Theme.StatusPaid / StatusOverdue / StatusPartial / StatusUpcoming.</summary>
        <Category("PayAlert"), DefaultValue("Upcoming")>
        Public Property Status As String
            Get
                Return _status
            End Get
            Set(value As String)
                _status = If(value, Theme.StatusUpcoming)
                Text = _status
                If AutoSize Then Size = GetPreferredSize(Size.Empty)
                Invalidate()
            End Set
        End Property

        <Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible), DefaultValue(True)>
        Public Overrides Property AutoSize As Boolean
            Get
                Return MyBase.AutoSize
            End Get
            Set(value As Boolean)
                MyBase.AutoSize = value
            End Set
        End Property

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim textSize = TextRenderer.MeasureText(_status, Font, Size.Empty, TextFormatFlags.NoPadding)
            Return New Size(textSize.Width + PxI(10 + 6 + 6 + 10), Math.Max(PxI(22), textSize.Height + PxI(8)))
        End Function

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            If AutoSize Then Size = GetPreferredSize(Size.Empty)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim pillHeight = Math.Min(Height - 1, PxI(22))
            Dim r As New RectangleF(0, (Height - pillHeight) / 2.0F, Width - 1, pillHeight)
            Dim fore = Theme.StatusText(_status)
            Gfx.FillRounded(g, r, r.Height / 2.0F, Theme.StatusSoft(_status))

            Dim dot = Px(6)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using brush As New SolidBrush(fore)
                g.FillEllipse(brush, r.X + Px(10), r.Y + (r.Height - dot) / 2.0F, dot, dot)
            End Using

            Dim textRect As New Rectangle(CInt(r.X + Px(10) + dot + Px(6)), CInt(r.Y), CInt(r.Width), CInt(r.Height))
            TextRenderer.DrawText(g, _status, Font, textRect, fore,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
        End Sub

    End Class

End Namespace
