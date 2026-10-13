Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Runtime.InteropServices

''' <summary>The PayAlert window/taskbar icon, drawn from the same mark as the in-app logo.</summary>
Public NotInheritable Class AppIcon

    Private Shared _icon As Icon

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property Icon As Icon
        Get
            If _icon Is Nothing Then _icon = Create(64)
            Return _icon
        End Get
    End Property

    Private Shared Function Create(size As Integer) As Icon
        Using bmp As New Bitmap(size, size, Imaging.PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                Gfx.HighQuality(g)
                g.Clear(Color.Transparent)
                Gfx.DrawLogoMark(g, New RectangleF(1, 1, size - 2, size - 2))
            End Using
            Dim handle = bmp.GetHicon()
            Try
                ' Clone so the icon owns its own handle, then free the temporary one.
                Using temp = Drawing.Icon.FromHandle(handle)
                    Return DirectCast(temp.Clone(), Icon)
                End Using
            Finally
                NativeMethods.DestroyIcon(handle)
            End Try
        End Using
    End Function

    Private NotInheritable Class NativeMethods
        <DllImport("user32.dll")>
        Public Shared Function DestroyIcon(hIcon As IntPtr) As Boolean
        End Function
    End Class

End Class
