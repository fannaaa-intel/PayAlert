Option Strict On
Option Explicit On

Imports System.Threading
Imports System.Windows.Forms
Imports PayAlert.Data
Imports PayAlert.Forms
Imports PayAlert.Services

Friend Module Program

    <STAThread>
    Friend Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        ' Never show raw exceptions: log the details, show a friendly PADialog.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        AddHandler Application.ThreadException, AddressOf OnThreadException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnDomainException

        Application.Run(New AppFlow())
    End Sub

    Private Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
        Debug.WriteLine("[Unhandled UI exception] " & e.Exception.ToString())
        Try
            Dialogs.PADialog.Error(Nothing, "Something went wrong", "PayAlert couldn't finish that. Please try again.")
        Catch ex As Exception
            Debug.WriteLine("[Unhandled UI exception] Could not show the error dialog: " & ex.ToString())
        End Try
    End Sub

    Private Sub OnDomainException(sender As Object, e As UnhandledExceptionEventArgs)
        Debug.WriteLine("[Unhandled exception] " & e.ExceptionObject.ToString())
    End Sub

End Module

''' <summary>
''' Decides which top-level window is showing: Login ⇄ Register, then the main window after login,
''' and back to Login on log out. The app ends when the current window is closed by the user.
''' </summary>
Friend NotInheritable Class AppFlow
    Inherits ApplicationContext

    Public Sub New()
        ShowLogin(Nothing, Nothing, Nothing)
    End Sub

    ''' <summary>Shows the next window, then closes the previous one without ending the app.</summary>
    Private Sub SwitchTo(nextForm As Form, previous As Form)
        MainForm = nextForm
        nextForm.Show()
        ' Close the old window after the current event finishes (we are usually inside one of its handlers).
        ' Closing a modeless form also disposes it.
        If previous IsNot Nothing AndAlso Not previous.IsDisposed Then previous.BeginInvoke(New MethodInvoker(AddressOf previous.Close))
    End Sub

    Private Shared Sub PlaceLike(nextForm As Form, previous As Form)
        ' Login and Register are the same size: open the next one exactly where the last one was.
        If previous IsNot Nothing AndAlso previous.WindowState = FormWindowState.Normal AndAlso previous.Size = nextForm.Size Then
            nextForm.StartPosition = FormStartPosition.Manual
            nextForm.Location = previous.Location
        End If
    End Sub

    Private Sub ShowLogin(prefillEmail As String, notice As String, previous As Form)
        ' Login and Register always use the Normal text size; Large belongs to the signed-in user.
        Theme.SetLargeText(False)
        Dim login As New frmLogin(prefillEmail, notice)
        AddHandler login.LoggedIn, Sub(s, e) ShowMain(login)
        AddHandler login.RegisterRequested, Sub(s, e) ShowRegister(login.EmailText, login)
        PlaceLike(login, previous)
        SwitchTo(login, previous)
    End Sub

    Private Sub ShowRegister(prefillEmail As String, previous As Form)
        Dim register As New frmRegister(prefillEmail)
        AddHandler register.Registered, Sub(s, e) ShowLogin(register.RegisteredEmail, "Account created. Please log in.", register)
        AddHandler register.LoginRequested, Sub(s, e) ShowLogin(Nothing, Nothing, register)
        PlaceLike(register, previous)
        SwitchTo(register, previous)
    End Sub

    ''' <summary>Opens the main window in the user's text size, on <paramref name="startKey"/> (a sidebar section).</summary>
    Private Sub ShowMain(previous As Form, Optional startKey As String = Nothing)
        Theme.SetLargeText(UserWantsLargeText())
        Dim main As New frmMain(startKey)
        AddHandler main.LoggedOut,
            Sub(s, e)
                Dim email = If(Session.CurrentUser?.Email, "")
                Session.SignOut()
                ShowLogin(email, Nothing, main)
            End Sub
        ' A new text size needs freshly built screens: open a new main window on Settings and close this one.
        AddHandler main.TextSizeChanged, Sub(s, e) ShowMain(main, "settings")
        If TypeOf previous Is frmMain Then
            ' Same place and size as the window it replaces.
            main.StartNotice = "Text size: " & If(Theme.IsLargeText, "Large", "Normal")
            main.WindowState = previous.WindowState
            If previous.WindowState = FormWindowState.Normal Then
                main.StartPosition = FormStartPosition.Manual
                main.Bounds = previous.Bounds
            End If
        End If
        SwitchTo(main, previous)
    End Sub

    Private Shared Function UserWantsLargeText() As Boolean
        If Not Session.IsSignedIn Then Return False
        Try
            Return SettingsRepo.Get(Session.CurrentUser.UserId).LargeText
        Catch ex As Exception
            ' The screens report database trouble themselves; start in the Normal size.
            Debug.WriteLine("[AppFlow] " & ex.ToString())
            Return False
        End Try
    End Function

End Class
