Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Dialogs
Imports PayAlert.Services

Namespace Forms

    ''' <summary>
    ''' Create an account (§7): full name, email, password and confirm password, each validated inline.
    ''' Creates the user and their settings row, then hands back to Login with the email filled in.
    ''' </summary>
    Public Class frmRegister
        Inherits AuthWindow

        ''' <summary>Raised after the account was created; RegisteredEmail holds the new email.</summary>
        Public Event Registered As EventHandler
        ''' <summary>Raised when the user goes back to Login without registering.</summary>
        Public Event LoginRequested As EventHandler

        Private ReadOnly _fields As New Dictionary(Of RegisterField, Tuple(Of PATextBox, Label))
        Private ReadOnly _createButton As PrimaryButton
        Private _busy As Boolean

        Public Sub New(Optional prefillEmail As String = Nothing)
            MyBase.New()
            Text = "PayAlert - Create an account"
            Art.Headline = "Start in under a minute"
            Art.Subline = "Add what you owe or are owed, and PayAlert keeps an eye on every due date for you."

            AddTitle("Create your account", "Fill in your details. You'll log in right after.")
            AddRow(RegisterField.FullName, "Full name", IconKind.User, "Juan Dela Cruz", AuthService.MaxNameLength, False, "")
            AddRow(RegisterField.Email, "Email", IconKind.Mail, "name@example.com", AuthService.MaxEmailLength, False, "")
            AddRow(RegisterField.Password, "Password", IconKind.Lock, "At least 8 characters", 128, True,
                   "Use at least " & AuthService.MinPasswordLength & " characters.")
            AddRow(RegisterField.ConfirmPassword, "Confirm password", IconKind.Lock, "Type it again", 128, True, "")
            _createButton = AddPrimary("CREATE ACCOUNT")
            Dim loginLink = AddSwitchRow("Already have an account?", "Log in")

            AddHandler _createButton.Click, Async Sub(s, e) Await RegisterAsync()
            AddHandler loginLink.LinkClicked, Sub(s, e) If Not _busy Then RaiseEvent LoginRequested(Me, EventArgs.Empty)
            AcceptButton = _createButton

            If Not String.IsNullOrEmpty(prefillEmail) Then _fields(RegisterField.Email).Item1.Text = prefillEmail
        End Sub

        Public Property RegisteredEmail As String = ""

        Private Sub AddRow(field As RegisterField, label As String, icon As IconKind, placeholder As String,
                           maxLength As Integer, isPassword As Boolean, hint As String)
            AddFieldLabel(label)
            Dim box = AddField(icon, placeholder, maxLength, isPassword)
            Dim note = AddFieldNote(hint)
            _fields(field) = Tuple.Create(box, note)
            ' Editing a field clears its own error straight away.
            AddHandler box.TextChanged, Sub(s, e) If Not _busy AndAlso box.HasError Then SetFieldError(box, note, Nothing)
        End Sub

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            _fields(RegisterField.FullName).Item1.Focus()
        End Sub

        Private Sub SetBusy(busy As Boolean)
            _busy = busy
            _createButton.Enabled = Not busy
            _createButton.Text = If(busy, "CREATING ACCOUNT...", "CREATE ACCOUNT")
            UseWaitCursor = busy
        End Sub

        Private Async Function RegisterAsync() As Task
            If _busy Then Return
            Dim name = _fields(RegisterField.FullName).Item1.Text
            Dim email = _fields(RegisterField.Email).Item1.Text
            Dim password = _fields(RegisterField.Password).Item1.Text
            Dim confirm = _fields(RegisterField.ConfirmPassword).Item1.Text

            SetBusy(True)
            Dim result As RegisterResult
            Try
                result = Await Task.Run(Function() AuthService.Register(name, email, password, confirm))
            Finally
                SetBusy(False)
            End Try

            If result.DatabaseMessage.Length > 0 Then
                PADialog.Error(Me, "Can't create the account right now", result.DatabaseMessage)
                Return
            End If

            Dim firstBad As PATextBox = Nothing
            For Each field In {RegisterField.FullName, RegisterField.Email, RegisterField.Password, RegisterField.ConfirmPassword}
                Dim message As String = Nothing
                result.Errors.TryGetValue(field, message)
                SetFieldError(_fields(field).Item1, _fields(field).Item2, message)
                If message IsNot Nothing AndAlso firstBad Is Nothing Then firstBad = _fields(field).Item1
            Next
            If firstBad IsNot Nothing Then
                firstBad.Focus()
                firstBad.SelectAll()
                Return
            End If

            RegisteredEmail = result.Email
            RaiseEvent Registered(Me, EventArgs.Empty)
        End Function

    End Class

End Namespace
