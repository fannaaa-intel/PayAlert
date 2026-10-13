Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Data
Imports PayAlert.Dialogs
Imports PayAlert.Models
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Payments (§7): status tabs with counts, a direction toggle, a category filter and live search over one grid.
    ''' Double-click or Enter opens Payment Details. Each tab and filter has its own empty state.
    ''' </summary>
    Public Class ucPayments
        Inherits PAView

        Private Shared ReadOnly TabNames As String() = {"All", "Upcoming", "Partial", "Paid", "Overdue"}
        Private Shared ReadOnly TabStatuses As String() = {Nothing, Theme.StatusUpcoming, Theme.StatusPartial, Theme.StatusPaid, Theme.StatusOverdue}

        Private ReadOnly _tabs As New StatusTabs()
        Private ReadOnly _direction As New SegmentedControl()
        Private ReadOnly _category As New PASelect()
        Private ReadOnly _search As New PATextBox()
        Private ReadOnly _countLabel As New Label()
        Private ReadOnly _grid As New PaymentGrid()
        Private ReadOnly _empty As New EmptyState()
        Private _all As New List(Of PaymentSummary)
        Private _categories As List(Of Category)
        Private _applying As Boolean

        ''' <param name="status">Optional starting tab (a Theme.Status* value).</param>
        ''' <param name="direction">Optional starting direction (Payment.DirectionPayable / DirectionReceivable).</param>
        Public Sub New(Optional status As String = Nothing, Optional direction As String = Nothing)
            MyBase.New()
            SidebarKey = "payments"
            Title = "Payments"
            Subtitle = "View and manage your reminders"

            Dim add As New PrimaryButton() With {.Text = "Add Payment", .Icon = IconKind.Plus, .Size = New Drawing.Size(150, Theme.ButtonHeight)}
            AddHandler add.Click, Sub(s, e) Navigator.Push(New ucPaymentEditor())
            AddHeaderAction(add)

            ' Row 1: status tabs, search on the right.
            _tabs.SetTabs(TabNames)
            _tabs.Dock = DockStyle.Fill
            _search.Icon = IconKind.Search
            _search.PlaceholderText = "Search by name or person"
            _search.Dock = DockStyle.Right
            _search.Width = 280
            Dim row1 As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            row1.Controls.Add(_tabs)
            row1.Controls.Add(New Panel() With {.Dock = DockStyle.Right, .Width = 16})
            row1.Controls.Add(_search)

            ' Row 2: direction, category, and how many are showing.
            _direction.SetItems("All", "I Owe", "Owed to Me")
            _direction.Dock = DockStyle.Left
            _direction.Width = 300
            _category.Dock = DockStyle.Left
            _category.Width = 220
            _category.Icon = IconKind.Payments
            _countLabel.Dock = DockStyle.Fill
            _countLabel.TextAlign = Drawing.ContentAlignment.MiddleRight
            _countLabel.Font = Theme.FontCaption
            _countLabel.ForeColor = Theme.TextMuted
            Dim row2 As New Panel() With {.Dock = DockStyle.Top, .Height = 42}
            row2.Controls.Add(_countLabel)
            row2.Controls.Add(_category)
            row2.Controls.Add(New Panel() With {.Dock = DockStyle.Left, .Width = 12})
            row2.Controls.Add(_direction)

            ' The list, with an empty state laid over it when nothing matches.
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill, .Padding = New Padding(6, 8, 6, 8)}
            _grid.Dock = DockStyle.Fill
            _empty.Dock = DockStyle.Fill
            _empty.Visible = False
            card.Controls.Add(_grid)
            card.Controls.Add(_empty)

            Body.Controls.Add(card)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 16})
            Body.Controls.Add(row2)
            Body.Controls.Add(New Panel() With {.Dock = DockStyle.Top, .Height = 12})
            Body.Controls.Add(row1)

            ' Starting filters (used by the dashboard cards).
            If status IsNot Nothing Then _tabs.SelectedIndex = Math.Max(0, Array.IndexOf(TabStatuses, status))
            If direction = Payment.DirectionPayable Then _direction.SelectedIndex = 1
            If direction = Payment.DirectionReceivable Then _direction.SelectedIndex = 2

            AddHandler _tabs.SelectedIndexChanged, Sub(s, e) ApplyFilters()
            AddHandler _direction.SelectedIndexChanged, Sub(s, e) ApplyFilters()
            AddHandler _category.SelectedIndexChanged, Sub(s, e) ApplyFilters()
            AddHandler _search.TextChanged, Sub(s, e) ApplyFilters()
            AddHandler _grid.PaymentActivated, Sub(s, p) Navigator.Push(New ucPaymentDetails(p.PaymentId))
            AddHandler _empty.ActionClick, AddressOf EmptyActionClicked
        End Sub

        Public Overrides Sub OnNavigatedTo()
            MyBase.OnNavigatedTo()
            Reload()
        End Sub

        Private Sub Reload()
            Try
                If _categories Is Nothing Then
                    _categories = CategoryRepo.GetAll()
                    Dim choices As New List(Of Object) From {"All categories"}
                    choices.AddRange(_categories)
                    _category.SetItems(choices, 0)
                End If
                _all = PaymentRepo.GetSummaries(Session.CurrentUser.UserId)
            Catch ex As Exception
                Debug.WriteLine("[ucPayments] " & ex.ToString())
                PADialog.Error(Me, "Can't load your payments", Db.FriendlyMessageFor(ex))
            End Try
            ApplyFilters()
        End Sub

        Private Function MatchesFilters(p As PaymentSummary) As Boolean
            Select Case _direction.SelectedIndex
                Case 1 : If p.IsReceivable Then Return False
                Case 2 : If Not p.IsReceivable Then Return False
            End Select
            Dim category = TryCast(_category.SelectedItem, Category)
            If category IsNot Nothing AndAlso p.CategoryId <> category.CategoryId Then Return False
            Dim query = _search.Text.Trim()
            If query.Length > 0 AndAlso
               p.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 AndAlso
               p.Counterparty.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 Then Return False
            Return True
        End Function

        Private Sub ApplyFilters()
            If _applying Then Return
            _applying = True
            Try
                Dim filtered = _all.Where(AddressOf MatchesFilters).ToList()
                _tabs.SetCount(0, filtered.Count)
                For i = 1 To TabStatuses.Length - 1
                    Dim status = TabStatuses(i)
                    _tabs.SetCount(i, filtered.Where(Function(p) p.Status = status).Count())
                Next
                Dim wanted = TabStatuses(_tabs.SelectedIndex)
                Dim shown = If(wanted Is Nothing, filtered, filtered.Where(Function(p) p.Status = wanted).ToList())
                _grid.SetRows(shown)
                _countLabel.Text = If(_all.Count = 0, "", "Showing " & shown.Count & " of " & _all.Count & If(_all.Count = 1, " payment", " payments"))
                UpdateEmptyState(shown.Count)
            Finally
                _applying = False
            End Try
        End Sub

        Private ReadOnly Property HasExtraFilters As Boolean
            Get
                Return _direction.SelectedIndex > 0 OrElse _category.SelectedIndex > 0 OrElse _search.Text.Trim().Length > 0
            End Get
        End Property

        Private Sub UpdateEmptyState(shownCount As Integer)
            Dim empty = shownCount = 0
            _grid.Visible = Not empty
            _empty.Visible = empty
            If Not empty Then Return

            Dim query = _search.Text.Trim()
            If _all.Count = 0 Then
                SetEmpty(IconKind.Payments, "No payments yet", "Add your first one. PayAlert will remind you before it's due.", "Add Payment", IconKind.Plus)
            ElseIf query.Length > 0 Then
                SetEmpty(IconKind.Search, "No payments match """ & query & """", "Try another name or person, or clear the filters.", "Clear filters", IconKind.Close)
            ElseIf HasExtraFilters Then
                SetEmpty(IconKind.Inbox, "Nothing for these filters", "No payments match this direction and category.", "Clear filters", IconKind.Close)
            Else
                Select Case TabStatuses(_tabs.SelectedIndex)
                    Case Theme.StatusUpcoming
                        SetEmpty(IconKind.Calendar, "Nothing upcoming", "Payments that aren't due yet will show here.", Nothing, IconKind.None)
                    Case Theme.StatusPartial
                        SetEmpty(IconKind.Payments, "No partial payments", "Payments with part of the amount already paid will show here.", Nothing, IconKind.None)
                    Case Theme.StatusPaid
                        SetEmpty(IconKind.Check, "Nothing fully paid yet", "Payments you've paid in full will show here.", Nothing, IconKind.None)
                    Case Else
                        SetEmpty(IconKind.Check, "Nothing overdue", "Nice. Every payment is on time.", Nothing, IconKind.None)
                End Select
            End If
        End Sub

        Private Sub SetEmpty(icon As IconKind, title As String, message As String, actionText As String, actionIcon As IconKind)
            _empty.Icon = icon
            _empty.Title = title
            _empty.Message = message
            _empty.ActionIcon = actionIcon
            _empty.ActionText = actionText
        End Sub

        Private Sub EmptyActionClicked(sender As Object, e As EventArgs)
            If _all.Count = 0 Then
                Navigator.Push(New ucPaymentEditor())
                Return
            End If
            _applying = True
            _search.Text = ""
            _direction.SelectedIndex = 0
            _category.SelectedIndex = 0
            _applying = False
            ApplyFilters()
        End Sub

    End Class

End Namespace
