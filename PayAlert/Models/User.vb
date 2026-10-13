Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>A PayAlert account (table users).</summary>
    Public NotInheritable Class User
        Public Property UserId As Integer
        Public Property FullName As String = ""
        Public Property Email As String = ""
        Public Property PasswordHash As String = ""
        Public Property Role As String = RoleUser
        Public Property IsActive As Boolean = True
        Public Property CreatedAt As DateTime

        Public Const RoleUser As String = "user"
        Public Const RoleAdmin As String = "admin"

        Public ReadOnly Property IsAdmin As Boolean
            Get
                Return String.Equals(Role, RoleAdmin, StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        ''' <summary>First word of the full name, for greetings.</summary>
        Public ReadOnly Property FirstName As String
            Get
                Dim trimmed = FullName.Trim()
                Dim space = trimmed.IndexOf(" "c)
                Return If(space > 0, trimmed.Substring(0, space), trimmed)
            End Get
        End Property
    End Class

End Namespace
