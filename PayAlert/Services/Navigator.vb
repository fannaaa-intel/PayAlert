Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Windows.Forms
Imports PayAlert.Views

Namespace Services

    ''' <summary>
    ''' Swaps screens (PAView) inside frmMain's content area and keeps a back stack (§7).
    ''' Sidebar items start a fresh stack with ShowRoot; inner screens are pushed with Push and left with GoBack.
    ''' Screens under the top of the stack stay alive (hidden), so going back keeps their state.
    ''' </summary>
    Public NotInheritable Class Navigator

        Private ReadOnly _host As Control
        Private ReadOnly _stack As New List(Of PAView)

        ''' <summary>Raised after a screen is shown.</summary>
        Public Event Navigated As EventHandler

        Public Sub New(host As Control)
            _host = host
        End Sub

        Public ReadOnly Property Current As PAView
            Get
                Return If(_stack.Count = 0, Nothing, _stack(_stack.Count - 1))
            End Get
        End Property

        Public ReadOnly Property CanGoBack As Boolean
            Get
                Return _stack.Count > 1
            End Get
        End Property

        ''' <summary>Clears the back stack (disposing those screens) and shows a top-level screen.</summary>
        Public Sub ShowRoot(view As PAView)
            Dim old = New List(Of PAView)(_stack)
            _stack.Clear()
            _stack.Add(view)
            Display(view, inner:=False)
            For Each screen In old
                Retire(screen)
            Next
        End Sub

        ''' <summary>Shows an inner screen on top of the current one; it gets the Back button.</summary>
        Public Sub Push(view As PAView)
            If view.SidebarKey.Length = 0 AndAlso Current IsNot Nothing Then view.SidebarKey = Current.SidebarKey
            _stack.Add(view)
            Display(view, inner:=True)
        End Sub

        ''' <summary>Closes the current inner screen and returns to the one under it.</summary>
        Public Function GoBack() As Boolean
            If Not CanGoBack Then Return False
            Dim leaving = Current
            _stack.RemoveAt(_stack.Count - 1)
            Display(Current, inner:=_stack.Count > 1)
            Retire(leaving)
            Return True
        End Function

        ''' <summary>Goes back to the bottom screen of the stack (e.g. "Back to Dashboard" after saving).</summary>
        Public Sub BackToRoot()
            While CanGoBack
                GoBack()
            End While
        End Sub

        ''' <summary>
        ''' Hides a screen now and disposes it once the current event is done: the click that navigated
        ''' away (the Back button, an action inside the screen) is usually still running on that screen.
        ''' </summary>
        Private Sub Retire(screen As PAView)
            If screen Is Nothing OrElse screen.IsDisposed Then Return
            screen.Visible = False
            If _host.IsHandleCreated Then
                _host.BeginInvoke(New MethodInvoker(
                    Sub()
                        _host.Controls.Remove(screen)
                        screen.Dispose()
                    End Sub))
            Else
                _host.Controls.Remove(screen)
                screen.Dispose()
            End If
        End Sub

        Private Sub Display(view As PAView, inner As Boolean)
            _host.SuspendLayout()
            view.Navigator = Me
            view.IsInner = inner
            If Not _host.Controls.Contains(view) Then
                view.Dock = DockStyle.Fill
                _host.Controls.Add(view)
            End If
            view.Visible = True
            view.BringToFront()
            For Each other In _stack
                If other IsNot view Then other.Visible = False
            Next
            _host.ResumeLayout(True)
            view.OnNavigatedTo()
            RaiseEvent Navigated(Me, EventArgs.Empty)
        End Sub

    End Class

End Namespace
