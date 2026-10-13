Option Strict On
Option Explicit On

Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Dialogs

Namespace Forms

    ''' <summary>
    ''' Base for PayAlert's fixed-size borderless windows (login, register): 14px rounded corners with a soft
    ''' shadow (anti-aliased by a layered shadow window), a short fade-in, custom minimize/close buttons, and
    ''' dragging by the top strip or any control registered with EnableDrag.
    ''' </summary>
    Public Class PAWindow
        Inherits Form

        Protected Const DragStripHeight As Integer = 48
        Private Const FadeMs As Double = 160

        Private _shadow As ShadowWindow
        Private _shadowAlpha As Byte
        Private ReadOnly _closeButton As New ChromeButton() With {.Kind = ChromeKind.Close}
        Private ReadOnly _minimizeButton As New ChromeButton() With {.Kind = ChromeKind.Minimize}
        Private ReadOnly _fade As New Timer() With {.Interval = 15}
        Private ReadOnly _clock As New Stopwatch()

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            FormBorderStyle = FormBorderStyle.None
            StartPosition = FormStartPosition.CenterScreen
            BackColor = Theme.Surface
            ForeColor = Theme.TextPrimary
            Font = Theme.FontBody
            KeyPreview = True
            Icon = AppIcon.Icon
            Opacity = 0
            Controls.Add(_closeButton)
            Controls.Add(_minimizeButton)
            AddHandler _fade.Tick, AddressOf OnFadeTick
        End Sub

        ' Keep the taskbar button's minimize/restore and Alt+Space menu working without a caption.
        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Const WS_MINIMIZEBOX As Integer = &H20000
                Const WS_SYSMENU As Integer = &H80000
                Dim cp = MyBase.CreateParams
                cp.Style = cp.Style Or WS_MINIMIZEBOX Or WS_SYSMENU
                Return cp
            End Get
        End Property

        Protected ReadOnly Property S As Single
            Get
                Return DeviceDpi / 96.0F
            End Get
        End Property

        Protected Function PxI(logical As Single) As Integer
            Return CInt(Math.Round(logical * S))
        End Function

        ''' <summary>Lets the user drag the window by this control (for decorative panels).</summary>
        Protected Sub EnableDrag(target As Control)
            AddHandler target.MouseDown,
                Sub(sender, e)
                    If e.Button = MouseButtons.Left Then Native.BeginDrag(Me)
                End Sub
        End Sub

        ''' <summary>Paints the anti-aliased window face drawn by the shadow window. Override to match split backgrounds.</summary>
        Protected Overridable Sub PaintWindowFace(g As Graphics, body As RectangleF, face As GraphicsPath)
            Using fill As New SolidBrush(Theme.Surface)
                g.FillPath(fill, face)
            End Using
            Using pen As New Pen(Theme.CardBorder)
                g.DrawPath(pen, face)
            End Using
        End Sub

        Private Function BuildShape(body As RectangleF, grow As Single) As GraphicsPath
            Return Gfx.RoundedRect(RectangleF.Inflate(body, grow, grow), Theme.DialogRadius * S + grow)
        End Function

        Private Sub PaintFace(g As Graphics, body As RectangleF, face As GraphicsPath)
            PaintWindowFace(g, body, face)
        End Sub

        Private Sub UpdateRegion()
            Using path = BuildShape(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), -1)
                Region = New Region(path)
            End Using
            _shadow?.ResetFace()
        End Sub

        Private Sub PlaceChrome()
            Dim top = PxI(10)
            _closeButton.Location = New Point(ClientSize.Width - PxI(10) - _closeButton.Width, top)
            _minimizeButton.Location = New Point(_closeButton.Left - PxI(4) - _minimizeButton.Width, top)
            _closeButton.BringToFront()
            _minimizeButton.BringToFront()
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            UpdateRegion()
            PlaceChrome()
        End Sub

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            _shadow = New ShadowWindow(AddressOf BuildShape, AddressOf PaintFace, PxI(22), PxI(8))
            _shadow.Show()
            FollowShadow()
            _clock.Restart()
            _fade.Start()
        End Sub

        Private Sub OnFadeTick(sender As Object, e As EventArgs)
            Dim t = Math.Min(1.0, _clock.Elapsed.TotalMilliseconds / FadeMs)
            Dim eased = Gfx.EaseOut(t)
            Opacity = eased
            _shadowAlpha = CByte(Math.Round(255 * eased))
            FollowShadow()
            If t >= 1 Then _fade.Stop()
        End Sub

        Private Sub FollowShadow()
            If _shadow Is Nothing OrElse _shadow.IsDisposed Then Return
            If WindowState = FormWindowState.Minimized OrElse Not Visible Then
                If _shadow.Visible Then _shadow.Hide()
                Return
            End If
            If Not _shadow.Visible Then _shadow.Show()
            _shadow.Follow(Bounds, _shadowAlpha)
            Native.PlaceBelow(_shadow, Me)
        End Sub

        Protected Overrides Sub OnLocationChanged(e As EventArgs)
            MyBase.OnLocationChanged(e)
            FollowShadow()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            If WindowState <> FormWindowState.Minimized AndAlso IsHandleCreated Then
                UpdateRegion()
                PlaceChrome()
            End If
            FollowShadow()
        End Sub

        Protected Overrides Sub OnActivated(e As EventArgs)
            MyBase.OnActivated(e)
            FollowShadow()
        End Sub

        Protected Overrides Sub OnVisibleChanged(e As EventArgs)
            MyBase.OnVisibleChanged(e)
            FollowShadow()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left AndAlso e.Y <= PxI(DragStripHeight) Then Native.BeginDrag(Me)
        End Sub

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            _fade.Stop()
            If _shadow IsNot Nothing Then
                _shadow.Close()
                _shadow.Dispose()
                _shadow = Nothing
            End If
            MyBase.OnFormClosed(e)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _fade.Dispose()
                _shadow?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
