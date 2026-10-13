Option Strict On
Option Explicit On

Imports System.Threading
Imports System.Windows.Forms

Friend Module Program

    <STAThread>
    Friend Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        ' Never show raw exceptions. Phase 2 routes these to PADialog.Error; for now they go to Debug.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        AddHandler Application.ThreadException, AddressOf OnThreadException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnDomainException

        ' Phase 1: the setup check window. Phase 3 replaces it with frmLogin.
        Application.Run(New Forms.frmSetupCheck())
    End Sub

    Private Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
        Debug.WriteLine("[Unhandled UI exception] " & e.Exception.ToString())
    End Sub

    Private Sub OnDomainException(sender As Object, e As UnhandledExceptionEventArgs)
        Debug.WriteLine("[Unhandled exception] " & e.ExceptionObject.ToString())
    End Sub

End Module
