Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Manage Users (§7 ucAdminUsers): every account with Name, Email, Role, Status, Payments and Joined; an
    ''' All / Active / Deactivated filter and search. Select an account to Activate/Deactivate it (confirmed first) or
    ''' Reset Password (a temporary password shown once). Neither works on the admin's own account.
    ''' </summary>
    Public Class ucAdminUsers
        Inherits PAView

        Private ReadOnly _toggle As New PAButton()
        Private ReadOnly _reset As New SecondaryButton()
        Private ReadOnly _filter As New SegmentedControl()
        Private ReadOnly _search As New PATextBox()
        Private ReadOnly _countLabel As New Label()
        Private ReadOnly _grid As New UserGrid()
        Private ReadOnly _empty As New EmptyState()
        Private _all As New List(Of UserSummary)

        ''' <param name="statusFilter">0 all, 1 active, 2 deactivated.</param>
        Public Sub New(Optional statusFilter As Integer = 0)
            MyBase.New()
            SidebarKey = "admin"
            Title = "Manage Users"
            Subtitle = "Activate, deactivate and reset passwords"

            _toggle.Text = "Deactivate"
            _toggle.Icon = IconKind.Close
            _toggle.Kind = ButtonKind.DangerText
            _toggle.Size = New Size(150, Theme.ButtonHeight)
            _reset.Text = "Reset Password"
            _reset.Icon = IconKind.Lock
            _reset.Size = New Size(170, Theme.ButtonHeight)
            AddHeaderAction(_reset)
            AddHeaderAction(_toggle)
            AddHandler _toggle.Click, AddressOf ToggleClicked
            AddHandler _reset.Click, AddressOf ResetClicked

            _filter.SetItems("All", "Active", "Deactivated")
            _filter.SelectedIndex = Math.Max(0, Math.Min(2, statusFilter))
            _filter.Dock = DockStyle.Left
            _filter.Width = 360
            _search.Icon = IconKind.Search
            _search.PlaceholderText = "Search by name or email"
            _search.Dock = DockStyle.Right
            _search.Width = 300
            Dim row1 As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            row1.Controls.Add(_filter)
            row1.Controls.Add(_search)

            _countLabel.Dock = DockStyle.Top
            _countLabel.Height = 30
            _countLabel.TextAlign = ContentAlignment.MiddleLeft
            _countLabel.Font = Theme.FontCaption
            _countLabel.ForeColor = Theme.TextMuted

            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Padding = New Padding(6, 8, 6, 8)}
            _grid.Dock = DockStyle.Fill
            _empty.Dock = DockStyle.Fill
            _empty.Visible = False
            card.Controls.Add(_grid)
            card.Controls.Add(_empty)

            Body.Controls.Add(card)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 6})
            Body.Controls.Add(_countLabel)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 6})
            Body.Controls.Add(row1)

            AddHandler _filter.SelectedIndexChanged, Sub(s, e) ApplyFilters()
            AddHandler _search.TextChanged, Sub(s, e) ApplyFilters()
            AddHandler _grid.SelectionChanged, Sub(s, e) UpdateActions()
            AddHandler _empty.ActionClick,
                Sub(s, e)
                    _search.Text = ""
                    _filter.SelectedIndex = 0
                End Sub
        End Sub

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Reload()
        End Sub

        Private Sub Reload()
            If Not AdminService.IsAdmin Then Return
            _grid.CurrentUserId = Session.CurrentUser.UserId
            Try
                _all = AdminRepo.GetUsers()
            Catch ex As Exception
                Debug.WriteLine("[ucAdminUsers] " & ex.ToString())
                PADialog.Error(Me, "Can't load the accounts", Db.FriendlyMessageFor(ex))
                _all = New List(Of UserSummary)
            End Try
            ApplyFilters()
        End Sub

        Private Function Matches(u As UserSummary) As Boolean
            If _filter.SelectedIndex = 1 AndAlso Not u.IsActive Then Return False
            If _filter.SelectedIndex = 2 AndAlso u.IsActive Then Return False
            Dim query = _search.Text.Trim()
            Return query.Length = 0 OrElse
                   u.FullName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 OrElse
                   u.Email.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
        End Function

        Private Sub ApplyFilters()
            Dim shown = _all.Where(AddressOf Matches).ToList()
            _grid.SetRows(shown)
            Dim empty = shown.Count = 0
            _grid.Visible = Not empty
            _empty.Visible = empty
            If empty Then
                Dim query = _search.Text.Trim()
                _empty.Icon = If(query.Length > 0, IconKind.Search, IconKind.User)
                _empty.Title = If(query.Length > 0, "No accounts match """ & query & """", "No deactivated accounts")
                _empty.Message = If(query.Length > 0, "Try another name or email.", "Every account can log in right now.")
                _empty.ActionText = "Show all accounts"
                _empty.ActionIcon = IconKind.Close
            End If
            UpdateActions()
        End Sub

        ''' <summary>Enables the header actions for the selected account and explains when they can't be used.</summary>
        Private Sub UpdateActions()
            Dim u = _grid.SelectedUser
            Dim canManage = u IsNot Nothing AndAlso AdminService.CanManage(u.UserId)
            _toggle.Enabled = canManage
            _reset.Enabled = canManage
            Dim activating = u IsNot Nothing AndAlso Not u.IsActive
            _toggle.Text = If(activating, "Activate", "Deactivate")
            _toggle.Icon = If(activating, IconKind.Check, IconKind.Close)
            _toggle.Kind = If(activating, ButtonKind.Primary, ButtonKind.DangerText)

            Dim counts = _grid.RowCount & " of " & _all.Count & If(_all.Count = 1, " account", " accounts")
            If u Is Nothing Then
                _countLabel.Text = counts & "  ·  Select an account to activate, deactivate or reset its password."
            ElseIf Not canManage Then
                _countLabel.Text = counts & "  ·  This is your account. Change your own password in Settings."
            Else
                _countLabel.Text = counts & "  ·  Selected: " & u.FullName & " (" & u.Email & ")"
            End If
        End Sub

        Private Sub ToggleClicked(sender As Object, e As EventArgs)
            Dim u = _grid.SelectedUser
            If u Is Nothing OrElse Not AdminService.CanManage(u.UserId) Then Return
            Dim activate = Not u.IsActive
            Dim confirmed = If(activate,
                PADialog.Confirm(Me, "Activate " & u.FullName & "?", "They'll be able to log in to PayAlert again.",
                                 "Activate", "Cancel", Tone.Success, IconKind.Check),
                PADialog.Confirm(Me, "Deactivate " & u.FullName & "?",
                                 "They won't be able to log in until you activate the account again. Their payments are kept.",
                                 "Deactivate", "Cancel", Tone.Danger, IconKind.Close))
            If Not confirmed Then Return
            Try
                If Not AdminService.SetActive(u.UserId, activate) Then
                    PADialog.Info(Me, "This account isn't available", "It may have been removed.", Tone.Warning)
                End If
            Catch ex As Exception
                Debug.WriteLine("[ucAdminUsers] " & ex.ToString())
                PADialog.Error(Me, "Couldn't change the account", Db.FriendlyMessageFor(ex))
                Return
            End Try
            Reload()
            PAToast.Notify(FindForm(), u.FullName & If(activate, " activated", " deactivated"), If(activate, Tone.Success, Tone.Danger))
        End Sub

        Private Sub ResetClicked(sender As Object, e As EventArgs)
            Dim u = _grid.SelectedUser
            If u Is Nothing OrElse Not AdminService.CanManage(u.UserId) Then Return
            If Not PADialog.Confirm(Me, "Reset " & u.FullName & "'s password?",
                                    "PayAlert will make a temporary password and show it once. Their current password stops working.",
                                    "Reset Password", "Cancel", Tone.Warning, IconKind.Lock) Then Return
            Dim temporary As String
            Try
                temporary = AdminService.ResetPassword(u.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucAdminUsers] " & ex.ToString())
                PADialog.Error(Me, "Couldn't reset the password", Db.FriendlyMessageFor(ex))
                Return
            End Try
            If temporary Is Nothing Then Return

            Dim choice = PADialog.Success(Me, "Temporary password created",
                                          {"Account: " & u.Email,
                                           "Temporary password: " & temporary,
                                           "Shown only once. They can change it in Settings."},
                                          "Done", "Copy Password")
            If choice = DialogChoice.Secondary Then
                Try
                    Clipboard.SetText(temporary)
                    PAToast.Notify(FindForm(), "Password copied", Tone.Success)
                Catch ex As Exception
                    Debug.WriteLine("[ucAdminUsers] Clipboard: " & ex.ToString())
                End Try
            End If
        End Sub

    End Class

End Namespace
