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
    ''' Accounts for the admin screens (§7 ucAdminUsers): Name · Email · Role · Status · Payments · Joined.
    ''' Styled like PaymentGrid: custom-drawn cells, horizontal rules only, initials disc by the name, Role and Status
    ''' as pills, no visible scrollbar. Click a header to sort; the selected row drives the screen's actions.
    ''' </summary>
    Public Class UserGrid
        Inherits DataGridView

        Private Const ColName As String = "name"
        Private Const ColEmail As String = "email"
        Private Const ColRole As String = "role"
        Private Const ColStatus As String = "status"
        Private Const ColPayments As String = "payments"
        Private Const ColJoined As String = "joined"

        Private _rows As New List(Of UserSummary)
        Private _sortColumn As String = ColJoined
        Private _sortAscending As Boolean
        Private _hoverRow As Integer = -1

        ''' <summary>The signed-in admin's id, marked "You" in the list.</summary>
        Public Property CurrentUserId As Integer

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
            DefaultCellStyle.SelectionBackColor = Theme.BrandSoft
            DefaultCellStyle.SelectionForeColor = Theme.TextPrimary
            ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface

            AddColumn(ColName, "NAME", 0, 30, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColEmail, "EMAIL", 0, 30, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColRole, "ROLE", 110, 0, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColStatus, "STATUS", 140, 0, DataGridViewContentAlignment.MiddleLeft)
            AddColumn(ColPayments, "PAYMENTS", If(Theme.IsLargeText, 124, 104), 0, DataGridViewContentAlignment.MiddleRight)
            AddColumn(ColJoined, "JOINED", 130, 0, DataGridViewContentAlignment.MiddleLeft)
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

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ' DataGridView row, header and fixed column sizes are not auto-scaled; size them for this screen.
            RowTemplate.Height = CInt(54 * S)
            ColumnHeadersHeight = CInt(40 * S)
            For Each column As DataGridViewColumn In Columns
                Dim fixedWidth = CInt(column.Tag)
                If fixedWidth > 0 Then column.Width = CInt(fixedWidth * S)
            Next
            For Each row As DataGridViewRow In Rows
                row.Height = RowTemplate.Height
            Next
        End Sub

        ' ---------------------------------------------------------------- data and sorting

        Public Sub SetRows(items As IEnumerable(Of UserSummary))
            Dim keep = SelectedUser?.UserId
            _rows = New List(Of UserSummary)(items)
            Rebuild(keep)
        End Sub

        ''' <summary>The selected account, or Nothing.</summary>
        Public ReadOnly Property SelectedUser As UserSummary
            Get
                ' SelectedRows, not CurrentRow: during SelectionChanged the current row may not have moved yet.
                If SelectedRows.Count = 0 Then Return Nothing
                Return TryCast(SelectedRows(0).Tag, UserSummary)
            End Get
        End Property

        Private Function SortedRows() As IEnumerable(Of UserSummary)
            Dim key As Func(Of UserSummary, Object)
            Select Case _sortColumn
                Case ColName : key = Function(u) u.FullName.ToLowerInvariant()
                Case ColEmail : key = Function(u) u.Email.ToLowerInvariant()
                Case ColRole : key = Function(u) u.RoleLabel
                Case ColStatus : key = Function(u) u.StatusLabel
                Case ColPayments : key = Function(u) u.PaymentCount
                Case Else : key = Function(u) u.CreatedAt
            End Select
            Dim ordered = If(_sortAscending, _rows.OrderBy(key), _rows.OrderByDescending(key))
            Return ordered.ThenBy(Function(u) u.FullName)
        End Function

        ''' <summary>Rebuilds the rows; keeps <paramref name="selectId"/> selected if it is still listed.</summary>
        Private Sub Rebuild(Optional selectId As Integer? = Nothing)
            SuspendLayout()
            Rows.Clear()
            For Each u In SortedRows()
                Dim index = Rows.Add()
                Rows(index).Tag = u
                If IsHandleCreated Then Rows(index).Height = RowTemplate.Height
            Next
            ResumeLayout()
            CurrentCell = Nothing
            ClearSelection()
            If selectId.HasValue Then
                For Each row As DataGridViewRow In Rows
                    If DirectCast(row.Tag, UserSummary).UserId = selectId.Value Then
                        CurrentCell = row.Cells(0)
                        row.Selected = True
                        Exit For
                    End If
                Next
            ElseIf RowCount > 0 Then
                FirstDisplayedScrollingRowIndex = 0
            End If
            Invalidate()
        End Sub

        Protected Overrides Sub OnVisibleChanged(e As EventArgs)
            MyBase.OnVisibleChanged(e)
            ' A grid that becomes visible selects its first row by itself; start with nothing selected.
            If Visible AndAlso SelectedUser Is Nothing Then
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
                _sortAscending = Not (name = ColJoined OrElse name = ColPayments)
            End If
            Rebuild(SelectedUser?.UserId)
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

        Protected Overrides Sub OnScroll(e As ScrollEventArgs)
            MyBase.OnScroll(e)
            Invalidate()
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

        Protected Overrides Sub OnCellPainting(e As DataGridViewCellPaintingEventArgs)
            MyBase.OnCellPainting(e)
            If e.ColumnIndex < 0 Then Return
            If e.RowIndex = -1 Then
                Dim column = Columns(e.ColumnIndex)
                RowPainter.PaintHeader(e, column, column.Name = _sortColumn, _sortAscending, S)
            ElseIf e.RowIndex >= 0 Then
                PaintCell(e)
            End If
            e.Handled = True
        End Sub

        Private Sub PaintCell(e As DataGridViewCellPaintingEventArgs)
            Dim g = e.Graphics
            Dim u = TryCast(Rows(e.RowIndex).Tag, UserSummary)
            If u Is Nothing Then Return
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
            Dim muted = Not u.IsActive
            Select Case column
                Case ColName
                    Gfx.HighQuality(g)
                    Dim disc = 32 * S
                    Dim discRect As New RectangleF(r.X, r.Y + (r.Height - disc) / 2.0F, disc, disc)
                    Using brush As New SolidBrush(If(muted, Theme.NeutralSoft, Theme.BrandSoft))
                        g.FillEllipse(brush, discRect)
                    End Using
                    TextRenderer.DrawText(g, Initials(u.FullName), Theme.FontCaptionBold, Rectangle.Round(discRect), If(muted, Theme.TextMuted, Theme.BrandDark),
                                          TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                    Dim textX = CInt(discRect.Right + 10 * S)
                    Dim nameRect As New Rectangle(textX, r.Y, r.Right - textX, r.Height)
                    If u.UserId = CurrentUserId Then
                        ' "You" tag after the name.
                        Dim tagText = "You"
                        Dim tagWidth = TextRenderer.MeasureText(g, tagText, Theme.FontSmallCaps, Size.Empty, TextFormatFlags.NoPadding).Width + CInt(12 * S)
                        Dim nameWidth = Math.Min(nameRect.Width - tagWidth - CInt(8 * S),
                                                 TextRenderer.MeasureText(g, u.FullName, Theme.FontBodyBold, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width)
                        TextRenderer.DrawText(g, u.FullName, Theme.FontBodyBold, New Rectangle(textX, r.Y, nameWidth + 2, r.Height), Theme.TextPrimary, flags)
                        Dim tag As New RectangleF(textX + nameWidth + 8 * S, r.Y + (r.Height - 18 * S) / 2.0F, tagWidth, 18 * S)
                        Gfx.FillRounded(g, tag, 9 * S, Theme.NeutralSoft)
                        TextRenderer.DrawText(g, tagText, Theme.FontSmallCaps, Rectangle.Round(tag), Theme.TextMuted,
                                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                    Else
                        TextRenderer.DrawText(g, u.FullName, Theme.FontBodyBold, nameRect, If(muted, Theme.TextMuted, Theme.TextPrimary), flags)
                    End If
                Case ColEmail
                    TextRenderer.DrawText(g, u.Email, Theme.FontBody, r, If(muted, Theme.TextMuted, Theme.TextPrimary), flags)
                Case ColRole
                    RowPainter.PaintPill(g, r, u.RoleLabel, If(u.IsAdmin, Theme.BrandDark, Theme.TextMuted),
                                         If(u.IsAdmin, Theme.BrandSoft, Theme.NeutralSoft), S)
                Case ColStatus
                    RowPainter.PaintPill(g, r, u.StatusLabel, If(u.IsActive, Theme.PaidText, Theme.TextMuted),
                                         If(u.IsActive, Theme.PaidSoft, Theme.NeutralSoft), S)
                Case ColPayments
                    TextRenderer.DrawText(g, u.PaymentCount.ToString(), Theme.FontBodyBold, r, If(muted, Theme.TextMuted, Theme.TextPrimary), flags Or TextFormatFlags.Right)
                Case ColJoined
                    TextRenderer.DrawText(g, Format.ShortDate(u.CreatedAt), Theme.FontBody, r, Theme.TextMuted, flags)
            End Select
        End Sub

        Private Shared Function Initials(fullName As String) As String
            Return String.Concat(fullName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries).
                                 Take(2).Select(Function(part) Char.ToUpperInvariant(part(0))))
        End Function

    End Class

End Namespace
