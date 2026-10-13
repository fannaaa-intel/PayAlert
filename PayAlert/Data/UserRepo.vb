Option Strict On
Option Explicit On

Imports System.Data
Imports MySql.Data.MySqlClient
Imports PayAlert.Models

Namespace Data

    ''' <summary>Reads and writes the users table (and the matching user_settings row).</summary>
    Public NotInheritable Class UserRepo

        Private Const ErrDuplicateKey As Integer = 1062

        Private Const SelectColumns As String =
            "SELECT user_id, full_name, email, password_hash, role, is_active, created_at FROM users "

        Private Sub New()
        End Sub

        ''' <summary>Finds a user by email (case-insensitive through the column collation), or Nothing.</summary>
        Public Shared Function FindByEmail(email As String) As User
            Dim table = Db.Query(SelectColumns & "WHERE email = @email LIMIT 1", Db.P("@email", email.Trim()))
            Return If(table.Rows.Count = 0, Nothing, Map(table.Rows(0)))
        End Function

        Public Shared Function FindById(userId As Integer) As User
            Dim table = Db.Query(SelectColumns & "WHERE user_id = @id LIMIT 1", Db.P("@id", userId))
            Return If(table.Rows.Count = 0, Nothing, Map(table.Rows(0)))
        End Function

        ''' <summary>True when another account (not <paramref name="exceptUserId"/>) already uses this email.</summary>
        Public Shared Function EmailTakenByOther(email As String, exceptUserId As Integer) As Boolean
            Return Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM users WHERE email = @email AND user_id <> @id",
                                                    Db.P("@email", email.Trim()), Db.P("@id", exceptUserId))) > 0
        End Function

        ''' <summary>Saves name and email. Returns False if the email was taken in the meantime.</summary>
        Public Shared Function UpdateProfile(userId As Integer, fullName As String, email As String) As Boolean
            Try
                Db.ExecuteNonQuery("UPDATE users SET full_name = @name, email = @email WHERE user_id = @id",
                                   Db.P("@name", fullName.Trim()), Db.P("@email", email.Trim()), Db.P("@id", userId))
                Return True
            Catch ex As MySqlException When ex.Number = ErrDuplicateKey
                Return False
            End Try
        End Function

        Public Shared Sub UpdatePasswordHash(userId As Integer, passwordHash As String)
            Db.ExecuteNonQuery("UPDATE users SET password_hash = @hash WHERE user_id = @id", Db.P("@hash", passwordHash), Db.P("@id", userId))
        End Sub

        Public Shared Function EmailExists(email As String) As Boolean
            Return Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM users WHERE email = @email", Db.P("@email", email.Trim()))) > 0
        End Function

        ''' <summary>
        ''' Creates a regular user plus their default settings row in one transaction.
        ''' Returns the new id, or 0 if the email was taken in the meantime.
        ''' </summary>
        Public Shared Function Create(fullName As String, email As String, passwordHash As String) As Integer
            Dim newId = 0
            Try
                Db.InTransaction(
                    Sub(conn As MySqlConnection, tx As MySqlTransaction)
                        newId = CreateUser(conn, tx, fullName, email, passwordHash, User.RoleUser)
                    End Sub)
            Catch ex As MySqlException When ex.Number = ErrDuplicateKey
                Return 0
            End Try
            Return newId
        End Function

        ''' <summary>Inserts a user and their user_settings row inside an existing transaction.</summary>
        Public Shared Function CreateUser(conn As MySqlConnection, tx As MySqlTransaction, fullName As String, email As String,
                                          passwordHash As String, role As String) As Integer
            Dim userId = Db.Insert(conn, tx,
                "INSERT INTO users (full_name, email, password_hash, role) VALUES (@name, @email, @hash, @role)",
                Db.P("@name", fullName.Trim()),
                Db.P("@email", email.Trim()),
                Db.P("@hash", passwordHash),
                Db.P("@role", role))
            Db.ExecuteNonQuery(conn, tx, "INSERT INTO user_settings (user_id) VALUES (@user)", Db.P("@user", userId))
            Return userId
        End Function

        Private Shared Function Map(row As DataRow) As User
            Return New User With {
                .UserId = Convert.ToInt32(row("user_id")),
                .FullName = CStr(row("full_name")),
                .Email = CStr(row("email")),
                .PasswordHash = CStr(row("password_hash")),
                .Role = CStr(row("role")),
                .IsActive = Convert.ToInt32(row("is_active")) = 1,
                .CreatedAt = Convert.ToDateTime(row("created_at"))}
        End Function

    End Class

End Namespace
