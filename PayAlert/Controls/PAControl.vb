Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' Base for PayAlert's custom-drawn controls: double-buffered, transparent over its parent,
    ''' and DPI helpers so every GDI+ measurement scales with the screen.
    ''' </summary>
    <ToolboxItem(False)>
    Public Class PAControl
        Inherits Control

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or ControlStyles.SupportsTransparentBackColor, True)
            BackColor = Color.Transparent
            ForeColor = Theme.TextPrimary
            Font = Theme.FontBody
        End Sub

        ''' <summary>Device DPI ÷ 96.</summary>
        Protected ReadOnly Property S As Single
            Get
                Return DeviceDpi / 96.0F
            End Get
        End Property

        Protected Function Px(logical As Single) As Single
            Return logical * S
        End Function

        Protected Function PxI(logical As Single) As Integer
            Return CInt(Math.Round(logical * S))
        End Function

    End Class

End Namespace
