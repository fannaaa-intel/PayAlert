Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports PayAlert.Data

Namespace Services

    ''' <summary>One demo payment as the view reports it, next to the status PLAN.md §8 expects.</summary>
    Public NotInheritable Class SetupCheckRow
        Public Property Name As String = ""
        Public Property Direction As String = ""
        Public Property Total As Decimal
        Public Property Balance As Decimal
        Public Property DueDate As Date
        Public Property Status As String = ""
        Public Property ExpectedStatus As String = ""

        Public ReadOnly Property MatchesExpected As Boolean
            Get
                Return ExpectedStatus.Length = 0 OrElse String.Equals(Status, ExpectedStatus, StringComparison.Ordinal)
            End Get
        End Property
    End Class

    Public NotInheritable Class SetupReport
        Public Property Connection As DbCheckResult
        Public Property Seed As SeedResult
        Public Property SeedError As String = ""
        Public Property UserCount As Integer
        Public Property CategoryCount As Integer
        Public Property Payments As New List(Of SetupCheckRow)

        Public ReadOnly Property AllPassed As Boolean
            Get
                Return Connection IsNot Nothing AndAlso Connection.Success AndAlso SeedError.Length = 0 AndAlso
                       CategoryCount >= 7 AndAlso Payments.Count > 0 AndAlso Payments.TrueForAll(Function(r) r.MatchesExpected)
            End Get
        End Property
    End Class

    ''' <summary>
    ''' TEMPORARY (Phase 1 only): runs the startup checks behind frmSetupCheck.
    ''' Removed in Phase 3, when frmLogin becomes the startup form and seeding moves into normal startup.
    ''' </summary>
    Public NotInheritable Class SetupCheck

        Private Sub New()
        End Sub

        Private Shared ReadOnly ExpectedStatuses As New Dictionary(Of String, String) From {
            {"BNPL Installment", "Upcoming"},
            {"Home Internet", "Upcoming"},
            {"Electricity Bill", "Upcoming"},
            {"Cash Loan", "Overdue"},
            {"Apartment Rent", "Partially Paid"},
            {"Water Bill", "Paid"},
            {"Streaming Plan", "Upcoming"},
            {"Loan to Maria", "Partially Paid"},
            {"Loan to Pedro", "Overdue"}
        }

        Public Shared Function Run() As SetupReport
            Dim report As New SetupReport With {.Connection = Db.TestConnection()}
            If Not report.Connection.Success Then Return report

            Try
                report.Seed = DemoDataSeeder.SeedIfEmpty()
            Catch ex As InvalidOperationException
                report.SeedError = ex.Message
                Debug.WriteLine("[SetupCheck] " & ex.ToString())
            Catch ex As Exception
                report.SeedError = Db.FriendlyMessageFor(ex)
                Debug.WriteLine("[SetupCheck] " & ex.ToString())
            End Try

            Try
                report.UserCount = CInt(Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM users")))
                report.CategoryCount = CInt(Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM categories")))

                Dim rows = Db.Query(
                    "SELECT v.name, v.direction, v.total_amount, v.balance, v.due_date, v.status " &
                    "FROM v_payment_summary v JOIN users u ON u.user_id = v.user_id " &
                    "WHERE u.email = @email ORDER BY v.due_date, v.name",
                    Db.P("@email", DemoDataSeeder.DemoEmail))

                For Each row As DataRow In rows.Rows
                    Dim name = CStr(row("name"))
                    Dim expected As String = Nothing
                    ExpectedStatuses.TryGetValue(name, expected)
                    report.Payments.Add(New SetupCheckRow With {
                        .Name = name,
                        .Direction = If(CStr(row("direction")) = "receivable", "Owed to Me", "I Owe"),
                        .Total = Convert.ToDecimal(row("total_amount")),
                        .Balance = Convert.ToDecimal(row("balance")),
                        .DueDate = Convert.ToDateTime(row("due_date")),
                        .Status = CStr(row("status")),
                        .ExpectedStatus = If(expected, "")})
                Next
            Catch ex As Exception
                If report.SeedError.Length = 0 Then report.SeedError = Db.FriendlyMessageFor(ex)
                Debug.WriteLine("[SetupCheck] " & ex.ToString())
            End Try

            Return report
        End Function

    End Class

End Namespace
