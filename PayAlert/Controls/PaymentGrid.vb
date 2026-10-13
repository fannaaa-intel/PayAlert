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
    ''' The payment list (§7 ucPayments): Due · Name · Category · Person · Total · Balance · Status.
    ''' A DataGridView styled per §6 and custom-drawn: the Due cell is a compact due tag whose colored stub is the
    ''' row's status edge, Name shows the direction under it, Status is a pill. Click a header to sort,
    ''' double-click or Enter to open. The stock scrollbar is hidden; the mouse wheel and keyboard scroll.
    ''' </summary>
    Public Class PaymentGrid
        Inherits DataGridView

        Private Const ColDue As String = "due"
        Private Const ColName As String = "name"
        Private Const ColCategory As String = "category"
        Private Const ColPerson As String = "person"
        Private Const ColTotal As String = "total"
        Private Const ColBalance As String = "balance"
        Private Const ColStatus As String = "status"

        Private _rows As New List(Of PaymentSummary)
        Private _sortColumn As String = ColDue
        Private _sortAscending As Boolean = True
        Private _hoverRow As Integer = -1

        ''' <summary>Raised on double-click or Enter.</summary>
        Public Event PaymentActivated As EventHandler(Of PaymentSummary)

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
            Cursor = Cursors.Hand

            ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface
            ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextMuted
            ColumnHeadersDefaultCellStyle.Font = Theme.FontCaptionBold
            ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface
            DefaultCellStyle.BackColor = Theme.Surface
            DefaultCellStyle.ForeColor = Theme.TextPrimary
            DefaultCellStyle.Font = Theme.FontBody
            DefaultCellStyle.SelectionBackColor = Theme.BrandSoft
            DefaultCellStyle.SelectionForeColor = Theme.TextPrimary

            AddColumn(ColDue, "DUE", 96, 0, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColName, "PAYMENT", 0, 30, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColCategory, "CATEGORY", 0, 17, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColPerson, "PERSON / COMPANY", 0, 19, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColTotal, "TOTAL", 0, 13, DataGridViewContentAlignment.MiddleRight)
            AddColumn(ColBalance, "BALANCE", 0, 13, DataGridViewContentAlignment.MiddleRight)
            AddColumn(ColStatus, "STATUS", 150, 0, DataGridViewContentAlignment.MiddleLeft)
        End Sub

        Private Sub AddColumn(name As String, header As String, fixedWidth As Integer, fillWeight As Single, align As DataGridViewContentAlignment)
            Dim column As New DataGridViewTextBoxColumn() With {
                .Name = name, .HeaderText = header, .SortMode = DataGridViewColumnSortMode.Programmatic, .ReadOnly = True}
            column.DefaultCellStyle.Alignment = align
            column.HeaderCell.Style.Alignment = If(align = DataGridViewContentAlignment.MiddleRight, DataGridViewContentAlignment.MiddleRight, DataGridViewContentAlignment.MiddleLeft)
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

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ' DataGridView row and header heights are not auto-scaled; size them for this screen.
            RowTemplate.Height = CInt(56 * S)
            ColumnHeadersHeight = CInt(40 * S)
            Columns(ColDue).Width = CInt(96 * S)
            Columns(ColStatus).Width = CInt(150 * S)
            For Each row As DataGridViewRow In Rows
                row.Height = RowTemplate.Height
            Next
        End Sub

        ' ---------------------------------------------------------------- data and sorting

        Public ReadOnly Property RowCountShown As Integer
            Get
                Return _rows.Count
            End Get
        End Property

        Public Sub SetRows(items As IEnumerable(Of PaymentSummary))
            _rows = New List(Of PaymentSummary)(items)
            Rebuild()
        End Sub

        Private Function SortedRows() As IEnumerable(Of PaymentSummary)
            Dim key As Func(Of PaymentSummary, Object)
            Select Case _sortColumn
                Case ColName : key = Function(p) p.Name.ToLowerInvariant()
                Case ColCategory : key = Function(p) p.CategoryName
                Case ColPerson : key = Function(p) p.Counterparty.ToLowerInvariant()
                Case ColTotal : key = Function(p) p.TotalAmount
                Case ColBalance : key = Function(p) p.Balance
                Case ColStatus : key = Function(p) StatusOrder(p.Status)
                Case Else : key = Function(p) p.DueDate
            End Select
            ' Ties fall back to the due date, then the name, so the order is always stable.
            Dim ordered = If(_sortAscending, _rows.OrderBy(key), _rows.OrderByDescending(key))
            Return ordered.ThenBy(Function(p) p.DueDate).ThenBy(Function(p) p.Name)
        End Function

        Private Shared Function StatusOrder(status As String) As Integer
            Select Case status
                Case Theme.StatusOverdue : Return 0
                Case Theme.StatusPartial : Return 1
                Case Theme.StatusUpcoming : Return 2
                Case Else : Return 3
            End Select
        End Function

        Private Sub Rebuild()
            SuspendLayout()
            Rows.Clear()
            For Each p In SortedRows()
                Dim index = Rows.Add(p.DueDate, p.Name, p.CategoryName, p.Counterparty, Format.Money(p.TotalAmount), Format.Money(p.Balance), p.Status)
                Rows(index).Tag = p
                If IsHandleCreated Then Rows(index).Height = RowTemplate.Height
            Next
            For Each column As DataGridViewColumn In Columns
                column.HeaderCell.SortGlyphDirection = SortOrder.None
            Next
            ResumeLayout()
            ' A new sort or filter always starts at the top, with nothing selected yet.
            CurrentCell = Nothing
            ClearSelection()
            If RowCount > 0 Then FirstDisplayedScrollingRowIndex = 0
            Invalidate()
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
                _sortAscending = True
            End If
            Rebuild()
        End Sub

        ' ---------------------------------------------------------------- input

        Protected Overrides Sub OnCellDoubleClick(e As DataGridViewCellEventArgs)
            MyBase.OnCellDoubleClick(e)
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
            Dim p = TryCast(Rows(rowIndex).Tag, PaymentSummary)
            If p IsNot Nothing Then RaiseEvent PaymentActivated(Me, p)
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
            Dim h = CInt(36 * S)
            Dim band As New Rectangle(0, ClientSize.Height - h, ClientSize.Width, h)
            Using fade As New Drawing2D.LinearGradientBrush(New Rectangle(band.X, band.Y - 1, band.Width, band.Height + 2),
                                                            Color.FromArgb(0, Theme.Surface), Theme.Surface, Drawing2D.LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(fade, band)
            End Using
            Gfx.HighQuality(e.Graphics)
            Dim size = 16 * S
            Icons.Draw(e.Graphics, IconKind.ChevronDown, New RectangleF((ClientSize.Width - size) / 2.0F, ClientSize.Height - size - 6 * S, size, size), Theme.TextMuted, 1.8F * S)
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
            Dim p = TryCast(Rows(e.RowIndex).Tag, PaymentSummary)
            If p Is Nothing Then Return
            Dim selected = (e.State And DataGridViewElementStates.Selected) <> 0
            Dim back = If(selected, Theme.BrandSoft, If(e.RowIndex = _hoverRow, Theme.Blend(Theme.Surface, Theme.AppBackground, 0.7), Theme.Surface))
            Using fill As New SolidBrush(back)
                g.FillRectangle(fill, e.CellBounds)
            End Using
            Using rule As New Pen(Theme.Border)
                g.DrawLine(rule, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1)
            End Using

            Dim column = Columns(e.ColumnIndex).Name
            Dim pad = CInt(10 * S)
            Dim r = New Rectangle(e.CellBounds.X + pad, e.CellBounds.Y, e.CellBounds.Width - pad * 2, e.CellBounds.Height - 1)
            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            Select Case column
                Case ColDue
                    RowPainter.PaintDueTag(g, e.CellBounds.X + 12 * S, e.CellBounds, p.DueDate, p.Status, S)
                Case ColName
                    Dim nameHeight = TextRenderer.MeasureText(g, "Ag", Theme.FontBodyBold, Size.Empty, flags).Height + CInt(2 * S)
                    Dim top = r.Y + (r.Height - nameHeight - CInt(18 * S)) \ 2
                    TextRenderer.DrawText(g, p.Name, Theme.FontBodyBold, New Rectangle(r.X, top, r.Width, nameHeight), Theme.TextPrimary, flags)
                    Gfx.HighQuality(g)
                    Dim arrow = 12 * S
                    Dim lineTop = top + nameHeight + CInt(3 * S)
                    Icons.Draw(g, If(p.IsReceivable, IconKind.ArrowIn, IconKind.ArrowOut), New RectangleF(r.X, lineTop + 2 * S, arrow, arrow), Theme.TextMuted, 1.5F * S)
                    TextRenderer.DrawText(g, p.DirectionLabel & If(p.IsRecurring, "  ·  repeats " & p.Recurrence, ""), Theme.FontCaption,
                                          New Rectangle(r.X + CInt(arrow + 4 * S), lineTop, r.Width - CInt(arrow + 4 * S), CInt(16 * S)), Theme.TextMuted, flags)
                Case ColCategory, ColPerson
                    Dim text = If(column = ColCategory, p.CategoryName, If(p.Counterparty.Length = 0, "—", p.Counterparty))
                    TextRenderer.DrawText(g, text, Theme.FontBody, r, If(column = ColPerson AndAlso p.Counterparty.Length = 0, Theme.TextMuted, Theme.TextPrimary), flags)
                Case ColTotal
                    TextRenderer.DrawText(g, Format.Money(p.TotalAmount), Theme.FontBody, r, Theme.TextMuted, flags Or TextFormatFlags.Right)
                Case ColBalance
                    TextRenderer.DrawText(g, Format.Money(p.Balance), Theme.FontBodyBold, r, If(p.IsPaid, Theme.TextMuted, Theme.TextPrimary), flags Or TextFormatFlags.Right)
                Case ColStatus
                    RowPainter.PaintStatusPill(g, r, p.Status, S)
            End Select
        End Sub

    End Class

End Namespace
