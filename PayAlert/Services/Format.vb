Option Strict On
Option Explicit On

Imports System.Globalization

Namespace Services

    ''' <summary>Money and date display helpers (PLAN.md §6 Formatting).</summary>
    Public NotInheritable Class Format

        Private Shared ReadOnly PhCulture As CultureInfo = CreatePhCulture()

        Private Sub New()
        End Sub

        Private Shared Function CreatePhCulture() As CultureInfo
            Dim culture = CType(CultureInfo.GetCultureInfo("en-PH").Clone(), CultureInfo)
            ' Pin the symbol and pattern so the output is ₱1,500.00 on every Windows build.
            culture.NumberFormat.CurrencySymbol = "₱"
            culture.NumberFormat.CurrencyPositivePattern = 0   ' ₱n
            culture.NumberFormat.CurrencyNegativePattern = 1   ' -₱n
            culture.NumberFormat.CurrencyDecimalDigits = 2
            Return culture
        End Function

        ''' <summary>₱1,500.00</summary>
        Public Shared Function Money(amount As Decimal) As String
            Return amount.ToString("C2", PhCulture)
        End Function

        ''' <summary>Oct 15, 2026</summary>
        Public Shared Function ShortDate(value As Date) As String
            Return value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>"Due today", "Due tomorrow", "Due in 3 days", "1 day overdue", "4 days overdue".</summary>
        Public Shared Function DuePhrase(daysUntilDue As Integer) As String
            Select Case daysUntilDue
                Case 0 : Return "Due today"
                Case 1 : Return "Due tomorrow"
                Case -1 : Return "1 day overdue"
                Case Is > 1 : Return "Due in " & daysUntilDue & " days"
                Case Else : Return (-daysUntilDue) & " days overdue"
            End Select
        End Function

        ''' <summary>Short relative form for info rows: "today", "tomorrow", "in 3 days", "2 days ago".</summary>
        Public Shared Function RelativeDay(daysUntilDue As Integer) As String
            Select Case daysUntilDue
                Case 0 : Return "today"
                Case 1 : Return "tomorrow"
                Case -1 : Return "yesterday"
                Case Is > 1 : Return "in " & daysUntilDue & " days"
                Case Else : Return (-daysUntilDue) & " days ago"
            End Select
        End Function

        ''' <summary>"Just now", "10 min ago", "2 hr ago", "Yesterday", then the date.</summary>
        Public Shared Function Relative(value As DateTime, Optional now As DateTime? = Nothing) As String
            Dim current = If(now, DateTime.Now)
            Dim span = current - value
            If span.TotalMinutes < 1 Then Return "Just now"
            If span.TotalMinutes < 60 Then Return CInt(Math.Floor(span.TotalMinutes)) & " min ago"
            If value.Date = current.Date Then Return CInt(Math.Floor(span.TotalHours)) & " hr ago"
            If value.Date = current.Date.AddDays(-1) Then Return "Yesterday"
            Return ShortDate(value)
        End Function

    End Class

End Namespace
