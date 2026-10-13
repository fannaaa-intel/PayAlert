Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports PayAlert.Controls
Imports PayAlert.Services

Namespace Views

    ''' <summary>
    ''' Base for every screen hosted in frmMain. Gives each screen the header band (title, subtitle,
    ''' right-aligned actions), a Body panel for its content, and, for inner screens, one "Back" button
    ''' at the top-left. The Navigator sets Navigator and IsInner when it shows the view.
    ''' </summary>
    Public Class PAView
        Inherits UserControl

        Protected Const ViewWidth As Integer = 1000
        Protected Const ViewHeight As Integer = 640

        Protected ReadOnly Header As New PageHeader()
        Protected ReadOnly Body As New Panel()
        Private ReadOnly _backRow As New Panel()
        Private ReadOnly _backButton As New PAButton()
        Private _actionsRight As Integer

        Public Sub New()
            SuspendLayout()
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            DoubleBuffered = True
            BackColor = Theme.AppBackground
            ForeColor = Theme.TextPrimary
            Font = Theme.FontBody
            Padding = New Padding(Theme.ContentPadding)
            Size = New Size(ViewWidth, ViewHeight)

            _backButton.Kind = ButtonKind.Ghost
            _backButton.Icon = IconKind.Back
            _backButton.Text = "Back"
            _backButton.Size = New Size(96, Theme.ButtonHeight)
            _backButton.Location = New Point(-10, 0)
            AddHandler _backButton.Click, Sub(s, e) Navigator?.GoBack()
            _backRow.Dock = DockStyle.Top
            _backRow.Height = 44
            _backRow.Visible = False
            _backRow.Controls.Add(_backButton)

            Header.Dock = DockStyle.Top
            Header.Height = 76
            Header.Width = ViewWidth - Theme.ContentPadding * 2
            _actionsRight = Header.Width

            Body.Dock = DockStyle.Fill
            Body.BackColor = Theme.AppBackground
            Dim spacer As New Panel() With {.Dock = DockStyle.Top, .Height = 20}

            ' Docked top-down: the last added docks first.
            Controls.Add(Body)
            Controls.Add(spacer)
            Controls.Add(Header)
            Controls.Add(_backRow)
            ResumeLayout(False)
        End Sub

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Navigator As Navigator

        ''' <summary>The sidebar item this screen belongs to (highlighted while it shows).</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property SidebarKey As String = ""

        <Category("PayAlert")>
        Public Property Title As String
            Get
                Return Header.Title
            End Get
            Set(value As String)
                Header.Title = value
            End Set
        End Property

        <Category("PayAlert")>
        Public Property Subtitle As String
            Get
                Return Header.Subtitle
            End Get
            Set(value As String)
                Header.Subtitle = value
            End Set
        End Property

        ''' <summary>Inner screens (reached from another screen) show the Back button.</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property IsInner As Boolean
            Get
                Return _backRow.Visible
            End Get
            Set(value As Boolean)
                _backRow.Visible = value
            End Set
        End Property

        ''' <summary>Adds a button to the right side of the header band (rightmost first).</summary>
        Protected Sub AddHeaderAction(action As Control)
            action.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            action.Location = New Point(_actionsRight - action.Width, 6)
            _actionsRight = action.Left - Theme.Space12
            Header.Controls.Add(action)
        End Sub

        ''' <summary>Called each time the Navigator shows this screen (first time and when coming back to it).</summary>
        Public Overridable Sub OnNavigatedTo()
        End Sub

        ''' <summary>A full-size soft card holding an empty state; used by screens until their phase is built.</summary>
        Protected Function AddPlaceholder(icon As IconKind, title As String, message As String,
                                          Optional actionText As String = Nothing, Optional action As Action = Nothing) As EmptyState
            Dim card As New SoftCard() With {.Dock = DockStyle.Fill}
            Dim empty As New EmptyState() With {.Dock = DockStyle.Fill, .Icon = icon, .Title = title, .Message = message}
            If Not String.IsNullOrEmpty(actionText) Then
                empty.ActionText = actionText
                empty.ActionIcon = IconKind.ArrowOut
                If action IsNot Nothing Then AddHandler empty.ActionClick, Sub(s, e) action()
            End If
            card.Controls.Add(empty)
            Body.Controls.Add(card)
            Return empty
        End Function

    End Class

End Namespace
