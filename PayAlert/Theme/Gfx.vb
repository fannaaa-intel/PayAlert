Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D

''' <summary>Shared anti-aliased GDI+ drawing helpers for PayAlert's custom controls.</summary>
Public NotInheritable Class Gfx

    Private Sub New()
    End Sub

    Public Shared Sub HighQuality(g As Graphics)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.CompositingQuality = CompositingQuality.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
    End Sub

    Public Shared Function RoundedRect(r As RectangleF, radius As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d = Math.Min(radius * 2.0F, Math.Min(r.Width, r.Height))
        If d <= 0.5F Then
            path.AddRectangle(r)
            Return path
        End If
        path.AddArc(r.X, r.Y, d, d, 180, 90)
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Public Shared Function RoundedRect(r As Rectangle, radius As Single) As GraphicsPath
        Return RoundedRect(New RectangleF(r.X, r.Y, r.Width, r.Height), radius)
    End Function

    ''' <summary>
    ''' A rounded rectangle with semicircle "ticket" notches cut into the left and/or right edge at height notchY.
    ''' Pass Single.NaN to skip a side. Used by DueTag, the dialog footer perforation and toasts.
    ''' </summary>
    Public Shared Function NotchedRect(r As RectangleF, radius As Single, notchRadius As Single,
                                       leftNotchY As Single, rightNotchY As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d = Math.Min(radius * 2.0F, Math.Min(r.Width, r.Height))
        Dim nr = Math.Max(0.0F, notchRadius)
        path.AddArc(r.X, r.Y, d, d, 180, 90)
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        If Not Single.IsNaN(rightNotchY) AndAlso nr > 0 Then
            ' From the top of the notch, sweep inward (through the left side of the circle) to its bottom.
            path.AddArc(r.Right - nr, rightNotchY - nr, nr * 2, nr * 2, 270, -180)
        End If
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        If Not Single.IsNaN(leftNotchY) AndAlso nr > 0 Then
            ' Moving up the left edge: from the bottom of the notch, sweep inward to its top.
            path.AddArc(r.X - nr, leftNotchY - nr, nr * 2, nr * 2, 90, -180)
        End If
        path.CloseFigure()
        Return path
    End Function

    Public Shared Sub FillRounded(g As Graphics, r As RectangleF, radius As Single, color As Color)
        Using path = RoundedRect(r, radius)
            Using brush As New SolidBrush(color)
                g.FillPath(brush, path)
            End Using
        End Using
    End Sub

    Public Shared Sub DrawRounded(g As Graphics, r As RectangleF, radius As Single, color As Color, Optional width As Single = 1.0F)
        Using path = RoundedRect(r, radius)
            Using pen As New Pen(color, width)
                g.DrawPath(pen, path)
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Very soft drop shadow under a rounded shape: a few low-alpha layers offset downwards.
    ''' depth is in device pixels (2–3 logical px per §6A); strength 1 = resting, 2 = hover lift.
    ''' </summary>
    Public Shared Sub SoftShadow(g As Graphics, r As RectangleF, radius As Single, depth As Single, Optional strength As Integer = 1)
        ' A tight contact shadow hugging the edge, plus a soft fall-off below it.
        Dim lifted = strength >= 2
        Using contact = RoundedRect(RectangleF.Inflate(r, 0.75F, 0.75F), radius + 0.75F)
            Using brush As New SolidBrush(Color.FromArgb(If(lifted, 16, 12), Theme.Overlay))
                g.FillPath(brush, contact)
            End Using
        End Using
        Dim layers = Math.Max(1, CInt(Math.Round(depth)))
        Dim alphaPerLayer = If(lifted, 16, 11)
        For i = layers To 1 Step -1
            Dim spread = i * 0.5F
            Dim layer = New RectangleF(r.X - spread * 0.5F, r.Y + i, r.Width + spread, r.Height)
            Using path = RoundedRect(layer, radius + spread)
                Using brush As New SolidBrush(Color.FromArgb(alphaPerLayer, Theme.Overlay))
                    g.FillPath(brush, path)
                End Using
            End Using
        Next
    End Sub

    ''' <summary>The PayAlert status edge: a 4px rounded strip in the status color.</summary>
    Public Shared Sub StatusEdge(g As Graphics, r As RectangleF, status As String)
        FillRounded(g, r, r.Width / 2.0F, Theme.StatusText(status))
    End Sub

    ''' <summary>Cubic ease-out, 0..1 to 0..1.</summary>
    Public Shared Function EaseOut(t As Double) As Double
        Dim x = Math.Max(0.0, Math.Min(1.0, t))
        Return 1.0 - Math.Pow(1.0 - x, 3)
    End Function

    ''' <summary>
    ''' The PayAlert mark drawn in code: a rounded Brand tile holding a white bell whose base is
    ''' a ticket notch. Placeholder until the client supplies their final logo artwork.
    ''' </summary>
    Public Shared Sub DrawLogoMark(g As Graphics, r As RectangleF)
        FillRounded(g, r, r.Width * 0.28F, Theme.Brand)
        Dim k = r.Width / 24.0F
        Dim cx = r.X + r.Width / 2.0F
        Using pen As New Pen(Theme.TextOnBrand, Math.Max(1.4F, 1.9F * k))
            pen.LineJoin = LineJoin.Round
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            Using bell As New GraphicsPath()
                bell.AddArc(r.X + 7 * k, r.Y + 5.5F * k, 10 * k, 10 * k, 180, 180)
                bell.AddLine(r.X + 17 * k, r.Y + 10.5F * k, r.X + 17 * k, r.Y + 14.5F * k)
                bell.AddLine(r.X + 17 * k, r.Y + 14.5F * k, r.X + 18.5F * k, r.Y + 16.5F * k)
                bell.AddLine(r.X + 18.5F * k, r.Y + 16.5F * k, r.X + 5.5F * k, r.Y + 16.5F * k)
                bell.AddLine(r.X + 5.5F * k, r.Y + 16.5F * k, r.X + 7 * k, r.Y + 14.5F * k)
                bell.CloseFigure()
                g.DrawPath(pen, bell)
            End Using
            g.DrawArc(pen, cx - 2.2F * k, r.Y + 16.8F * k, 4.4F * k, 3.4F * k, 0, 180)
        End Using
    End Sub

End Class
