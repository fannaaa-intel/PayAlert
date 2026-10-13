Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' PayAlert signature "Due Tag" (§6A): a small rounded ticket with a notched left edge.
    ''' The stub left of the notch is tinted with the status color and doubles as the row's status edge in lists.
    ''' The day is large and the month small and neutral, so the status color shows only once.
    ''' </summary>
    Public Class DueTag
        Inherits PAControl

        Private _dueDate As Date = Date.Today
        Private _status As String = Theme.StatusUpcoming

        Private Shared ReadOnly DayFont As New Font(Theme.FontFamilyName, 15.0F, FontStyle.Bold)

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            Size = New Size(54, 58)
        End Sub

        <Category("PayAlert")>
        Public Property DueDate As Date
            Get
                Return _dueDate
            End Get
            Set(value As Date)
                _dueDate = value.Date
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue("Upcoming")>
        Public Property Status As String
            Get
                Return _status
            End Get
            Set(value As String)
                _status = If(value, Theme.StatusUpcoming)
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)

            Dim shadow = Px(2)
            Dim r As New RectangleF(Px(1), Px(0.5F), Width - Px(2), Height - shadow - Px(1.5F))
            Dim radius = Px(9)
            Dim notchR = Px(5)
            Dim notchY = r.Y + r.Height / 2.0F
            Dim stubWidth = Px(9)

            Using shape = Gfx.NotchedRect(r, radius, notchR, notchY, Single.NaN)
                ' Soft lift, then the white ticket body.
                Dim lifted = r
                lifted.Offset(0, shadow * 0.6F)
                Using shadowPath = Gfx.NotchedRect(lifted, radius, notchR, notchY + shadow * 0.6F, Single.NaN)
                    Using brush As New SolidBrush(Color.FromArgb(14, Theme.Overlay))
                        g.FillPath(brush, shadowPath)
                    End Using
                End Using
                Using fill As New SolidBrush(Theme.Surface)
                    g.FillPath(fill, shape)
                End Using

                ' Status stub: the same anti-aliased path, clipped by a straight vertical line.
                Dim state = g.Save()
                g.SetClip(New RectangleF(r.X - 1, r.Y - 1, stubWidth + 1, r.Height + 2))
                Using stub As New SolidBrush(Theme.StatusText(_status))
                    g.FillPath(stub, shape)
                End Using
                g.Restore(state)

                Using pen As New Pen(Theme.CardBorder)
                    g.DrawPath(pen, shape)
                End Using
            End Using

            ' Day (large) over month (small caps in the status color).
            Dim textArea As New Rectangle(CInt(r.X + stubWidth), CInt(r.Y), CInt(r.Width - stubWidth), CInt(r.Height))
            Dim day = _dueDate.Day.ToString(CultureInfo.InvariantCulture)
            Dim month = _dueDate.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant()
            Dim flags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim daySize = TextRenderer.MeasureText(g, day, DayFont, Size.Empty, flags)
            Dim monthSize = TextRenderer.MeasureText(g, month, Theme.FontSmallCaps, Size.Empty, flags)
            Dim blockHeight = daySize.Height + monthSize.Height - PxI(3)
            Dim top = textArea.Y + (textArea.Height - blockHeight) \ 2
            TextRenderer.DrawText(g, day, DayFont, New Rectangle(textArea.X, top, textArea.Width, daySize.Height), Theme.TextPrimary, flags)
            TextRenderer.DrawText(g, month, Theme.FontSmallCaps,
                                  New Rectangle(textArea.X, top + daySize.Height - PxI(3), textArea.Width, monthSize.Height),
                                  Theme.TextMuted, flags)
        End Sub

    End Class

End Namespace
