Option Strict On
Option Explicit On

Imports System.Configuration
Imports System.Data
Imports MySql.Data.MySqlClient

Namespace Data

    ''' <summary>Result of <see cref="Db.TestConnection"/>: a friendly message for the UI plus technical detail for Debug.</summary>
    Public NotInheritable Class DbCheckResult
        Public Property Success As Boolean
        Public Property FriendlyMessage As String = ""
        Public Property ServerVersion As String = ""
        Public Property Detail As String = ""
    End Class

    ''' <summary>
    ''' Connection factory and small parameterized-query helpers.
    ''' Every SQL statement in the app goes through here with MySqlParameter values; never concatenate SQL.
    ''' </summary>
    Public NotInheritable Class Db

        Private Const ConnectionName As String = "PayAlertDb"

        ' MySQL / MariaDB server error numbers we translate into friendly messages.
        Private Const ErrAccessDenied As Integer = 1045
        Private Const ErrUnknownDatabase As Integer = 1049
        Private Const ErrUnknownTable As Integer = 1146
        Private Const ErrCannotConnect As Integer = 1042   ' "Unable to connect to any of the specified hosts"

        Public Const MsgCannotConnect As String = "Can't reach the database. Make sure MySQL is running in XAMPP."
        Public Const MsgNoDatabase As String = "The PayAlert database wasn't found. Run the SQL files in the Database folder in phpMyAdmin first."
        Public Const MsgMissingTables As String = "The PayAlert database is missing some tables. Run 01_schema.sql, 02_views.sql and 03_seed.sql in phpMyAdmin."
        Public Const MsgAccessDenied As String = "MySQL refused the login. Check the user name and password in App.config."
        Public Const MsgUnexpected As String = "Something went wrong while talking to the database. Please try again."

        Private Shared _connectionString As String

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property ConnectionString As String
            Get
                If _connectionString Is Nothing Then
                    Dim setting = ConfigurationManager.ConnectionStrings(ConnectionName)
                    If setting Is Nothing OrElse String.IsNullOrWhiteSpace(setting.ConnectionString) Then
                        Throw New ConfigurationErrorsException("Connection string '" & ConnectionName & "' is missing from App.config.")
                    End If
                    _connectionString = setting.ConnectionString
                End If
                Return _connectionString
            End Get
        End Property

        ''' <summary>Returns an open connection. The caller disposes it with a Using block.</summary>
        Public Shared Function OpenConnection() As MySqlConnection
            Dim conn As New MySqlConnection(ConnectionString)
            Try
                conn.Open()
                Return conn
            Catch
                conn.Dispose()
                Throw
            End Try
        End Function

        ''' <summary>Creates a named parameter. Usage: Db.P("@id", 5)</summary>
        Public Shared Function P(name As String, value As Object) As MySqlParameter
            Return New MySqlParameter(name, If(value, DBNull.Value))
        End Function

        Private Shared Function CreateCommand(conn As MySqlConnection, tx As MySqlTransaction, sql As String,
                                              params As MySqlParameter()) As MySqlCommand
            Dim cmd As New MySqlCommand(sql, conn, tx)
            If params IsNot Nothing Then cmd.Parameters.AddRange(params)
            Return cmd
        End Function

        ' ---------- one-shot helpers (open and close their own connection) ----------

        Public Shared Function ExecuteNonQuery(sql As String, ParamArray params As MySqlParameter()) As Integer
            Using conn = OpenConnection()
                Return ExecuteNonQuery(conn, Nothing, sql, params)
            End Using
        End Function

        Public Shared Function ExecuteScalar(sql As String, ParamArray params As MySqlParameter()) As Object
            Using conn = OpenConnection()
                Return ExecuteScalar(conn, Nothing, sql, params)
            End Using
        End Function

        Public Shared Function Query(sql As String, ParamArray params As MySqlParameter()) As DataTable
            Using conn = OpenConnection()
                Return Query(conn, Nothing, sql, params)
            End Using
        End Function

        ' ---------- helpers that share a connection / transaction ----------

        Public Shared Function ExecuteNonQuery(conn As MySqlConnection, tx As MySqlTransaction, sql As String,
                                               ParamArray params As MySqlParameter()) As Integer
            Using cmd = CreateCommand(conn, tx, sql, params)
                Return cmd.ExecuteNonQuery()
            End Using
        End Function

        Public Shared Function ExecuteScalar(conn As MySqlConnection, tx As MySqlTransaction, sql As String,
                                             ParamArray params As MySqlParameter()) As Object
            Using cmd = CreateCommand(conn, tx, sql, params)
                Return cmd.ExecuteScalar()
            End Using
        End Function

        ''' <summary>Runs an INSERT and returns the new AUTO_INCREMENT id.</summary>
        Public Shared Function Insert(conn As MySqlConnection, tx As MySqlTransaction, sql As String,
                                      ParamArray params As MySqlParameter()) As Integer
            Using cmd = CreateCommand(conn, tx, sql, params)
                cmd.ExecuteNonQuery()
                Return CInt(cmd.LastInsertedId)
            End Using
        End Function

        Public Shared Function Query(conn As MySqlConnection, tx As MySqlTransaction, sql As String,
                                     ParamArray params As MySqlParameter()) As DataTable
            Using cmd = CreateCommand(conn, tx, sql, params)
                Using reader = cmd.ExecuteReader()
                    Dim table As New DataTable()
                    table.Load(reader)
                    Return table
                End Using
            End Using
        End Function

        ''' <summary>Runs work inside one transaction; commits on success, rolls back and rethrows on failure.</summary>
        Public Shared Sub InTransaction(work As Action(Of MySqlConnection, MySqlTransaction))
            Using conn = OpenConnection()
                Using tx = conn.BeginTransaction()
                    Try
                        work(conn, tx)
                        tx.Commit()
                    Catch
                        tx.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        End Sub

        ' ---------- connection test ----------

        ''' <summary>
        ''' Opens a connection and checks that the PayAlert tables and view exist.
        ''' Never throws: failures come back as a friendly message.
        ''' </summary>
        Public Shared Function TestConnection() As DbCheckResult
            Dim result As New DbCheckResult()
            Try
                Using conn = OpenConnection()
                    ' MariaDB reports itself as "5.5.5-10.4.32-MariaDB" for old-client compatibility; drop that prefix.
                    Dim version = conn.ServerVersion
                    If version.StartsWith("5.5.5-", StringComparison.Ordinal) Then version = version.Substring(6)
                    result.ServerVersion = version
                    ' Touch every table and the view so a half-run setup is caught here, not on some later screen.
                    For Each check In {"SELECT 1 FROM users LIMIT 1",
                                       "SELECT 1 FROM user_settings LIMIT 1",
                                       "SELECT 1 FROM categories LIMIT 1",
                                       "SELECT 1 FROM payments LIMIT 1",
                                       "SELECT 1 FROM payment_transactions LIMIT 1",
                                       "SELECT 1 FROM notifications LIMIT 1",
                                       "SELECT 1 FROM v_payment_summary LIMIT 1"}
                        ExecuteScalar(conn, Nothing, check)
                    Next
                End Using
                result.Success = True
                result.FriendlyMessage = "Connected to payalert_db."
            Catch ex As Exception
                result.Success = False
                result.FriendlyMessage = FriendlyMessageFor(ex)
                result.Detail = ex.ToString()
                Debug.WriteLine("[Db.TestConnection] " & ex.ToString())
            End Try
            Return result
        End Function

        ''' <summary>Maps a database exception to a message that is safe to show the user.</summary>
        Public Shared Function FriendlyMessageFor(ex As Exception) As String
            Dim mysqlEx = TryCast(ex, MySqlException)
            If mysqlEx Is Nothing AndAlso ex IsNot Nothing Then mysqlEx = TryCast(ex.InnerException, MySqlException)
            If mysqlEx Is Nothing Then
                If TypeOf ex Is ConfigurationErrorsException Then Return "The database settings in App.config are missing or invalid."
                Return MsgUnexpected
            End If

            Select Case mysqlEx.Number
                Case ErrUnknownDatabase : Return MsgNoDatabase
                Case ErrUnknownTable : Return MsgMissingTables
                Case ErrAccessDenied : Return MsgAccessDenied
                Case ErrCannotConnect, 0
                    ' The connector sometimes wraps the server's login error in a generic one; look inside.
                    Dim inner = If(mysqlEx.InnerException?.Message, "")
                    If inner.IndexOf("Unknown database", StringComparison.OrdinalIgnoreCase) >= 0 Then Return MsgNoDatabase
                    If inner.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0 Then Return MsgAccessDenied
                    Return MsgCannotConnect
                Case Else
                    Return MsgUnexpected
            End Select
        End Function

    End Class

End Namespace
