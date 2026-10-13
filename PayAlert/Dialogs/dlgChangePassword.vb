Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Dialogs

    ''' <summary>
    ''' Change Password (§7), shown inside a PADialog: current password, new password (8+ characters, different from the
    ''' current one) and confirmation. Problems show under each field once it has been left; Save stays disabled until
    ''' everything is valid. A wrong current password keeps the window open with the message under that field.
    ''' </summary>
    Public NotInheritable Class dlgChangePassword
        Inherits Panel
        Implements IDialogBody

        Private Const RowGap As Integer = 8
        Private Const CaptionHeight As Integer = 22
        Private Const FieldHeight As Integer = 42
        Private Const NoteHeight As Integer = 20

        Private ReadOnly _current As New PATextBox()
        Private ReadOnly _new As New PATextBox()
        Private ReadOnly _confirm As New PATextBox()
        Private ReadOnly _notes As New Dictionary(Of PATextBox, Label)
        Private ReadOnly _hints As New Dictionary(Of PATextBox, String)
        Private ReadOnly _touched As New HashSet(Of PATextBox)
        Private ReadOnly _serverErrors As New Dictionary(Of PATextBox, String)
        Private _canSubmit As Boolean

        Public Event CanSubmitChanged As EventHandler Implements IDialogBody.CanSubmitChanged

        ''' <summary>Opens the window. Returns True when the password was changed.</summary>
        Public Shared Function Run(owner As Control) As Boolean
            Dim body As New dlgChangePassword()
            Return PADialog.ShowForm(owner, Tone.Brand, IconKind.Lock, "Change Password",
                                     "Enter your current password, then choose a new one.", body, "Save Password") = DialogChoice.Primary
        End Function

        Private Sub New()
            BackColor = Theme.Surface
            Font = Theme.FontBody
            Dim width = Theme.Px(540 - 48)
            Dim y = 0
            For Each item In {Tuple.Create(_current, "Current password", ""),
                              Tuple.Create(_new, "New password", "At least " & AuthService.MinPasswordLength & " characters."),
                              Tuple.Create(_confirm, "Confirm new password", "Type the new password again.")}
                Dim box = item.Item1
                box.IsPassword = True
                box.Icon = IconKind.Lock
                box.MaxLength = 100
                Dim note As New Label()
                AddField(item.Item2, box, note, y, width)
                _notes(box) = note
                _hints(box) = item.Item3
                y += Theme.Px(CaptionHeight + FieldHeight + NoteHeight + RowGap)
                AddHandler box.TextChanged,
                    Sub(s, e)
                        _serverErrors.Remove(box)
                        CheckForm()
                    End Sub
                AddHandler box.Leave,
                    Sub(s, e)
                        If box.Text.Length > 0 Then _touched.Add(box)
                        CheckForm()
                    End Sub
            Next
            Size = New Size(width, y - Theme.Px(RowGap))
            _current.TabIndex = 0
            _new.TabIndex = 1
            _confirm.TabIndex = 2
            CheckForm()
        End Sub

        Private Sub AddField(caption As String, input As PATextBox, note As Label, y As Integer, width As Integer)
            Controls.Add(New Label() With {
                .Text = caption, .Font = Theme.FontBodySemibold, .ForeColor = Theme.TextPrimary, .BackColor = Theme.Surface,
                .Bounds = New Rectangle(0, y, width, Theme.Px(CaptionHeight))})
            input.SetBounds(0, y + Theme.Px(CaptionHeight), width, Theme.Px(FieldHeight))
            note.Font = Theme.FontCaption
            note.ForeColor = Theme.TextMuted
            note.BackColor = Theme.Surface
            note.AutoEllipsis = True
            note.SetBounds(0, input.Bottom + Theme.Px(2), width, Theme.Px(NoteHeight) - Theme.Px(2))
            Controls.Add(input)
            Controls.Add(note)
        End Sub

        ''' <summary>Validates, shows messages on fields already left (or rejected by the server), and enables Save.</summary>
        Private Function CheckForm() As Boolean
            Dim result = AuthService.ChangePasswordErrors(_current.Text, _new.Text, _confirm.Text)
            Dim map As New Dictionary(Of PATextBox, AccountField) From {
                {_current, AccountField.CurrentPassword}, {_new, AccountField.NewPassword}, {_confirm, AccountField.ConfirmPassword}}
            For Each pair In map
                Dim box = pair.Key
                Dim message As String = Nothing
                If _serverErrors.ContainsKey(box) Then
                    message = _serverErrors(box)
                ElseIf _touched.Contains(box) AndAlso result.Errors.ContainsKey(pair.Value) Then
                    message = result.Errors(pair.Value)
                End If
                _notes(box).Text = If(message, _hints(box))
                _notes(box).ForeColor = If(message Is Nothing, Theme.TextMuted, Theme.OverdueText)
                box.HasError = message IsNot Nothing
            Next
            SetCanSubmit(result.Errors.Count = 0 AndAlso _serverErrors.Count = 0)
            Return _canSubmit
        End Function

        Private Sub SetCanSubmit(value As Boolean)
            If value = _canSubmit Then Return
            _canSubmit = value
            RaiseEvent CanSubmitChanged(Me, EventArgs.Empty)
        End Sub

        Public ReadOnly Property CanSubmit As Boolean Implements IDialogBody.CanSubmit
            Get
                Return _canSubmit
            End Get
        End Property

        Public Sub FocusFirstField() Implements IDialogBody.FocusFirstField
            _current.Focus()
        End Sub

        Public Function Submit() As Boolean Implements IDialogBody.Submit
            _touched.Add(_current)
            _touched.Add(_new)
            _touched.Add(_confirm)
            If Not CheckForm() Then Return False

            Dim result = AuthService.ChangePassword(Session.CurrentUser.UserId, _current.Text, _new.Text, _confirm.Text)
            If result.DatabaseMessage.Length > 0 Then
                PADialog.Error(Me, "Couldn't change your password", result.DatabaseMessage)
                Return False
            End If
            If Not result.Success Then
                If result.Errors.ContainsKey(AccountField.CurrentPassword) Then _serverErrors(_current) = result.Errors(AccountField.CurrentPassword)
                CheckForm()
                _current.Focus()
                _current.SelectAll()
                Return False
            End If
            Return True
        End Function

    End Class

End Namespace
