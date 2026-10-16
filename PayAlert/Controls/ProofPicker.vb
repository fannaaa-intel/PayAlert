Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' "Proof of payment" field for Record Payment: a field-sized box that opens a picture (a photo or screenshot of the
    ''' receipt). Empty, it has a dashed outline and says what to attach; with a picture, it shows a small preview,
    ''' the file name, and an × to remove it. Click, Enter or Space picks a file; Delete removes it.
    ''' Pictures are shrunk by ProofImage when picked; problems are reported through PickFailed for an inline note.
    ''' </summary>
    Public Class ProofPicker
        Inherits PAControl

        Private _proof As PreparedProof
        Private _thumb As Image
        Private _hover As Boolean
        Private _removeHover As Boolean
        Private _hasError As Boolean

        ''' <summary>Raised when a picture is attached or removed.</summary>
        Public Event ProofChanged As EventHandler
        ''' <summary>Raised with a friendly message when the picked file can't be used.</summary>
        Public Event PickFailed As EventHandler(Of String)

        Public Sub New()
            MyBase.New()
            SetStyle(ControlStyles.Selectable, True)
            TabStop = True
            Cursor = Cursors.Hand
            Size = New Size(240, 42)
        End Sub

        ''' <summary>The attached picture, ready to save, or Nothing.</summary>
        Public ReadOnly Property Proof As PreparedProof
            Get
                Return _proof
            End Get
        End Property

        Public ReadOnly Property HasProof As Boolean
            Get
                Return _proof IsNot Nothing
            End Get
        End Property

        Public Property HasError As Boolean
            Get
                Return _hasError
            End Get
            Set(value As Boolean)
                If value = _hasError Then Return
                _hasError = value
                Invalidate()
            End Set
        End Property

        ''' <summary>Shows the file dialog and attaches the chosen picture.</summary>
        Public Sub Pick()
            Using dialog As New OpenFileDialog() With {
                .Title = "Choose a photo or screenshot of the receipt",
                .Filter = ProofImage.FileFilter,
                .Multiselect = False,
                .CheckFileExists = True}
                If dialog.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                AttachFile(dialog.FileName)
            End Using
        End Sub

        ''' <summary>Attaches the picture at <paramref name="path"/> (also used by tests). Returns False if it can't be used.</summary>
        Public Function AttachFile(path As String) As Boolean
            Dim prepared As PreparedProof
            Try
                UseWaitCursor = True
                prepared = ProofImage.Prepare(path)
            Catch ex As ProofImageException
                RaiseEvent PickFailed(Me, ex.Message)
                Return False
            Finally
                UseWaitCursor = False
            End Try
            SetProof(prepared)
            Return True
        End Function

        Public Sub Clear()
            SetProof(Nothing)
        End Sub

        Private Sub SetProof(value As PreparedProof)
            _proof = value
            _thumb?.Dispose()
            _thumb = If(value Is Nothing, Nothing, ProofImage.ToImage(value.Data))
            _removeHover = False
            Invalidate()
            RaiseEvent ProofChanged(Me, EventArgs.Empty)
        End Sub

        ' ---------------------------------------------------------------- input

        Private ReadOnly Property RemoveRect As Rectangle
            Get
                Dim side = PxI(30)
                Return New Rectangle(Width - side - PxI(8), (Height - side) \ 2, side, side)
            End Get
        End Property

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            _removeHover = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim over = HasProof AndAlso RemoveRect.Contains(e.Location)
            If over <> _removeHover Then
                _removeHover = over
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If e.Button <> MouseButtons.Left Then Return
            Focus()
            If HasProof AndAlso RemoveRect.Contains(e.Location) Then Clear() Else Pick()
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Space OrElse keyData = Keys.Delete OrElse keyData = Keys.Back OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Select Case e.KeyCode
                Case Keys.Space
                    Pick()
                    e.Handled = True
                Case Keys.Delete, Keys.Back
                    If HasProof Then Clear()
                    e.Handled = True
            End Select
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        ' ---------------------------------------------------------------- painting

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.HighQuality(g)
            Dim ring = Px(3)
            Dim field As New RectangleF(ring, ring, Width - ring * 2 - 1, Height - ring * 2 - 1)
            Dim radius = Px(9)
            Dim focusedNow = Focused

            If _hasError OrElse focusedNow Then
                Gfx.FillRounded(g, RectangleF.Inflate(field, ring - Px(0.5F), ring - Px(0.5F)), radius + ring,
                                If(_hasError, Theme.OverdueSoft, Theme.BrandSoft))
            End If
            Gfx.FillRounded(g, field, radius, If(HasProof OrElse Not _hover, Theme.Surface, Theme.Blend(Theme.Surface, Theme.BrandSoft, 0.6)))
            Dim border = If(_hasError, Theme.OverdueText, If(focusedNow, Theme.Brand, If(_hover, Theme.BorderStrong, Theme.CardBorder)))
            Using path = Gfx.RoundedRect(field, radius)
                Using pen As New Pen(border, If(focusedNow OrElse _hasError, Px(1.25F), 1.0F))
                    ' Dashed while empty: a drop-zone look that says "add something here".
                    If Not HasProof Then
                        pen.DashStyle = Drawing2D.DashStyle.Custom
                        pen.DashPattern = {4.0F, 3.0F}
                    End If
                    g.DrawPath(pen, path)
                End Using
            End Using

            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis
            If Not HasProof Then
                Dim iconSize = Px(18)
                Dim tint = If(_hover OrElse focusedNow, Theme.Brand, Theme.TextMuted)
                Icons.Draw(g, IconKind.Image, New RectangleF(Px(14), (Height - iconSize) / 2.0F, iconSize, iconSize), tint, Px(1.6F))
                Dim textX = PxI(14 + 18 + 10)
                TextRenderer.DrawText(g, "Attach a photo or screenshot", Theme.FontBody, New Rectangle(textX, 0, Width - textX - PxI(10), Height),
                                      If(_hover OrElse focusedNow, Theme.TextPrimary, Theme.TextMuted), flags)
                Return
            End If

            ' Preview, file name and size, and the remove button.
            Dim thumbSide = Height - PxI(14)
            Dim thumbRect As New RectangleF(Px(8), (Height - thumbSide) / 2.0F, thumbSide, thumbSide)
            If _thumb IsNot Nothing Then
                ' The whole picture, fitted on a soft square, so a mostly white receipt still reads as a page.
                Gfx.FillRounded(g, thumbRect, Px(5), Theme.NeutralSoft)
                Dim area = RectangleF.Inflate(thumbRect, -Px(2), -Px(2))
                Dim k = Math.Min(area.Width / _thumb.Width, area.Height / _thumb.Height)
                Dim w = _thumb.Width * k, h = _thumb.Height * k
                Dim picture As New RectangleF(area.X + (area.Width - w) / 2.0F, area.Y + (area.Height - h) / 2.0F, w, h)
                g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                g.DrawImage(_thumb, picture)
                Using edge As New Pen(Theme.BorderStrong)
                    g.DrawRectangle(edge, picture.X, picture.Y, picture.Width, picture.Height)
                End Using
            End If
            Dim textLeft = CInt(thumbRect.Right + Px(10))
            Dim remove = RemoveRect
            Dim sizeText = "  ·  " & Math.Max(1, _proof.Data.Length \ 1024) & " KB"
            Dim sizeWidth = TextRenderer.MeasureText(g, sizeText, Theme.FontCaption, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width
            Dim nameWidth = Math.Max(0, remove.Left - PxI(6) - textLeft - sizeWidth)
            Dim nameSize = TextRenderer.MeasureText(g, _proof.FileName, Theme.FontBodySemibold, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            Dim shownName = Math.Min(nameWidth, nameSize.Width + 1)
            TextRenderer.DrawText(g, _proof.FileName, Theme.FontBodySemibold, New Rectangle(textLeft, 0, shownName, Height), Theme.TextPrimary, flags)
            TextRenderer.DrawText(g, sizeText, Theme.FontCaption, New Rectangle(textLeft + shownName, 0, sizeWidth + 1, Height), Theme.TextMuted, flags)

            If _removeHover Then Gfx.FillRounded(g, remove, Px(7), Theme.OverdueSoft)
            Dim x = Px(14)
            Icons.Draw(g, IconKind.Close, New RectangleF(remove.X + (remove.Width - x) / 2.0F, remove.Y + (remove.Height - x) / 2.0F, x, x),
                       If(_removeHover, Theme.OverdueText, Theme.TextMuted), Px(1.6F))
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _thumb?.Dispose()
                _thumb = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
