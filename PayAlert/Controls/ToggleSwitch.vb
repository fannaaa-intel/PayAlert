Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' On/off switch: a pill track (Brand when on, neutral when off) with a white knob that slides over.
    ''' Click, Space or Enter toggles it; replaces CheckBox for settings.
    ''' </summary>
    <DefaultEvent("CheckedChanged")>
    Public Class ToggleSwitch
        Inherits PAControl

        Private Const AnimationMs As Double = 140

        Private _checked As Boolean
        Private _hover As Boolean
        Private _shown As Double            ' 0 = off position, 1 = on position
        Private _from As Double
        Private ReadOnly _clock As New Stopwatch()
        Private ReadOnly _timer As New Timer() With {.Interval = 15}

        Public Event CheckedChanged As EventHandler

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(48, 28)
            AddHandler _timer.Tick, AddressOf OnTick
        End Sub

        <Category("PayAlert"), DefaultValue(False)>
        Public Property Checked As Boolean
            Get
                Return _checked
            End Get
            Set(value As Boolean)
                If value = _checked Then Return
                _checked = value
                If IsHandleCreated AndAlso Visible Then
                    _from = _shown
                    _clock.Restart()
                    _timer.Start()
                Else
                    _shown = If(_checked, 1.0, 0.0)
                End If
                Invalidate()
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Sets the state without animating or raising CheckedChanged (for loading saved values).</summary>
        Public Sub SetCheckedSilently(value As Boolean)
            _checked = value
            _shown = If(value, 1.0, 0.0)
            _timer.Stop()
            Invalidate()
        End Sub

        Private Sub OnTick(sender As Object, e As EventArgs)
            Dim t = Math.Min(1.0, _clock.Elapsed.TotalMilliseconds / AnimationMs)
            Dim target = If(_checked, 1.0, 0.0)
            _shown = _from + (target - _from) * Gfx.EaseOut(t)
            If t >= 1 Then
                _shown = target
                _timer.Stop()
            End If
            Invalidate()
        End Sub

        Protected Overrides Sub OnClick(e As EventArgs)
            MyBase.OnClick(e)
            If Enabled Then Checked = Not Checked
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Space OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Space AndAlso Enabled Then
                Checked = Not Checked
                e.Handled = True
            End If
        End Sub

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

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim ring = Px(3)
            Dim track As New RectangleF(ring, ring, Width - ring * 2 - 1, Height - ring * 2 - 1)
            Dim radius = track.Height / 2.0F
            If Focused AndAlso ShowFocusCues Then
                Gfx.FillRounded(g, RectangleF.Inflate(track, ring - Px(0.5F), ring - Px(0.5F)), radius + ring, Theme.BrandSoft)
            End If
            Dim offColor = If(_hover, Theme.BorderStrong, Theme.Blend(Theme.BorderStrong, Theme.Surface, 0.15))
            Dim onColor = If(_hover, Theme.BrandDark, Theme.Brand)
            Dim fill = Theme.Blend(offColor, onColor, _shown)
            If Not Enabled Then fill = Theme.Blend(fill, Theme.Surface, 0.5)
            Gfx.FillRounded(g, track, radius, fill)

            Dim knob = track.Height - Px(6)
            Dim knobX = CSng(track.X + Px(3) + (track.Width - knob - Px(6)) * _shown)
            Dim knobRect As New RectangleF(knobX, track.Y + Px(3), knob, knob)
            Using shadow As New SolidBrush(Color.FromArgb(40, Theme.Overlay))
                g.FillEllipse(shadow, knobRect.X, knobRect.Y + Px(1), knob, knob)
            End Using
            Using brush As New SolidBrush(Theme.Surface)
                g.FillEllipse(brush, knobRect)
            End Using
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then _timer.Dispose()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
