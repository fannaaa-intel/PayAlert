Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports PayAlert.Models

Namespace Data

    ''' <summary>
    ''' Reads and changes across all accounts, for administrators only (AdminService checks the caller's role).
    ''' Payment counts come from v_payment_summary, so deleted payments never count.
    ''' </summary>
    Public NotInheritable Class AdminRepo

        Private Sub New()
        End Sub

        Public Shared Function GetStats() As AdminStats
            ' Overdue is counted by its rule (money left and the due date passed), not by comparing the view's status
            ' text: that text's collation can differ from the connection's on some servers ("Illegal mix of collations").
            Dim table = Db.Query(
                "SELECT (SELECT COUNT(*) FROM users) AS total_users, " &
                "(SELECT COUNT(*) FROM users WHERE is_active = 1) AS active_users, " &
                "(SELECT COUNT(*) FROM v_payment_summary) AS total_payments, " &
                "(SELECT COUNT(*) FROM v_payment_summary WHERE balance > 0 AND days_until_due < 0) AS overdue_payments")
            Dim row = table.Rows(0)
            Return New AdminStats With {
                .TotalUsers = Convert.ToInt32(row("total_users")),
                .ActiveUsers = Convert.ToInt32(row("active_users")),
                .TotalPayments = Convert.ToInt32(row("total_payments")),
                .OverduePayments = Convert.ToInt32(row("overdue_payments"))}
        End Function

        ''' <summary>Every account, newest first, with its payment count.</summary>
        Public Shared Function GetUsers() As List(Of UserSummary)
            Dim table = Db.Query(
                "SELECT u.user_id, u.full_name, u.email, u.role, u.is_active, u.created_at, " &
                "(SELECT COUNT(*) FROM v_payment_summary v WHERE v.user_id = u.user_id) AS payment_count " &
                "FROM users u ORDER BY u.created_at DESC, u.user_id DESC")
            Dim list As New List(Of UserSummary)(table.Rows.Count)
            For Each row As DataRow In table.Rows
                list.Add(New UserSummary With {
                    .UserId = Convert.ToInt32(row("user_id")),
                    .FullName = CStr(row("full_name")),
                    .Email = CStr(row("email")),
                    .Role = CStr(row("role")),
                    .IsActive = Convert.ToInt32(row("is_active")) <> 0,
                    .CreatedAt = Convert.ToDateTime(row("created_at")),
                    .PaymentCount = Convert.ToInt32(row("payment_count"))})
            Next
            Return list
        End Function

        ''' <summary>Activates or deactivates an account. Returns False if it doesn't exist.</summary>
        Public Shared Function SetActive(userId As Integer, active As Boolean) As Boolean
            Return Db.ExecuteNonQuery("UPDATE users SET is_active = @active WHERE user_id = @id",
                                      Db.P("@active", If(active, 1, 0)), Db.P("@id", userId)) > 0
        End Function

    End Class

End Namespace
