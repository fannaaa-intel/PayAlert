Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Services

Namespace Forms

    ''' <summary>
    ''' Login (§7): the brand panel on the left, the form on the right. Email and password (with show/hide),
    ''' inline red errors, Enter logs in. On first start it also creates the demo data if the database is empty.
    ''' </summary>
    Public Class frmLogin
        Inherits AuthWindow

        ''' <summary>Raised after a successful login; Session.CurrentUser is set.</summary>
        Public Event LoggedIn As EventHandler
        ''' <summary>Raised when the user wants to create an account.</summary>
        Public Event RegisterRequested As EventHandler

        Private Shared _startupChecked As Boolean

        Private ReadOnly _message As InlineMessage
        Private ReadOnly _email As PATextBox
        Private ReadOnly _emailNote As Label
        Private ReadOnly _password As PATextBox
        Private ReadOnly _passwordNote As Label
        Private ReadOnly _loginButton As PrimaryButton
        Private ReadOnly _initialNotice As String
        Private _busy As Boolean

        Public Sub New(Optional prefillEmail As String = Nothing, Optional notice As String = Nothing)
            MyBase.New()
            Text = "PayAlert - Log in"
            _initialNotice = notice
            Art.Headline = "Never Miss a Payment"
            Art.Subline = "Track what you owe and what you're owed, and get a reminder before every due date."

            AddTitle("Welcome back", "Log in to see what's due and what's owed to you.")
            _message = AddMessage()
            AddFieldLabel("Email")
            _email = AddField(IconKind.Mail, "name@example.com", AuthService.MaxEmailLength)
            _emailNote = AddFieldNote()
            AddFieldLabel("Password")
            _password = AddField(IconKind.Lock, "Your password", 128, isPassword:=True)
            _passwordNote = AddFieldNote()
            _loginButton = AddPrimary("LOG IN")
            Dim createLink = AddSwitchRow("New to PayAlert?", "Create an account")

            AddHandler _loginButton.Click, Async Sub(s, e) Await LogInAsync()
            AddHandler createLink.LinkClicked, Sub(s, e) If Not _busy Then RaiseEvent RegisterRequested(Me, EventArgs.Empty)
            AddHandler _email.TextChanged, Sub(s, e) ClearErrors()
            AddHandler _password.TextChanged, Sub(s, e) ClearErrors()
            AcceptButton = _loginButton

            If Not String.IsNullOrEmpty(prefillEmail) Then _email.Text = prefillEmail
            If Not String.IsNullOrEmpty(notice) Then _message.ShowMessage(notice, Tone.Success)
        End Sub

        ''' <summary>The email typed so far (handed to the register window).</summary>
        Public ReadOnly Property EmailText As String
            Get
                Return _email.Text.Trim()
            End Get
        End Property

        Protected Overrides Async Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            If _email.Text.Length > 0 Then _password.Focus() Else _email.Focus()
            If Not _startupChecked Then
                _startupChecked = True
                Await PrepareDatabaseAsync()
            End If
        End Sub

        ''' <summary>First start only: creates the demo accounts and payments when the users table is empty.</summary>
        Private Async Function PrepareDatabaseAsync() As Task
            Try
                Await Task.Run(Sub() DemoDataSeeder.SeedIfEmpty())
            Catch ex As InvalidOperationException
                Debug.WriteLine("[frmLogin] " & ex.ToString())
                PADialog.Error(Me, "The database isn't ready", ex.Message)
            Catch ex As Exception
                Debug.WriteLine("[frmLogin] " & ex.ToString())
                PADialog.Error(Me, "Can't reach the database", Db.FriendlyMessageFor(ex))
            End Try
        End Function

        Private Sub ClearErrors()
            If _busy Then Return
            SetFieldError(_email, _emailNote, Nothing)
            SetFieldError(_password, _passwordNote, Nothing)
            ' Keep the "Account created" notice; clear only error banners.
            If _message.Visible AndAlso _message.Tone <> Tone.Success Then _message.Clear()
        End Sub

        Private Sub SetBusy(busy As Boolean)
            _busy = busy
            _loginButton.Enabled = Not busy
            _loginButton.Text = If(busy, "LOGGING IN...", "LOG IN")
            UseWaitCursor = busy
        End Sub

        Private Async Function LogInAsync() As Task
            If _busy Then Return
            Dim email = _email.Text.Trim()
            Dim password = _password.Text

            If email.Length = 0 OrElse password.Length = 0 Then
                SetFieldError(_email, _emailNote, If(email.Length = 0, "Enter your email.", Nothing))
                SetFieldError(_password, _passwordNote, If(password.Length = 0, "Enter your password.", Nothing))
                If email.Length = 0 Then _email.Focus() Else _password.Focus()
                Return
            End If

            SetBusy(True)
            Dim result As LoginResult
            Try
                result = Await Task.Run(Function() AuthService.Login(email, password))
            Finally
                SetBusy(False)
            End Try

            Select Case result.Outcome
                Case LoginOutcome.Success
                    Session.SignIn(result.User)
                    RaiseEvent LoggedIn(Me, EventArgs.Empty)
                Case LoginOutcome.WrongCredentials
                    _message.ShowMessage(AuthService.MsgWrongCredentials, Tone.Danger)
                    _password.HasError = True
                    _password.Focus()
                    _password.SelectAll()
                Case LoginOutcome.Deactivated
                    _message.ShowMessage(AuthService.MsgDeactivated, Tone.Danger)
                Case Else
                    PADialog.Error(Me, "Can't reach the database", result.Message)
            End Select
        End Function

    End Class

End Namespace
