Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    Public Enum ChromeKind
        Minimize
        Close
    End Enum

    ''' <summary>Minimize / close button for PayAlert's borderless windows.</summary>
    Public Class ChromeButton
        Inherits PAControl

        Private _kind As ChromeKind = ChromeKind.Close
        Private _hover As Boolean

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            Cursor = Cursors.Hand
            Size = New Size(36, 32)
        End Sub

        <Category("PayAlert"), DefaultValue(GetType(ChromeKind), "Close")>
        Public Property Kind As ChromeKind
            Get
                Return _kind
            End Get
            Set(value As ChromeKind)
                _kind = value
                Invalidate()
            End Set
        End Property

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

        Protected Overrides Sub OnClick(e As EventArgs)
            MyBase.OnClick(e)
            Dim form = FindForm()
            If form Is Nothing Then Return
            If _kind = ChromeKind.Close Then form.Close() Else form.WindowState = FormWindowState.Minimized
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim isClose = _kind = ChromeKind.Close
            If _hover Then
                Gfx.FillRounded(g, New RectangleF(0, 0, Width - 1, Height - 1), Px(8), If(isClose, Theme.OverdueSoft, Theme.NeutralSoft))
            End If
            Dim color = If(_hover, If(isClose, Theme.OverdueText, Theme.TextPrimary), Theme.TextMuted)
            Dim size = Px(14)
            Dim box As New RectangleF((Width - size) / 2.0F, (Height - size) / 2.0F, size, size)
            If isClose Then
                Icons.Draw(g, IconKind.Close, box, color, Px(1.6F))
            Else
                Using pen As New Pen(color, Px(1.6F))
                    pen.StartCap = Drawing2D.LineCap.Round
                    pen.EndCap = Drawing2D.LineCap.Round
                    g.DrawLine(pen, box.X + Px(2), box.Y + box.Height / 2.0F, box.Right - Px(2), box.Y + box.Height / 2.0F)
                End Using
            End If
        End Sub

    End Class

End Namespace
