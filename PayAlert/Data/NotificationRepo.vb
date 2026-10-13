Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports PayAlert.Models

Namespace Data

    ''' <summary>
    ''' In-app reminders. The unique key (payment_id, type, notif_date) plus INSERT IGNORE means a reminder is created
    ''' at most once a day however often the ReminderService runs. Reads hide reminders of deleted payments.
    ''' </summary>
    Public NotInheritable Class NotificationRepo

        Private Sub New()
        End Sub

        ''' <summary>Adds the reminder unless the same one already exists for that day. Returns True if it was new.</summary>
        Public Shared Function AddIfNew(n As Notification) As Boolean
            Return Db.ExecuteNonQuery(
                "INSERT IGNORE INTO notifications (user_id, payment_id, type, message, notif_date) " &
                "VALUES (@user, @payment, @type, @message, @date)",
                Db.P("@user", n.UserId), Db.P("@payment", n.PaymentId), Db.P("@type", n.Type),
                Db.P("@message", Truncate(n.Message, 255)), Db.P("@date", n.NotifDate.Date)) > 0
        End Function

        ''' <summary>The user's reminders, newest day first; within a day the most urgent first.</summary>
        Public Shared Function GetForUser(userId As Integer) As List(Of Notification)
            Dim table = Db.Query(
                "SELECT n.notif_id, n.user_id, n.payment_id, n.type, n.message, n.notif_date, n.is_read, n.created_at, " &
                "p.name AS payment_name, p.due_date " &
                "FROM notifications n JOIN payments p ON p.payment_id = n.payment_id " &
                "WHERE n.user_id = @user AND p.is_deleted = 0 " &
                "ORDER BY n.notif_date DESC, FIELD(n.type, 'overdue', 'due_today', 'due_tomorrow', 'due_soon'), p.due_date, n.notif_id DESC",
                Db.P("@user", userId))
            Dim list As New List(Of Notification)(table.Rows.Count)
            For Each row As DataRow In table.Rows
                list.Add(New Notification With {
                    .NotifId = Convert.ToInt32(row("notif_id")),
                    .UserId = Convert.ToInt32(row("user_id")),
                    .PaymentId = Convert.ToInt32(row("payment_id")),
                    .Type = CStr(row("type")),
                    .Message = CStr(row("message")),
                    .NotifDate = Convert.ToDateTime(row("notif_date")).Date,
                    .IsRead = Convert.ToInt32(row("is_read")) <> 0,
                    .CreatedAt = If(row.IsNull("created_at"), DateTime.Now, Convert.ToDateTime(row("created_at"))),
                    .PaymentName = CStr(row("payment_name")),
                    .DueDate = Convert.ToDateTime(row("due_date")).Date})
            Next
            Return list
        End Function

        ''' <summary>Unread reminders (of payments that still exist), for the sidebar badge.</summary>
        Public Shared Function CountUnread(userId As Integer) As Integer
            Return Convert.ToInt32(Db.ExecuteScalar(
                "SELECT COUNT(*) FROM notifications n JOIN payments p ON p.payment_id = n.payment_id " &
                "WHERE n.user_id = @user AND n.is_read = 0 AND p.is_deleted = 0",
                Db.P("@user", userId)))
        End Function

        Public Shared Function MarkRead(notifId As Integer, userId As Integer) As Boolean
            Return Db.ExecuteNonQuery("UPDATE notifications SET is_read = 1 WHERE notif_id = @id AND user_id = @user AND is_read = 0",
                                      Db.P("@id", notifId), Db.P("@user", userId)) > 0
        End Function

        ''' <summary>Marks every unread reminder read. Returns how many changed.</summary>
        Public Shared Function MarkAllRead(userId As Integer) As Integer
            Return Db.ExecuteNonQuery("UPDATE notifications SET is_read = 1 WHERE user_id = @user AND is_read = 0", Db.P("@user", userId))
        End Function

        Private Shared Function Truncate(value As String, maxLength As Integer) As String
            If value Is Nothing Then Return ""
            Return If(value.Length <= maxLength, value, value.Substring(0, maxLength - 1) & "…")
        End Function

    End Class

End Namespace
