Option Strict On
Option Explicit On

Imports System.Drawing

''' <summary>Message tones used by PADialog, PAToast and accents (PLAN.md §6B).</summary>
Public Enum Tone
    Neutral
    Success
    Warning
    Danger
    Brand
End Enum

''' <summary>
''' PayAlert visual tokens (PLAN.md §6). Every color, font and size in the app comes from here;
''' no other file hard-codes a color.
''' </summary>
Public NotInheritable Class Theme

    Private Sub New()
    End Sub

    ' ---------- colors (§6) ----------
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

    ' ---------- supporting tokens (derived from the §6 palette) ----------
    ''' <summary>Card outline: one step darker than Border so white cards stand off the light page.</summary>
    Public Shared ReadOnly CardBorder As Color = ColorTranslator.FromHtml("#DADDE3")
    ''' <summary>Hover border for secondary buttons and fields.</summary>
    Public Shared ReadOnly BorderStrong As Color = ColorTranslator.FromHtml("#D1D5DB")
    ''' <summary>Soft background for the Neutral tone and quiet hovers.</summary>
    Public Shared ReadOnly NeutralSoft As Color = ColorTranslator.FromHtml("#EEF0F4")
    ''' <summary>Dim overlay behind dialogs and the color of soft shadows (always used with alpha).</summary>
    Public Shared ReadOnly Overlay As Color = ColorTranslator.FromHtml("#0F172A")
    Public Shared ReadOnly DangerDark As Color = ColorTranslator.FromHtml("#B9242A")

    ' ---------- status (names as returned by v_payment_summary) ----------
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

    ' ---------- tones ----------
    Public Shared Function ToneColor(t As Tone) As Color
        Select Case t
            Case Tone.Success : Return PaidText
            Case Tone.Warning : Return UpcomingText
            Case Tone.Danger : Return OverdueText
            Case Tone.Brand : Return Brand
            Case Else : Return TextMuted
        End Select
    End Function

    Public Shared Function ToneSoft(t As Tone) As Color
        Select Case t
            Case Tone.Success : Return PaidSoft
            Case Tone.Warning : Return UpcomingSoft
            Case Tone.Danger : Return OverdueSoft
            Case Tone.Brand : Return BrandSoft
            Case Else : Return NeutralSoft
        End Select
    End Function

    ''' <summary>Mixes two colors; amount 0 returns a, 1 returns b.</summary>
    Public Shared Function Blend(a As Color, b As Color, amount As Double) As Color
        Dim t = Math.Max(0.0, Math.Min(1.0, amount))
        Return Color.FromArgb(
            CInt(a.A + (CInt(b.A) - a.A) * t),
            CInt(a.R + (CInt(b.R) - a.R) * t),
            CInt(a.G + (CInt(b.G) - a.G) * t),
            CInt(a.B + (CInt(b.B) - a.B) * t))
    End Function

    ' ---------- type (§6: Segoe UI) ----------
    Public Const FontFamilyName As String = "Segoe UI"
    Public Const SemiboldFamilyName As String = "Segoe UI Semibold"
    Public Const SizePageTitle As Single = 18.0F
    Public Const SizeSection As Single = 12.0F
    Public Const SizeBody As Single = 10.0F
    Public Const SizeCaption As Single = 9.0F
    Public Const SizeBigNumber As Single = 20.0F
    Public Const SizeDialogTitle As Single = 14.0F

    ''' <summary>
    ''' How much bigger text is than the §6 sizes: 1 for Normal, <see cref="LargeTextScale"/> for Large (Settings →
    ''' Text size, kept per user). Set it with <see cref="SetLargeText"/> before building a window: controls take
    ''' the fonts below when they are created or painted.
    ''' </summary>
    Public Const LargeTextScale As Single = 1.2F
    Private Shared _textScale As Single = 1.0F

    Public Shared ReadOnly Property TextScale As Single
        Get
            Return _textScale
        End Get
    End Property

    Public Shared ReadOnly Property IsLargeText As Boolean
        Get
            Return _textScale > 1.0F
        End Get
    End Property

    ''' <summary>
    ''' Switches every shared font between the Normal and Large sizes. Windows that are already open keep the fonts
    ''' they were built with (old instances are never disposed, since controls may still use them); open a new window to apply.
    ''' </summary>
    Public Shared Sub SetLargeText(large As Boolean)
        Dim wanted = If(large, LargeTextScale, 1.0F)
        If wanted = _textScale AndAlso _fontBody IsNot Nothing Then Return
        _textScale = wanted
        BuildFonts()
    End Sub

    ''' <summary>A §6 point size at the current text size, rounded to half points so text stays crisp.</summary>
    Public Shared Function TextSize(points As Single) As Single
        Return CSng(Math.Round(points * _textScale * 2.0F) / 2.0F)
    End Function

    ' Shared font instances live for the whole app; controls use them and never dispose them.
    Private Shared _fontPageTitle, _fontSection, _fontBody, _fontBodyBold, _fontBodySemibold, _fontCaption, _fontCaptionBold,
                   _fontSmallCaps, _fontBigNumber, _fontDialogTitle, _fontButton, _fontWordmark As Font

    Private Shared Sub BuildFonts()
        _fontPageTitle = New Font(FontFamilyName, TextSize(SizePageTitle), FontStyle.Bold)
        _fontSection = New Font(SemiboldFamilyName, TextSize(SizeSection), FontStyle.Regular)
        _fontBody = New Font(FontFamilyName, TextSize(SizeBody), FontStyle.Regular)
        _fontBodyBold = New Font(FontFamilyName, TextSize(SizeBody), FontStyle.Bold)
        _fontBodySemibold = New Font(SemiboldFamilyName, TextSize(SizeBody), FontStyle.Regular)
        _fontCaption = New Font(FontFamilyName, TextSize(SizeCaption), FontStyle.Regular)
        _fontCaptionBold = New Font(FontFamilyName, TextSize(SizeCaption), FontStyle.Bold)
        _fontSmallCaps = New Font(FontFamilyName, TextSize(7.5F), FontStyle.Bold)
        _fontBigNumber = New Font(FontFamilyName, TextSize(SizeBigNumber), FontStyle.Bold)
        _fontDialogTitle = New Font(FontFamilyName, TextSize(SizeDialogTitle), FontStyle.Bold)
        _fontButton = New Font(SemiboldFamilyName, TextSize(SizeBody), FontStyle.Regular)
        _fontWordmark = New Font(FontFamilyName, 13.0F, FontStyle.Bold)
    End Sub

    Private Shared Sub EnsureFonts()
        If _fontBody Is Nothing Then BuildFonts()
    End Sub

    Public Shared ReadOnly Property FontPageTitle As Font
        Get
            EnsureFonts()
            Return _fontPageTitle
        End Get
    End Property

    Public Shared ReadOnly Property FontSection As Font
        Get
            EnsureFonts()
            Return _fontSection
        End Get
    End Property

    Public Shared ReadOnly Property FontBody As Font
        Get
            EnsureFonts()
            Return _fontBody
        End Get
    End Property

    Public Shared ReadOnly Property FontBodyBold As Font
        Get
            EnsureFonts()
            Return _fontBodyBold
        End Get
    End Property

    Public Shared ReadOnly Property FontBodySemibold As Font
        Get
            EnsureFonts()
            Return _fontBodySemibold
        End Get
    End Property

    Public Shared ReadOnly Property FontCaption As Font
        Get
            EnsureFonts()
            Return _fontCaption
        End Get
    End Property

    Public Shared ReadOnly Property FontCaptionBold As Font
        Get
            EnsureFonts()
            Return _fontCaptionBold
        End Get
    End Property

    Public Shared ReadOnly Property FontSmallCaps As Font
        Get
            EnsureFonts()
            Return _fontSmallCaps
        End Get
    End Property

    Public Shared ReadOnly Property FontBigNumber As Font
        Get
            EnsureFonts()
            Return _fontBigNumber
        End Get
    End Property

    Public Shared ReadOnly Property FontDialogTitle As Font
        Get
            EnsureFonts()
            Return _fontDialogTitle
        End Get
    End Property

    Public Shared ReadOnly Property FontButton As Font
        Get
            EnsureFonts()
            Return _fontButton
        End Get
    End Property

    ''' <summary>The sidebar wordmark: part of the logo, so it keeps one size.</summary>
    Public Shared ReadOnly Property FontWordmark As Font
        Get
            EnsureFonts()
            Return _fontWordmark
        End Get
    End Property

    ' Factories kept for callers that own and dispose their fonts.
    Public Shared Function PageTitleFont() As Font
        Return New Font(FontFamilyName, TextSize(SizePageTitle), FontStyle.Bold)
    End Function

    Public Shared Function SectionFont() As Font
        Return New Font(SemiboldFamilyName, TextSize(SizeSection), FontStyle.Regular)
    End Function

    Public Shared Function BodyFont(Optional style As FontStyle = FontStyle.Regular) As Font
        Return New Font(FontFamilyName, TextSize(SizeBody), style)
    End Function

    Public Shared Function CaptionFont(Optional style As FontStyle = FontStyle.Regular) As Font
        Return New Font(FontFamilyName, TextSize(SizeCaption), style)
    End Function

    ' ---------- spacing and sizes (logical pixels at 96 DPI) ----------
    Public Const Space4 As Integer = 4
    Public Const Space8 As Integer = 8
    Public Const Space12 As Integer = 12
    Public Const Space16 As Integer = 16
    Public Const Space24 As Integer = 24
    Public Const ContentPadding As Integer = 24
    Public Const CardPadding As Integer = 16
    Public Const ButtonHeight As Integer = 36
    Public Const ButtonRadius As Integer = 8
    Public Const FieldHeight As Integer = 36
    Public Const CardRadius As Integer = 12
    Public Const DialogRadius As Integer = 14
    Public Const StatusEdgeWidth As Integer = 4
    Public Const TitleAccentWidth As Integer = 40
    Public Const TitleAccentHeight As Integer = 3
    Public Const SidebarWidth As Integer = 220
    Public Const SidebarWidthLarge As Integer = 248

    ' ---------- DPI ----------
    Private Shared _scale As Single

    ''' <summary>Screen DPI ÷ 96 (the app is system-DPI aware, so this is fixed for the session).</summary>
    Public Shared ReadOnly Property Scale As Single
        Get
            If _scale <= 0 Then
                Using g = Graphics.FromHwnd(IntPtr.Zero)
                    _scale = g.DpiX / 96.0F
                End Using
            End If
            Return _scale
        End Get
    End Property

    ''' <summary>Logical pixels to device pixels, for windows built at runtime without auto-scaling.</summary>
    Public Shared Function Px(logical As Single) As Integer
        Return CInt(Math.Round(logical * Scale))
    End Function

End Class
