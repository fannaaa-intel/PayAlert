Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' Sidebar item (§6A): line icon plus text on the white sidebar. The active item sits on a rounded
    ''' BrandSoft capsule with a small Brand dot at its left and bold text. Optional count badge.
    ''' </summary>
    <DefaultEvent("Click")>
    Public Class SidebarButton
        Inherits PAControl

        Private _icon As IconKind = IconKind.Dashboard
        Private _isActive As Boolean
        Private _badgeCount As Integer
        Private _hover As Boolean

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.StandardClick, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(196, 44)
        End Sub

        <Category("PayAlert"), DefaultValue(GetType(IconKind), "Dashboard")>
        Public Property Icon As IconKind
            Get
                Return _icon
            End Get
            Set(value As IconKind)
                _icon = value
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(False)>
        Public Property IsActive As Boolean
            Get
                Return _isActive
            End Get
            Set(value As Boolean)
                _isActive = value
                Invalidate()
            End Set
        End Property

        ''' <summary>Count shown in a small Brand badge on the right; 0 hides it.</summary>
        <Category("PayAlert"), DefaultValue(0)>
        Public Property BadgeCount As Integer
            Get
                Return _badgeCount
            End Get
            Set(value As Integer)
                _badgeCount = Math.Max(0, value)
                Invalidate()
            End Set
        End Property

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If keyData = Keys.Enter AndAlso Focused Then
                OnClick(EventArgs.Empty)
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)

            Dim capsule As New RectangleF(Px(4), Px(3), Width - Px(8) - 1, Height - Px(6) - 1)
            Dim radius = capsule.Height / 2.0F
            If _isActive Then
                Gfx.FillRounded(g, capsule, radius, Theme.BrandSoft)
            ElseIf _hover Then
                Gfx.FillRounded(g, capsule, radius, Theme.NeutralSoft)
            End If
            If Focused AndAlso ShowFocusCues Then
                Gfx.DrawRounded(g, capsule, radius, Color.FromArgb(140, Theme.Brand), Px(1.5F))
            End If

            Dim centerY = capsule.Y + capsule.Height / 2.0F
            If _isActive Then
                Dim dot = Px(6)
                Using brush As New SolidBrush(Theme.Brand)
                    g.FillEllipse(brush, capsule.X + Px(12), centerY - dot / 2.0F, dot, dot)
                End Using
            End If

            Dim iconSize = Px(20)
            Dim iconX = capsule.X + Px(28)
            Dim iconColor = If(_isActive, Theme.Brand, If(_hover, Theme.TextPrimary, Theme.TextMuted))
            Icons.Draw(g, _icon, New RectangleF(iconX, centerY - iconSize / 2.0F, iconSize, iconSize), iconColor, Px(1.8F))

            Dim textX = CInt(iconX + iconSize + Px(12))
            Dim badgeWidth = 0
            If _badgeCount > 0 Then
                Dim badgeText = If(_badgeCount > 99, "99+", _badgeCount.ToString())
                Dim badgeTextSize = TextRenderer.MeasureText(g, badgeText, Theme.FontCaptionBold, Size.Empty, TextFormatFlags.NoPadding)
                Dim badgeH = Px(20)
                Dim badgeW = Math.Max(badgeH, badgeTextSize.Width + Px(12))
                Dim badge As New RectangleF(capsule.Right - Px(10) - badgeW, centerY - badgeH / 2.0F, badgeW, badgeH)
                Gfx.FillRounded(g, badge, badgeH / 2.0F, If(_isActive, Theme.Brand, Theme.OverdueText))
                TextRenderer.DrawText(g, badgeText, Theme.FontCaptionBold, Rectangle.Round(badge), Theme.TextOnBrand,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                badgeWidth = CInt(badgeW + Px(14))
            End If

            Dim textColor = If(_isActive OrElse _hover, Theme.TextPrimary, Theme.TextMuted)
            TextRenderer.DrawText(g, Text, If(_isActive, Theme.FontBodyBold, Theme.FontBody),
                                  New Rectangle(textX, CInt(capsule.Y), CInt(capsule.Right) - textX - badgeWidth - PxI(8), CInt(capsule.Height)),
                                  textColor, TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
        End Sub

    End Class

End Namespace
