Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms

Namespace Dialogs

    ''' <summary>
    ''' The semi-transparent dark layer laid over the owner window while a PADialog is open (§6B).
    ''' The dialog drives its level so the dim fades in and out with the dialog.
    ''' </summary>
    Friend NotInheritable Class DimOverlay
        Inherits Form

        Private Const MaxOpacity As Double = 0.42

        Public Sub New(owner As Form)
            FormBorderStyle = FormBorderStyle.None
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            BackColor = Theme.Overlay
            Opacity = 0
            Text = ""
            Dim frame = Native.VisibleBounds(owner)
            Bounds = frame
            ' Match Windows 11's rounded window corners so the dim doesn't poke out past them.
            If Native.HasRoundedWindows AndAlso owner.WindowState <> FormWindowState.Maximized Then
                Using path = Gfx.RoundedRect(New Rectangle(0, 0, frame.Width, frame.Height), 8 * Theme.Scale)
                    Region = New Region(path)
                End Using
            End If
        End Sub

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim cp = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or Native.WS_EX_NOACTIVATE Or Native.WS_EX_TOOLWINDOW
                Return cp
            End Get
        End Property

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>0 = clear, 1 = fully dimmed.</summary>
        Public Sub SetLevel(level As Double)
            If IsDisposed Then Return
            Opacity = MaxOpacity * Math.Max(0.0, Math.Min(1.0, level))
        End Sub

    End Class

End Namespace
