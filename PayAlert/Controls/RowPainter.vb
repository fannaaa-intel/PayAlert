Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Globalization
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' Drawing shared by list rows (PaymentGrid, UpcomingList): the compact due tag, whose colored stub is the
    ''' row's status edge, and the status pill. Sizes are logical pixels times <c>s</c> (device DPI ÷ 96).
    ''' </summary>
    Friend NotInheritable Class RowPainter

        Private Shared ReadOnly DayFont As New Font(Theme.FontFamilyName, 12.0F, FontStyle.Bold)

        Private Sub New()
        End Sub

        ''' <summary>Width of the compact due tag, for laying out the rest of the row.</summary>
        Public Shared Function DueTagWidth(s As Single) As Single
            Return 62 * s
        End Function

        ''' <summary>Compact due tag: notched ticket, status-colored stub, "15 OCT", vertically centered in the row.</summary>
        Public Shared Sub PaintDueTag(g As Graphics, x As Single, row As Rectangle, dueDate As Date, status As String, s As Single)
            Gfx.HighQuality(g)
            Dim w = DueTagWidth(s)
            Dim h = 38 * s
            Dim r As New RectangleF(x, row.Y + (row.Height - h) / 2.0F, w, h)
            Using shape = Gfx.NotchedRect(r, 8 * s, 4.5F * s, r.Y + h / 2.0F, Single.NaN)
                Using fill As New SolidBrush(Theme.Surface)
                    g.FillPath(fill, shape)
                End Using
                Dim state = g.Save()
                g.SetClip(New RectangleF(r.X - 1, r.Y - 1, 8 * s + 1, h + 2))
                Using stub As New SolidBrush(Theme.StatusText(status))
                    g.FillPath(stub, shape)
                End Using
                g.Restore(state)
                Using pen As New Pen(Theme.CardBorder)
                    g.DrawPath(pen, shape)
                End Using
            End Using
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine
            Dim day = dueDate.Day.ToString(CultureInfo.InvariantCulture)
            Dim month = dueDate.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant()
            Dim dayWidth = TextRenderer.MeasureText(g, day, DayFont, Size.Empty, flags).Width
            Dim monthWidth = TextRenderer.MeasureText(g, month, Theme.FontSmallCaps, Size.Empty, flags).Width
            Dim textX = CInt(r.X + 8 * s + (w - 8 * s - dayWidth - 3 * s - monthWidth) / 2.0F)
            TextRenderer.DrawText(g, day, DayFont, New Rectangle(textX, CInt(r.Y), dayWidth + 2, CInt(h)), Theme.TextPrimary, flags)
            TextRenderer.DrawText(g, month, Theme.FontSmallCaps, New Rectangle(textX + dayWidth + CInt(3 * s), CInt(r.Y + 1 * s), monthWidth + 2, CInt(h)), Theme.TextMuted, flags)
        End Sub

        ''' <summary>Width the status pill needs.</summary>
        Public Shared Function StatusPillWidth(g As Graphics, status As String, s As Single) As Single
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Return TextRenderer.MeasureText(g, status, Theme.FontCaptionBold, Size.Empty, flags).Width + 32 * s
        End Function

        ''' <summary>Status pill starting at r.X, vertically centered in r.</summary>
        Public Shared Sub PaintStatusPill(g As Graphics, r As Rectangle, status As String, s As Single)
            Gfx.HighQuality(g)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine
            Dim textWidth = TextRenderer.MeasureText(g, status, Theme.FontCaptionBold, Size.Empty, flags).Width
            Dim h = 24 * s
            Dim pill As New RectangleF(r.X, r.Y + (r.Height - h) / 2.0F, textWidth + 32 * s, h)
            Gfx.FillRounded(g, pill, h / 2.0F, Theme.StatusSoft(status))
            Dim dot = 6 * s
            Using brush As New SolidBrush(Theme.StatusText(status))
                g.FillEllipse(brush, pill.X + 10 * s, pill.Y + (h - dot) / 2.0F, dot, dot)
            End Using
            TextRenderer.DrawText(g, status, Theme.FontCaptionBold, New Rectangle(CInt(pill.X + 22 * s), CInt(pill.Y), textWidth + 4, CInt(h)),
                                  Theme.StatusText(status), flags)
        End Sub

    End Class

End Namespace
