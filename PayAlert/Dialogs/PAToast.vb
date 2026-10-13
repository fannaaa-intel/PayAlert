Option Strict On
Option Explicit On

Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Namespace Dialogs

    ''' <summary>
    ''' Non-blocking feedback (§6B): a small rounded card that slides in at the bottom-right of the main
    ''' window with a tone status edge, an icon, a one-line message and an optional "View" link.
    ''' Closes after 3 seconds; hovering pauses the countdown. Never takes focus.
    ''' </summary>
    Public NotInheritable Class PAToast
        Inherits Form

        Private Const LifeMs As Double = 3000
        Private Const InMs As Double = 180
        Private Const OutMs As Double = 160
        Private Const ToastHeight As Integer = 56
        Private Const EdgeMargin As Integer = 24
        Private Const SlideDistance As Integer = 16

        Private Shared _current As PAToast

        Private ReadOnly _host As Form
        Private ReadOnly _tone As Tone
        Private ReadOnly _message As String
        Private ReadOnly _actionText As String
        Private ReadOnly _onAction As Action
        Private _shadow As ShadowWindow
        Private _shadowAlpha As Byte
        Private ReadOnly _timer As New Timer() With {.Interval = 15}
        Private ReadOnly _clock As New Stopwatch()
        Private _phase As Integer           ' 1 = in, 2 = holding, 3 = out
        Private _held As Double
        Private _lastTick As Double
        Private _linkRect As Rectangle
        Private _linkHover As Boolean
        Private _messageRect As Rectangle

        ''' <summary>Shows a toast over the owner's window. Replaces any toast already showing.</summary>
        Public Shared Sub Notify(owner As Control, message As String, Optional tone As Tone = Tone.Success,
                                 Optional actionText As String = Nothing, Optional onAction As Action = Nothing)
            Dim host = PADialog.ResolveOwner(owner)
            If host Is Nothing Then
                Debug.WriteLine("[PAToast] No visible window to show the toast on: " & message)
                Return
            End If
            _current?.Dismiss(immediate:=True)
            Dim toast As New PAToast(host, message, tone, actionText, onAction)
            _current = toast
            toast.Present()
        End Sub

        Private Sub New(host As Form, message As String, tone As Tone, actionText As String, onAction As Action)
            _host = host
            _tone = tone
            _message = If(message, "")
            _actionText = If(actionText, "")
            _onAction = onAction

            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
            AutoScaleMode = AutoScaleMode.None
            FormBorderStyle = FormBorderStyle.None
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            BackColor = Theme.Surface
            Font = Theme.FontBody
            Opacity = 0
            Text = _message
            ComputeLayout()
        End Sub

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim cp = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or Native.WS_EX_NOACTIVATE Or Native.WS_EX_TOOLWINDOW
                Return cp
            End Get
        End Property

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Private Shared Function Px(logical As Single) As Integer
            Return Theme.Px(logical)
        End Function

        Private Sub ComputeLayout()
            Dim h = Px(ToastHeight)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim messageWidth = TextRenderer.MeasureText(_message, Theme.FontBodySemibold, Size.Empty, flags).Width
            Dim linkWidth = If(_actionText.Length = 0, 0, TextRenderer.MeasureText(_actionText, Theme.FontBodyBold, Size.Empty, flags).Width)
            Dim textLeft = Px(20 + 30 + 12)
            Dim rightPart = If(linkWidth > 0, Px(18) + linkWidth + Px(20), Px(22))
            Dim w = Math.Max(Px(300), Math.Min(Px(460), textLeft + messageWidth + rightPart))
            ClientSize = New Size(w, h)
            _messageRect = New Rectangle(textLeft, 0, w - textLeft - rightPart, h)
            If linkWidth > 0 Then _linkRect = New Rectangle(w - Px(20) - linkWidth, 0, linkWidth, h)

            Using path = BuildShape(New RectangleF(0, 0, w, h), -1)
                Region = New Region(path)
            End Using
        End Sub

        Private Function BuildShape(body As RectangleF, grow As Single) As GraphicsPath
            Return Gfx.RoundedRect(RectangleF.Inflate(body, grow, grow), Px(Theme.CardRadius) + grow)
        End Function

        Private Sub PaintFace(g As Graphics, body As RectangleF, face As GraphicsPath)
            Using fill As New SolidBrush(Theme.Surface)
                g.FillPath(fill, face)
            End Using
            Using pen As New Pen(Theme.Border)
                g.DrawPath(pen, face)
            End Using
        End Sub

        ' ---------------------------------------------------------------- lifetime

        Private Function RestLocation() As Point
            ' Bottom-right of the window's client area, but never under the taskbar.
            Dim client = Rectangle.Intersect(_host.RectangleToScreen(_host.ClientRectangle), Screen.FromControl(_host).WorkingArea)
            If client.IsEmpty Then client = Screen.FromControl(_host).WorkingArea
            Return New Point(client.Right - Width - Px(EdgeMargin), client.Bottom - Height - Px(EdgeMargin))
        End Function

        Private Sub Present()
            _shadow = New ShadowWindow(AddressOf BuildShape, AddressOf PaintFace, Px(14), Px(4))
            _shadow.Show(_host)
            Dim rest = RestLocation()
            Location = New Point(rest.X, rest.Y + Px(SlideDistance))
            Show(_host)
            AddHandler _host.Move, AddressOf HostMoved
            AddHandler _host.Resize, AddressOf HostMoved
            AddHandler _host.FormClosing, AddressOf HostClosing
            _phase = 1
            _clock.Restart()
            AddHandler _timer.Tick, AddressOf OnTick
            _timer.Start()
        End Sub

        Private Sub OnTick(sender As Object, e As EventArgs)
            Dim elapsed = _clock.Elapsed.TotalMilliseconds
            Dim rest = RestLocation()
            Select Case _phase
                Case 1
                    Dim t = Math.Min(1.0, elapsed / InMs)
                    Dim eased = Gfx.EaseOut(t)
                    SetLevel(eased)
                    Location = New Point(rest.X, rest.Y + CInt(Px(SlideDistance) * (1 - eased)))
                    If t >= 1 Then
                        _phase = 2
                        _held = 0
                        _lastTick = elapsed
                    End If
                Case 2
                    ' Hovering pauses the countdown.
                    If Not Bounds.Contains(Cursor.Position) Then _held += elapsed - _lastTick
                    _lastTick = elapsed
                    If _held >= LifeMs Then
                        _phase = 3
                        _clock.Restart()
                    End If
                Case 3
                    Dim t = Math.Min(1.0, elapsed / OutMs)
                    SetLevel(1 - t)
                    Location = New Point(rest.X, rest.Y + CInt(Px(8) * t))
                    If t >= 1 Then Dismiss(immediate:=True)
            End Select
        End Sub

        Private Sub SetLevel(level As Double)
            Opacity = Math.Max(0.0, Math.Min(1.0, level))
            _shadowAlpha = CByte(Math.Round(255 * Opacity))
            _shadow?.Follow(Bounds, _shadowAlpha)
        End Sub

        Protected Overrides Sub OnLocationChanged(e As EventArgs)
            MyBase.OnLocationChanged(e)
            _shadow?.Follow(Bounds, _shadowAlpha)
        End Sub

        Private Sub HostMoved(sender As Object, e As EventArgs)
            If _host.WindowState = FormWindowState.Minimized Then
                Dismiss(immediate:=True)
            ElseIf _phase = 2 Then
                Location = RestLocation()
            End If
        End Sub

        Private Sub HostClosing(sender As Object, e As FormClosingEventArgs)
            Dismiss(immediate:=True)
        End Sub

        ''' <summary>Starts the fade-out, or removes the toast at once.</summary>
        Private Sub Dismiss(immediate As Boolean)
            If IsDisposed Then Return
            If Not immediate Then
                If _phase <> 3 Then
                    _phase = 3
                    _clock.Restart()
                End If
                Return
            End If
            _timer.Stop()
            RemoveHandler _host.Move, AddressOf HostMoved
            RemoveHandler _host.Resize, AddressOf HostMoved
            RemoveHandler _host.FormClosing, AddressOf HostClosing
            If _current Is Me Then _current = Nothing
            If _shadow IsNot Nothing Then
                _shadow.Close()
                _shadow.Dispose()
                _shadow = Nothing
            End If
            Close()
            Dispose()
        End Sub

        ' ---------------------------------------------------------------- input

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim over = _actionText.Length > 0 AndAlso _linkRect.Contains(e.Location)
            If over <> _linkHover Then
                _linkHover = over
                Cursor = If(over, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If e.Button <> MouseButtons.Left Then Return
            If _actionText.Length > 0 AndAlso _linkRect.Contains(e.Location) Then
                Dim action = _onAction
                Dismiss(immediate:=True)
                action?.Invoke()
            Else
                Dismiss(immediate:=False)
            End If
        End Sub

        ' ---------------------------------------------------------------- painting

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim toneColor = Theme.ToneColor(_tone)
            Dim h = ClientSize.Height

            ' Status edge in the tone color.
            Gfx.FillRounded(g, New RectangleF(Px(8), Px(14), Px(Theme.StatusEdgeWidth), h - Px(28)), Px(2), toneColor)

            Dim badge As New RectangleF(Px(20), (h - Px(30)) / 2.0F, Px(30), Px(30))
            Using brush As New SolidBrush(Theme.ToneSoft(_tone))
                g.FillEllipse(brush, badge)
            End Using
            Icons.Draw(g, IconFor(_tone), RectangleF.Inflate(badge, -Px(7), -Px(7)), toneColor, Theme.Scale * 2.0F)

            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis
            TextRenderer.DrawText(g, _message, Theme.FontBodySemibold, _messageRect, Theme.TextPrimary, flags)
            If _actionText.Length > 0 Then
                Dim linkFont = If(_linkHover, New Font(Theme.FontBodyBold, FontStyle.Bold Or FontStyle.Underline), Theme.FontBodyBold)
                TextRenderer.DrawText(g, _actionText, linkFont, _linkRect, If(_linkHover, Theme.BrandDark, Theme.Brand), flags)
                If _linkHover Then linkFont.Dispose()
            End If
        End Sub

        Private Shared Function IconFor(tone As Tone) As IconKind
            Select Case tone
                Case Tone.Success : Return IconKind.Check
                Case Tone.Warning : Return IconKind.Alert
                Case Tone.Danger : Return IconKind.Close
                Case Else : Return IconKind.Info
            End Select
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _timer.Dispose()
                _shadow?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
