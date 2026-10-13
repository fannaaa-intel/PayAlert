Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports PayAlert.Controls

Namespace Forms

    ''' <summary>
    ''' Shared layout for frmLogin and frmRegister: the brand art panel on the left and a centered,
    ''' fixed-width form column on the right. Subclasses add their rows with the Add* helpers.
    ''' </summary>
    Public Class AuthWindow
        Inherits PAWindow

        Protected Const WindowWidth As Integer = 960
        Protected Const WindowHeight As Integer = 620
        Protected Const ArtWidth As Integer = 430
        Protected Const ColumnWidth As Integer = 340

        Protected ReadOnly Art As New AuthArt()
        Private ReadOnly _formSide As New Panel()
        Protected ReadOnly Column As New FlowLayoutPanel()

        Private Shared ReadOnly WelcomeFont As New Font(Theme.FontFamilyName, 20.0F, FontStyle.Bold)

        Public Sub New()
            MyBase.New()
            ClientSize = New Size(WindowWidth, WindowHeight)

            Art.Dock = DockStyle.Left
            Art.Width = ArtWidth
            EnableDrag(Art)

            _formSide.Dock = DockStyle.Fill
            _formSide.BackColor = Theme.Surface
            EnableDrag(_formSide)
            AddHandler _formSide.Layout, AddressOf CenterColumn

            Column.FlowDirection = FlowDirection.TopDown
            Column.WrapContents = False
            Column.AutoSize = True
            Column.AutoSizeMode = AutoSizeMode.GrowAndShrink
            Column.BackColor = Theme.Surface
            Column.Margin = New Padding(0)
            Column.Padding = New Padding(0)
            AddHandler Column.SizeChanged, Sub(s, e) CenterColumn(s, Nothing)
            _formSide.Controls.Add(Column)

            Controls.Add(_formSide)
            Controls.Add(Art)
        End Sub

        Private Sub CenterColumn(sender As Object, e As LayoutEventArgs)
            Column.Location = New Point(Math.Max(0, (_formSide.ClientSize.Width - Column.Width) \ 2),
                                        Math.Max(PxI(DragStripHeight), (_formSide.ClientSize.Height - Column.Height) \ 2))
        End Sub

        ''' <summary>The art panel is BrandSoft, so the anti-aliased left corners must be BrandSoft too.</summary>
        Protected Overrides Sub PaintWindowFace(g As Graphics, body As RectangleF, face As GraphicsPath)
            Using fill As New SolidBrush(Theme.Surface)
                g.FillPath(fill, face)
            End Using
            Dim state = g.Save()
            g.SetClip(New RectangleF(body.X - 1, body.Y - 1, Art.Width + 1, body.Height + 2))
            Using artFill As New SolidBrush(Theme.BrandSoft)
                g.FillPath(artFill, face)
            End Using
            g.Restore(state)
            Using pen As New Pen(Theme.CardBorder)
                g.DrawPath(pen, face)
            End Using
        End Sub

        ' ---------------------------------------------------------------- column helpers (logical sizes)

        Protected Function AddTitle(title As String, subtitle As String) As Label
            Column.Controls.Add(New Label() With {
                .Text = title, .Font = WelcomeFont, .ForeColor = Theme.TextPrimary, .BackColor = Theme.Surface,
                .AutoSize = False, .Size = New Size(ColumnWidth, 38), .Margin = New Padding(0)})
            Dim subLabel As New Label() With {
                .Text = subtitle, .Font = Theme.FontBody, .ForeColor = Theme.TextMuted, .BackColor = Theme.Surface,
                .AutoSize = False, .Size = New Size(ColumnWidth, 24), .Margin = New Padding(0, 2, 0, 18)}
            Column.Controls.Add(subLabel)
            Return subLabel
        End Function

        Protected Function AddMessage() As InlineMessage
            Dim message As New InlineMessage() With {.Size = New Size(ColumnWidth, 44), .Margin = New Padding(0, 0, 0, 14)}
            Column.Controls.Add(message)
            Return message
        End Function

        Protected Sub AddFieldLabel(text As String)
            Column.Controls.Add(New Label() With {
                .Text = text, .Font = Theme.FontBodySemibold, .ForeColor = Theme.TextPrimary, .BackColor = Theme.Surface,
                .AutoSize = False, .Size = New Size(ColumnWidth, 22), .Margin = New Padding(0)})
        End Sub

        Protected Function AddField(icon As IconKind, placeholder As String, maxLength As Integer, Optional isPassword As Boolean = False) As PATextBox
            Dim field As New PATextBox() With {
                .Icon = icon, .PlaceholderText = placeholder, .MaxLength = maxLength, .IsPassword = isPassword,
                .Size = New Size(ColumnWidth, 42), .Margin = New Padding(0, 0, 0, 2)}
            Column.Controls.Add(field)
            Return field
        End Function

        ''' <summary>A one-line slot under a field for its inline error (or a muted hint).</summary>
        Protected Function AddFieldNote(Optional hint As String = "") As Label
            Dim note As New Label() With {
                .Text = hint, .Tag = hint, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .BackColor = Theme.Surface,
                .AutoSize = False, .Size = New Size(ColumnWidth, 20), .Margin = New Padding(0, 0, 0, 8)}
            Column.Controls.Add(note)
            Return note
        End Function

        Protected Function AddPrimary(text As String) As PrimaryButton
            Dim button As New PrimaryButton() With {.Text = text, .Size = New Size(ColumnWidth, 44), .Margin = New Padding(0, 8, 0, 16)}
            Column.Controls.Add(button)
            Return button
        End Function

        ''' <summary>"Question? Action" row; returns the action link.</summary>
        Protected Function AddSwitchRow(question As String, action As String) As LinkLabel
            Dim row As New FlowLayoutPanel() With {
                .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .AutoSize = True, .BackColor = Theme.Surface,
                .Margin = New Padding(0)}
            row.Controls.Add(New Label() With {
                .Text = question, .Font = Theme.FontBody, .ForeColor = Theme.TextMuted, .BackColor = Theme.Surface,
                .AutoSize = True, .Margin = New Padding(0, 0, 4, 0)})
            Dim link As New LinkLabel() With {
                .Text = action, .Font = Theme.FontBodySemibold, .AutoSize = True, .BackColor = Theme.Surface,
                .LinkColor = Theme.Brand, .ActiveLinkColor = Theme.BrandDark, .VisitedLinkColor = Theme.Brand,
                .LinkBehavior = LinkBehavior.HoverUnderline, .Margin = New Padding(0), .TabStop = True}
            row.Controls.Add(link)
            Column.Controls.Add(row)
            Return link
        End Function

        ''' <summary>Shows an inline field error, or restores the field's hint when message is empty.</summary>
        Protected Shared Sub SetFieldError(field As PATextBox, note As Label, message As String)
            Dim hasError = Not String.IsNullOrEmpty(message)
            field.HasError = hasError
            note.Text = If(hasError, message, CStr(note.Tag))
            note.ForeColor = If(hasError, Theme.OverdueText, Theme.TextMuted)
        End Sub

    End Class

End Namespace
