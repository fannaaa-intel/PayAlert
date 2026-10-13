Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' PayAlert signature "Soft card" (§6A): a Panel with 12px rounded corners, a 1px Border line and a
    ''' very soft 2–3px shadow, drawn with GDI+. Optional status edge (4px rounded strip on the left).
    ''' Children are laid out inside the card face (DisplayRectangle excludes the shadow and status edge).
    ''' </summary>
    Public Class SoftCard
        Inherits Panel

        Private _radius As Integer = Theme.CardRadius
        Private _fillColor As Color = Theme.Surface
        Private _borderColor As Color = Theme.CardBorder
        Private _shadowDepth As Integer = 3
        Private _statusEdge As String = ""
        Private _hoverable As Boolean
        Private _hover As Boolean

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or ControlStyles.SupportsTransparentBackColor, True)
            BackColor = Color.Transparent
            ForeColor = Theme.TextPrimary
            Font = Theme.FontBody
            Padding = New Padding(Theme.CardPadding)
        End Sub

        Private ReadOnly Property S As Single
            Get
                Return DeviceDpi / 96.0F
            End Get
        End Property

        <Category("PayAlert"), DefaultValue(Theme.CardRadius)>
        Public Property CornerRadius As Integer
            Get
                Return _radius
            End Get
            Set(value As Integer)
                _radius = Math.Max(0, value)
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property FillColor As Color
            Get
                Return _fillColor
            End Get
            Set(value As Color)
                _fillColor = value
                Invalidate(True)
            End Set
        End Property

        <Category("PayAlert")>
        Public Property BorderColor As Color
            Get
                Return _borderColor
            End Get
            Set(value As Color)
                _borderColor = value
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(3)>
        Public Property ShadowDepth As Integer
            Get
                Return _shadowDepth
            End Get
            Set(value As Integer)
                _shadowDepth = Math.Max(0, Math.Min(3, value))
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>Status name for the left status edge; empty for none.</summary>
        <Category("PayAlert"), DefaultValue("")>
        Public Property StatusEdge As String
            Get
                Return _statusEdge
            End Get
            Set(value As String)
                _statusEdge = If(value, "")
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>When true the card lifts slightly and shows a hand cursor on hover.</summary>
        <Category("PayAlert"), DefaultValue(False)>
        Public Property Hoverable As Boolean
            Get
                Return _hoverable
            End Get
            Set(value As Boolean)
                _hoverable = value
                Cursor = If(value, Cursors.Hand, Cursors.Default)
            End Set
        End Property

        Private ReadOnly Property ShadowPx As Integer
            Get
                Return CInt(Math.Round(_shadowDepth * S))
            End Get
        End Property

        Private ReadOnly Property EdgeSpace As Integer
            Get
                Return If(_statusEdge.Length = 0, 0, CInt(Math.Round(10 * S)))
            End Get
        End Property

        ''' <summary>The card face (without the shadow band).</summary>
        Public ReadOnly Property FaceBounds As Rectangle
            Get
                Return New Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1 - ShadowPx))
            End Get
        End Property

        Public Overrides ReadOnly Property DisplayRectangle As Rectangle
            Get
                Dim r = MyBase.DisplayRectangle
                r.X += EdgeSpace
                r.Width -= EdgeSpace
                r.Height -= ShadowPx
                Return r
            End Get
        End Property

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            MyBase.OnPaintBackground(e)   ' transparent: paints the parent behind the card
            Dim g = e.Graphics
            Gfx.HighQuality(g)

            Dim face As New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F - ShadowPx)
            Dim radius = _radius * S
            Dim lifted = _hoverable AndAlso _hover
            If _shadowDepth > 0 Then Gfx.SoftShadow(g, face, radius, ShadowPx, If(lifted, 2, 1))
            Gfx.FillRounded(g, face, radius, _fillColor)
            Gfx.DrawRounded(g, face, radius, If(lifted, Theme.BorderStrong, _borderColor))

            If _statusEdge.Length > 0 Then
                Dim inset = 14 * S
                Dim edge As New RectangleF(6 * S, face.Y + inset, Theme.StatusEdgeWidth * S, Math.Max(8 * S, face.Height - inset * 2))
                Gfx.StatusEdge(g, edge, _statusEdge)
            End If
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            SetHover(True)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            ' Leaving into a child control still counts as hovering the card.
            SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)))
        End Sub

        Protected Overrides Sub OnControlAdded(e As ControlEventArgs)
            MyBase.OnControlAdded(e)
            AddHandler e.Control.MouseLeave, AddressOf ChildMouseLeave
            AddHandler e.Control.MouseEnter, AddressOf ChildMouseEnter
            If _hoverable Then AddHandler e.Control.Click, AddressOf ChildClick
        End Sub

        Private Sub ChildMouseEnter(sender As Object, e As EventArgs)
            SetHover(True)
        End Sub

        Private Sub ChildMouseLeave(sender As Object, e As EventArgs)
            SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)))
        End Sub

        Private Sub ChildClick(sender As Object, e As EventArgs)
            ' A click on a label inside a clickable card counts as a click on the card.
            If TypeOf sender IsNot IButtonControl Then OnClick(e)
        End Sub

        Private Sub SetHover(value As Boolean)
            If Not _hoverable OrElse value = _hover Then Return
            _hover = value
            Invalidate(True)
        End Sub

    End Class

End Namespace
