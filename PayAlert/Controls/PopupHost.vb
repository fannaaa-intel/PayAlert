Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ''' <summary>
    ''' A rounded, shadowed drop-down surface that hosts one control (the dropdown list, the calendar).
    ''' Built on ToolStripDropDown, so it closes on an outside click, Esc or focus loss like a native popup.
    ''' </summary>
    Friend NotInheritable Class PopupHost
        Inherits ToolStripDropDown

        Private ReadOnly _content As Control

        Public Sub New(content As Control)
            _content = content
            AutoSize = False
            Padding = Padding.Empty
            Margin = Padding.Empty
            DropShadowEnabled = True
            Renderer = New PopupRenderer()
            Dim host As New ToolStripControlHost(content) With {.AutoSize = False, .Margin = Padding.Empty, .Padding = Padding.Empty}
            host.Size = content.Size
            Items.Add(host)
            Size = content.Size
        End Sub

        ''' <summary>Opens the popup just under the anchor control, left-aligned with it.</summary>
        Public Sub ShowBelow(anchor As Control)
            Dim gap = CInt(Math.Round(4 * anchor.DeviceDpi / 96.0F))
            Show(anchor, New Point(0, anchor.Height + gap))
            _content.Focus()
        End Sub

        Protected Overrides Sub OnSizeChanged(e As EventArgs)
            MyBase.OnSizeChanged(e)
            Using path = Gfx.RoundedRect(New Rectangle(0, 0, Width, Height), 10 * DeviceDpi / 96.0F)
                Region = New Region(path)
            End Using
        End Sub

        Private NotInheritable Class PopupRenderer
            Inherits ToolStripRenderer

            Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
                Using fill As New SolidBrush(Theme.Surface)
                    e.Graphics.FillRectangle(fill, e.AffectedBounds)
                End Using
            End Sub

            Protected Overrides Sub OnRenderToolStripBorder(e As ToolStripRenderEventArgs)
                Gfx.HighQuality(e.Graphics)
                Dim r = e.ToolStrip.ClientRectangle
                Gfx.DrawRounded(e.Graphics, New RectangleF(0.5F, 0.5F, r.Width - 1.5F, r.Height - 1.5F), 10 * e.ToolStrip.DeviceDpi / 96.0F, Theme.CardBorder)
            End Sub
        End Class

    End Class

End Namespace
