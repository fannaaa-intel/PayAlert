Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' PayAlert text field: rounded frame, optional leading line icon, placeholder, Brand focus ring,
    ''' red error state, and for passwords a show/hide eye on the right.
    ''' </summary>
    <DefaultEvent("TextChanged")>
    Public Class PATextBox
        Inherits PAControl

        Private WithEvents Box As New TextBox()
        Private _icon As IconKind = IconKind.None
        Private _isPassword As Boolean
        Private _revealed As Boolean
        Private _hasError As Boolean
        Private _placeholder As String = ""
        Private _hover As Boolean
        Private _eyeHover As Boolean

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, False)
            Cursor = Cursors.IBeam
            Box.BorderStyle = BorderStyle.None
            Box.BackColor = Theme.Surface
            Box.ForeColor = Theme.TextPrimary
            Box.Font = Theme.FontBody
            Controls.Add(Box)
            Size = New Size(320, 40)
        End Sub

        <Browsable(True), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
        Public Overrides Property Text As String
            Get
                Return If(Box Is Nothing, "", Box.Text)
            End Get
            Set(value As String)
                If Box IsNot Nothing Then Box.Text = If(value, "")
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(GetType(IconKind), "None")>
        Public Property Icon As IconKind
            Get
                Return _icon
            End Get
            Set(value As IconKind)
                _icon = value
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>Masks the text and shows the show/hide eye.</summary>
        <Category("PayAlert"), DefaultValue(False)>
        Public Property IsPassword As Boolean
            Get
                Return _isPassword
            End Get
            Set(value As Boolean)
                _isPassword = value
                _revealed = False
                ApplyMask()
                PerformLayout()
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue(False)>
        Public Property HasError As Boolean
            Get
                Return _hasError
            End Get
            Set(value As Boolean)
                If _hasError = value Then Return
                _hasError = value
                Invalidate()
            End Set
        End Property

        <Category("PayAlert"), DefaultValue("")>
        Public Property PlaceholderText As String
            Get
                Return _placeholder
            End Get
            Set(value As String)
                _placeholder = If(value, "")
                ApplyCueBanner()
            End Set
        End Property

        ''' <summary>Several lines of text (for notes); the field grows with its Height.</summary>
        <Category("Behavior"), DefaultValue(False)>
        Public Property Multiline As Boolean
            Get
                Return Box.Multiline
            End Get
            Set(value As Boolean)
                Box.Multiline = value
                Box.WordWrap = True
                Box.ScrollBars = ScrollBars.None
                Box.AcceptsReturn = value
                PerformLayout()
            End Set
        End Property

        <Category("Behavior"), DefaultValue(32767)>
        Public Property MaxLength As Integer
            Get
                Return Box.MaxLength
            End Get
            Set(value As Integer)
                Box.MaxLength = value
            End Set
        End Property

        <Browsable(False)>
        Public ReadOnly Property InnerTextBox As TextBox
            Get
                Return Box
            End Get
        End Property

        Public Sub SelectAll()
            Box.SelectAll()
        End Sub

        Public Shadows Sub Focus()
            Box.Focus()
        End Sub

        ' ---------------------------------------------------------------- inner box

        Private Sub ApplyMask()
            If Box Is Nothing Then Return
            Box.UseSystemPasswordChar = _isPassword AndAlso Not _revealed
        End Sub

        Private Sub ApplyCueBanner()
            If Box IsNot Nothing AndAlso Box.IsHandleCreated Then
                NativeMethods.SendMessage(Box.Handle, NativeMethods.EM_SETCUEBANNER, New IntPtr(1), _placeholder)
            End If
        End Sub

        Private Sub Box_HandleCreated(sender As Object, e As EventArgs) Handles Box.HandleCreated
            ApplyCueBanner()
        End Sub

        Private Sub Box_TextChanged(sender As Object, e As EventArgs) Handles Box.TextChanged
            OnTextChanged(e)
        End Sub

        Private Sub Box_FocusChanged(sender As Object, e As EventArgs) Handles Box.Enter, Box.Leave
            Invalidate()
        End Sub

        Private Sub Box_KeyDown(sender As Object, e As KeyEventArgs) Handles Box.KeyDown
            OnKeyDown(e)
        End Sub

        Private Sub Box_MouseEnter(sender As Object, e As EventArgs) Handles Box.MouseEnter
            SetHover(True)
        End Sub

        Private Sub Box_MouseLeave(sender As Object, e As EventArgs) Handles Box.MouseLeave
            SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)))
        End Sub

        ' ---------------------------------------------------------------- frame and eye

        Private ReadOnly Property EyeRect As Rectangle
            Get
                If Not _isPassword Then Return Rectangle.Empty
                Dim w = PxI(38)
                Return New Rectangle(Width - w - PxI(3), 0, w, Height)
            End Get
        End Property

        Private Sub SetHover(value As Boolean)
            If value = _hover Then Return
            _hover = value
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            SetHover(True)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _eyeHover = False
            SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)))
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim overEye = EyeRect.Contains(e.Location)
            If overEye <> _eyeHover Then
                _eyeHover = overEye
                Cursor = If(overEye, Cursors.Hand, Cursors.IBeam)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If _isPassword AndAlso EyeRect.Contains(e.Location) Then
                _revealed = Not _revealed
                Dim caret = Box.SelectionStart
                ApplyMask()
                Box.SelectionStart = caret
                Invalidate()
            End If
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
            Dim left = If(_icon = IconKind.None, PxI(14), PxI(14 + 18 + 10))
            Dim right = If(_isPassword, EyeRect.Width + PxI(4), PxI(14))
            If Box.Multiline Then
                Box.SetBounds(left, PxI(11), Math.Max(10, Width - left - right), Math.Max(10, Height - PxI(22)))
            Else
                Box.SetBounds(left, (Height - Box.PreferredHeight) \ 2 + PxI(1), Math.Max(10, Width - left - right), Box.PreferredHeight)
            End If
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim focusedNow = Box.Focused
            Dim ring = Px(3)
            Dim field As New RectangleF(ring, ring, Width - ring * 2 - 1, Height - ring * 2 - 1)
            Dim radius = Px(9)

            If _hasError OrElse focusedNow Then
                Gfx.FillRounded(g, RectangleF.Inflate(field, ring - Px(0.5F), ring - Px(0.5F)), radius + ring,
                                If(_hasError, Theme.OverdueSoft, Theme.BrandSoft))
            End If
            Gfx.FillRounded(g, field, radius, If(Enabled, Theme.Surface, Theme.AppBackground))
            Dim border = If(_hasError, Theme.OverdueText, If(focusedNow, Theme.Brand, If(_hover, Theme.BorderStrong, Theme.CardBorder)))
            Gfx.DrawRounded(g, field, radius, border, If(focusedNow OrElse _hasError, Px(1.25F), 1.0F))

            If _icon <> IconKind.None Then
                Dim size = Px(18)
                Icons.Draw(g, _icon, New RectangleF(Px(14), (Height - size) / 2.0F, size, size),
                           If(_hasError, Theme.OverdueText, If(focusedNow, Theme.Brand, Theme.TextMuted)), Px(1.6F))
            End If

            If _isPassword Then
                Dim eye = EyeRect
                Dim size = Px(18)
                Icons.Draw(g, If(_revealed, IconKind.EyeOff, IconKind.Eye),
                           New RectangleF(eye.X + (eye.Width - size) / 2.0F, (Height - size) / 2.0F, size, size),
                           If(_eyeHover, Theme.TextPrimary, Theme.TextMuted), Px(1.6F))
            End If
        End Sub

        Private NotInheritable Class NativeMethods
            Public Const EM_SETCUEBANNER As Integer = &H1501

            <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
            Public Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, <MarshalAs(UnmanagedType.LPWStr)> lParam As String) As IntPtr
            End Function
        End Class

    End Class

End Namespace
