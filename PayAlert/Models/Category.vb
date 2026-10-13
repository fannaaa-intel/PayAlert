Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>A payment category (table categories).</summary>
    Public NotInheritable Class Category
        Public Property CategoryId As Integer
        Public Property Name As String = ""

        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

End Namespace
