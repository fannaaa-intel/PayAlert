Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' Centered empty state: a soft "orbit" illustration around a line icon, a title, a message and an
    ''' optional primary action button.
    ''' </summary>
    <DefaultEvent("ActionClick")>
    Public Class EmptyState
        Inherits PAControl

        Private _icon As IconKind = IconKind.Inbox
        Private _title As String = "Nothing here yet"
        Private _message As String = ""
        Private ReadOnly _action As New PrimaryButton()
        Private _compact As Boolean

        Public Event ActionClick As EventHandler

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            _action.Visible = False
            _action.Icon = IconKind.Plus
            AddHandler _action.Click, Sub(s, e) RaiseEvent ActionClick(Me, EventArgs.Empty)
            Controls.Add(_action)
            Size = New Size(420, 320)
        End Sub

        <Category("PayAlert"), DefaultValue(GetType(IconKind), "Inbox")>
        Public Property Icon As IconKind
            Get
                Return _icon
            End Get
            Set(value As IconKind)
                _icon = value
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Title As String
            Get
                Return _title
            End Get
            Set(value As String)
                _title = If(value, "")
                PerformLayout()
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Message As String
            Get
                Return _message
            End Get
            Set(value As String)
                _message = If(value, "")
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>Text of the action button; empty hides the button.</summary>
        <Category("PayAlert")>
        Public Property ActionText As String
            Get
                Return _action.Text
            End Get
            Set(value As String)
                _action.Text = If(value, "")
                _action.Visible = _action.Text.Length > 0
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>Smaller illustration and spacing, for empty lists inside a short card.</summary>
        <Category("PayAlert"), DefaultValue(False)>
        Public Property Compact As Boolean
            Get
                Return _compact
            End Get
            Set(value As Boolean)
                _compact = value
                PerformLayout()
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(GetType(IconKind), "Plus")>
        Public Property ActionIcon As IconKind
            Get
                Return _action.Icon
            End Get
            Set(value As IconKind)
                _action.Icon = value
            End Set
        End Property

        Private Structure ArtLayout
            Public Art As RectangleF
            Public TitleRect As Rectangle
            Public MessageRect As Rectangle
            Public ButtonRect As Rectangle
        End Structure

        Private Function Measure() As ArtLayout
            Dim result As New ArtLayout()
            Dim art = Px(If(_compact, 60, 104))
            Dim gap = Px(If(_compact, 10, 18))
            Dim textWidth = CInt(Math.Min(Width - Px(32), Px(360)))
            Dim flags = TextFormatFlags.WordBreak Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding
            Dim titleSize = TextRenderer.MeasureText(_title, Theme.FontSection, New Size(textWidth, Integer.MaxValue), flags)
            Dim messageSize = If(_message.Length = 0, Size.Empty,
                                 TextRenderer.MeasureText(_message, Theme.FontBody, New Size(textWidth, Integer.MaxValue), flags))
            Dim hasButton = _action.Text.Length > 0
            Dim buttonWidth = If(hasButton, _action.PreferredWidth(), 0)
            Dim buttonHeight = PxI(Theme.ButtonHeight)

            Dim total = art + gap + titleSize.Height + If(_message.Length > 0, Px(6) + messageSize.Height, 0) +
                        If(hasButton, Px(20) + buttonHeight, 0)
            Dim y = Math.Max(0.0F, (Height - total) / 2.0F)
            result.Art = New RectangleF((Width - art) / 2.0F, y, art, art)
            y += art + gap
            result.TitleRect = New Rectangle((Width - textWidth) \ 2, CInt(y), textWidth, titleSize.Height)
            y += titleSize.Height
            If _message.Length > 0 Then
                y += Px(6)
                result.MessageRect = New Rectangle((Width - textWidth) \ 2, CInt(y), textWidth, messageSize.Height)
                y += messageSize.Height
            End If
            If hasButton Then
                y += Px(20)
                result.ButtonRect = New Rectangle((Width - buttonWidth) \ 2, CInt(y), buttonWidth, buttonHeight)
            End If
            Return result
        End Function

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            If _action Is Nothing Then Return   ' the base constructor lays out before field initializers run
            If _action.Visible Then _action.Bounds = Measure().ButtonRect
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim parts = Measure()

            ' Orbit illustration: dashed ring, a Brand "satellite" dot, and the icon on a soft disc.
            Dim art = parts.Art
            Using orbit As New Pen(Theme.BorderStrong, Px(1.25F))
                orbit.DashStyle = DashStyle.Custom
                orbit.DashPattern = {2.0F, 3.0F}
                g.DrawEllipse(orbit, RectangleF.Inflate(art, -Px(2), -Px(2)))
            End Using
            Dim angle = -40 * Math.PI / 180.0
            Dim radius = art.Width / 2.0F - Px(2)
            Dim cx = art.X + art.Width / 2.0F
            Dim cy = art.Y + art.Height / 2.0F
            Dim satellite = Px(If(_compact, 8, 10))
            Using brush As New SolidBrush(Theme.Brand)
                g.FillEllipse(brush, cx + CSng(Math.Cos(angle)) * radius - satellite / 2.0F,
                              cy + CSng(Math.Sin(angle)) * radius - satellite / 2.0F, satellite, satellite)
            End Using
            Dim k = art.Width / Px(104)
            Dim disc = RectangleF.Inflate(art, -Px(18) * k, -Px(18) * k)
            Using brush As New SolidBrush(Theme.BrandSoft)
                g.FillEllipse(brush, disc)
            End Using
            Icons.Draw(g, _icon, RectangleF.Inflate(disc, -Px(19) * k, -Px(19) * k), Theme.Brand, Px(2))

            Dim flags = TextFormatFlags.WordBreak Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding
            TextRenderer.DrawText(g, _title, Theme.FontSection, parts.TitleRect, Theme.TextPrimary, flags)
            If _message.Length > 0 Then
                TextRenderer.DrawText(g, _message, Theme.FontBody, parts.MessageRect, Theme.TextMuted, flags)
            End If
        End Sub

    End Class

End Namespace
