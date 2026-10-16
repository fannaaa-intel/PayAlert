Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Controls

    ''' <summary>
    ''' Recorded payments and collections. Full mode (ucHistory): Date · Payment · Direction · Amount · Method / Reference · Note.
    ''' Compact mode (Payment Details): Date · Amount · Method / Reference · Note. The reference number shows under the
    ''' method, and a picture mark before the note means a proof is attached. Styled and scrolled like PaymentGrid:
    ''' custom-drawn cells, horizontal rules only, no visible scrollbar (wheel and keys scroll, the bottom fades when more is below).
    ''' Click a header to sort; one click on a row or Enter raises TransactionActivated (the record's details).
    ''' </summary>
    Public Class TransactionGrid
        Inherits DataGridView

        Private Const ColDate As String = "date"
        Private Const ColPayment As String = "payment"
        Private Const ColDirection As String = "direction"
        Private Const ColAmount As String = "amount"
        Private Const ColMethod As String = "method"
        Private Const ColNote As String = "note"

        Private _compact As Boolean
        Private _rows As New List(Of PaymentTransaction)
        Private _sortColumn As String = ColDate
        Private _sortAscending As Boolean
        Private _hoverRow As Integer = -1

        ''' <summary>Raised on a click or Enter on a row.</summary>
        Public Event TransactionActivated As EventHandler(Of PaymentTransaction)

        Public Sub New()
            DoubleBuffered = True
            BorderStyle = BorderStyle.None
            BackgroundColor = Theme.Surface
            GridColor = Theme.Border
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
            RowHeadersVisible = False
            AllowUserToAddRows = False
            AllowUserToDeleteRows = False
            AllowUserToResizeRows = False
            AllowUserToResizeColumns = False
            AllowUserToOrderColumns = False
            Me.ReadOnly = True
            MultiSelect = False
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
            ScrollBars = ScrollBars.None
            EditMode = DataGridViewEditMode.EditProgrammatically
            EnableHeadersVisualStyles = False
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            StandardTab = True

            ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface
            ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextMuted
            ColumnHeadersDefaultCellStyle.Font = Theme.FontCaptionBold
            ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface
            DefaultCellStyle.BackColor = Theme.Surface
            DefaultCellStyle.ForeColor = Theme.TextPrimary
            DefaultCellStyle.Font = Theme.FontBody
            DefaultCellStyle.SelectionBackColor = Theme.BrandSoft
            DefaultCellStyle.SelectionForeColor = Theme.TextPrimary

            BuildColumns()
        End Sub

        ''' <summary>Compact mode drops the Payment and Direction columns (for one payment's own history).</summary>
        <System.ComponentModel.Category("PayAlert"), System.ComponentModel.DefaultValue(False)>
        Public Property Compact As Boolean
            Get
                Return _compact
            End Get
            Set(value As Boolean)
                If value = _compact Then Return
                _compact = value
                BuildColumns()
                Rebuild()
            End Set
        End Property

        Private Sub BuildColumns()
            Columns.Clear()
            AddColumn(ColDate, "DATE", 150, 0, DataGridViewContentAlignment.MiddleLeft)
            If Not _compact Then
                AddColumn(ColPayment, "PAYMENT", 0, 28, DataGridViewContentAlignment.MiddleLeft)
                AddColumn(ColDirection, "DIRECTION", 140, 0, DataGridViewContentAlignment.MiddleLeft)
            End If
            AddColumn(ColAmount, "AMOUNT", 0, If(_compact, 18, 14), DataGridViewContentAlignment.MiddleRight)
            AddColumn(ColMethod, "METHOD / REF.", 0, If(_compact, 26, 19), DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColNote, "NOTE", 0, If(_compact, 36, 24), DataGridViewContentAlignment.MiddleLeft)
            Cursor = Cursors.Hand
            If IsHandleCreated Then ApplyScale()
        End Sub

        Private Sub AddColumn(name As String, header As String, fixedWidth As Integer, fillWeight As Single, align As DataGridViewContentAlignment)
            Dim column As New DataGridViewTextBoxColumn() With {
                .Name = name, .HeaderText = header, .SortMode = DataGridViewColumnSortMode.Programmatic, .ReadOnly = True, .Tag = fixedWidth}
            column.DefaultCellStyle.Alignment = align
            If fixedWidth > 0 Then
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                column.Width = fixedWidth
            Else
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                column.FillWeight = fillWeight
            End If
            Columns.Add(column)
        End Sub

        Private ReadOnly Property S As Single
            Get
                Return DeviceDpi / 96.0F
            End Get
        End Property

        Private ReadOnly Property RowPixels As Integer
            Get
                ' Room for the method and its reference number on two lines (taller with Large text).
                Return CInt(If(_compact, If(Theme.IsLargeText, 50, 42), If(Theme.IsLargeText, 58, 52)) * S)
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ApplyScale()
        End Sub

        ' DataGridView row, header and fixed column sizes are not auto-scaled; size them for this screen.
        Private Sub ApplyScale()
            RowTemplate.Height = RowPixels
            ColumnHeadersHeight = CInt(If(_compact, 34, 40) * S)
            For Each column As DataGridViewColumn In Columns
                Dim fixedWidth = CInt(column.Tag)
                If fixedWidth > 0 Then column.Width = CInt(fixedWidth * S)
            Next
            For Each row As DataGridViewRow In Rows
                row.Height = RowPixels
            Next
        End Sub

        ' ---------------------------------------------------------------- data and sorting

        Public Sub SetRows(items As IEnumerable(Of PaymentTransaction))
            _rows = New List(Of PaymentTransaction)(items)
            Rebuild()
        End Sub

        Private Function SortedRows() As IEnumerable(Of PaymentTransaction)
            Dim key As Func(Of PaymentTransaction, Object)
            Select Case _sortColumn
                Case ColPayment : key = Function(t) t.PaymentName.ToLowerInvariant()
                Case ColDirection : key = Function(t) t.Direction
                Case ColAmount : key = Function(t) t.Amount
                Case ColMethod : key = Function(t) t.Method.ToLowerInvariant()
                Case ColNote : key = Function(t) t.Note.ToLowerInvariant()
                Case Else : key = Function(t) t.PaidOn
            End Select
            ' Ties fall back to newest first, so the order is always stable.
            Dim ordered = If(_sortAscending, _rows.OrderBy(key), _rows.OrderByDescending(key))
            Return ordered.ThenByDescending(Function(t) t.PaidOn).ThenByDescending(Function(t) t.TxnId)
        End Function

        Private Sub Rebuild()
            SuspendLayout()
            Rows.Clear()
            For Each t In SortedRows()
                Dim index = Rows.Add()
                Rows(index).Tag = t
                If IsHandleCreated Then Rows(index).Height = RowPixels
            Next
            For Each column As DataGridViewColumn In Columns
                column.HeaderCell.SortGlyphDirection = SortOrder.None
            Next
            ResumeLayout()
            CurrentCell = Nothing
            ClearSelection()
            If RowCount > 0 Then FirstDisplayedScrollingRowIndex = 0
            Invalidate()
        End Sub

        Protected Overrides Sub OnVisibleChanged(e As EventArgs)
            MyBase.OnVisibleChanged(e)
            ' A grid that becomes visible selects its first row by itself; start with nothing selected.
            If Visible Then
                CurrentCell = Nothing
                ClearSelection()
            End If
        End Sub

        Protected Overrides Sub OnSelectionChanged(e As EventArgs)
            MyBase.OnSelectionChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnColumnHeaderMouseClick(e As DataGridViewCellMouseEventArgs)
            MyBase.OnColumnHeaderMouseClick(e)
            Dim name = Columns(e.ColumnIndex).Name
            If name = _sortColumn Then
                _sortAscending = Not _sortAscending
            Else
                _sortColumn = name
                ' Dates and amounts start with the newest / largest; text starts at A.
                _sortAscending = Not (name = ColDate OrElse name = ColAmount)
            End If
            Rebuild()
        End Sub

        ' ---------------------------------------------------------------- input

        Protected Overrides Sub OnCellClick(e As DataGridViewCellEventArgs)
            MyBase.OnCellClick(e)
            If e.RowIndex >= 0 Then Activate(e.RowIndex)
        End Sub

        Protected Overrides Function ProcessDataGridViewKey(e As KeyEventArgs) As Boolean
            If e.KeyCode = Keys.Enter AndAlso CurrentRow IsNot Nothing Then
                Activate(CurrentRow.Index)
                Return True
            End If
            Return MyBase.ProcessDataGridViewKey(e)
        End Function

        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If keyData = Keys.Enter AndAlso CurrentRow IsNot Nothing Then
                Activate(CurrentRow.Index)
                Return True
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        Private Sub Activate(rowIndex As Integer)
            Dim t = TryCast(Rows(rowIndex).Tag, PaymentTransaction)
            If t IsNot Nothing Then RaiseEvent TransactionActivated(Me, t)
        End Sub

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            ' No visible scrollbar: scroll three rows per notch.
            If RowCount = 0 Then Return
            Dim target = FirstDisplayedScrollingRowIndex - Math.Sign(e.Delta) * 3
            FirstDisplayedScrollingRowIndex = Math.Max(0, Math.Min(RowCount - 1, target))
            Invalidate()
        End Sub

        Protected Overrides Sub OnCellMouseEnter(e As DataGridViewCellEventArgs)
            MyBase.OnCellMouseEnter(e)
            If e.RowIndex <> _hoverRow Then
                Dim old = _hoverRow
                _hoverRow = e.RowIndex
                If old >= 0 AndAlso old < RowCount Then InvalidateRow(old)
                If _hoverRow >= 0 Then InvalidateRow(_hoverRow)
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _hoverRow >= 0 AndAlso _hoverRow < RowCount Then InvalidateRow(_hoverRow)
            _hoverRow = -1
        End Sub

        ' ---------------------------------------------------------------- painting

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If RowCount = 0 Then Return
            Dim lastVisible = FirstDisplayedScrollingRowIndex + DisplayedRowCount(False) - 1
            If lastVisible >= RowCount - 1 Then Return
            ' More rows below and no scrollbar: fade the bottom edge and show a small "more" chevron.
            Dim h = CInt(32 * S)
            Dim band As New Rectangle(0, ClientSize.Height - h, ClientSize.Width, h)
            Using fade As New Drawing2D.LinearGradientBrush(New Rectangle(band.X, band.Y - 1, band.Width, band.Height + 2),
                                                            Color.FromArgb(0, Theme.Surface), Theme.Surface, Drawing2D.LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(fade, band)
            End Using
            Gfx.HighQuality(e.Graphics)
            Dim size = 16 * S
            Icons.Draw(e.Graphics, IconKind.ChevronDown, New RectangleF((ClientSize.Width - size) / 2.0F, ClientSize.Height - size - 4 * S, size, size), Theme.TextMuted, 1.8F * S)
        End Sub

        Protected Overrides Sub OnScroll(e As ScrollEventArgs)
            MyBase.OnScroll(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnCellPainting(e As DataGridViewCellPaintingEventArgs)
            MyBase.OnCellPainting(e)
            If e.ColumnIndex < 0 Then Return
            If e.RowIndex = -1 Then
                PaintHeader(e)
            ElseIf e.RowIndex >= 0 Then
                PaintCell(e)
            End If
            e.Handled = True
        End Sub

        Private Sub PaintHeader(e As DataGridViewCellPaintingEventArgs)
            Dim column = Columns(e.ColumnIndex)
            RowPainter.PaintHeader(e, column, column.Name = _sortColumn, _sortAscending, S)
        End Sub

        Private Sub PaintCell(e As DataGridViewCellPaintingEventArgs)
            Dim g = e.Graphics
            Dim t = TryCast(Rows(e.RowIndex).Tag, PaymentTransaction)
            If t Is Nothing Then Return
            Dim selected = (e.State And DataGridViewElementStates.Selected) <> 0
            Dim back = If(selected, Theme.BrandSoft, If(e.RowIndex = _hoverRow, Theme.Blend(Theme.Surface, Theme.AppBackground, 0.7), Theme.Surface))
            Using fill As New SolidBrush(back)
                g.FillRectangle(fill, e.CellBounds)
            End Using
            Using rule As New Pen(Theme.Border)
                g.DrawLine(rule, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1)
            End Using

            Dim column = Columns(e.ColumnIndex).Name
            Dim pad = CInt(If(e.ColumnIndex = 0, 14, 10) * S)
            Dim r = New Rectangle(e.CellBounds.X + pad, e.CellBounds.Y, e.CellBounds.Width - pad * 2, e.CellBounds.Height - 1)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            Select Case column
                Case ColDate
                    Gfx.HighQuality(g)
                    Dim iconSize = 15 * S
                    Icons.Draw(g, IconKind.Calendar, New RectangleF(r.X, r.Y + (r.Height - iconSize) / 2.0F, iconSize, iconSize), Theme.TextMuted, 1.5F * S)
                    Dim textX = r.X + CInt(iconSize + 8 * S)
                    TextRenderer.DrawText(g, Format.ShortDate(t.PaidOn), Theme.FontBody, New Rectangle(textX, r.Y, r.Right - textX, r.Height), Theme.TextPrimary, flags)
                Case ColPayment
                    If t.PaymentDeleted Then
                        ' Two lines: the name, and a muted note that the payment was deleted later.
                        Dim lineHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontBodyBold, Size.Empty, flags).Height
                        Dim top = r.Y + (r.Height - lineHeight - CInt(16 * S)) \ 2
                        TextRenderer.DrawText(g, t.PaymentName, Theme.FontBodyBold, New Rectangle(r.X, top, r.Width, lineHeight), Theme.TextMuted, flags)
                        TextRenderer.DrawText(g, "Payment deleted", Theme.FontCaption, New Rectangle(r.X, top + lineHeight + CInt(1 * S), r.Width, CInt(16 * S)), Theme.TextMuted, flags)
                    Else
                        TextRenderer.DrawText(g, t.PaymentName, Theme.FontBodyBold, r, Theme.TextPrimary, flags)
                    End If
                Case ColDirection
                    Gfx.HighQuality(g)
                    Dim arrow = 13 * S
                    Icons.Draw(g, If(t.IsReceivable, IconKind.ArrowIn, IconKind.ArrowOut), New RectangleF(r.X, r.Y + (r.Height - arrow) / 2.0F, arrow, arrow), Theme.TextMuted, 1.5F * S)
                    Dim textX = r.X + CInt(arrow + 6 * S)
                    TextRenderer.DrawText(g, t.DirectionLabel, Theme.FontBody, New Rectangle(textX, r.Y, r.Right - textX, r.Height), Theme.TextPrimary, flags)
                Case ColAmount
                    TextRenderer.DrawText(g, Format.Money(t.Amount), Theme.FontBodyBold, r, Theme.TextPrimary, flags Or TextFormatFlags.Right)
                Case ColMethod
                    Dim methodText = If(t.Method.Length = 0, "—", t.Method)
                    Dim methodColor = If(t.Method.Length = 0, Theme.TextMuted, Theme.TextPrimary)
                    If t.ReferenceNo.Length = 0 Then
                        TextRenderer.DrawText(g, methodText, Theme.FontBody, r, methodColor, flags)
                    Else
                        ' Two lines: the method, and the reference number under it.
                        ' Measure without EndEllipsis (it makes MeasureText return almost nothing).
                        Dim measure = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
                        Dim lineHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontBody, Size.Empty, measure).Height
                        Dim refHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontCaption, Size.Empty, measure).Height
                        Dim top = r.Y + (r.Height - lineHeight - refHeight) \ 2
                        TextRenderer.DrawText(g, methodText, Theme.FontBody, New Rectangle(r.X, top, r.Width, lineHeight), methodColor, flags)
                        TextRenderer.DrawText(g, "Ref. " & t.ReferenceNo, Theme.FontCaption, New Rectangle(r.X, top + lineHeight, r.Width, refHeight),
                                              Theme.TextMuted, flags)
                    End If
                Case ColNote
                    Dim noteRect = r
                    If t.HasProof Then
                        ' A small picture mark: this record has a proof of payment.
                        Gfx.HighQuality(g)
                        Dim iconSize = 16 * S
                        Icons.Draw(g, IconKind.Image, New RectangleF(r.X, r.Y + (r.Height - iconSize) / 2.0F, iconSize, iconSize), Theme.TextMuted, 1.5F * S)
                        Dim textX = r.X + CInt(iconSize + 8 * S)
                        noteRect = New Rectangle(textX, r.Y, r.Right - textX, r.Height)
                    End If
                    TextRenderer.DrawText(g, If(t.Note.Length = 0, "—", t.Note), Theme.FontBody, noteRect, Theme.TextMuted, flags)
            End Select
        End Sub

    End Class

End Namespace
