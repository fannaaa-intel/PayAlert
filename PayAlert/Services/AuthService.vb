Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports PayAlert.Data
Imports PayAlert.Models

Namespace Services

    Public Enum LoginOutcome
        Success
        WrongCredentials
        Deactivated
        DatabaseError
    End Enum

    Public NotInheritable Class LoginResult
        Public Property Outcome As LoginOutcome
        Public Property User As User
        ''' <summary>Friendly text for DatabaseError.</summary>
        Public Property Message As String = ""
    End Class

    Public Enum RegisterField
        FullName
        Email
        Password
        ConfirmPassword
    End Enum

    Public NotInheritable Class RegisterResult
        Public Property Success As Boolean
        Public Property Email As String = ""
        ''' <summary>Inline validation messages, one per field that needs fixing.</summary>
        Public ReadOnly Property Errors As New Dictionary(Of RegisterField, String)
        ''' <summary>Friendly text when the database couldn't be reached.</summary>
        Public Property DatabaseMessage As String = ""
    End Class

    Public Enum AccountField
        FullName
        Email
        CurrentPassword
        NewPassword
        ConfirmPassword
    End Enum

    ''' <summary>Outcome of a profile or password change: inline messages per field, or a database message.</summary>
    Public NotInheritable Class AccountResult
        Public Property Success As Boolean
        Public ReadOnly Property Errors As New Dictionary(Of AccountField, String)
        Public Property DatabaseMessage As String = ""
    End Class

    ''' <summary>Login, registration and password hashing (BCrypt).</summary>
    Public NotInheritable Class AuthService

        Public Const MinPasswordLength As Integer = 8
        Public Const MaxNameLength As Integer = 100
        Public Const MaxEmailLength As Integer = 150
        Private Const BcryptWorkFactor As Integer = 11

        Public Const MsgWrongCredentials As String = "Wrong email or password."
        Public Const MsgDeactivated As String = "This account is deactivated."

        Private Shared ReadOnly EmailPattern As New Regex("^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled)

        Private Sub New()
        End Sub

        Public Shared Function HashPassword(password As String) As String
            Return BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor)
        End Function

        Public Shared Function VerifyPassword(password As String, hash As String) As Boolean
            If String.IsNullOrEmpty(hash) Then Return False
            Try
                Return BCrypt.Net.BCrypt.Verify(password, hash)
            Catch ex As BCrypt.Net.SaltParseException
                Debug.WriteLine("[AuthService] Stored hash is not a valid BCrypt hash: " & ex.Message)
                Return False
            End Try
        End Function

        Public Shared Function IsValidEmail(email As String) As Boolean
            Return email IsNot Nothing AndAlso email.Length <= MaxEmailLength AndAlso EmailPattern.IsMatch(email.Trim())
        End Function

        ''' <summary>
        ''' Checks an email and password. Unknown email and wrong password give the same answer;
        ''' the deactivated message only shows once the password is right. Never throws.
        ''' </summary>
        Public Shared Function Login(email As String, password As String) As LoginResult
            Try
                Dim user = UserRepo.FindByEmail(If(email, ""))
                If user Is Nothing OrElse Not VerifyPassword(If(password, ""), user.PasswordHash) Then
                    Return New LoginResult With {.Outcome = LoginOutcome.WrongCredentials}
                End If
                If Not user.IsActive Then Return New LoginResult With {.Outcome = LoginOutcome.Deactivated}
                Return New LoginResult With {.Outcome = LoginOutcome.Success, .User = user}
            Catch ex As Exception
                Debug.WriteLine("[AuthService.Login] " & ex.ToString())
                Return New LoginResult With {.Outcome = LoginOutcome.DatabaseError, .Message = Db.FriendlyMessageFor(ex)}
            End Try
        End Function

        ''' <summary>
        ''' Validates the form and, when everything is fine, creates the account and its settings row.
        ''' Never throws.
        ''' </summary>
        Public Shared Function Register(fullName As String, email As String, password As String, confirmPassword As String) As RegisterResult
            Dim result As New RegisterResult()
            Dim name = If(fullName, "").Trim()
            Dim mail = If(email, "").Trim()
            Dim pass = If(password, "")
            Dim confirm = If(confirmPassword, "")
            result.Email = mail

            If name.Length = 0 Then
                result.Errors(RegisterField.FullName) = "Enter your full name."
            ElseIf name.Length > MaxNameLength Then
                result.Errors(RegisterField.FullName) = "Keep your name under " & MaxNameLength & " characters."
            End If

            If mail.Length = 0 Then
                result.Errors(RegisterField.Email) = "Enter your email."
            ElseIf Not IsValidEmail(mail) Then
                result.Errors(RegisterField.Email) = "Enter a valid email, like name@example.com."
            End If

            If pass.Length = 0 Then
                result.Errors(RegisterField.Password) = "Enter a password."
            ElseIf pass.Length < MinPasswordLength Then
                result.Errors(RegisterField.Password) = "Use at least " & MinPasswordLength & " characters."
            End If

            If confirm.Length = 0 Then
                result.Errors(RegisterField.ConfirmPassword) = "Confirm your password."
            ElseIf pass <> confirm Then
                result.Errors(RegisterField.ConfirmPassword) = "Passwords don't match."
            End If

            Try
                ' Only ask the database about the email once its format is fine.
                If Not result.Errors.ContainsKey(RegisterField.Email) AndAlso UserRepo.EmailExists(mail) Then
                    result.Errors(RegisterField.Email) = "This email is already registered."
                End If
                If result.Errors.Count > 0 Then Return result

                Dim newId = UserRepo.Create(name, mail, HashPassword(pass))
                If newId = 0 Then
                    result.Errors(RegisterField.Email) = "This email is already registered."
                    Return result
                End If
                result.Success = True
            Catch ex As Exception
                Debug.WriteLine("[AuthService.Register] " & ex.ToString())
                result.DatabaseMessage = Db.FriendlyMessageFor(ex)
            End Try
            Return result
        End Function

        ''' <summary>Checks a name and email the same way registration does (no database). Messages per field.</summary>
        Public Shared Function ValidateProfile(fullName As String, email As String) As AccountResult
            Dim result As New AccountResult()
            Dim name = If(fullName, "").Trim()
            Dim mail = If(email, "").Trim()
            If name.Length = 0 Then
                result.Errors(AccountField.FullName) = "Enter your full name."
            ElseIf name.Length > MaxNameLength Then
                result.Errors(AccountField.FullName) = "Keep your name under " & MaxNameLength & " characters."
            End If
            If mail.Length = 0 Then
                result.Errors(AccountField.Email) = "Enter your email."
            ElseIf Not IsValidEmail(mail) Then
                result.Errors(AccountField.Email) = "Enter a valid email, like name@example.com."
            End If
            Return result
        End Function

        ''' <summary>Saves the signed-in user's name and email (which must stay unique). Never throws.</summary>
        Public Shared Function UpdateProfile(userId As Integer, fullName As String, email As String) As AccountResult
            Dim result = ValidateProfile(fullName, email)
            Try
                Dim mail = If(email, "").Trim()
                If Not result.Errors.ContainsKey(AccountField.Email) AndAlso UserRepo.EmailTakenByOther(mail, userId) Then
                    result.Errors(AccountField.Email) = "Another account already uses this email."
                End If
                If result.Errors.Count > 0 Then Return result
                If Not UserRepo.UpdateProfile(userId, fullName, mail) Then
                    result.Errors(AccountField.Email) = "Another account already uses this email."
                    Return result
                End If
                result.Success = True
            Catch ex As Exception
                Debug.WriteLine("[AuthService.UpdateProfile] " & ex.ToString())
                result.DatabaseMessage = Db.FriendlyMessageFor(ex)
            End Try
            Return result
        End Function

        ''' <summary>
        ''' Checks the rules and the current password, then saves the new one. Never throws.
        ''' </summary>
        Public Shared Function ChangePassword(userId As Integer, currentPassword As String, newPassword As String,
                                              confirmPassword As String) As AccountResult
            Dim result = ChangePasswordErrors(currentPassword, newPassword, confirmPassword)
            If result.Errors.Count > 0 Then Return result

            Try
                Dim user = UserRepo.FindById(userId)
                If user Is Nothing OrElse Not VerifyPassword(currentPassword, user.PasswordHash) Then
                    result.Errors(AccountField.CurrentPassword) = "That's not your current password."
                    Return result
                End If
                UserRepo.UpdatePasswordHash(userId, HashPassword(newPassword))
                result.Success = True
            Catch ex As Exception
                Debug.WriteLine("[AuthService.ChangePassword] " & ex.ToString())
                result.DatabaseMessage = Db.FriendlyMessageFor(ex)
            End Try
            Return result
        End Function

        ''' <summary>The password-change rules that need no database (shown live in the dialog).</summary>
        Public Shared Function ChangePasswordErrors(currentPassword As String, newPassword As String, confirmPassword As String) As AccountResult
            Dim result As New AccountResult()
            Dim current = If(currentPassword, "")
            Dim pass = If(newPassword, "")
            Dim confirm = If(confirmPassword, "")
            If current.Length = 0 Then result.Errors(AccountField.CurrentPassword) = "Enter your current password."
            If pass.Length = 0 Then
                result.Errors(AccountField.NewPassword) = "Enter a new password."
            ElseIf pass.Length < MinPasswordLength Then
                result.Errors(AccountField.NewPassword) = "Use at least " & MinPasswordLength & " characters."
            ElseIf current.Length > 0 AndAlso pass = current Then
                result.Errors(AccountField.NewPassword) = "Use a different password from your current one."
            End If
            If confirm.Length = 0 Then
                result.Errors(AccountField.ConfirmPassword) = "Confirm your new password."
            ElseIf pass <> confirm Then
                result.Errors(AccountField.ConfirmPassword) = "Passwords don't match."
            End If
            Return result
        End Function

    End Class

End Namespace
