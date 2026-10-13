Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports PayAlert.Models

Namespace Data

    Public NotInheritable Class CategoryRepo

        Private Sub New()
        End Sub

        ''' <summary>All categories in their seeded order, with "Others" last.</summary>
        Public Shared Function GetAll() As List(Of Category)
            Dim list As New List(Of Category)
            Dim table = Db.Query("SELECT category_id, name FROM categories ORDER BY (name = @others), category_id",
                                 Db.P("@others", "Others"))
            For Each row As DataRow In table.Rows
                list.Add(New Category With {.CategoryId = Convert.ToInt32(row("category_id")), .Name = CStr(row("name"))})
            Next
            Return list
        End Function

    End Class

End Namespace
