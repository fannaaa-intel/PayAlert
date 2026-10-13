Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Windows.Forms

Namespace Dialogs

    ''' <summary>
    ''' A click-through layered window that sits just behind a borderless PayAlert window and draws
    ''' its soft drop shadow plus an anti-aliased copy of its outline. The front window's Region is
    ''' inset by one pixel, so the visible edge comes from here and the corners stay smooth.
    ''' </summary>
    Friend NotInheritable Class ShadowWindow
        Inherits Form

        ''' <summary>Builds the window outline for a body rectangle grown (or shrunk) by the given amount.</summary>
        Public Delegate Function ShapeBuilder(body As RectangleF, grow As Single) As GraphicsPath

        Private ReadOnly _shape As ShapeBuilder
        Private ReadOnly _paintFace As Action(Of Graphics, RectangleF, GraphicsPath)
        Private ReadOnly _spread As Integer
        Private ReadOnly _offsetY As Integer
        Private _bitmap As Bitmap
        Private _renderedSize As Size

        Public Sub New(shape As ShapeBuilder, paintFace As Action(Of Graphics, RectangleF, GraphicsPath), spread As Integer, offsetY As Integer)
            _shape = shape
            _paintFace = paintFace
            _spread = spread
            _offsetY = offsetY
            FormBorderStyle = FormBorderStyle.None
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            Text = ""
        End Sub

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim cp = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or Native.WS_EX_LAYERED Or Native.WS_EX_TRANSPARENT Or Native.WS_EX_NOACTIVATE Or Native.WS_EX_TOOLWINDOW
                Return cp
            End Get
        End Property

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>Positions the shadow around the target window bounds (screen coordinates) at the given alpha.</summary>
        Public Sub Follow(target As Rectangle, alpha As Byte)
            If IsDisposed Then Return
            If _bitmap Is Nothing OrElse _renderedSize <> target.Size Then Render(target.Size)
            Native.PushLayeredBitmap(Me, _bitmap, New Point(target.X - _spread, target.Y - _spread), alpha)
        End Sub

        ''' <summary>Forces a redraw on the next Follow (when the face content changes).</summary>
        Public Sub ResetFace()
            _renderedSize = Size.Empty
        End Sub

        Private Sub Render(bodySize As Size)
            _bitmap?.Dispose()
            _bitmap = New Bitmap(bodySize.Width + _spread * 2, bodySize.Height + _spread * 2, PixelFormat.Format32bppArgb)
            _renderedSize = bodySize
            Using g = Graphics.FromImage(_bitmap)
                g.Clear(Color.Transparent)
                Gfx.HighQuality(g)
                Dim body As New RectangleF(_spread, _spread, bodySize.Width, bodySize.Height)

                ' Soft shadow: many faint layers, widest first, nudged downward.
                For i = _spread To 1 Step -1
                    Dim layerBody = body
                    layerBody.Offset(0, _offsetY * (CSng(i) / _spread))
                    Dim strength = 1.0 - CDbl(i) / _spread
                    Dim alpha = CInt(Math.Max(1, 2 + 9 * strength * strength))
                    Using path = _shape(layerBody, i * 0.85F)
                        Using brush As New SolidBrush(Color.FromArgb(alpha, Theme.Overlay))
                            g.FillPath(brush, path)
                        End Using
                    End Using
                Next

                ' Cut the shadow out from under the body so semi-transparent edges never look muddy.
                Using face = _shape(body, 0)
                    Dim state = g.Save()
                    g.CompositingMode = CompositingMode.SourceCopy
                    Using clear As New SolidBrush(Color.Transparent)
                        g.FillPath(clear, face)
                    End Using
                    g.Restore(state)
                    _paintFace(g, body, face)
                End Using
            End Using
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then _bitmap?.Dispose()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
