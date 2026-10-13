Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D

Public Enum IconKind
    None
    Dashboard
    Payments
    History
    Bell
    Settings
    Admin
    Logout
    Plus
    Check
    Alert
    Info
    Close
    Trash
    Back
    Search
    Calendar
    ArrowIn
    ArrowOut
    Clock
    Inbox
    User
    Eye
    EyeOff
    Edit
    Mail
    Lock
    ChevronDown
    ChevronRight
End Enum

''' <summary>
''' PayAlert's own line icons, drawn with GDI+ on a 24-unit grid (PLAN.md §6A: no copied icon packs).
''' Round caps and joins; stroke scales with the icon size unless a width is given.
''' </summary>
Public NotInheritable Class Icons

    Private Sub New()
    End Sub

    Public Shared Sub Draw(g As Graphics, kind As IconKind, bounds As RectangleF, color As Color, Optional strokeWidth As Single = 0)
        If kind = IconKind.None OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        Dim k = Math.Min(bounds.Width, bounds.Height) / 24.0F
        Dim ox = bounds.X + (bounds.Width - 24 * k) / 2.0F
        Dim oy = bounds.Y + (bounds.Height - 24 * k) / 2.0F
        Dim P As Func(Of Single, Single, PointF) = Function(x, y) New PointF(ox + x * k, oy + y * k)
        Dim R As Func(Of Single, Single, Single, Single, RectangleF) = Function(x, y, w, h) New RectangleF(ox + x * k, oy + y * k, w * k, h * k)

        Dim state = g.Save()
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        Using pen As New Pen(color, If(strokeWidth > 0, strokeWidth, Math.Max(1.3F, 1.75F * k)))
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            pen.LineJoin = LineJoin.Round
            Using brush As New SolidBrush(color)
                Dim dot = Math.Max(pen.Width * 0.72F, 1.25F * k)

                Select Case kind
                    Case IconKind.Dashboard
                        DrawRound(g, pen, R(3, 3, 8, 10), 2 * k)
                        DrawRound(g, pen, R(13, 3, 8, 6), 2 * k)
                        DrawRound(g, pen, R(13, 11, 8, 10), 2 * k)
                        DrawRound(g, pen, R(3, 15, 8, 6), 2 * k)

                    Case IconKind.Payments
                        ' A payment ticket: notched on both sides with a perforation.
                        Using ticket = Gfx.NotchedRect(R(3, 6, 18, 12), 2.2F * k, 2.2F * k, oy + 12 * k, oy + 12 * k)
                            g.DrawPath(pen, ticket)
                        End Using
                        Using dash As New Pen(color, pen.Width * 0.8F)
                            dash.DashStyle = DashStyle.Custom
                            dash.DashPattern = {1.4F, 1.8F}
                            g.DrawLine(dash, P(9, 8.5F), P(9, 15.5F))
                        End Using
                        g.DrawLine(pen, P(12.5F, 10.5F), P(17.5F, 10.5F))
                        g.DrawLine(pen, P(12.5F, 13.5F), P(15.5F, 13.5F))

                    Case IconKind.History
                        g.DrawArc(pen, R(4, 4, 16, 16), 160, 290)
                        g.DrawLines(pen, {P(2.6F, 11.6F), P(4.5F, 14.8F), P(7.6F, 13.2F)})
                        g.DrawLines(pen, {P(12, 8), P(12, 12), P(15, 14)})

                    Case IconKind.Bell
                        Using bell As New GraphicsPath()
                            bell.AddArc(R(6, 4, 12, 12), 180, 180)
                            bell.AddLine(P(18, 10), P(18, 15))
                            bell.AddLine(P(18, 15), P(19.5F, 17))
                            bell.AddLine(P(19.5F, 17), P(4.5F, 17))
                            bell.AddLine(P(4.5F, 17), P(6, 15))
                            bell.CloseFigure()
                            g.DrawPath(pen, bell)
                        End Using
                        g.DrawArc(pen, R(9.5F, 17.5F, 5, 4), 0, 180)
                        g.DrawLine(pen, P(12, 2.6F), P(12, 4))

                    Case IconKind.Settings
                        ' Three sliders: PayAlert's settings mark.
                        For Each slider In {New PointF(7, 15), New PointF(12, 8), New PointF(17, 13)}
                            Dim y = slider.X, knob = slider.Y
                            g.DrawLine(pen, P(4, y), P(knob - 2.4F, y))
                            g.DrawLine(pen, P(knob + 2.4F, y), P(20, y))
                            g.DrawEllipse(pen, R(knob - 2.4F, y - 2.4F, 4.8F, 4.8F))
                        Next

                    Case IconKind.Admin
                        Using shield As New GraphicsPath()
                            shield.AddLine(P(12, 3), P(19, 6))
                            shield.AddLine(P(19, 6), P(19, 11.5F))
                            shield.AddBezier(P(19, 11.5F), P(19, 16), P(16, 19.5F), P(12, 21))
                            shield.AddBezier(P(12, 21), P(8, 19.5F), P(5, 16), P(5, 11.5F))
                            shield.AddLine(P(5, 11.5F), P(5, 6))
                            shield.CloseFigure()
                            g.DrawPath(pen, shield)
                        End Using
                        g.DrawLines(pen, {P(9, 12), P(11.2F, 14.2F), P(15.2F, 10)})

                    Case IconKind.Logout
                        g.DrawLines(pen, {P(11, 4), P(6, 4), P(5, 5), P(5, 19), P(6, 20), P(11, 20)})
                        g.DrawLine(pen, P(10, 12), P(20, 12))
                        g.DrawLines(pen, {P(16, 8), P(20, 12), P(16, 16)})

                    Case IconKind.Plus
                        g.DrawLine(pen, P(12, 5), P(12, 19))
                        g.DrawLine(pen, P(5, 12), P(19, 12))

                    Case IconKind.Check
                        g.DrawLines(pen, {P(5, 12.5F), P(10, 17), P(19, 7.5F)})

                    Case IconKind.Alert
                        g.DrawLine(pen, P(12, 5.5F), P(12, 13.5F))
                        Dim c = P(12, 18)
                        g.FillEllipse(brush, c.X - dot, c.Y - dot, dot * 2, dot * 2)

                    Case IconKind.Info
                        Dim c = P(12, 6.5F)
                        g.FillEllipse(brush, c.X - dot, c.Y - dot, dot * 2, dot * 2)
                        g.DrawLine(pen, P(12, 10.5F), P(12, 18))

                    Case IconKind.Close
                        g.DrawLine(pen, P(6.5F, 6.5F), P(17.5F, 17.5F))
                        g.DrawLine(pen, P(17.5F, 6.5F), P(6.5F, 17.5F))

                    Case IconKind.Trash
                        g.DrawLine(pen, P(4, 7), P(20, 7))
                        g.DrawLines(pen, {P(9, 7), P(9, 4.5F), P(15, 4.5F), P(15, 7)})
                        g.DrawLines(pen, {P(6, 7), P(7, 20), P(17, 20), P(18, 7)})
                        g.DrawLine(pen, P(10, 11), P(10, 16))
                        g.DrawLine(pen, P(14, 11), P(14, 16))

                    Case IconKind.Back
                        g.DrawLines(pen, {P(14.5F, 6), P(8.5F, 12), P(14.5F, 18)})

                    Case IconKind.Search
                        g.DrawEllipse(pen, R(4.5F, 4.5F, 11, 11))
                        g.DrawLine(pen, P(14.2F, 14.2F), P(19.5F, 19.5F))

                    Case IconKind.Calendar
                        DrawRound(g, pen, R(4, 6, 16, 14), 2.5F * k)
                        g.DrawLine(pen, P(4, 10.5F), P(20, 10.5F))
                        g.DrawLine(pen, P(8.5F, 3.5F), P(8.5F, 7.5F))
                        g.DrawLine(pen, P(15.5F, 3.5F), P(15.5F, 7.5F))

                    Case IconKind.ArrowIn
                        g.DrawLine(pen, P(17, 7), P(7, 17))
                        g.DrawLines(pen, {P(7, 10), P(7, 17), P(14, 17)})

                    Case IconKind.ArrowOut
                        g.DrawLine(pen, P(7, 17), P(17, 7))
                        g.DrawLines(pen, {P(10, 7), P(17, 7), P(17, 14)})

                    Case IconKind.Clock
                        g.DrawEllipse(pen, R(4, 4, 16, 16))
                        g.DrawLines(pen, {P(12, 8), P(12, 12), P(15, 13.6F)})

                    Case IconKind.Inbox
                        Using tray As New GraphicsPath()
                            tray.AddLines({P(3, 13), P(6.5F, 5.5F), P(17.5F, 5.5F), P(21, 13), P(21, 18), P(20, 19), P(4, 19), P(3, 18)})
                            tray.CloseFigure()
                            g.DrawPath(pen, tray)
                        End Using
                        g.DrawLines(pen, {P(3, 13), P(8, 13), P(9.5F, 15.5F), P(14.5F, 15.5F), P(16, 13), P(21, 13)})

                    Case IconKind.User
                        g.DrawEllipse(pen, R(8, 3.5F, 8, 8))
                        g.DrawArc(pen, R(4.5F, 14, 15, 13), 200, 140)

                    Case IconKind.Eye, IconKind.EyeOff
                        Using eye As New GraphicsPath()
                            eye.AddBezier(P(2.5F, 12), P(6.5F, 5.5F), P(17.5F, 5.5F), P(21.5F, 12))
                            eye.AddBezier(P(21.5F, 12), P(17.5F, 18.5F), P(6.5F, 18.5F), P(2.5F, 12))
                            eye.CloseFigure()
                            g.DrawPath(pen, eye)
                        End Using
                        g.DrawEllipse(pen, R(9.2F, 9.2F, 5.6F, 5.6F))
                        If kind = IconKind.EyeOff Then g.DrawLine(pen, P(4.5F, 4.5F), P(19.5F, 19.5F))

                    Case IconKind.ChevronDown
                        g.DrawLines(pen, {P(6.5F, 9.5F), P(12, 15), P(17.5F, 9.5F)})

                    Case IconKind.ChevronRight
                        g.DrawLines(pen, {P(9.5F, 6), P(15.5F, 12), P(9.5F, 18)})

                    Case IconKind.Mail
                        DrawRound(g, pen, R(3, 5.5F, 18, 13), 2.5F * k)
                        g.DrawLines(pen, {P(4, 7), P(12, 13), P(20, 7)})

                    Case IconKind.Lock
                        DrawRound(g, pen, R(5, 10.5F, 14, 10), 2.5F * k)
                        g.DrawArc(pen, R(8, 4, 8, 8), 180, 180)
                        g.DrawLine(pen, P(8, 8), P(8, 10.5F))
                        g.DrawLine(pen, P(16, 8), P(16, 10.5F))
                        g.DrawLine(pen, P(12, 14.5F), P(12, 16.5F))

                    Case IconKind.Edit
                        g.DrawLines(pen, {P(14.5F, 5.5F), P(18.5F, 9.5F), P(9, 19), P(5, 19), P(5, 15), P(14.5F, 5.5F)})
                        g.DrawLine(pen, P(12.5F, 7.5F), P(16.5F, 11.5F))
                End Select
            End Using
        End Using
        g.Restore(state)
    End Sub

    Private Shared Sub DrawRound(g As Graphics, pen As Pen, r As RectangleF, radius As Single)
        Using path = Gfx.RoundedRect(r, radius)
            g.DrawPath(pen, path)
        End Using
    End Sub

End Class
