Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' One line in a settings card: a line icon on a soft disc, a title with a muted description, and on the right
    ''' either a control (switch, segmented choice, value) or, for rows that open something, a chevron. Rows that open
    ''' something highlight on hover and respond to click, Enter and Space.
    ''' </summary>
    <DefaultEvent("Click")>
    Public Class SettingRow
        Inherits PAControl

        Private _icon As IconKind = IconKind.Settings
        Private _title As String = ""
        Private _description As String = ""
        Private _opens As Boolean
        Private _showRule As Boolean = True
        Private _trailing As Control
        Private _hover As Boolean

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.StandardClick, True)
            ' Only rows that open something take focus (see Opens).
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            BackColor = Color.Transparent
            Size = New Size(420, 64)
        End Sub

        <Category("PayAlert")>
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
                Invalidate()
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Description As String
            Get
                Return _description
            End Get
            Set(value As String)
                _description = If(value, "")
                Invalidate()
            End Set
        End Property

        ''' <summary>The row opens something: chevron, hover highlight, clickable and focusable.</summary>
        <Category("PayAlert"), DefaultValue(False)>
        Public Property Opens As Boolean
            Get
                Return _opens
            End Get
            Set(value As Boolean)
                _opens = value
                SetStyle(ControlStyles.Selectable, value)
                TabStop = value
                Cursor = If(value, Cursors.Hand, Cursors.Default)
                Invalidate()
            End Set
        End Property

        ''' <summary>Hairline under the row (off for the last row of a card).</summary>
        <Category("PayAlert"), DefaultValue(True)>
        Public Property ShowRule As Boolean
            Get
                Return _showRule
            End Get
            Set(value As Boolean)
                _showRule = value
                Invalidate()
            End Set
        End Property

        ''' <summary>The control shown on the right, vertically centered (keeps its own width and height).</summary>
        <Browsable(False)>
        Public Property Trailing As Control
            Get
                Return _trailing
            End Get
            Set(value As Control)
                If _trailing IsNot Nothing Then Controls.Remove(_trailing)
                _trailing = value
                If value IsNot Nothing Then Controls.Add(value)
                PerformLayout()
            End Set
        End Property

        ''' <summary>"Title: description", for tests and screen readers.</summary>
        Public Overrides Property Text As String
            Get
                Return _title & ": " & _description
            End Get
            Set(value As String)
            End Set
        End Property

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            If _trailing Is Nothing Then Return
            _trailing.Location = New Point(Width - _trailing.Width - PxI(4), (Height - _trailing.Height) \ 2)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            If _opens Then Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            If _opens Then Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If _opens Then Focus()
        End Sub

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If _opens AndAlso Focused AndAlso (keyData = Keys.Enter OrElse keyData = Keys.Space) Then
                OnClick(EventArgs.Empty)
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

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
            Dim active = _opens AndAlso (_hover OrElse (Focused AndAlso ShowFocusCues))
            If active Then
                Gfx.FillRounded(g, New RectangleF(0, Px(4), Width - 1, Height - Px(8)), Px(10),
                                If(Focused AndAlso ShowFocusCues, Theme.BrandSoft, Theme.Blend(Theme.Surface, Theme.AppBackground, 0.8)))
            End If
            If _showRule Then
                Using rule As New Pen(Theme.Border)
                    g.DrawLine(rule, Px(4), Height - 0.5F, Width - Px(4), Height - 0.5F)
                End Using
            End If

            Dim disc = Px(36)
            Dim discRect As New RectangleF(Px(8), (Height - disc) / 2.0F, disc, disc)
            Using brush As New SolidBrush(If(active, Theme.Surface, Theme.NeutralSoft))
                g.FillEllipse(brush, discRect)
            End Using
            Icons.Draw(g, _icon, RectangleF.Inflate(discRect, -Px(9), -Px(9)), If(active, Theme.Brand, Theme.TextMuted), Px(1.7F))

            Dim textX = CInt(discRect.Right + Px(14))
            Dim right = If(_trailing IsNot Nothing, _trailing.Left - PxI(16), Width - PxI(If(_opens, 40, 8)))
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.Top Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            Dim titleHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontBodySemibold, Size.Empty, flags).Height
            Dim descHeight = If(_description.Length = 0, 0, TextRenderer.MeasureText(g, "Ag", Theme.FontCaption, Size.Empty, flags).Height)
            Dim top = (Height - titleHeight - If(descHeight > 0, PxI(2) + descHeight, 0)) \ 2
            TextRenderer.DrawText(g, _title, Theme.FontBodySemibold, New Rectangle(textX, top, right - textX, titleHeight + PxI(3)), Theme.TextPrimary, flags)
            If descHeight > 0 Then
                TextRenderer.DrawText(g, _description, Theme.FontCaption, New Rectangle(textX, top + titleHeight + PxI(2), right - textX, descHeight + PxI(3)),
                                      Theme.TextMuted, flags)
            End If

            If _opens AndAlso _trailing Is Nothing Then
                Dim chevron = Px(16)
                Icons.Draw(g, IconKind.ChevronRight, New RectangleF(Width - Px(14) - chevron, (Height - chevron) / 2.0F, chevron, chevron),
                           If(active, Theme.Brand, Theme.TextMuted), Px(1.8F))
            End If
        End Sub

    End Class

End Namespace
