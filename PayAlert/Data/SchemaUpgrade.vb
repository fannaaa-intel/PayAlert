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

        ''' <summary>One change: what to look for in information_schema, and the statement that adds it.</summary>
        Private NotInheritable Class Change
            Public Kind As String          ' "table", "column" or "constraint"
            Public Table As String
            Public Name As String
            Public Ddl As String
        End Class

        ''' <summary>The same changes as 04_revision.sql, in order.</summary>
        Private Shared ReadOnly Changes As Change() = {
            New Change With {.Kind = "column", .Table = "user_settings", .Name = "large_text",
                             .Ddl = "ALTER TABLE user_settings ADD COLUMN large_text TINYINT(1) NOT NULL DEFAULT 0"},
            New Change With {.Kind = "table", .Table = "payment_proofs", .Name = "payment_proofs",
                             .Ddl = "CREATE TABLE payment_proofs (" &
                                    "proof_id INT NOT NULL AUTO_INCREMENT, " &
                                    "user_id INT NOT NULL, " &
                                    "file_name VARCHAR(255) NOT NULL, " &
                                    "content_type VARCHAR(50) NOT NULL, " &
                                    "data MEDIUMBLOB NOT NULL, " &
                                    "created_at DATETIME DEFAULT CURRENT_TIMESTAMP, " &
                                    "PRIMARY KEY (proof_id), " &
                                    "CONSTRAINT fk_proof_user FOREIGN KEY (user_id) REFERENCES users (user_id)" &
                                    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"},
            New Change With {.Kind = "column", .Table = "payment_transactions", .Name = "reference_no",
                             .Ddl = "ALTER TABLE payment_transactions ADD COLUMN reference_no VARCHAR(50) NULL AFTER method"},
            New Change With {.Kind = "column", .Table = "payment_transactions", .Name = "payer_name",
                             .Ddl = "ALTER TABLE payment_transactions ADD COLUMN payer_name VARCHAR(100) NULL AFTER reference_no"},
            New Change With {.Kind = "column", .Table = "payment_transactions", .Name = "proof_id",
                             .Ddl = "ALTER TABLE payment_transactions ADD COLUMN proof_id INT NULL AFTER payer_name"},
            New Change With {.Kind = "constraint", .Table = "payment_transactions", .Name = "fk_txn_proof",
                             .Ddl = "ALTER TABLE payment_transactions ADD CONSTRAINT fk_txn_proof FOREIGN KEY (proof_id) REFERENCES payment_proofs (proof_id)"}}

        ''' <summary>Adds whatever is missing. Throws on database errors; the caller shows a friendly message.</summary>
        Public Shared Sub Apply()
            Using conn = Db.OpenConnection()
                For Each item In Changes
                    If Not Exists(conn, item) Then Db.ExecuteNonQuery(conn, Nothing, item.Ddl)
                Next
            End Using
        End Sub

        Private Shared Function Exists(conn As MySqlConnection, item As Change) As Boolean
            Dim sql As String
            Select Case item.Kind
                Case "table"
                    sql = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table"
                Case "constraint"
                    sql = "SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() " &
                          "AND TABLE_NAME = @table AND CONSTRAINT_NAME = @name"
                Case Else
                    sql = "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @name"
            End Select
            Return Convert.ToInt64(Db.ExecuteScalar(conn, Nothing, sql, Db.P("@table", item.Table), Db.P("@name", item.Name))) > 0
        End Function

    End Class

End Namespace
