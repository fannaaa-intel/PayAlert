Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Dialogs

    ''' <summary>Win32 calls used by the dialog system (layered shadow windows, dragging, real window frames).</summary>
    Friend NotInheritable Class Native

        Private Sub New()
        End Sub

        Public Const WS_EX_LAYERED As Integer = &H80000
        Public Const WS_EX_TRANSPARENT As Integer = &H20
        Public Const WS_EX_TOOLWINDOW As Integer = &H80
        Public Const WS_EX_NOACTIVATE As Integer = &H8000000
        Public Const WM_NCLBUTTONDOWN As Integer = &HA1
        Public Const HTCAPTION As Integer = 2
        Private Const ULW_ALPHA As Integer = 2
        Private Const AC_SRC_OVER As Byte = 0
        Private Const AC_SRC_ALPHA As Byte = 1
        Private Const DWMWA_EXTENDED_FRAME_BOUNDS As Integer = 9

        <StructLayout(LayoutKind.Sequential)>
        Private Structure POINTAPI
            Public X As Integer
            Public Y As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure SIZEAPI
            Public CX As Integer
            Public CY As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential, Pack:=1)>
        Private Structure BLENDFUNCTION
            Public BlendOp As Byte
            Public BlendFlags As Byte
            Public SourceConstantAlpha As Byte
            Public AlphaFormat As Byte
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure RECTAPI
            Public Left As Integer
            Public Top As Integer
            Public Right As Integer
            Public Bottom As Integer
        End Structure

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function UpdateLayeredWindow(hwnd As IntPtr, hdcDst As IntPtr, ByRef pptDst As POINTAPI, ByRef psize As SIZEAPI,
                                                    hdcSrc As IntPtr, ByRef pptSrc As POINTAPI, crKey As Integer,
                                                    ByRef pblend As BLENDFUNCTION, dwFlags As Integer) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function GetDC(hWnd As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function ReleaseDC(hWnd As IntPtr, hDC As IntPtr) As Integer
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function CreateCompatibleDC(hDC As IntPtr) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteDC(hdc As IntPtr) As Boolean
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function SelectObject(hDC As IntPtr, hObject As IntPtr) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteObject(hObject As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function ReleaseCapture() As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function SetWindowPos(hWnd As IntPtr, hWndInsertAfter As IntPtr, x As Integer, y As Integer,
                                             cx As Integer, cy As Integer, flags As UInteger) As Boolean
        End Function

        <DllImport("dwmapi.dll")>
        Private Shared Function DwmGetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef value As RECTAPI, size As Integer) As Integer
        End Function

        ''' <summary>Pushes a 32-bit ARGB bitmap to a layered window at the given screen position and overall alpha.</summary>
        Public Shared Sub PushLayeredBitmap(window As Form, bitmap As Bitmap, location As Point, alpha As Byte)
            If Not window.IsHandleCreated OrElse bitmap Is Nothing Then Return
            Dim screenDc = GetDC(IntPtr.Zero)
            Dim memDc = CreateCompatibleDC(screenDc)
            Dim hBitmap = IntPtr.Zero
            Dim oldBitmap = IntPtr.Zero
            Try
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0))
                oldBitmap = SelectObject(memDc, hBitmap)
                Dim size As New SIZEAPI With {.CX = bitmap.Width, .CY = bitmap.Height}
                Dim source As New POINTAPI With {.X = 0, .Y = 0}
                Dim top As New POINTAPI With {.X = location.X, .Y = location.Y}
                Dim blend As New BLENDFUNCTION With {
                    .BlendOp = AC_SRC_OVER, .BlendFlags = 0, .SourceConstantAlpha = alpha, .AlphaFormat = AC_SRC_ALPHA}
                UpdateLayeredWindow(window.Handle, screenDc, top, size, memDc, source, 0, blend, ULW_ALPHA)
            Finally
                ReleaseDC(IntPtr.Zero, screenDc)
                If hBitmap <> IntPtr.Zero Then
                    SelectObject(memDc, oldBitmap)
                    DeleteObject(hBitmap)
                End If
                DeleteDC(memDc)
            End Try
        End Sub

        ''' <summary>Moves a window directly below another one in the z-order, without moving, sizing or activating it.</summary>
        Public Shared Sub PlaceBelow(window As Form, reference As Form)
            Const SWP_NOSIZE As UInteger = &H1UI
            Const SWP_NOMOVE As UInteger = &H2UI
            Const SWP_NOACTIVATE As UInteger = &H10UI
            If window.IsHandleCreated AndAlso reference.IsHandleCreated Then
                SetWindowPos(window.Handle, reference.Handle, 0, 0, 0, 0, SWP_NOSIZE Or SWP_NOMOVE Or SWP_NOACTIVATE)
            End If
        End Sub

        ''' <summary>Starts a window drag as if the title bar was pressed.</summary>
        Public Shared Sub BeginDrag(window As Form)
            ReleaseCapture()
            SendMessage(window.Handle, WM_NCLBUTTONDOWN, New IntPtr(HTCAPTION), IntPtr.Zero)
        End Sub

        ''' <summary>The visible window frame (without the invisible resize borders Windows 10/11 add).</summary>
        Public Shared Function VisibleBounds(window As Form) As Rectangle
            If window.IsHandleCreated Then
                Dim r As New RECTAPI()
                Try
                    If DwmGetWindowAttribute(window.Handle, DWMWA_EXTENDED_FRAME_BOUNDS, r, Marshal.SizeOf(GetType(RECTAPI))) = 0 Then
                        Return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)
                    End If
                Catch ex As DllNotFoundException
                    Debug.WriteLine("[Native.VisibleBounds] " & ex.Message)
                End Try
            End If
            Return window.Bounds
        End Function

        ''' <summary>True on Windows 11, which draws rounded window corners.</summary>
        Public Shared ReadOnly Property HasRoundedWindows As Boolean
            Get
                Return Environment.OSVersion.Version.Major >= 10 AndAlso Environment.OSVersion.Version.Build >= 22000
            End Get
        End Property

    End Class

End Namespace
