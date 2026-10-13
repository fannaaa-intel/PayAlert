Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' PayAlert signature "Balance Ring" (§6A): a thick anti-aliased ring showing paid ÷ total.
    ''' The center shows the remaining balance with "left of ₱Total" under it. Animates when values change.
    ''' </summary>
    Public Class BalanceRing
        Inherits PAControl

        Private Const AnimationMs As Double = 650

        Private _total As Decimal = 0D
        Private _paid As Decimal = 0D
        Private _shown As Double          ' fraction currently drawn
        Private _from As Double
        Private _target As Double
        Private ReadOnly _clock As New Stopwatch()
        Private ReadOnly _timer As New Timer() With {.Interval = 15}

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            Size = New Size(160, 160)
            AddHandler _timer.Tick, AddressOf OnTick
        End Sub

        <Category("PayAlert")>
        Public ReadOnly Property Total As Decimal
            Get
                Return _total
            End Get
        End Property

        <Category("PayAlert")>
        Public ReadOnly Property Paid As Decimal
            Get
                Return _paid
            End Get
        End Property

        Public ReadOnly Property Balance As Decimal
            Get
                Return Math.Max(0D, _total - _paid)
            End Get
        End Property

        ''' <summary>Sets the amounts; the ring sweeps from its current position to the new one.</summary>
        Public Sub SetValues(total As Decimal, paid As Decimal, Optional animate As Boolean = True)
            _total = Math.Max(0D, total)
            _paid = Math.Max(0D, Math.Min(paid, _total))
            _target = If(_total = 0D, 0.0, CDbl(_paid / _total))
            If animate AndAlso Not DesignMode Then
                _from = _shown
                _clock.Restart()
                _timer.Start()
            Else
                _shown = _target
                _timer.Stop()
            End If
            Invalidate()
        End Sub

        ''' <summary>Replays the sweep from empty (used when a screen first appears).</summary>
        Public Sub Replay()
            _shown = 0
            SetValues(_total, _paid, True)
        End Sub

        Private Sub OnTick(sender As Object, e As EventArgs)
            Dim t = _clock.Elapsed.TotalMilliseconds / AnimationMs
            _shown = _from + (_target - _from) * Gfx.EaseOut(t)
            If t >= 1 Then
                _shown = _target
                _timer.Stop()
            End If
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)

            Dim diameter = Math.Min(Width, Height) - Px(4)
            If diameter <= Px(20) Then Return
            Dim thickness = Math.Max(Px(8), diameter * 0.105F)
            Dim ringRect As New RectangleF((Width - diameter) / 2.0F + thickness / 2.0F,
                                           (Height - diameter) / 2.0F + thickness / 2.0F,
                                           diameter - thickness, diameter - thickness)

            Using track As New Pen(Theme.NeutralSoft, thickness)
                g.DrawEllipse(track, ringRect)
            End Using

            Dim complete = _total > 0D AndAlso _paid >= _total
            Dim ringColor = If(complete, Theme.PaidText, Theme.Brand)
            Dim sweep = CSng(360.0 * Math.Max(0.0, Math.Min(1.0, _shown)))
            If sweep > 0.5F Then
                Using arc As New Pen(ringColor, thickness)
                    arc.StartCap = LineCap.Round
                    arc.EndCap = LineCap.Round
                    If sweep >= 359.5F Then
                        g.DrawEllipse(arc, ringRect)
                    Else
                        g.DrawArc(arc, ringRect, -90, sweep)
                        ' A small white knob rides the end of the arc.
                        Dim angle = (-90 + sweep) * Math.PI / 180.0
                        Dim cx = ringRect.X + ringRect.Width / 2.0F + CSng(Math.Cos(angle)) * ringRect.Width / 2.0F
                        Dim cy = ringRect.Y + ringRect.Height / 2.0F + CSng(Math.Sin(angle)) * ringRect.Height / 2.0F
                        Dim knob = thickness * 0.24F
                        Using brush As New SolidBrush(Theme.Surface)
                            g.FillEllipse(brush, cx - knob, cy - knob, knob * 2, knob * 2)
                        End Using
                    End If
                End Using
            End If

            ' Center text: remaining balance, sized to fit, with the total underneath.
            Dim inner = diameter - thickness * 2
            Dim amount = Format.Money(Balance)
            Dim caption = If(complete, "Paid in full", "left of " & Format.Money(_total))
            Dim flags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim amountFont As Font = Nothing
            For Each points In {18.0F, 16.0F, 14.0F, 12.0F, 11.0F, 10.0F}
                Dim candidate As New Font(Theme.FontFamilyName, points, FontStyle.Bold)
                If TextRenderer.MeasureText(g, amount, candidate, Drawing.Size.Empty, flags).Width <= inner * 0.84F OrElse points = 10.0F Then
                    amountFont = candidate
                    Exit For
                End If
                candidate.Dispose()
            Next
            Using amountFont
                Dim amountSize = TextRenderer.MeasureText(g, amount, amountFont, Drawing.Size.Empty, flags)
                Dim captionSize = TextRenderer.MeasureText(g, caption, Theme.FontCaption, Drawing.Size.Empty, flags)
                Dim blockHeight = amountSize.Height + captionSize.Height
                Dim top = CInt((Height - blockHeight) / 2.0F)
                TextRenderer.DrawText(g, amount, amountFont, New Rectangle(0, top, Width, amountSize.Height),
                                      If(complete, Theme.PaidText, Theme.TextPrimary), flags)
                TextRenderer.DrawText(g, caption, Theme.FontCaption, New Rectangle(0, top + amountSize.Height, Width, captionSize.Height),
                                      Theme.TextMuted, flags)
            End Using
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then _timer.Dispose()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
