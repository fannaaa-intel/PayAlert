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

    End Class

End Namespace
