Option Strict On
Option Explicit On

Imports System.Drawing

''' <summary>
''' PayAlert visual tokens (PLAN.md §6). Every color, font and size in the app comes from here.
''' Phase 1 holds the color and type tokens; Phase 2 adds control styling helpers.
''' </summary>
Public NotInheritable Class Theme

    Private Sub New()
    End Sub

    ' ---------- colors ----------
    Public Shared ReadOnly Brand As Color = ColorTranslator.FromHtml("#F7931E")
    Public Shared ReadOnly BrandDark As Color = ColorTranslator.FromHtml("#D9740F")
    Public Shared ReadOnly BrandSoft As Color = ColorTranslator.FromHtml("#FFF1E0")
    Public Shared ReadOnly AppBackground As Color = ColorTranslator.FromHtml("#F5F6F8")
    Public Shared ReadOnly Surface As Color = ColorTranslator.FromHtml("#FFFFFF")
    Public Shared ReadOnly Border As Color = ColorTranslator.FromHtml("#E5E7EB")
    Public Shared ReadOnly TextPrimary As Color = ColorTranslator.FromHtml("#1F2937")
    Public Shared ReadOnly TextMuted As Color = ColorTranslator.FromHtml("#6B7280")
    Public Shared ReadOnly TextOnBrand As Color = Color.White

    Public Shared ReadOnly UpcomingText As Color = ColorTranslator.FromHtml("#B58100")
    Public Shared ReadOnly UpcomingSoft As Color = ColorTranslator.FromHtml("#FFF6D6")
    Public Shared ReadOnly PartialText As Color = ColorTranslator.FromHtml("#2F6FD6")
    Public Shared ReadOnly PartialSoft As Color = ColorTranslator.FromHtml("#E3EEFD")
    Public Shared ReadOnly PaidText As Color = ColorTranslator.FromHtml("#1E8E4E")
    Public Shared ReadOnly PaidSoft As Color = ColorTranslator.FromHtml("#E3F5EA")
    Public Shared ReadOnly OverdueText As Color = ColorTranslator.FromHtml("#D93036")
    Public Shared ReadOnly OverdueSoft As Color = ColorTranslator.FromHtml("#FDE7E8")

    ' ---------- status names (as returned by v_payment_summary) ----------
    Public Const StatusPaid As String = "Paid"
    Public Const StatusOverdue As String = "Overdue"
    Public Const StatusPartial As String = "Partially Paid"
    Public Const StatusUpcoming As String = "Upcoming"

    Public Shared Function StatusText(status As String) As Color
        Select Case status
            Case StatusPaid : Return PaidText
            Case StatusOverdue : Return OverdueText
            Case StatusPartial : Return PartialText
            Case Else : Return UpcomingText
        End Select
    End Function

    Public Shared Function StatusSoft(status As String) As Color
        Select Case status
            Case StatusPaid : Return PaidSoft
            Case StatusOverdue : Return OverdueSoft
            Case StatusPartial : Return PartialSoft
            Case Else : Return UpcomingSoft
        End Select
    End Function

    ' ---------- type ----------
    Public Const FontFamilyName As String = "Segoe UI"
    Public Const SizePageTitle As Single = 18.0F
    Public Const SizeSection As Single = 12.0F
    Public Const SizeBody As Single = 10.0F
    Public Const SizeCaption As Single = 9.0F
    Public Const SizeBigNumber As Single = 20.0F

    Public Shared Function PageTitleFont() As Font
        Return New Font(FontFamilyName, SizePageTitle, FontStyle.Bold)
    End Function

    Public Shared Function SectionFont() As Font
        Return New Font("Segoe UI Semibold", SizeSection, FontStyle.Regular)
    End Function

    Public Shared Function BodyFont(Optional style As FontStyle = FontStyle.Regular) As Font
        Return New Font(FontFamilyName, SizeBody, style)
    End Function

    Public Shared Function CaptionFont(Optional style As FontStyle = FontStyle.Regular) As Font
        Return New Font(FontFamilyName, SizeCaption, style)
    End Function

    ' ---------- spacing (logical pixels at 96 DPI) ----------
    Public Const Space4 As Integer = 4
    Public Const Space8 As Integer = 8
    Public Const Space12 As Integer = 12
    Public Const Space16 As Integer = 16
    Public Const Space24 As Integer = 24
    Public Const ContentPadding As Integer = 24
    Public Const CardPadding As Integer = 16
    Public Const ButtonHeight As Integer = 36
    Public Const CardRadius As Integer = 12
    Public Const StatusEdgeWidth As Integer = 4
    Public Const TitleAccentWidth As Integer = 40
    Public Const TitleAccentHeight As Integer = 3

End Class
