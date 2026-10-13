Option Strict On
Option Explicit On

Imports PayAlert.Models

Namespace Data

    ''' <summary>The user's preferences (user_settings).</summary>
    Public NotInheritable Class SettingsRepo

        Private Sub New()
        End Sub

        ''' <summary>The user's settings; defaults if the row is missing.</summary>
        Public Shared Function [Get](userId As Integer) As UserSettings
            Dim table = Db.Query("SELECT notifications_enabled, default_reminder_days, currency FROM user_settings WHERE user_id = @user",
                                 Db.P("@user", userId))
            Dim settings As New UserSettings With {.UserId = userId}
            If table.Rows.Count > 0 Then
                Dim row = table.Rows(0)
                settings.NotificationsEnabled = Convert.ToInt32(row("notifications_enabled")) <> 0
                settings.DefaultReminderDays = Convert.ToInt32(row("default_reminder_days"))
                settings.Currency = CStr(row("currency"))
            End If
            Return settings
        End Function

        ''' <summary>Saves the notification switch and default reminder (creates the row if it's missing).</summary>
        Public Shared Sub Save(settings As UserSettings)
            Dim days = If(settings.DefaultReminderDays = 1 OrElse settings.DefaultReminderDays = 7, settings.DefaultReminderDays, 3)
            Db.ExecuteNonQuery(
                "INSERT INTO user_settings (user_id, notifications_enabled, default_reminder_days) VALUES (@user, @enabled, @days) " &
                "ON DUPLICATE KEY UPDATE notifications_enabled = VALUES(notifications_enabled), default_reminder_days = VALUES(default_reminder_days)",
                Db.P("@user", settings.UserId), Db.P("@enabled", If(settings.NotificationsEnabled, 1, 0)), Db.P("@days", days))
        End Sub

    End Class

End Namespace
