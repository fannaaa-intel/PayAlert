Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Dialogs
Imports PayAlert.Forms
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Profile (§7): edit the full name and email (the email must stay unique). Save is enabled once something changed
    ''' and everything is valid; problems show under the fields. Saving updates the sidebar and returns to Settings.
    ''' </summary>
    Public Class ucProfile
        Inherits PAView

        Private Const CardMaxWidth As Integer = 820

        Private ReadOnly _identity As New ProfileIdentity()
        Private ReadOnly _name As New PATextBox()
        Private ReadOnly _email As New PATextBox()
        Private ReadOnly _nameNote As New Label()
        Private ReadOnly _emailNote As New Label()
        Private ReadOnly _save As New PrimaryButton()
        Private ReadOnly _cancel As New SecondaryButton()
        Private ReadOnly _touched As New HashSet(Of PATextBox)
        Private _serverEmailError As String

        Public Sub New()
            MyBase.New()
            SidebarKey = "settings"
            Title = "Profile"
            Subtitle = "Your name and email"

            _name.Icon = IconKind.User
            _name.MaxLength = AuthService.MaxNameLength
            _name.PlaceholderText = "e.g. Juan Dela Cruz"
            _email.Icon = IconKind.Mail
            _email.MaxLength = AuthService.MaxEmailLength
            _email.PlaceholderText = "name@example.com"

            Dim fields As New TableLayoutPanel() With {.Dock = DockStyle.Top, .Height = 84, .ColumnCount = 2, .RowCount = 1, .BackColor = Theme.Surface}
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            fields.Controls.Add(FieldCell("Full name *", _name, _nameNote, "Shown in the sidebar and greetings.", New Padding(0, 0, 9, 0)), 0, 0)
            fields.Controls.Add(FieldCell("Email *", _email, _emailNote, "You log in with this.", New Padding(9, 0, 0, 0)), 1, 0)

            _save.Text = "Save Profile"
            _save.Size = New Size(140, Theme.ButtonHeight)
            _save.Enabled = False
            _cancel.Text = "Cancel"
            _cancel.Size = New Size(110, Theme.ButtonHeight)
            Dim footer As New FlowLayoutPanel() With {
                .Dock = DockStyle.Top, .Height = 52, .FlowDirection = FlowDirection.RightToLeft, .BackColor = Theme.Surface,
                .Padding = New Padding(0, 14, 0, 0), .WrapContents = False}
            _save.Margin = New Padding(0)
            _cancel.Margin = New Padding(0, 0, 10, 0)
            footer.Controls.Add(_save)
            footer.Controls.Add(_cancel)

            _identity.Dock = DockStyle.Top
            _identity.Height = 96
            Dim card As New SoftCard() With {.Location = New Point(0, 0), .Padding = New Padding(24, 18, 24, 16)}
            card.Height = 18 + 96 + 18 + 84 + 52 + 16 + 6
            card.Controls.Add(footer)
            card.Controls.Add(fields)
            card.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 18, .BackColor = Theme.Surface})
            card.Controls.Add(_identity)
            Body.Controls.Add(card)
            AddHandler Body.Layout, Sub(s, e) card.Width = Math.Min(Body.ClientSize.Width, CInt(CardMaxWidth * DeviceDpi / 96.0F))

            AddHandler _save.Click, AddressOf SaveClicked
            AddHandler _cancel.Click, Sub(s, e) Navigator.GoBack()
            For Each box In {_name, _email}
                AddHandler box.TextChanged,
                    Sub(s, e)
                        If box Is _email Then _serverEmailError = Nothing
                        CheckForm()
                    End Sub
                AddHandler box.Leave,
                    Sub(s, e)
                        _touched.Add(box)
                        CheckForm()
                    End Sub
            Next
        End Sub

        Private Shared Function FieldCell(caption As String, input As PATextBox, note As Label, hint As String, margin As Padding) As Panel
            Dim cell As New Panel() With {.Dock = DockStyle.Fill, .Margin = margin, .BackColor = Theme.Surface}
            note.Text = hint
            note.Tag = hint
            note.Font = Theme.FontCaption
            note.ForeColor = Theme.TextMuted
            note.BackColor = Theme.Surface
            note.Dock = DockStyle.Top
            note.Height = 20
            note.AutoEllipsis = True
            input.Dock = DockStyle.Top
            input.Height = 42
            cell.Controls.Add(note)
            cell.Controls.Add(input)
            cell.Controls.Add(New Label() With {
                .Text = caption, .Font = Theme.FontBodySemibold, .ForeColor = Theme.TextPrimary, .BackColor = Theme.Surface,
                .Dock = DockStyle.Top, .Height = 22})
            Return cell
        End Function

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Dim user = Session.CurrentUser
            _identity.SetUser(user.FullName, user.Email, user.IsAdmin, user.CreatedAt)
            _name.Text = user.FullName
            _email.Text = user.Email
            _touched.Clear()
            _serverEmailError = Nothing
            CheckForm()
            _name.Focus()
        End Sub

        Private ReadOnly Property HasChanges As Boolean
            Get
                Dim user = Session.CurrentUser
                Return _name.Text.Trim() <> user.FullName OrElse Not String.Equals(_email.Text.Trim(), user.Email, StringComparison.Ordinal)
            End Get
        End Property

        Private Function CheckForm() As Boolean
            Dim result = AuthService.ValidateProfile(_name.Text, _email.Text)
            ShowNote(_name, _nameNote, If(_touched.Contains(_name) AndAlso result.Errors.ContainsKey(AccountField.FullName), result.Errors(AccountField.FullName), Nothing))
            Dim emailError = If(_serverEmailError, If(_touched.Contains(_email) AndAlso result.Errors.ContainsKey(AccountField.Email), result.Errors(AccountField.Email), Nothing))
            ShowNote(_email, _emailNote, emailError)
            Dim valid = result.Errors.Count = 0 AndAlso _serverEmailError Is Nothing
            _save.Enabled = valid AndAlso HasChanges
            Return valid
        End Function

        Private Shared Sub ShowNote(box As PATextBox, note As Label, message As String)
            note.Text = If(message, CStr(note.Tag))
            note.ForeColor = If(message Is Nothing, Theme.TextMuted, Theme.OverdueText)
            box.HasError = message IsNot Nothing
        End Sub

        Private Sub SaveClicked(sender As Object, e As EventArgs)
            _touched.Add(_name)
            _touched.Add(_email)
            If Not CheckForm() OrElse Not HasChanges Then Return

            Dim user = Session.CurrentUser
            Dim result = AuthService.UpdateProfile(user.UserId, _name.Text, _email.Text)
            If result.DatabaseMessage.Length > 0 Then
                PADialog.Error(Me, "Couldn't save your profile", result.DatabaseMessage)
                Return
            End If
            If Not result.Success Then
                If result.Errors.ContainsKey(AccountField.Email) Then _serverEmailError = result.Errors(AccountField.Email)
                CheckForm()
                _email.Focus()
                Return
            End If

            user.FullName = _name.Text.Trim()
            user.Email = _email.Text.Trim()
            Dim main = TryCast(FindForm(), frmMain)
            main?.RefreshUserBlock()
            Navigator.GoBack()
            PAToast.Notify(main, "Profile saved", Tone.Success)
        End Sub

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If keyData = Keys.Enter AndAlso TypeOf ActiveControl IsNot PAButton AndAlso _save.Enabled Then
                SaveClicked(Me, EventArgs.Empty)
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        ' ---------------------------------------------------------------- identity band

        ''' <summary>
        ''' The top of the profile card: a large initials disc, the name, "User · Member since …", and the email, with a
        ''' perforated rule under it (the ticket motif).
        ''' </summary>
        Private NotInheritable Class ProfileIdentity
            Inherits PAControl

            Private _name As String = ""
            Private _email As String = ""
            Private _isAdmin As Boolean
            Private _since As DateTime

            Public Sub SetUser(fullName As String, email As String, isAdmin As Boolean, createdAt As DateTime)
                _name = fullName
                _email = email
                _isAdmin = isAdmin
                _since = createdAt
                Invalidate()
            End Sub

            Protected Overrides Sub OnPaint(e As PaintEventArgs)
                Dim g = e.Graphics
                Gfx.HighQuality(g)
                Dim disc = Px(64)
                Dim discRect As New RectangleF(0, (Height - Px(14) - disc) / 2.0F, disc, disc)
                Using brush As New SolidBrush(Theme.BrandSoft)
                    g.FillEllipse(brush, discRect)
                End Using
                Using ring As New Pen(Theme.Blend(Theme.BrandSoft, Theme.Brand, 0.35), Px(1.5F))
                    g.DrawEllipse(ring, RectangleF.Inflate(discRect, -Px(0.75F), -Px(0.75F)))
                End Using
                Dim initials = String.Concat(_name.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries).
                                             Take(2).Select(Function(part) Char.ToUpperInvariant(part(0))))
                TextRenderer.DrawText(g, initials, Theme.FontSection, Rectangle.Round(discRect), Theme.BrandDark,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

                Dim x = CInt(discRect.Right + Px(18))
                Dim w = Width - x
                Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
                Dim nameHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontDialogTitle, Size.Empty, flags).Height
                Dim captionHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontCaption, Size.Empty, flags).Height
                Dim top = CInt(discRect.Y + (disc - nameHeight - captionHeight * 2 - Px(6)) / 2.0F)
                TextRenderer.DrawText(g, _name, Theme.FontDialogTitle, New Rectangle(x, top, w, nameHeight + PxI(3)), Theme.TextPrimary, flags)
                Dim since = If(_since = DateTime.MinValue, "", "  ·  Member since " & _since.ToString("MMM d, yyyy", CultureInfo.InvariantCulture))
                TextRenderer.DrawText(g, If(_isAdmin, "Administrator", "User") & since, Theme.FontCaption,
                                      New Rectangle(x, top + nameHeight + PxI(3), w, captionHeight + PxI(3)), Theme.TextMuted, flags)
                TextRenderer.DrawText(g, _email, Theme.FontCaption, New Rectangle(x, top + nameHeight + captionHeight + PxI(6), w, captionHeight + PxI(3)),
                                      Theme.TextMuted, flags)

                Using perforation As New Pen(Theme.BorderStrong, Math.Max(1.0F, S))
                    perforation.DashStyle = DashStyle.Custom
                    perforation.DashPattern = {3.0F, 3.0F}
                    g.DrawLine(perforation, 0, Height - Px(1), Width, Height - Px(1))
                End Using
            End Sub
        End Class

    End Class

End Namespace
