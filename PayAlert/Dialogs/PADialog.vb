Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports PayAlert.Controls

Namespace Dialogs

    Public Enum DialogChoice
        Primary
        Secondary
        Cancel
    End Enum

    ''' <summary>
    ''' PayAlert's message dialog (§6B). Replaces every message box in the app.
    ''' A borderless 420px "ticket": tone strip across the top, a 48px icon badge, bold title, muted message,
    ''' and a perforated footer (notched on both sides) holding right-aligned buttons. Opens over a dim overlay
    ''' with a 150ms fade and upward slide; Enter = primary (Cancel has focus in Danger dialogs), Esc = cancel.
    ''' </summary>
    Public NotInheritable Class PADialog
        Inherits Form

        Private Const DialogWidth As Integer = 420
        Private Const Pad As Integer = 24
        Private Const StripHeight As Integer = 4
        Private Const BadgeSize As Integer = 48
        Private Const NotchRadius As Integer = 8
        Private Const FadeInMs As Double = 150
        Private Const FadeOutMs As Double = 120
        Private Const SlideDistance As Integer = 14
        Private Const ShadowSpread As Integer = 18

        Private ReadOnly _tone As Tone
        Private ReadOnly _icon As IconKind
        Private ReadOnly _title As String
        Private ReadOnly _message As String
        Private ReadOnly _details As New List(Of KeyValuePair(Of String, String))
        Private ReadOnly _primary As PAButton
        Private ReadOnly _secondary As PAButton
        Private ReadOnly _focusSecondary As Boolean
        Private _choice As DialogChoice = DialogChoice.Cancel

        ' Layout in device pixels (dialogs are built at runtime, so they scale themselves).
        Private _badgeRect As Rectangle
        Private _titleRect As Rectangle
        Private _messageRect As Rectangle
        Private _detailsRect As Rectangle
        Private _headerBottom As Integer
        Private _perforationY As Integer

        Private _overlay As DimOverlay
        Private _shadow As ShadowWindow
        Private _shadowAlpha As Byte
        Private ReadOnly _timer As New Timer() With {.Interval = 15}
        Private ReadOnly _clock As New Stopwatch()
        Private _phase As Integer          ' 0 = resting, 1 = opening, 2 = closing
        Private _restTop As Integer
        Private _closeAllowed As Boolean

        ' ================================================================ public API

        ''' <summary>Asks a yes/no question. Danger tone uses a red confirm button and starts focus on Cancel.</summary>
        Public Shared Function Confirm(owner As Control, title As String, message As String,
                                       Optional confirmText As String = "Confirm", Optional cancelText As String = "Cancel",
                                       Optional tone As Tone = Tone.Brand, Optional icon As IconKind = IconKind.None) As Boolean
            Dim badge = If(icon <> IconKind.None, icon, If(tone = Tone.Danger, IconKind.Trash, DefaultIcon(tone)))
            Dim kind = If(tone = Tone.Danger, ButtonKind.Danger, ButtonKind.Primary)
            Using dlg As New PADialog(tone, badge, title, message, Nothing, confirmText, kind, cancelText, tone = Tone.Danger)
                Return dlg.Run(owner) = DialogChoice.Primary
            End Using
        End Function

        ''' <summary>Shows information with a single OK button.</summary>
        Public Shared Sub Info(owner As Control, title As String, message As String, Optional tone As Tone = Tone.Neutral)
            Using dlg As New PADialog(tone, DefaultIcon(tone), title, message, Nothing, "OK", ButtonKind.Primary, Nothing, False)
                dlg.Run(owner)
            End Using
        End Sub

        ''' <summary>
        ''' Success with a short receipt. Each detail line "Label: Value" becomes a row; other lines show as-is.
        ''' Returns which button was chosen (Esc returns Cancel).
        ''' </summary>
        Public Shared Function Success(owner As Control, title As String, detailsLines As IEnumerable(Of String),
                                       Optional primaryText As String = "OK", Optional secondaryText As String = Nothing) As DialogChoice
            Using dlg As New PADialog(Tone.Success, IconKind.Check, title, "", detailsLines, primaryText, ButtonKind.Primary, secondaryText, False)
                Return dlg.Run(owner)
            End Using
        End Function

        ''' <summary>A friendly error. Never pass exception text or codes here.</summary>
        Public Shared Sub [Error](owner As Control, title As String, friendlyMessage As String)
            Using dlg As New PADialog(Tone.Danger, IconKind.Close, title, friendlyMessage, Nothing, "OK", ButtonKind.Primary, Nothing, False)
                dlg.Run(owner)
            End Using
        End Sub

        Private Shared Function DefaultIcon(tone As Tone) As IconKind
            Select Case tone
                Case Tone.Success : Return IconKind.Check
                Case Tone.Warning : Return IconKind.Alert
                Case Tone.Danger : Return IconKind.Close
                Case Else : Return IconKind.Info
            End Select
        End Function

        ' ================================================================ construction and layout

        Private Sub New(tone As Tone, icon As IconKind, title As String, message As String, detailsLines As IEnumerable(Of String),
                        primaryText As String, primaryKind As ButtonKind, secondaryText As String, focusSecondary As Boolean)
            _tone = tone
            _icon = icon
            _title = If(title, "")
            _message = If(message, "")
            _focusSecondary = focusSecondary AndAlso Not String.IsNullOrEmpty(secondaryText)
            If detailsLines IsNot Nothing Then
                For Each line In detailsLines
                    If String.IsNullOrWhiteSpace(line) Then Continue For
                    Dim split = line.IndexOf(": ", StringComparison.Ordinal)
                    If split > 0 Then
                        _details.Add(New KeyValuePair(Of String, String)(line.Substring(0, split), line.Substring(split + 2)))
                    Else
                        _details.Add(New KeyValuePair(Of String, String)("", line))
                    End If
                Next
            End If

            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
            AutoScaleMode = AutoScaleMode.None
            FormBorderStyle = FormBorderStyle.None
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            KeyPreview = True
            BackColor = Theme.Surface
            Font = Theme.FontBody
            Text = _title
            Opacity = 0

            _primary = New PAButton() With {.Kind = primaryKind, .Text = If(String.IsNullOrEmpty(primaryText), "OK", primaryText)}
            AddHandler _primary.Click, Sub(s, e) Choose(DialogChoice.Primary)
            Controls.Add(_primary)
            If Not String.IsNullOrEmpty(secondaryText) Then
                _secondary = New PAButton() With {.Kind = ButtonKind.Secondary, .Text = secondaryText}
                AddHandler _secondary.Click, Sub(s, e) Choose(DialogChoice.Secondary)
                Controls.Add(_secondary)
            End If

            ComputeLayout()
        End Sub

        Private Shared Function Px(logical As Single) As Integer
            Return Theme.Px(logical)
        End Function

        Private Sub ComputeLayout()
            Dim dialogW = Px(DialogWidth)
            Dim p = Px(Pad)
            Dim inner = dialogW - p * 2
            Dim wrap = TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding

            Dim y = Px(StripHeight) + Px(24)
            _badgeRect = New Rectangle(p, y, Px(BadgeSize), Px(BadgeSize))
            Dim titleX = _badgeRect.Right + Px(16)
            Dim titleWidth = dialogW - titleX - p
            Dim titleSize = TextRenderer.MeasureText(_title, Theme.FontDialogTitle, New Size(titleWidth, Integer.MaxValue), wrap)
            Dim titleY = If(titleSize.Height < _badgeRect.Height, _badgeRect.Y + (_badgeRect.Height - titleSize.Height) \ 2, y)
            _titleRect = New Rectangle(titleX, titleY, titleWidth, titleSize.Height)
            _headerBottom = Math.Max(_badgeRect.Bottom, _titleRect.Bottom)
            y = _headerBottom

            If _message.Length > 0 Then
                y += Px(16)
                Dim messageSize = TextRenderer.MeasureText(_message, Theme.FontBody, New Size(inner, Integer.MaxValue), wrap)
                _messageRect = New Rectangle(p, y, inner, messageSize.Height)
                y = _messageRect.Bottom
            End If

            If _details.Count > 0 Then
                y += Px(18)
                _detailsRect = New Rectangle(p, y, inner, Px(10) * 2 + Px(30) * _details.Count)
                y = _detailsRect.Bottom
            End If

            y += Px(24)
            _perforationY = y

            ' Footer: buttons right-aligned.
            Dim buttonTop = _perforationY + Px(16)
            Dim buttonHeight = Px(Theme.ButtonHeight)
            Dim right = dialogW - p
            _primary.SetBounds(right - _primary.PreferredWidth(), buttonTop, _primary.PreferredWidth(), buttonHeight)
            If _secondary IsNot Nothing Then
                Dim w = _secondary.PreferredWidth()
                _secondary.SetBounds(_primary.Left - Px(10) - w, buttonTop, w, buttonHeight)
            End If
            ClientSize = New Size(dialogW, buttonTop + buttonHeight + Px(16))

            ' Tab order: secondary (left) then primary (right), like reading order.
            If _secondary IsNot Nothing Then _secondary.TabIndex = 0
            _primary.TabIndex = 1

            Using path = BuildShape(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), -1)
                Region = New Region(path)
            End Using
        End Sub

        ''' <summary>Window outline: rounded corners plus ticket notches at the perforation line.</summary>
        Private Function BuildShape(body As RectangleF, grow As Single) As GraphicsPath
            Dim r = RectangleF.Inflate(body, grow, grow)
            Dim notchY = body.Y + _perforationY
            Return Gfx.NotchedRect(r, Px(Theme.DialogRadius) + grow, Px(NotchRadius) - grow, notchY, notchY)
        End Function

        ''' <summary>Face drawn by the shadow window (anti-aliased edge), matching OnPaint below.</summary>
        Private Sub PaintFace(g As Graphics, body As RectangleF, face As GraphicsPath)
            Using fill As New SolidBrush(Theme.Surface)
                g.FillPath(fill, face)
            End Using
            Dim state = g.Save()
            g.SetClip(New RectangleF(body.X - 1, body.Y + _perforationY, body.Width + 2, body.Height))
            Using footer As New SolidBrush(Theme.AppBackground)
                g.FillPath(footer, face)
            End Using
            g.Restore(state)
            state = g.Save()
            g.SetClip(New RectangleF(body.X - 1, body.Y - 1, body.Width + 2, Px(StripHeight) + 1))
            Using strip As New SolidBrush(Theme.ToneColor(_tone))
                g.FillPath(strip, face)
            End Using
            g.Restore(state)
            Using pen As New Pen(Theme.Border)
                g.DrawPath(pen, face)
            End Using
        End Sub

        ' ================================================================ showing and animation

        Private Function Run(owner As Control) As DialogChoice
            Dim ownerForm = ResolveOwner(owner)
            Try
                If ownerForm IsNot Nothing Then
                    _overlay = New DimOverlay(ownerForm)
                    _overlay.Show(ownerForm)
                    PlaceCentered(Native.VisibleBounds(ownerForm))
                    ShowDialog(ownerForm)
                Else
                    PlaceCentered(Screen.FromPoint(Cursor.Position).WorkingArea)
                    ShowDialog()
                End If
            Finally
                If _overlay IsNot Nothing Then
                    _overlay.Close()
                    _overlay.Dispose()
                    _overlay = Nothing
                End If
                If ownerForm IsNot Nothing AndAlso Not ownerForm.IsDisposed Then ownerForm.Activate()
            End Try
            Return _choice
        End Function

        Friend Shared Function ResolveOwner(owner As Control) As Form
            Dim form As Form = Nothing
            If owner IsNot Nothing Then form = If(TryCast(owner, Form), owner.FindForm())
            If form Is Nothing Then form = Form.ActiveForm
            ' Helper windows are never owners; use the window they belong to.
            While form IsNot Nothing AndAlso (TypeOf form Is DimOverlay OrElse TypeOf form Is ShadowWindow OrElse TypeOf form Is PAToast)
                form = form.Owner
            End While
            If form Is Nothing OrElse form.IsDisposed OrElse Not form.Visible OrElse form.WindowState = FormWindowState.Minimized Then Return Nothing
            Return form
        End Function

        Private Sub PlaceCentered(area As Rectangle)
            Dim work = Screen.FromRectangle(area).WorkingArea
            Dim x = area.X + (area.Width - Width) \ 2
            Dim y = area.Y + (area.Height - Height) \ 2 - Px(24)
            x = Math.Max(work.Left, Math.Min(x, work.Right - Width))
            y = Math.Max(work.Top, Math.Min(y, work.Bottom - Height))
            Location = New Point(x, y)
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ' The shadow window is shown before the dialog, so the dialog stays above it.
            _shadow = New ShadowWindow(AddressOf BuildShape, AddressOf PaintFace, Px(ShadowSpread), Px(6))
            _shadow.Show(Owner)
            ActiveControl = If(_focusSecondary, _secondary, _primary)
        End Sub

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            _restTop = Top
            _phase = 1
            Top = _restTop + Px(SlideDistance)
            _clock.Restart()
            AddHandler _timer.Tick, AddressOf OnTick
            _timer.Start()
        End Sub

        Private Sub OnTick(sender As Object, e As EventArgs)
            Dim elapsed = _clock.Elapsed.TotalMilliseconds
            If _phase = 1 Then
                Dim t = Math.Min(1.0, elapsed / FadeInMs)
                Dim eased = Gfx.EaseOut(t)
                SetLevel(eased)
                Top = _restTop + CInt(Px(SlideDistance) * (1 - eased))
                If t >= 1 Then
                    _phase = 0
                    _timer.Stop()
                End If
            ElseIf _phase = 2 Then
                Dim t = Math.Min(1.0, elapsed / FadeOutMs)
                SetLevel(1 - t)
                Top = _restTop + CInt(Px(6) * t)
                If t >= 1 Then
                    _timer.Stop()
                    _closeAllowed = True
                    Close()
                End If
            End If
        End Sub

        Private Sub SetLevel(level As Double)
            Opacity = Math.Max(0.0, Math.Min(1.0, level))
            _overlay?.SetLevel(level)
            _shadowAlpha = CByte(Math.Round(255 * Math.Max(0.0, Math.Min(1.0, level))))
            _shadow?.Follow(Bounds, _shadowAlpha)
        End Sub

        Protected Overrides Sub OnLocationChanged(e As EventArgs)
            MyBase.OnLocationChanged(e)
            If _phase = 0 Then _restTop = Top
            _shadow?.Follow(Bounds, _shadowAlpha)
        End Sub

        Private Sub Choose(choice As DialogChoice)
            If _phase = 2 Then Return
            _choice = choice
            Close()
        End Sub

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If Not _closeAllowed Then
                ' Play the fade-out first; the timer closes the dialog for real when it ends.
                e.Cancel = True
                If _phase <> 2 Then
                    _phase = 2
                    _restTop = Top
                    _clock.Restart()
                    _timer.Start()
                End If
                Return
            End If
            MyBase.OnFormClosing(e)
        End Sub

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            _timer.Stop()
            If _shadow IsNot Nothing Then
                _shadow.Close()
                _shadow.Dispose()
                _shadow = Nothing
            End If
            MyBase.OnFormClosed(e)
        End Sub

        ' ================================================================ input

        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            If keyData = Keys.Escape Then
                Choose(DialogChoice.Cancel)
                Return True
            End If
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If keyData = Keys.Enter Then
                Dim focusedButton = TryCast(ActiveControl, PAButton)
                If focusedButton IsNot Nothing Then focusedButton.PerformClick() Else _primary.PerformClick()
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            ' Drag by the title area.
            If e.Button = MouseButtons.Left AndAlso e.Y <= _headerBottom + Px(8) AndAlso _phase = 0 Then Native.BeginDrag(Me)
        End Sub

        ' ================================================================ painting

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim toneColor = Theme.ToneColor(_tone)
            Dim toneSoft = Theme.ToneSoft(_tone)

            ' Footer band and its perforation.
            Using footer As New SolidBrush(Theme.AppBackground)
                g.FillRectangle(footer, 0, _perforationY, ClientSize.Width, ClientSize.Height - _perforationY)
            End Using
            Using perforation As New Pen(Theme.BorderStrong, Math.Max(1.0F, Theme.Scale))
                perforation.DashStyle = DashStyle.Custom
                perforation.DashPattern = {3.0F, 3.0F}
                Dim inset = Px(NotchRadius) + Px(8)
                g.DrawLine(perforation, inset, _perforationY, ClientSize.Width - inset, _perforationY)
            End Using

            ' Tone strip across the top.
            Using strip As New SolidBrush(toneColor)
                g.FillRectangle(strip, 0, 0, ClientSize.Width, Px(StripHeight))
            End Using

            ' Icon badge: soft halo, soft disc, tone-colored line icon.
            Dim halo = RectangleF.Inflate(_badgeRect, Px(5), Px(5))
            Using brush As New SolidBrush(Color.FromArgb(90, toneSoft))
                g.FillEllipse(brush, halo)
            End Using
            Using brush As New SolidBrush(toneSoft)
                g.FillEllipse(brush, _badgeRect)
            End Using
            Icons.Draw(g, _icon, RectangleF.Inflate(_badgeRect, -Px(12), -Px(12)), toneColor, Theme.Scale * 2.3F)

            Dim wrap = TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding
            TextRenderer.DrawText(g, _title, Theme.FontDialogTitle, _titleRect, Theme.TextPrimary, wrap)
            If _message.Length > 0 Then
                TextRenderer.DrawText(g, _message, Theme.FontBody, _messageRect, Theme.TextMuted, wrap)
            End If

            If _details.Count > 0 Then PaintDetails(g)
        End Sub

        ''' <summary>The success "receipt": label on the left, value on the right, dotted rules between rows.</summary>
        Private Sub PaintDetails(g As Graphics)
            Gfx.FillRounded(g, _detailsRect, Px(10), Theme.AppBackground)
            Dim rowHeight = Px(30)
            Dim y = _detailsRect.Y + Px(10)
            Dim x = _detailsRect.X + Px(14)
            Dim w = _detailsRect.Width - Px(28)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            For i = 0 To _details.Count - 1
                Dim row As New Rectangle(x, y, w, rowHeight)
                Dim item = _details(i)
                If item.Key.Length > 0 Then
                    TextRenderer.DrawText(g, item.Key, Theme.FontBody, row, Theme.TextMuted, flags)
                    TextRenderer.DrawText(g, item.Value, Theme.FontBodyBold, row, Theme.TextPrimary, flags Or TextFormatFlags.Right)
                Else
                    TextRenderer.DrawText(g, item.Value, Theme.FontBodyBold, row, Theme.TextPrimary, flags)
                End If
                If i < _details.Count - 1 Then
                    Using rule As New Pen(Theme.BorderStrong)
                        rule.DashStyle = DashStyle.Dot
                        g.DrawLine(rule, x, y + rowHeight, x + w, y + rowHeight)
                    End Using
                End If
                y += rowHeight
            Next
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _timer.Dispose()
                _shadow?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
