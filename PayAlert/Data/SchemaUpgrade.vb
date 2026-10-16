Option Strict On
Option Explicit On

Imports MySql.Data.MySqlClient

Namespace Data

    ''' <summary>
    ''' Brings a database made with the first release up to date (the revision after Phase 11), so nobody has to run
    ''' Database/04_revision.sql by hand. Every step checks information_schema first, so it is safe on every start.
    ''' </summary>
    Public NotInheritable Class SchemaUpgrade

        Private Sub New()
        End Sub

        ''' <summary>Table, column, and the statement that adds it. Same changes as 04_revision.sql.</summary>
        Private Shared ReadOnly Columns As String()() = {
            New String() {"user_settings", "large_text",
                          "ALTER TABLE user_settings ADD COLUMN large_text TINYINT(1) NOT NULL DEFAULT 0"}}

        ''' <summary>Adds whatever is missing. Throws on database errors; the caller shows a friendly message.</summary>
        Public Shared Sub Apply()
            Using conn = Db.OpenConnection()
                For Each change In Columns
                    If Not ColumnExists(conn, change(0), change(1)) Then Db.ExecuteNonQuery(conn, Nothing, change(2))
                Next
            End Using
        End Sub

        Private Shared Function ColumnExists(conn As MySqlConnection, table As String, column As String) As Boolean
            Dim count = Db.ExecuteScalar(conn, Nothing,
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column",
                Db.P("@table", table), Db.P("@column", column))
            Return Convert.ToInt64(count) > 0
        End Function

    End Class

End Namespace
