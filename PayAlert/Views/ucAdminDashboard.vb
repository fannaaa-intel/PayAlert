Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Admin dashboard (§7, manuscript Figure 10): Total Users, Active Users, Total Payments and Overdue Payments across
    ''' PayAlert, and the newest accounts. The user cards and "Manage Users" open the account list.
    ''' Payments stay private to their owners, so the payment cards are counts only.
    ''' </summary>
    Public Class ucAdminDashboard
        Inherits PAView

        Private Const NewestCount As Integer = 6

        Private ReadOnly _totalUsers As New SummaryCard()
        Private ReadOnly _activeUsers As New SummaryCard()
        Private ReadOnly _totalPayments As New SummaryCard()
        Private ReadOnly _overduePayments As New SummaryCard()
        Private ReadOnly _newest As New UserGrid()
        Private ReadOnly _newestNote As New Label()

        Public Sub New()
            MyBase.New()
            SidebarKey = "admin"
            Title = "Admin"
            Subtitle = "Users and payments across PayAlert"
            Dim manage As New PrimaryButton() With {.Text = "Manage Users", .Icon = IconKind.User, .Size = New Size(160, Theme.ButtonHeight)}
            AddHandler manage.Click, Sub(s, e) Navigator.Push(New ucAdminUsers())
            AddHeaderAction(manage)

            SetupCard(_totalUsers, "TOTAL USERS", IconKind.User, "View accounts", True)
            SetupCard(_activeUsers, "ACTIVE USERS", IconKind.Check, "View active", True)
            SetupCard(_totalPayments, "TOTAL PAYMENTS", IconKind.Payments, "", False)
            SetupCard(_overduePayments, "OVERDUE PAYMENTS", IconKind.Alert, "", False)
            _overduePayments.AccentColor = Theme.OverdueText
            AddHandler _totalUsers.Click, Sub(s, e) Navigator.Push(New ucAdminUsers(0))
            AddHandler _activeUsers.Click, Sub(s, e) Navigator.Push(New ucAdminUsers(1))

            Dim cards As New TableLayoutPanel() With {.Dock = DockStyle.Top, .Height = 132, .ColumnCount = 4, .RowCount = 1}
            For i = 0 To 3
                cards.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            Next
            cards.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim list = {_totalUsers, _activeUsers, _totalPayments, _overduePayments}
            For i = 0 To 3
                list(i).Dock = DockStyle.Fill
                list(i).Margin = New Padding(0, 0, If(i < 3, 16, 0), 16)
                cards.Controls.Add(list(i), i, 0)
            Next

            ' Newest accounts.
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Padding = New Padding(16, 14, 16, 10)}
            Dim heading As New Label() With {
                .Text = "Newest accounts", .Font = Theme.FontSection, .ForeColor = Theme.TextPrimary, .BackColor = Color.Transparent,
                .Dock = DockStyle.Left, .Width = 200, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(4, 0, 0, 0)}
            Dim viewAll As New PAButton() With {.Kind = ButtonKind.Ghost, .Text = "View all", .Dock = DockStyle.Right}
            viewAll.Width = viewAll.PreferredWidth()
            AddHandler viewAll.Click, Sub(s, e) Navigator.Push(New ucAdminUsers())
            _newestNote.Dock = DockStyle.Fill
            _newestNote.TextAlign = ContentAlignment.MiddleLeft
            _newestNote.Font = Theme.FontCaption
            _newestNote.ForeColor = Theme.TextMuted
            _newestNote.BackColor = Color.Transparent
            Dim headRow As New Panel() With {.Dock = DockStyle.Top, .Height = 36, .BackColor = Color.Transparent}
            headRow.Controls.Add(_newestNote)
            headRow.Controls.Add(viewAll)
            headRow.Controls.Add(heading)
            _newest.Dock = DockStyle.Fill
            AddHandler _newest.CellDoubleClick, Sub(s, e) Navigator.Push(New ucAdminUsers())
            card.Controls.Add(_newest)
            card.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 4, .BackColor = Color.Transparent})
            card.Controls.Add(headRow)

            Body.Controls.Add(card)
            Body.Controls.Add(cards)
        End Sub

        Private Shared Sub SetupCard(card As SummaryCard, caption As String, icon As IconKind, hint As String, clickable As Boolean)
            card.Caption = caption
            card.Icon = icon
            card.Hint = hint
            card.Value = "0"
            If Not clickable Then
                card.Cursor = Cursors.Default
                card.TabStop = False
            End If
        End Sub

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            If Not AdminService.IsAdmin Then Return
            Try
                Dim stats = AdminRepo.GetStats()
                Dim users = AdminRepo.GetUsers()
                _totalUsers.Value = stats.TotalUsers.ToString()
                _activeUsers.Value = stats.ActiveUsers.ToString()
                _totalPayments.Value = stats.TotalPayments.ToString()
                _overduePayments.Value = stats.OverduePayments.ToString()
                _overduePayments.ValueColor = If(stats.OverduePayments > 0, Theme.OverdueText, Theme.TextPrimary)
                _newest.CurrentUserId = Session.CurrentUser.UserId
                _newest.SetRows(users.Take(NewestCount))
                Dim inactive = stats.TotalUsers - stats.ActiveUsers
                _newestNote.Text = If(inactive > 0, inactive & If(inactive = 1, " account is", " accounts are") & " deactivated", "")
            Catch ex As Exception
                Debug.WriteLine("[ucAdminDashboard] " & ex.ToString())
                PADialog.Error(Me, "Can't load the admin dashboard", Db.FriendlyMessageFor(ex))
            End Try
        End Sub

    End Class

End Namespace
