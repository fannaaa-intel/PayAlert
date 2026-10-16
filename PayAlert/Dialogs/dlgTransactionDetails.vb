Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Dialogs

    ''' <summary>
    ''' One recorded payment or collection, to check it against the receipt: amount, date, method, reference number,
    ''' who paid and the note on the left; the proof picture on the right ("View full size" opens it in the Windows
    ''' picture viewer). Opened by clicking a row in Payment History (details screen) or History. Use <see cref="Run"/>.
    ''' </summary>
    Public NotInheritable Class dlgTransactionDetails
        Inherits Panel
        Implements IDialogBody

        Private Const PreviewWidth As Integer = 196
        Private Const RowHeight As Integer = 30

        Private ReadOnly _txn As PaymentTransaction
        Private ReadOnly _preview As New ProofPreview()
        Private ReadOnly _viewFull As New PAButton()
        Private _proof As ProofFile

        Public Event CanSubmitChanged As EventHandler Implements IDialogBody.CanSubmitChanged

        ''' <summary>
        ''' Shows <paramref name="txn"/>. With <paramref name="offerOpenPayment"/>, a second button opens its payment:
        ''' returns True when the user chose it.
        ''' </summary>
        Public Shared Function Run(owner As Control, txn As PaymentTransaction, Optional offerOpenPayment As Boolean = False) As Boolean
            Dim body As New dlgTransactionDetails(txn)
            Dim receivable = txn.IsReceivable
            Dim title = If(receivable, "Collection record", "Payment record")
            Dim message = """" & txn.PaymentName & """  ·  " & Format.Money(txn.Amount) & If(receivable, " received on ", " paid on ") & Format.ShortDate(txn.PaidOn)
            Dim choice = PADialog.ShowForm(owner, Tone.Brand, IconKind.Hash, title, message, body, "Done",
                                           If(offerOpenPayment AndAlso Not txn.PaymentDeleted, "Open Payment", Nothing))
            Return choice = DialogChoice.Secondary
        End Function

        Private Sub New(txn As PaymentTransaction)
            _txn = txn
            BackColor = Theme.Surface
            Font = Theme.FontBody

            Dim inner = Theme.Px(540 - 48)
            Dim previewW = Theme.Px(PreviewWidth)
            Dim leftW = inner - previewW - Theme.Px(20)
            Dim receivable = txn.IsReceivable

            Dim rows As New List(Of KeyValuePair(Of String, String)) From {
                Row(If(receivable, "Amount received", "Amount paid"), Format.Money(txn.Amount)),
                Row(If(receivable, "Date received", "Date paid"), Format.ShortDate(txn.PaidOn)),
                Row("Method", Dash(txn.Method)),
                Row("Reference no.", Dash(txn.ReferenceNo)),
                Row(If(receivable, "Received from", "Paid by"), Dash(txn.PayerName)),
                Row("Note", Dash(txn.Note))}
            Dim y = 0
            For Each pair In rows
                AddRow(pair.Key, pair.Value, y, leftW)
                y += Theme.Px(RowHeight)
            Next

            _preview.SetBounds(inner - previewW, 0, previewW, previewW)
            _viewFull.Kind = ButtonKind.Ghost
            _viewFull.Icon = IconKind.Image
            _viewFull.Text = "View full size"
            _viewFull.SetBounds(inner - previewW, _preview.Bottom + Theme.Px(6), previewW, Theme.Px(Theme.ButtonHeight))
            _viewFull.Visible = False
            AddHandler _viewFull.Click, AddressOf ViewFullClicked
            Controls.Add(_preview)
            Controls.Add(_viewFull)
            Size = New Size(inner, Math.Max(y, _viewFull.Bottom))

            LoadProof()
        End Sub

        Private Shared Function Row(label As String, value As String) As KeyValuePair(Of String, String)
            Return New KeyValuePair(Of String, String)(label, value)
        End Function

        Private Shared Function Dash(value As String) As String
            Return If(String.IsNullOrWhiteSpace(value), "—", value)
        End Function

        Private Sub AddRow(caption As String, value As String, y As Integer, width As Integer)
            Dim captionWidth = Theme.Px(If(Theme.IsLargeText, 140, 120))
            Controls.Add(New Label() With {
                .Text = caption, .Font = Theme.FontBody, .ForeColor = Theme.TextMuted, .BackColor = Theme.Surface,
                .AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft,
                .Bounds = New Rectangle(0, y, captionWidth, Theme.Px(RowHeight))})
            Controls.Add(New Label() With {
                .Text = value, .Font = Theme.FontBodySemibold, .ForeColor = Theme.TextPrimary, .BackColor = Theme.Surface,
                .AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft, .UseMnemonic = False,
                .Bounds = New Rectangle(captionWidth, y, width - captionWidth, Theme.Px(RowHeight))})
        End Sub

        Private Sub LoadProof()
            If Not _txn.HasProof Then
                _preview.Message = "No proof attached"
                Return
            End If
            Try
                _proof = ProofRepo.Get(_txn.ProofId.Value, Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[dlgTransactionDetails] " & ex.ToString())
                _preview.Message = "Can't load the picture"
                Return
            End Try
            If _proof Is Nothing Then
                _preview.Message = "No proof attached"
                Return
            End If
            _preview.Picture = ProofImage.ToImage(_proof.Data)
            _preview.Message = If(_preview.Picture Is Nothing, "Can't show the picture", "")
            _viewFull.Visible = _preview.Picture IsNot Nothing
        End Sub

        ''' <summary>Opens the picture in the Windows picture viewer, from a copy in the temp folder.</summary>
        Private Sub ViewFullClicked(sender As Object, e As EventArgs)
            If _proof Is Nothing Then Return
            Try
                Dim folder = Path.Combine(Path.GetTempPath(), "PayAlert")
                Directory.CreateDirectory(folder)
                Dim file = Path.Combine(folder, "proof-" & _proof.ProofId & ".jpg")
                IO.File.WriteAllBytes(file, _proof.Data)
                Process.Start(New ProcessStartInfo(file) With {.UseShellExecute = True})
            Catch ex As Exception
                Debug.WriteLine("[dlgTransactionDetails] " & ex.ToString())
                PADialog.Error(Me, "Can't open the picture", "Windows couldn't open a picture viewer. The proof is still saved in PayAlert.")
            End Try
        End Sub

        ' ---------------------------------------------------------------- IDialogBody (read-only: always ready)

        Public ReadOnly Property CanSubmit As Boolean Implements IDialogBody.CanSubmit
            Get
                Return True
            End Get
        End Property

        Public Sub FocusFirstField() Implements IDialogBody.FocusFirstField
        End Sub

        Public Function Submit() As Boolean Implements IDialogBody.Submit
            Return True
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then _preview.Picture?.Dispose()
            MyBase.Dispose(disposing)
        End Sub

        ''' <summary>The proof picture fitted in a rounded frame, or a short message in a dashed frame when there is none.</summary>
        Private NotInheritable Class ProofPreview
            Inherits PAControl

            Private _picture As Image
            Private _message As String = ""

            Public Sub New()
                MyBase.New()
                SetStyle(ControlStyles.Selectable, False)
                TabStop = False
            End Sub

            Public Property Picture As Image
                Get
                    Return _picture
                End Get
                Set(value As Image)
                    _picture = value
                    Invalidate()
                End Set
            End Property

            Public Property Message As String
                Get
                    Return _message
                End Get
                Set(value As String)
                    _message = If(value, "")
                    Invalidate()
                End Set
            End Property

            Protected Overrides Sub OnPaint(e As PaintEventArgs)
                Dim g = e.Graphics
                Gfx.HighQuality(g)
                Dim frame As New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F)
                Dim radius = Px(10)
                Gfx.FillRounded(g, frame, radius, Theme.AppBackground)
                If _picture IsNot Nothing Then
                    ' Fit the whole receipt inside the frame (no cropping: every number must be visible).
                    Dim pad = Px(6)
                    Dim area = RectangleF.Inflate(frame, -pad, -pad)
                    Dim k = Math.Min(area.Width / _picture.Width, area.Height / _picture.Height)
                    Dim w = _picture.Width * k, h = _picture.Height * k
                    g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                    g.DrawImage(_picture, New RectangleF(area.X + (area.Width - w) / 2.0F, area.Y + (area.Height - h) / 2.0F, w, h))
                    Gfx.DrawRounded(g, frame, radius, Theme.CardBorder, 1.0F)
                    Return
                End If
                Using path = Gfx.RoundedRect(frame, radius)
                    Using pen As New Pen(Theme.BorderStrong, 1.0F)
                        pen.DashStyle = Drawing2D.DashStyle.Custom
                        pen.DashPattern = {4.0F, 3.0F}
                        g.DrawPath(pen, path)
                    End Using
                End Using
                Dim iconSize = Px(28)
                Icons.Draw(g, IconKind.Image, New RectangleF((Width - iconSize) / 2.0F, Height / 2.0F - iconSize - Px(4), iconSize, iconSize), Theme.TextMuted, Px(1.6F))
                TextRenderer.DrawText(g, _message, Theme.FontCaption, New Rectangle(PxI(8), CInt(Height / 2.0F + Px(4)), Width - PxI(16), PxI(40)),
                                      Theme.TextMuted, TextFormatFlags.HorizontalCenter Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)
            End Sub

        End Class

    End Class

End Namespace
