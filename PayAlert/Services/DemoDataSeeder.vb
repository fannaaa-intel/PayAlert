Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports MySql.Data.MySqlClient
Imports PayAlert.Data

Namespace Services

    Public NotInheritable Class SeedResult
        Public Property Seeded As Boolean
        Public Property Message As String = ""
        Public Property UsersCreated As Integer
        Public Property PaymentsCreated As Integer
        Public Property TransactionsCreated As Integer
    End Class

    ''' <summary>
    ''' Creates the demo accounts and payments the first time the app starts with an empty users table.
    ''' Due dates are relative to today, so every status (Upcoming, Partially Paid, Paid, Overdue) always shows.
    ''' </summary>
    Public NotInheritable Class DemoDataSeeder

        Public Const AdminEmail As String = "admin@payalert.com"
        Public Const AdminPassword As String = "Admin@123"
        Public Const DemoEmail As String = "demo@payalert.com"
        Public Const DemoPassword As String = "Demo@123"

        Private Sub New()
        End Sub

        Private NotInheritable Class DemoPayment
            Public Name As String
            Public Direction As String
            Public Category As String
            Public Person As String
            Public Total As Decimal
            Public DueOffsetDays As Integer
            Public PaidSoFar As Decimal
            Public PaidOffsetDays As Integer
            Public Method As String
            Public TxnNote As String
            Public Recurrence As String
            Public Notes As String
        End Class

        ''' <summary>The §8 demo set. Sample names are generic on purpose: no real companies or brands.</summary>
        Private Shared Function DemoPayments() As List(Of DemoPayment)
            Return New List(Of DemoPayment) From {
                New DemoPayment With {.Name = "BNPL Installment", .Direction = "payable", .Category = "Buy Now Pay Later", .Person = "Online Store",
                                      .Total = 1500D, .DueOffsetDays = 1, .Notes = "Installment 2 of 4 for the study desk."},
                New DemoPayment With {.Name = "Home Internet", .Direction = "payable", .Category = "Bills", .Person = "Internet Provider",
                                      .Total = 1699D, .DueOffsetDays = 3},
                New DemoPayment With {.Name = "Electricity Bill", .Direction = "payable", .Category = "Bills", .Person = "Power Utility",
                                      .Total = 2300D, .DueOffsetDays = 10},
                New DemoPayment With {.Name = "Cash Loan", .Direction = "payable", .Category = "Loan", .Person = "Lending App",
                                      .Total = 1000D, .DueOffsetDays = -4},
                New DemoPayment With {.Name = "Apartment Rent", .Direction = "payable", .Category = "Rent", .Person = "Landlord",
                                      .Total = 5000D, .DueOffsetDays = 6,
                                      .PaidSoFar = 2000D, .PaidOffsetDays = -5, .Method = "Bank Transfer", .TxnNote = "First half of the month's rent"},
                New DemoPayment With {.Name = "Water Bill", .Direction = "payable", .Category = "Bills", .Person = "Water Utility",
                                      .Total = 500D, .DueOffsetDays = -2,
                                      .PaidSoFar = 500D, .PaidOffsetDays = -3, .Method = "E-wallet", .TxnNote = "Paid in full"},
                New DemoPayment With {.Name = "Streaming Plan", .Direction = "payable", .Category = "Subscription", .Person = "Streaming Service",
                                      .Total = 549D, .DueOffsetDays = 12, .Recurrence = "monthly"},
                New DemoPayment With {.Name = "Loan to Maria", .Direction = "receivable", .Category = "Loan", .Person = "Maria Santos",
                                      .Total = 3000D, .DueOffsetDays = 5,
                                      .PaidSoFar = 1000D, .PaidOffsetDays = -7, .Method = "Cash", .TxnNote = "First partial payment",
                                      .Notes = "Borrowed for school fees. Agreed to pay back in parts."},
                New DemoPayment With {.Name = "Loan to Pedro", .Direction = "receivable", .Category = "Loan", .Person = "Pedro Reyes",
                                      .Total = 800D, .DueOffsetDays = -1}
            }
        End Function

        ''' <summary>
        ''' Seeds only when the users table is empty. Everything is written in one transaction,
        ''' so a failure leaves the database untouched. Throws on database errors; the caller shows a friendly message.
        ''' </summary>
        Public Shared Function SeedIfEmpty() As SeedResult
            Dim result As New SeedResult()

            If CountUsers() > 0 Then
                result.Seeded = False
                result.Message = "Demo data is already in place."
                Return result
            End If

            Dim categoryIds = LoadCategoryIds()
            Dim plan = DemoPayments()
            For Each item In plan
                If Not categoryIds.ContainsKey(item.Category) Then
                    Throw New InvalidOperationException("Category '" & item.Category & "' is missing. Run 03_seed.sql in phpMyAdmin.")
                End If
            Next

            Dim today = Date.Today

            Db.InTransaction(
                Sub(conn As MySqlConnection, tx As MySqlTransaction)
                    ' Re-check inside the transaction in case another copy of the app seeded first.
                    If CInt(Convert.ToInt64(Db.ExecuteScalar(conn, tx, "SELECT COUNT(*) FROM users"))) > 0 Then Return

                    CreateUser(conn, tx, "PayAlert Administrator", AdminEmail, AdminPassword, "admin")
                    Dim demoUserId = CreateUser(conn, tx, "Juan Dela Cruz", DemoEmail, DemoPassword, "user")
                    result.UsersCreated = 2

                    For Each item In plan
                        Dim paymentId = Db.Insert(conn, tx,
                            "INSERT INTO payments (user_id, name, direction, counterparty, category_id, total_amount, due_date, " &
                            "reminder_days, is_recurring, recurrence, notes) " &
                            "VALUES (@user, @name, @direction, @person, @category, @total, @due, @remind, @recurring, @recurrence, @notes)",
                            Db.P("@user", demoUserId),
                            Db.P("@name", item.Name),
                            Db.P("@direction", item.Direction),
                            Db.P("@person", item.Person),
                            Db.P("@category", categoryIds(item.Category)),
                            Db.P("@total", item.Total),
                            Db.P("@due", today.AddDays(item.DueOffsetDays)),
                            Db.P("@remind", 3),
                            Db.P("@recurring", If(item.Recurrence Is Nothing, 0, 1)),
                            Db.P("@recurrence", item.Recurrence),
                            Db.P("@notes", item.Notes))
                        result.PaymentsCreated += 1

                        If item.PaidSoFar > 0D Then
                            Db.ExecuteNonQuery(conn, tx,
                                "INSERT INTO payment_transactions (payment_id, amount, paid_on, method, note) " &
                                "VALUES (@payment, @amount, @paidOn, @method, @note)",
                                Db.P("@payment", paymentId),
                                Db.P("@amount", item.PaidSoFar),
                                Db.P("@paidOn", today.AddDays(item.PaidOffsetDays)),
                                Db.P("@method", item.Method),
                                Db.P("@note", item.TxnNote))
                            result.TransactionsCreated += 1
                        End If
                    Next
                End Sub)

            result.Seeded = result.UsersCreated > 0
            result.Message = If(result.Seeded,
                                "Demo data created: " & result.UsersCreated & " accounts, " & result.PaymentsCreated & " payments, " &
                                result.TransactionsCreated & " payment records.",
                                "Demo data is already in place.")
            Debug.WriteLine("[DemoDataSeeder] " & result.Message)
            Return result
        End Function

        Private Shared Function CountUsers() As Integer
            Return CInt(Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM users")))
        End Function

        Private Shared Function LoadCategoryIds() As Dictionary(Of String, Integer)
            Dim ids As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            For Each row As DataRow In Db.Query("SELECT category_id, name FROM categories").Rows
                ids(CStr(row("name"))) = Convert.ToInt32(row("category_id"))
            Next
            Return ids
        End Function

        ''' <summary>Creates a user plus the matching user_settings row (default settings).</summary>
        Private Shared Function CreateUser(conn As MySqlConnection, tx As MySqlTransaction,
                                           fullName As String, email As String, password As String, role As String) As Integer
            Return UserRepo.CreateUser(conn, tx, fullName, email, AuthService.HashPassword(password), role)
        End Function

    End Class

End Namespace
