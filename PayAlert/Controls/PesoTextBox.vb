Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' Money field with a "₱" prefix. Accepts digits and one decimal point (2 decimals, up to 10 digits
    ''' before the point, matching DECIMAL(12,2)). Shows the raw number while editing and formats on blur.
    ''' </summary>
    <DefaultEvent("ValueChanged")>
    Public Class PesoTextBox
        Inherits PAControl

        Private Const MaxIntegerDigits As Integer = 10
        Private Shared ReadOnly RawPattern As New Regex("^\d{0," & MaxIntegerDigits & "}(\.\d{0,2})?$", RegexOptions.Compiled)

        Private WithEvents Box As New TextBox()
        Private _hasError As Boolean
        Private _hover As Boolean
        Private _placeholder As String = "0.00"
        Private _updating As Boolean
        Private _lastValue As Decimal?

        Public Event ValueChanged As EventHandler

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            Cursor = Cursors.IBeam
            Box.BorderStyle = BorderStyle.None
            Box.BackColor = Theme.Surface
            Box.ForeColor = Theme.TextPrimary
            Box.Font = Theme.FontBody
            Box.MaxLength = MaxIntegerDigits + 6
            Box.ShortcutsEnabled = True
            Controls.Add(Box)
            Size = New Size(220, Theme.FieldHeight)
        End Sub

        ''' <summary>The amount, or Nothing when the field is empty.</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Value As Decimal?
            Get
                Return Parse(Box.Text)
            End Get
            Set(v As Decimal?)
                _updating = True
                Box.Text = If(v.HasValue, If(Box.Focused, RawText(v.Value), Formatted(v.Value)), "")
                _updating = False
                RaiseIfChanged()
            End Set
        End Property

        ''' <summary>Red border and ring for inline validation (the message itself is shown by the form).</summary>
        <Category("PayAlert"), DefaultValue(False)>
        Public Property HasError As Boolean
            Get
                Return _hasError
            End Get
            Set(v As Boolean)
                _hasError = v
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue("0.00")>
        Public Property PlaceholderText As String
            Get
                Return _placeholder
            End Get
            Set(v As String)
                _placeholder = If(v, "")
                ApplyCueBanner()
            End Set
        End Property

        ''' <summary>The inner TextBox, for focus and tab-order wiring.</summary>
        <Browsable(False)>
        Public ReadOnly Property InnerTextBox As TextBox
            Get
                Return Box
            End Get
        End Property

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Return New Size(Width, PxI(Theme.FieldHeight))
        End Function

        ' ---------------------------------------------------------------- parsing and formatting

        Private Shared Function Parse(text As String) As Decimal?
            Dim raw = text.Replace(",", "").Trim()
            If raw.Length = 0 OrElse raw = "." Then Return Nothing
            Dim result As Decimal
            If Decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, result) Then Return result
            Return Nothing
        End Function

        Private Shared Function Formatted(v As Decimal) As String
            Return v.ToString("#,##0.00", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function RawText(v As Decimal) As String
            Return v.ToString("0.##", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>Keeps only digits and the first decimal point, trimmed to the allowed length (for paste).</summary>
        Private Shared Function Sanitize(text As String) As String
            Dim sb As New StringBuilder()
            Dim seenPoint = False
            Dim intDigits = 0, decDigits = 0
            For Each c In text
                If Char.IsDigit(c) Then
                    If seenPoint Then
                        If decDigits < 2 Then sb.Append(c) : decDigits += 1
                    ElseIf intDigits < MaxIntegerDigits Then
                        sb.Append(c) : intDigits += 1
                    End If
                ElseIf c = "."c AndAlso Not seenPoint Then
                    seenPoint = True
                    sb.Append(c)
                End If
            Next
            Return sb.ToString()
        End Function

        Private Sub RaiseIfChanged()
            Dim current = Value
            If Nullable.Equals(current, _lastValue) Then Return
            _lastValue = current
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Sub

        ' ---------------------------------------------------------------- inner TextBox events

        Private Sub Box_KeyPress(sender As Object, e As KeyPressEventArgs) Handles Box.KeyPress
            If Char.IsControl(e.KeyChar) Then Return
            Dim text = Box.Text.Replace(",", "")
            Dim start = Math.Min(Box.SelectionStart, text.Length)
            Dim length = Math.Min(Box.SelectionLength, text.Length - start)
            Dim proposed = text.Substring(0, start) & e.KeyChar & text.Substring(start + length)
            If Not RawPattern.IsMatch(proposed) Then e.Handled = True
        End Sub

        Private Sub Box_TextChanged(sender As Object, e As EventArgs) Handles Box.TextChanged
            If _updating Then Return
            If Box.Focused Then
                Dim raw = Box.Text.Replace(",", "")
                If Not RawPattern.IsMatch(raw) Then
                    ' Pasted text: clean it up and keep the caret at the end.
                    _updating = True
                    Box.Text = Sanitize(raw)
                    Box.SelectionStart = Box.Text.Length
                    _updating = False
                End If
            End If
            RaiseIfChanged()
        End Sub

        Private Sub Box_Enter(sender As Object, e As EventArgs) Handles Box.Enter
            Dim v = Value
            If v.HasValue Then
                _updating = True
                Box.Text = RawText(v.Value)
                _updating = False
            End If
            Box.SelectAll()
            Invalidate()
        End Sub

        Private Sub Box_Leave(sender As Object, e As EventArgs) Handles Box.Leave
            Dim v = Value
            _updating = True
            Box.Text = If(v.HasValue, Formatted(v.Value), "")
            _updating = False
            Invalidate()
        End Sub

        Private Sub Box_HandleCreated(sender As Object, e As EventArgs) Handles Box.HandleCreated
            ApplyCueBanner()
        End Sub

        Private Sub Box_MouseEnter(sender As Object, e As EventArgs) Handles Box.MouseEnter
            SetHover(True)
        End Sub

        Private Sub Box_MouseLeave(sender As Object, e As EventArgs) Handles Box.MouseLeave
            SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)))
        End Sub

        Private Sub ApplyCueBanner()
            If Box.IsHandleCreated Then NativeMethods.SendMessage(Box.Handle, NativeMethods.EM_SETCUEBANNER, New IntPtr(1), _placeholder)
        End Sub

        ' ---------------------------------------------------------------- frame

        Private Sub SetHover(v As Boolean)
            If v = _hover Then Return
            _hover = v
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            SetHover(True)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)))
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Box.Focus()
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Box.Focus()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            If Box Is Nothing Then Return
            Box.BackColor = If(Enabled, Theme.Surface, Theme.AppBackground)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            If Box Is Nothing Then Return   ' the base constructor lays out before field initializers run
            Dim prefixWidth = TextRenderer.MeasureText("₱", Theme.FontBodySemibold, Size.Empty, TextFormatFlags.NoPadding).Width
            Dim left = PxI(14) + prefixWidth + PxI(8)
            Box.SetBounds(left, (Height - Box.PreferredHeight) \ 2 + PxI(1), Math.Max(10, Width - left - PxI(14)), Box.PreferredHeight)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim focused = Box.Focused
            Dim ringWidth = Px(3)
            Dim field As New RectangleF(ringWidth, ringWidth, Width - ringWidth * 2 - 1, Height - ringWidth * 2 - 1)
            Dim radius = Px(8)

            If _hasError OrElse focused Then
                Gfx.FillRounded(g, RectangleF.Inflate(field, ringWidth - Px(0.5F), ringWidth - Px(0.5F)), radius + ringWidth,
                                If(_hasError, Theme.OverdueSoft, Theme.BrandSoft))
            End If
            Gfx.FillRounded(g, field, radius, If(Enabled, Theme.Surface, Theme.AppBackground))
            Dim borderColor = If(_hasError, Theme.OverdueText, If(focused, Theme.Brand, If(_hover, Theme.BorderStrong, Theme.Border)))
            Gfx.DrawRounded(g, field, radius, borderColor, If(focused OrElse _hasError, Px(1.25F), 1.0F))

            TextRenderer.DrawText(g, "₱", Theme.FontBodySemibold, New Rectangle(PxI(14), 0, PxI(20), Height),
                                  If(focused, Theme.Brand, Theme.TextMuted), TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
        End Sub

        Private NotInheritable Class NativeMethods
            Public Const EM_SETCUEBANNER As Integer = &H1501

            <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
            Public Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, <MarshalAs(UnmanagedType.LPWStr)> lParam As String) As IntPtr
            End Function
        End Class

    End Class

End Namespace
