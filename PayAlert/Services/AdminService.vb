Option Strict On
Option Explicit On

Imports System.Security.Cryptography
Imports System.Text
Imports PayAlert.Data

Namespace Services

    ''' <summary>
    ''' Admin actions (§7 ucAdminUsers). Each one checks that the signed-in user is an administrator, and none can be
    ''' used on the admin's own account (they'd lock themselves out; their password changes in Settings instead).
    ''' </summary>
    Public NotInheritable Class AdminService

        ''' <summary>Letters and digits that can't be mistaken for each other (no 0/O, 1/l/I).</summary>
        Private Const PasswordAlphabet As String = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789"

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property IsAdmin As Boolean
            Get
                Return Session.IsAdmin
            End Get
        End Property

        ''' <summary>True when the action may be used on this account (it isn't the signed-in admin's own).</summary>
        Public Shared Function CanManage(userId As Integer) As Boolean
            Return Session.IsAdmin AndAlso Session.CurrentUser.UserId <> userId
        End Function

        ''' <summary>Activates or deactivates another account. Returns False if that isn't allowed or the account is gone.</summary>
        Public Shared Function SetActive(userId As Integer, active As Boolean) As Boolean
            If Not CanManage(userId) Then Return False
            Return AdminRepo.SetActive(userId, active)
        End Function

        ''' <summary>
        ''' Gives another account a new temporary password and returns it, so it can be shown once.
        ''' Returns Nothing if that isn't allowed.
        ''' </summary>
        Public Shared Function ResetPassword(userId As Integer) As String
            If Not CanManage(userId) Then Return Nothing
            Dim temporary = GenerateTemporaryPassword()
            UserRepo.UpdatePasswordHash(userId, AuthService.HashPassword(temporary))
            Return temporary
        End Function

        ''' <summary>A random, easy-to-read temporary password like "Kp7m-Xw3q" (9 characters with the dash).</summary>
        Public Shared Function GenerateTemporaryPassword() As String
            Dim bytes(7) As Byte
            Using rng = RandomNumberGenerator.Create()
                Dim builder As New StringBuilder()
                ' Rejection sampling keeps every character equally likely.
                While builder.Length < 8
                    rng.GetBytes(bytes)
                    For Each b In bytes
                        If b < 256 - (256 Mod PasswordAlphabet.Length) AndAlso builder.Length < 8 Then
                            builder.Append(PasswordAlphabet(b Mod PasswordAlphabet.Length))
                        End If
                    Next
                End While
                Return builder.ToString(0, 4) & "-" & builder.ToString(4, 4)
            End Using
        End Function

    End Class

End Namespace
