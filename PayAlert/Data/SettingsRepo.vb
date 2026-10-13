Option Strict On
Option Explicit On

Imports PayAlert.Models

Namespace Data

    ''' <summary>The user's preferences (user_settings). Phase 9 adds saving.</summary>
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

    End Class

End Namespace
