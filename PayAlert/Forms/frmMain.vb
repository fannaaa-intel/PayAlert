Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Services
Imports PayAlert.Views

Namespace Forms

    ''' <summary>
    ''' The main window (§7): a fixed 220px white sidebar and a content area where the Navigator shows
    ''' one screen at a time. The Admin item appears only for administrators. Alt+Left goes back.
    ''' </summary>
    Public Class frmMain
        Inherits Form

        ''' <summary>Raised after the user confirms Log out.</summary>
        Public Event LoggedOut As EventHandler

        Private NotInheritable Class NavItem
            Public Key As String
            Public Text As String
            Public Icon As IconKind
            Public Factory As Func(Of PAView)
            Public AdminOnly As Boolean
        End Class

        Private ReadOnly _items As New List(Of NavItem) From {
            New NavItem With {.Key = "dashboard", .Text = "Dashboard", .Icon = IconKind.Dashboard, .Factory = Function() New ucDashboard()},
            New NavItem With {.Key = "payments", .Text = "Payments", .Icon = IconKind.Payments, .Factory = Function() New ucPayments()},
            New NavItem With {.Key = "history", .Text = "History", .Icon = IconKind.History, .Factory = Function() New ucHistory()},
            New NavItem With {.Key = "notifications", .Text = "Notifications", .Icon = IconKind.Bell, .Factory = Function() New ucNotifications()},
            New NavItem With {.Key = "settings", .Text = "Settings", .Icon = IconKind.Settings, .Factory = Function() New ucSettings()},
            New NavItem With {.Key = "admin", .Text = "Admin", .Icon = IconKind.Admin, .Factory = Function() New ucAdminDashboard(), .AdminOnly = True}}

        Private ReadOnly _sidebar As New Panel()
        Private ReadOnly _content As New Panel()
        Private ReadOnly _buttons As New Dictionary(Of String, SidebarButton)
        Private ReadOnly _logoutButton As New SidebarButton()
        Private ReadOnly _navigator As Navigator
        ''' <summary>Re-checks reminders every 5 minutes while the app is open (§3.7).</summary>
        Private ReadOnly _reminderTimer As New Timer() With {.Interval = 5 * 60 * 1000}

        Public Sub New()
            SuspendLayout()
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            Text = "PayAlert"
            Icon = AppIcon.Icon
            Size = New Size(1280, 760)
            MinimumSize = New Size(1100, 680)
            StartPosition = FormStartPosition.CenterScreen
            WindowState = FormWindowState.Maximized
            BackColor = Theme.AppBackground
            ForeColor = Theme.TextPrimary
            Font = Theme.FontBody
            DoubleBuffered = True
            KeyPreview = True

            _content.Dock = DockStyle.Fill
            _content.BackColor = Theme.AppBackground
            Controls.Add(_content)
            BuildSidebar()
            Controls.Add(_sidebar)

            _navigator = New Navigator(_content)
            AddHandler _navigator.Navigated, AddressOf OnNavigated
            AddHandler _reminderTimer.Tick, Sub(s, e) RunReminders()
            ResumeLayout(False)
            PerformLayout()
        End Sub

        Public ReadOnly Property Navigator As Navigator
            Get
                Return _navigator
            End Get
        End Property

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            RunReminders()
            _reminderTimer.Start()
            Open("dashboard")
        End Sub

        ' ---------------------------------------------------------------- sidebar

        Private Sub BuildSidebar()
            _sidebar.Dock = DockStyle.Left
            _sidebar.Width = Theme.SidebarWidth
            _sidebar.BackColor = Theme.Surface
            AddHandler _sidebar.Paint, AddressOf PaintSidebar
            ' Keep Log out pinned to the bottom of the sidebar's real height (anchoring breaks when the panel
            ' starts at its default size before docking).
            AddHandler _sidebar.Layout,
                Sub(s, e)
                    _logoutButton.Top = _sidebar.ClientSize.Height - _logoutButton.Height - CInt(20 * DeviceDpi / 96.0F)
                    _sidebar.Invalidate()
                End Sub

            Dim y = 84
            Dim index = 0
            For Each item In _items
                If item.AdminOnly AndAlso Not Session.IsAdmin Then Continue For
                Dim button As New SidebarButton() With {
                    .Text = item.Text, .Icon = item.Icon, .Location = New Point(12, y), .Size = New Size(196, 44),
                    .TabIndex = index, .Tag = item.Key}
                AddHandler button.Click, Sub(s, e) Open(CStr(DirectCast(s, SidebarButton).Tag))
                _sidebar.Controls.Add(button)
                _buttons(item.Key) = button
                y += 48
                index += 1
            Next

            _logoutButton.Text = "Log out"
            _logoutButton.Icon = IconKind.Logout
            _logoutButton.Size = New Size(196, 44)
            _logoutButton.Location = New Point(12, 0)
            _logoutButton.TabIndex = index
            AddHandler _logoutButton.Click, Sub(s, e) RequestLogOut()
            _sidebar.Controls.Add(_logoutButton)
        End Sub

        ''' <summary>Opens a sidebar section as a fresh screen (clicking the current one also starts it over).</summary>
        Private Sub Open(key As String)
            Dim item = _items.FirstOrDefault(Function(i) i.Key = key)
            If item Is Nothing OrElse (item.AdminOnly AndAlso Not Session.IsAdmin) Then Return
            _navigator.ShowRoot(item.Factory())
        End Sub

        Private Sub OnNavigated(sender As Object, e As EventArgs)
            Dim key = If(_navigator.Current?.SidebarKey, "")
            For Each pair In _buttons
                pair.Value.IsActive = pair.Key = key
            Next
            Text = If(_navigator.Current Is Nothing, "PayAlert", _navigator.Current.Title & " - PayAlert")
        End Sub

        ' ---------------------------------------------------------------- reminders

        ''' <summary>
        ''' Creates today's reminders (if notifications are on), refreshes the badge, and reloads the Notifications screen
        ''' when it is showing. Runs at login and on the 5-minute timer; problems are logged, never shown, since the
        ''' screens report database trouble themselves.
        ''' </summary>
        Public Sub RunReminders()
            If Not Session.IsSignedIn Then Return
            Try
                Dim created = ReminderService.Run(Session.CurrentUser.UserId)
                If created > 0 Then TryCast(_navigator.Current, ucNotifications)?.Reload()
            Catch ex As Exception
                Debug.WriteLine("[frmMain.RunReminders] " & ex.ToString())
            End Try
            RefreshUnreadCount()
        End Sub

        ''' <summary>Reads the unread count again and shows it on the Notifications item.</summary>
        Public Sub RefreshUnreadCount()
            If Not Session.IsSignedIn Then Return
            Try
                Dim userId = Session.CurrentUser.UserId
                ' With reminders turned off, the badge stays clear.
                SetUnreadCount(If(SettingsRepo.Get(userId).NotificationsEnabled, NotificationRepo.CountUnread(userId), 0))
            Catch ex As Exception
                Debug.WriteLine("[frmMain.RefreshUnreadCount] " & ex.ToString())
            End Try
        End Sub

        ''' <summary>Unread count shown on the Notifications item.</summary>
        Public Sub SetUnreadCount(count As Integer)
            Dim button As SidebarButton = Nothing
            If _buttons.TryGetValue("notifications", button) Then button.BadgeCount = count
        End Sub

        ''' <summary>Repaints the signed-in user block after the profile changes.</summary>
        Public Sub RefreshUserBlock()
            _sidebar.Invalidate()
        End Sub

        ''' <summary>Asks for confirmation, then logs out (from the sidebar or Settings).</summary>
        Public Sub RequestLogOut()
            If PADialog.Confirm(Me, "Log out of PayAlert?", "You'll need your email and password to log in again.",
                                "Log out", "Stay", Tone.Neutral, IconKind.Logout) Then
                RaiseEvent LoggedOut(Me, EventArgs.Empty)
            End If
        End Sub

        Private Sub PaintSidebar(sender As Object, e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim s = DeviceDpi / 96.0F
            Using pen As New Pen(Theme.Border)
                g.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height)
            End Using

            ' Wordmark.
            Dim mark As New RectangleF(22 * s, 24 * s, 30 * s, 30 * s)
            Gfx.DrawLogoMark(g, mark)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim paySize = TextRenderer.MeasureText(g, "Pay", Theme.FontWordmark, Size.Empty, flags)
            Dim x = CInt(mark.Right + 10 * s)
            Dim y = CInt(mark.Y + (mark.Height - paySize.Height) / 2.0F)
            TextRenderer.DrawText(g, "Pay", Theme.FontWordmark, New Point(x, y), Theme.TextPrimary, flags)
            TextRenderer.DrawText(g, "Alert", Theme.FontWordmark, New Point(x + paySize.Width, y), Theme.Brand, flags)

            ' Signed-in user just above Log out: initials disc, name, role and email.
            Dim user = Session.CurrentUser
            If user Is Nothing Then Return
            Dim top = _logoutButton.Top - CInt(64 * s)
            Using rule As New Pen(Theme.Border)
                g.DrawLine(rule, CInt(20 * s), top - CInt(10 * s), _sidebar.Width - CInt(20 * s), top - CInt(10 * s))
            End Using
            Dim disc As New RectangleF(20 * s, top + 6 * s, 36 * s, 36 * s)
            Using fill As New SolidBrush(Theme.BrandSoft)
                g.FillEllipse(fill, disc)
            End Using
            Dim initials = String.Concat(user.FullName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries).
                                         Take(2).Select(Function(part) Char.ToUpperInvariant(part(0))))
            TextRenderer.DrawText(g, initials, Theme.FontCaptionBold, Rectangle.Round(disc), Theme.BrandDark,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Dim textX = CInt(disc.Right + 10 * s)
            Dim textWidth = _sidebar.Width - textX - CInt(16 * s)
            TextRenderer.DrawText(g, user.FullName, Theme.FontBodyBold, New Rectangle(textX, CInt(top + 6 * s), textWidth, CInt(20 * s)),
                                  Theme.TextPrimary, flags Or TextFormatFlags.EndEllipsis)
            TextRenderer.DrawText(g, If(user.IsAdmin, "Administrator", "User") & "  ·  " & user.Email, Theme.FontCaption,
                                  New Rectangle(textX, CInt(top + 26 * s), textWidth, CInt(18 * s)), Theme.TextMuted, flags Or TextFormatFlags.EndEllipsis)
        End Sub

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            _reminderTimer.Stop()
            _reminderTimer.Dispose()
            MyBase.OnFormClosed(e)
        End Sub

        ' ---------------------------------------------------------------- keys

        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            If keyData = (Keys.Alt Or Keys.Left) AndAlso _navigator.GoBack() Then Return True
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

    End Class

End Namespace
