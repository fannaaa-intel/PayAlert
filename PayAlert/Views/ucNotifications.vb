Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Forms
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Notifications (§7, manuscript Figure 5): reminder cards, newest first, grouped by day, with an All / Unread filter.
    ''' Clicking a card marks it read and opens the payment. Mark all as read clears the sidebar badge.
    ''' </summary>
    Public Class ucNotifications
        Inherits PAView

        Private ReadOnly _markAll As New SecondaryButton()
        Private ReadOnly _filter As New SegmentedControl()
        Private ReadOnly _countLabel As New Label()
        Private ReadOnly _list As New NotificationList()
        Private ReadOnly _emptyCard As New SoftCard()
        Private ReadOnly _empty As New EmptyState()
        Private _all As New List(Of Notification)
        Private _enabled As Boolean = True

        Public Sub New()
            MyBase.New()
            SidebarKey = "notifications"
            Title = "Notifications"
            Subtitle = "Reminders before your due dates"

            _markAll.Text = "Mark all as read"
            _markAll.Icon = IconKind.Check
            _markAll.Size = New Size(176, Theme.ButtonHeight)
            AddHandler _markAll.Click, AddressOf MarkAllClicked
            AddHeaderAction(_markAll)

            _filter.SetItems("All", "Unread")
            _filter.Dock = DockStyle.Left
            _filter.Width = 220
            _countLabel.Dock = DockStyle.Fill
            _countLabel.TextAlign = ContentAlignment.MiddleRight
            _countLabel.Font = Theme.FontCaption
            _countLabel.ForeColor = Theme.TextMuted
            Dim row As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            row.Controls.Add(_countLabel)
            row.Controls.Add(_filter)

            _list.Dock = DockStyle.Fill
            _emptyCard.Dock = DockStyle.Fill
            _emptyCard.Visible = False
            _empty.Dock = DockStyle.Fill
            _emptyCard.Controls.Add(_empty)

            Body.Controls.Add(_list)
            Body.Controls.Add(_emptyCard)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 16})
            Body.Controls.Add(row)

            AddHandler _filter.SelectedIndexChanged, Sub(s, e) ApplyFilter()
            AddHandler _list.NotificationClicked, AddressOf NotificationClicked
            AddHandler _empty.ActionClick, AddressOf EmptyActionClicked
        End Sub

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Reload()
        End Sub

        ''' <summary>Loads the reminders again (also called by the 5-minute check when new ones arrive).</summary>
        Public Sub Reload()
            Try
                Dim userId = Session.CurrentUser.UserId
                _enabled = SettingsRepo.Get(userId).NotificationsEnabled
                _all = NotificationRepo.GetForUser(userId)
            Catch ex As Exception
                Debug.WriteLine("[ucNotifications] " & ex.ToString())
                PADialog.Error(Me, "Can't load your notifications", Db.FriendlyMessageFor(ex))
                _all = New List(Of Notification)
            End Try
            ApplyFilter()
        End Sub

        Private Sub ApplyFilter()
            Dim unread = _all.Where(Function(n) Not n.IsRead).Count()
            Dim shown = If(_filter.SelectedIndex = 1, _all.Where(Function(n) Not n.IsRead).ToList(), _all)
            _list.SetItems(shown)
            _markAll.Enabled = unread > 0
            _countLabel.Text = If(_all.Count = 0, "", unread & " unread  ·  " & _all.Count & If(_all.Count = 1, " reminder", " reminders"))
            TryCast(FindForm(), frmMain)?.SetUnreadCount(unread)

            Dim empty = shown.Count = 0
            _list.Visible = Not empty
            _emptyCard.Visible = empty
            If Not empty Then Return
            If Not _enabled AndAlso _all.Count = 0 Then
                SetEmpty(IconKind.Bell, "Reminders are turned off", "Turn them on in Settings to get alerts before payments are due.", "Open Settings", IconKind.Settings)
            ElseIf _all.Count = 0 Then
                SetEmpty(IconKind.Bell, "No reminders yet", "PayAlert reminds you here before a payment is due, on the day, and when it's overdue.", Nothing, IconKind.None)
            Else
                SetEmpty(IconKind.Check, "You're all caught up", "No unread reminders.", "Show all", IconKind.Bell)
            End If
        End Sub

        Private Sub SetEmpty(icon As IconKind, title As String, message As String, actionText As String, actionIcon As IconKind)
            _empty.Icon = icon
            _empty.Title = title
            _empty.Message = message
            _empty.ActionIcon = actionIcon
            _empty.ActionText = actionText
        End Sub

        Private Sub EmptyActionClicked(sender As Object, e As EventArgs)
            If Not _enabled AndAlso _all.Count = 0 Then
                Navigator.ShowRoot(New ucSettings())
            Else
                _filter.SelectedIndex = 0
            End If
        End Sub

        Private Sub NotificationClicked(sender As Object, n As Notification)
            Try
                If Not n.IsRead Then NotificationRepo.MarkRead(n.NotifId, Session.CurrentUser.UserId)
                n.IsRead = True
            Catch ex As Exception
                Debug.WriteLine("[ucNotifications] " & ex.ToString())
            End Try
            TryCast(FindForm(), frmMain)?.RefreshUnreadCount()
            Navigator.Push(New ucPaymentDetails(n.PaymentId))
        End Sub

        Private Sub MarkAllClicked(sender As Object, e As EventArgs)
            Dim changed As Integer
            Try
                changed = NotificationRepo.MarkAllRead(Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucNotifications] " & ex.ToString())
                PADialog.Error(Me, "Couldn't mark them as read", Db.FriendlyMessageFor(ex))
                Return
            End Try
            Reload()
            If changed > 0 Then PAToast.Notify(FindForm(), "Marked all as read", Tone.Success)
        End Sub

    End Class

End Namespace
