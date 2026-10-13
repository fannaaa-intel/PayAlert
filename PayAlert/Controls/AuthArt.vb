Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' The brand panel on the login and register windows: the PayAlert mark, a small stack of tilted
    ''' payment tickets with a reminder bell (drawn in code), a headline and a short line of copy.
    ''' </summary>
    Public Class AuthArt
        Inherits PAControl

        Private _headline As String = "Never Miss a Payment"
        Private _subline As String = ""

        Private Shared ReadOnly HeadlineFont As New Font(Theme.FontFamilyName, 22.0F, FontStyle.Bold)
        Private Shared ReadOnly TicketTitleFont As New Font(Theme.FontFamilyName, 9.5F, FontStyle.Bold)
        Private Shared ReadOnly TicketAmountFont As New Font(Theme.FontFamilyName, 12.0F, FontStyle.Bold)
        Private Shared ReadOnly TicketSmallFont As New Font(Theme.FontFamilyName, 8.0F, FontStyle.Bold)

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            BackColor = Theme.BrandSoft
            Size = New Size(440, 600)
        End Sub

        <Category("PayAlert")>
        Public Property Headline As String
            Get
                Return _headline
            End Get
            Set(value As String)
                _headline = If(value, "")
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Subline As String
            Get
                Return _subline
            End Get
            Set(value As String)
                _subline = If(value, "")
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim pad = Px(40)

            ' Reminder "ripples" spreading from the top-right corner.
            For i = 1 To 4
                Dim d = Px(130 * i)
                Using pen As New Pen(Color.FromArgb(Math.Max(10, 34 - i * 6), Theme.Brand), Px(1.5F))
                    g.DrawEllipse(pen, Width - d / 2.0F - Px(10), -d / 2.0F + Px(30), d, d)
                End Using
            Next

            ' Mark and wordmark.
            Dim mark As New RectangleF(pad, pad, Px(34), Px(34))
            Gfx.DrawLogoMark(g, mark)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim payWidth = TextRenderer.MeasureText(g, "Pay", Theme.FontWordmark, Size.Empty, flags).Width
            Dim wordTop = CInt(mark.Y + (mark.Height - TextRenderer.MeasureText(g, "Pay", Theme.FontWordmark, Size.Empty, flags).Height) / 2.0F)
            TextRenderer.DrawText(g, "Pay", Theme.FontWordmark, New Point(CInt(mark.Right + Px(10)), wordTop), Theme.TextPrimary, flags)
            TextRenderer.DrawText(g, "Alert", Theme.FontWordmark, New Point(CInt(mark.Right + Px(10)) + payWidth, wordTop), Theme.Brand, flags)

            ' The ticket stack, centered in the middle band of the panel.
            Dim cx = Width / 2.0F
            Dim cy = Height * 0.42F
            DrawTicket(g, New PointF(cx - Px(18), cy - Px(70)), -7.0F, "Water Bill", Format.Money(500D), "PAID", Theme.StatusPaid)
            DrawTicket(g, New PointF(cx + Px(20), cy + Px(4)), 5.0F, "Apartment Rent", Format.Money(3000D), "PARTIALLY PAID", Theme.StatusPartial)
            Dim front = New PointF(cx - Px(6), cy + Px(80))
            DrawTicket(g, front, -2.5F, "Electricity Bill", Format.Money(2300D), "DUE TOMORROW", Theme.StatusUpcoming)
            DrawBell(g, New PointF(front.X + Px(118), front.Y - Px(44)))

            ' Headline and copy at the bottom.
            Dim textWidth = Width - pad * 2
            Dim sublineHeight = If(_subline.Length = 0, 0, TextRenderer.MeasureText(g, _subline, Theme.FontBody, New Size(CInt(textWidth), Integer.MaxValue), TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding).Height)
            Dim headlineHeight = TextRenderer.MeasureText(g, _headline, HeadlineFont, New Size(CInt(textWidth), Integer.MaxValue), TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding).Height
            Dim footerTop = Height - pad - Px(16)
            Dim sublineTop = footerTop - Px(26) - sublineHeight
            Dim headlineTop = sublineTop - Px(8) - headlineHeight
            TextRenderer.DrawText(g, _headline, HeadlineFont, New Rectangle(CInt(pad) - PxI(1), CInt(headlineTop), CInt(textWidth), headlineHeight),
                                  Theme.TextPrimary, TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)
            Gfx.FillRounded(g, New RectangleF(pad, headlineTop - Px(14), Px(Theme.TitleAccentWidth), Px(Theme.TitleAccentHeight)), Px(1.5F), Theme.Brand)
            If _subline.Length > 0 Then
                TextRenderer.DrawText(g, _subline, Theme.FontBody, New Rectangle(CInt(pad), CInt(sublineTop), CInt(textWidth), sublineHeight),
                                      Theme.TextMuted, TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)
            End If
            TextRenderer.DrawText(g, "Academic prototype  ·  demo data only", Theme.FontCaption, New Point(CInt(pad), CInt(footerTop)),
                                  Theme.Blend(Theme.TextMuted, Theme.BrandSoft, 0.25), TextFormatFlags.NoPadding)
        End Sub

        ''' <summary>One white payment ticket with a status stub and notch, rotated around its center.</summary>
        Private Sub DrawTicket(g As Graphics, center As PointF, angle As Single, title As String, amount As String, label As String, status As String)
            Dim w = Px(250)
            Dim h = Px(76)
            Dim state = g.Save()
            g.TranslateTransform(center.X, center.Y)
            g.RotateTransform(angle)
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
            Dim body As New RectangleF(-w / 2.0F, -h / 2.0F, w, h)
            Dim radius = Px(12)
            Dim notchR = Px(7)

            For i = 1 To 6
                Dim layer = body
                layer.Offset(0, i * 1.2F * S)
                Using path = Gfx.NotchedRect(RectangleF.Inflate(layer, i * 0.6F, i * 0.6F), radius + i * 0.6F, notchR - i * 0.3F, layer.Y + h / 2.0F, Single.NaN)
                    Using shadow As New SolidBrush(Color.FromArgb(6, Theme.Overlay))
                        g.FillPath(shadow, path)
                    End Using
                End Using
            Next
            Using shape = Gfx.NotchedRect(body, radius, notchR, 0, Single.NaN)
                Using fill As New SolidBrush(Theme.Surface)
                    g.FillPath(fill, shape)
                End Using
                Dim clip = g.Save()
                g.SetClip(New RectangleF(body.X - 1, body.Y - 1, Px(12), h + 2), CombineMode.Intersect)
                Using stub As New SolidBrush(Theme.StatusText(status))
                    g.FillPath(stub, shape)
                End Using
                g.Restore(clip)
                Using pen As New Pen(Theme.CardBorder)
                    g.DrawPath(pen, shape)
                End Using
            End Using

            Dim textX = body.X + Px(28)
            Using primary As New SolidBrush(Theme.TextPrimary)
                g.DrawString(title, TicketTitleFont, primary, textX, body.Y + Px(13))
                g.DrawString(amount, TicketAmountFont, primary, textX - Px(1), body.Y + Px(33))
            End Using
            ' Status chip on the right.
            Dim chipSize = g.MeasureString(label, TicketSmallFont)
            Dim chip As New RectangleF(body.Right - Px(16) - chipSize.Width - Px(14), body.Y + Px(14), chipSize.Width + Px(14), Px(20))
            Gfx.FillRounded(g, chip, chip.Height / 2.0F, Theme.StatusSoft(status))
            Using text As New SolidBrush(Theme.StatusText(status))
                g.DrawString(label, TicketSmallFont, text, chip.X + Px(7), chip.Y + (chip.Height - chipSize.Height) / 2.0F + Px(0.5F))
            End Using
            g.Restore(state)
        End Sub

        ''' <summary>A round Brand badge with a bell, ringing over the front ticket.</summary>
        Private Sub DrawBell(g As Graphics, center As PointF)
            Dim d = Px(46)
            Dim r As New RectangleF(center.X - d / 2.0F, center.Y - d / 2.0F, d, d)
            For i = 1 To 4
                Using shadow As New SolidBrush(Color.FromArgb(10, Theme.BrandDark))
                    g.FillEllipse(shadow, RectangleF.Inflate(New RectangleF(r.X, r.Y + i * S, r.Width, r.Height), i * 0.5F, i * 0.5F))
                End Using
            Next
            Using fill As New SolidBrush(Theme.Brand)
                g.FillEllipse(fill, r)
            End Using
            Using ring As New Pen(Theme.Surface, Px(3))
                g.DrawEllipse(ring, r)
            End Using
            Icons.Draw(g, IconKind.Bell, RectangleF.Inflate(r, -Px(12), -Px(12)), Theme.TextOnBrand, Px(2))
            ' Two short "ringing" strokes beside the badge.
            Using pen As New Pen(Theme.Brand, Px(2))
                pen.StartCap = LineCap.Round
                pen.EndCap = LineCap.Round
                g.DrawArc(pen, RectangleF.Inflate(r, Px(8), Px(8)), -60, 32)
                g.DrawArc(pen, RectangleF.Inflate(r, Px(8), Px(8)), 208, 32)
            End Using
        End Sub

    End Class

End Namespace
