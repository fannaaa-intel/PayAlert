Option Strict On
Option Explicit On

Imports MySql.Data.MySqlClient

Namespace Data

    ''' <summary>A stored proof of payment: the picture and the name of the file it came from.</summary>
    Public NotInheritable Class ProofFile
        Public Property ProofId As Integer
        Public Property FileName As String = ""
        Public Property ContentType As String = ""
        Public Property Data As Byte()
    End Class

    ''' <summary>
    ''' Proof-of-payment pictures (payment_proofs). Stored in the database so they move with it; Services.ProofImage
    ''' keeps each one small enough for XAMPP's 1 MB packet limit. Reads check the owner, like every other query.
    ''' </summary>
    Public NotInheritable Class ProofRepo

        Private Sub New()
        End Sub

        ''' <summary>Saves a picture inside the caller's transaction and returns its new id.</summary>
        Public Shared Function Insert(conn As MySqlConnection, tx As MySqlTransaction, userId As Integer, fileName As String,
                                      contentType As String, data As Byte()) As Integer
            Return Db.Insert(conn, tx,
                "INSERT INTO payment_proofs (user_id, file_name, content_type, data) VALUES (@user, @name, @type, @data)",
                Db.P("@user", userId), Db.P("@name", fileName), Db.P("@type", contentType), Db.P("@data", data))
        End Function

        ''' <summary>The picture, or Nothing if it doesn't exist or isn't the user's.</summary>
        Public Shared Function [Get](proofId As Integer, userId As Integer) As ProofFile
            Dim table = Db.Query("SELECT proof_id, file_name, content_type, data FROM payment_proofs WHERE proof_id = @id AND user_id = @user",
                                 Db.P("@id", proofId), Db.P("@user", userId))
            If table.Rows.Count = 0 Then Return Nothing
            Dim row = table.Rows(0)
            Return New ProofFile With {
                .ProofId = Convert.ToInt32(row("proof_id")),
                .FileName = CStr(row("file_name")),
                .ContentType = CStr(row("content_type")),
                .Data = DirectCast(row("data"), Byte())}
        End Function

    End Class

End Namespace
