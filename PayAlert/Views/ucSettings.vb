Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Forms
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Settings (§7): cards for Account (Profile, Change Password), Notifications (on/off, default reminder),
    ''' Preferences (currency, read-only) and Log out. Changes save the moment they're made and confirm with a toast.
    ''' </summary>
    Public Class ucSettings
        Inherits PAView

        Private Shared ReadOnly ReminderChoices As Integer() = {1, 3, 7}
        Private Const CardMaxWidth As Integer = 1120

        Private ReadOnly _profileRow As New SettingRow()
        Private ReadOnly _passwordRow As New SettingRow()
        Private ReadOnly _notifyRow As New SettingRow()
        Private ReadOnly _reminderRow As New SettingRow()
        Private ReadOnly _currencyRow As New SettingRow()
        Private ReadOnly _logoutRow As New SettingRow()
        Private ReadOnly _notify As New ToggleSwitch()
        Private ReadOnly _reminder As New SegmentedControl()
        Private ReadOnly _logout As New DangerButton()
        Private _settings As UserSettings
        Private _loading As Boolean

        Public Sub New()
            MyBase.New()
            SidebarKey = "settings"
            Title = "Settings"
            Subtitle = "Your account and reminder preferences"

            _profileRow.Icon = IconKind.User
            _profileRow.Title = "Profile"
            _profileRow.Opens = True
            AddHandler _profileRow.Click, Sub(s, e) Navigator.Push(New ucProfile())
            _passwordRow.Icon = IconKind.Lock
            _passwordRow.Title = "Change Password"
            _passwordRow.Description = "Use at least " & AuthService.MinPasswordLength & " characters."
            _passwordRow.Opens = True
            _passwordRow.ShowRule = False
            AddHandler _passwordRow.Click, AddressOf ChangePasswordClicked

            _notify.Size = New Size(52, 30)
            _notifyRow.Icon = IconKind.Bell
            _notifyRow.Title = "Reminders"
            _notifyRow.Description = "Alerts inside PayAlert before, on, and after due dates."
            _notifyRow.Trailing = _notify
            _reminder.SetItems("1 day", "3 days", "7 days")
            _reminder.Size = New Size(246, Theme.FieldHeight)
            _reminderRow.Icon = IconKind.Clock
            _reminderRow.Title = "Default reminder"
            _reminderRow.Description = "Used for each new payment."
            _reminderRow.Trailing = _reminder
            _reminderRow.ShowRule = False
            AddHandler _notify.CheckedChanged, AddressOf NotifyChanged
            AddHandler _reminder.SelectedIndexChanged, AddressOf ReminderChanged

            Dim currency As New Label() With {
                .Text = "PHP  (₱)", .AutoSize = True, .Font = Theme.FontBodyBold, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent}
            _currencyRow.Icon = IconKind.Payments
            _currencyRow.Title = "Currency"
            _currencyRow.Description = "Amounts show as ₱1,500.00. Fixed in this version."
            _currencyRow.Trailing = currency
            _currencyRow.ShowRule = False

            _logout.Text = "Log out"
            _logout.Icon = IconKind.Logout
            _logout.Filled = False
            _logout.Size = New Size(120, Theme.ButtonHeight)
            AddHandler _logout.Click, Sub(s, e) TryCast(FindForm(), frmMain)?.RequestLogOut()
            _logoutRow.Icon = IconKind.Logout
            _logoutRow.Title = "Log out of PayAlert"
            _logoutRow.Description = "You'll need to log in again."
            _logoutRow.Trailing = _logout
            _logoutRow.ShowRule = False

            ' Two columns of cards: Account and Preferences on the left, Notifications and Log out on the right.
            Dim columns As New TableLayoutPanel() With {.ColumnCount = 2, .RowCount = 1, .BackColor = Theme.AppBackground,
                                                        .Location = New Point(0, 0), .Anchor = AnchorStyles.Top Or AnchorStyles.Left}
            columns.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            columns.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            columns.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            columns.Controls.Add(Column(New Padding(0, 0, 8, 0),
                                        SettingsCard("Account", _profileRow, _passwordRow),
                                        SettingsCard("Preferences", _currencyRow)), 0, 0)
            columns.Controls.Add(Column(New Padding(8, 0, 0, 0),
                                        SettingsCard("Notifications", _notifyRow, _reminderRow),
                                        SettingsCard("Session", _logoutRow)), 1, 0)
            Body.Controls.Add(columns)
            AddHandler Body.Layout,
                Sub(s, e)
                    columns.Width = Math.Min(Body.ClientSize.Width, CInt(CardMaxWidth * DeviceDpi / 96.0F))
                    columns.Height = Body.ClientSize.Height
                End Sub
        End Sub

        ''' <summary>A column of cards stacked from the top with 16px between them.</summary>
        Private Shared Function Column(margin As Padding, ParamArray cards As SoftCard()) As Panel
            Dim panel As New Panel() With {.Dock = DockStyle.Fill, .Margin = margin, .BackColor = Theme.AppBackground}
            ' Docked top-down: the last added docks first, so add in reverse.
            For i = cards.Length - 1 To 0 Step -1
                panel.Controls.Add(cards(i))
                If i > 0 Then panel.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 16})
            Next
            Return panel
        End Function

        ''' <summary>A soft card with a section heading over its rows.</summary>
        Private Shared Function SettingsCard(heading As String, ParamArray rows As SettingRow()) As SoftCard
            Dim card As New SoftCard() With {.Dock = DockStyle.Top, .Padding = New Padding(14, 12, 14, 8)}
            card.Height = 12 + 34 + 64 * rows.Length + 8 + 6
            For i = rows.Length - 1 To 0 Step -1
                rows(i).Dock = DockStyle.Top
                rows(i).Height = 64
                card.Controls.Add(rows(i))
            Next
            card.Controls.Add(New Label() With {
                .Text = heading, .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Top, .Height = 34, .Padding = New Padding(6, 0, 0, 0), .TextAlign = ContentAlignment.MiddleLeft})
            Return card
        End Function

        ' ---------------------------------------------------------------- data

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Dim user = Session.CurrentUser
            _profileRow.Description = user.FullName & "  ·  " & user.Email
            Try
                _settings = SettingsRepo.Get(user.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucSettings] " & ex.ToString())
                PADialog.Error(Me, "Can't load your settings", Db.FriendlyMessageFor(ex))
                _settings = New UserSettings With {.UserId = user.UserId}
            End Try
            _loading = True
            _notify.SetCheckedSilently(_settings.NotificationsEnabled)
            _reminder.SelectedIndex = Math.Max(0, Array.IndexOf(ReminderChoices, _settings.DefaultReminderDays))
            _loading = False
        End Sub

        ''' <summary>Saves the settings; on failure shows why and returns False so the caller can undo the change.</summary>
        Private Function SaveSettings() As Boolean
            Try
                SettingsRepo.Save(_settings)
                Return True
            Catch ex As Exception
                Debug.WriteLine("[ucSettings] " & ex.ToString())
                PADialog.Error(Me, "Couldn't save your settings", Db.FriendlyMessageFor(ex))
                Return False
            End Try
        End Function

        Private Sub NotifyChanged(sender As Object, e As EventArgs)
            If _loading OrElse _settings Is Nothing Then Return
            _settings.NotificationsEnabled = _notify.Checked
            If Not SaveSettings() Then
                _settings.NotificationsEnabled = Not _notify.Checked
                _notify.SetCheckedSilently(_settings.NotificationsEnabled)
                Return
            End If
            ' Turning reminders on checks for due payments right away; turning them off clears the badge.
            TryCast(FindForm(), frmMain)?.RunReminders()
            PAToast.Notify(FindForm(), If(_notify.Checked, "Reminders turned on", "Reminders turned off"), Tone.Success)
        End Sub

        Private Sub ReminderChanged(sender As Object, e As EventArgs)
            If _loading OrElse _settings Is Nothing OrElse _reminder.SelectedIndex < 0 Then Return
            Dim previous = _settings.DefaultReminderDays
            _settings.DefaultReminderDays = ReminderChoices(_reminder.SelectedIndex)
            If Not SaveSettings() Then
                _settings.DefaultReminderDays = previous
                _loading = True
                _reminder.SelectedIndex = Math.Max(0, Array.IndexOf(ReminderChoices, previous))
                _loading = False
                Return
            End If
            Dim days = _settings.DefaultReminderDays
            PAToast.Notify(FindForm(), "Default reminder: " & days & If(days = 1, " day", " days") & " before", Tone.Success)
        End Sub

        Private Sub ChangePasswordClicked(sender As Object, e As EventArgs)
            If dlgChangePassword.Run(Me) Then PAToast.Notify(FindForm(), "Password changed", Tone.Success)
        End Sub

    End Class

End Namespace
