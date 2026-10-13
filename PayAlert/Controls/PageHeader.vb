Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' PayAlert "Header band" (§6A): the page title and subtitle on a thin band line, with a short
    ''' Brand underline accent (40 × 3 px) sitting on the band under the title.
    ''' Add action buttons as children anchored to the right.
    ''' </summary>
    Public Class PageHeader
        Inherits Panel

        Private _title As String = "Page title"
        Private _subtitle As String = ""

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or ControlStyles.SupportsTransparentBackColor, True)
            BackColor = Color.Transparent
            Height = 76
        End Sub

        Private ReadOnly Property S As Single
            Get
                Return DeviceDpi / 96.0F
            End Get
        End Property

        <Category("PayAlert")>
        Public Property Title As String
            Get
                Return _title
            End Get
            Set(value As String)
                _title = If(value, "")
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Subtitle As String
            Get
                Return _subtitle
            End Get
            Set(value As String)
                _subtitle = If(value, "")
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Dim g = e.Graphics
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            Dim titleSize = TextRenderer.MeasureText(g, "Ag", Theme.FontPageTitle, Size.Empty, flags)
            Dim textWidth = Width
            For Each child As Control In Controls
                If child.Visible AndAlso (child.Anchor And AnchorStyles.Right) = AnchorStyles.Right Then
                    textWidth = Math.Min(textWidth, child.Left - CInt(16 * S))
                End If
            Next

            TextRenderer.DrawText(g, _title, Theme.FontPageTitle, New Rectangle(-CInt(1 * S), 0, textWidth, titleSize.Height), Theme.TextPrimary, flags)
            If _subtitle.Length > 0 Then
                TextRenderer.DrawText(g, _subtitle, Theme.FontBody, New Rectangle(0, titleSize.Height + CInt(2 * S), textWidth, CInt(22 * S)), Theme.TextMuted, flags)
            End If

            ' The band: a hairline across the page, with the Brand accent resting on it under the title.
            Dim bandY = Height - CInt(Theme.TitleAccentHeight * S)
            Using pen As New Pen(Theme.Border)
                g.DrawLine(pen, 0, Height - CInt(2 * S), Width, Height - CInt(2 * S))
            End Using
            Gfx.HighQuality(g)
            Gfx.FillRounded(g, New RectangleF(0, bandY, Theme.TitleAccentWidth * S, Theme.TitleAccentHeight * S), 1.5F * S, Theme.Brand)
        End Sub

    End Class

End Namespace
