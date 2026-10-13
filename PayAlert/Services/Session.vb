Option Strict On
Option Explicit On

Imports PayAlert.Models

Namespace Services

    ''' <summary>The signed-in user for this run of the app.</summary>
    Public NotInheritable Class Session

        Private Shared _currentUser As User

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property CurrentUser As User
            Get
                Return _currentUser
            End Get
        End Property

        Public Shared ReadOnly Property IsSignedIn As Boolean
            Get
                Return _currentUser IsNot Nothing
            End Get
        End Property

        Public Shared ReadOnly Property IsAdmin As Boolean
            Get
                Return _currentUser IsNot Nothing AndAlso _currentUser.IsAdmin
            End Get
        End Property

        Public Shared Sub SignIn(user As User)
            If user Is Nothing Then Throw New ArgumentNullException(NameOf(user))
            ' The hash is never needed after login; don't keep it in memory.
            user.PasswordHash = ""
            _currentUser = user
        End Sub

        Public Shared Sub SignOut()
            _currentUser = Nothing
        End Sub

    End Class

End Namespace
