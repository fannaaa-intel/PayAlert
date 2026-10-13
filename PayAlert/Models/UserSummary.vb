Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>One account as the admin sees it: the user plus how many payments they track.</summary>
    Public NotInheritable Class UserSummary
        Public Property UserId As Integer
        Public Property FullName As String = ""
        Public Property Email As String = ""
        Public Property Role As String = User.RoleUser
        Public Property IsActive As Boolean = True
        Public Property CreatedAt As DateTime
        Public Property PaymentCount As Integer

        Public ReadOnly Property IsAdmin As Boolean
            Get
                Return String.Equals(Role, User.RoleAdmin, StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        Public ReadOnly Property RoleLabel As String
            Get
                Return If(IsAdmin, "Admin", "User")
            End Get
        End Property

        Public ReadOnly Property StatusLabel As String
            Get
                Return If(IsActive, "Active", "Deactivated")
            End Get
        End Property
    End Class

    ''' <summary>The admin dashboard numbers (§7 ucAdminDashboard).</summary>
    Public NotInheritable Class AdminStats
        Public Property TotalUsers As Integer
        Public Property ActiveUsers As Integer
        Public Property TotalPayments As Integer
        Public Property OverduePayments As Integer
    End Class

End Namespace
