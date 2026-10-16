Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>The user's preferences (table user_settings).</summary>
    Public NotInheritable Class UserSettings
        Public Property UserId As Integer
        Public Property NotificationsEnabled As Boolean = True
        ''' <summary>1, 3 or 7.</summary>
        Public Property DefaultReminderDays As Integer = 3
        Public Property Currency As String = "PHP"
        ''' <summary>Text size: False = Normal, True = Large (easier to read).</summary>
        Public Property LargeText As Boolean
    End Class

End Namespace
