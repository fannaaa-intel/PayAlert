Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq

Namespace Services

    ''' <summary>A proof picture ready to save: a JPEG small enough for the database, and the original file name.</summary>
    Public NotInheritable Class PreparedProof
        Public Property FileName As String = ""
        Public Property ContentType As String = ProofImage.JpegType
        Public Property Data As Byte()
    End Class

    ''' <summary>Thrown when a picked file can't be used as proof; Message is safe to show the user.</summary>
    Public NotInheritable Class ProofImageException
        Inherits Exception

        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    ''' <summary>
    ''' Turns a photo or screenshot of a receipt into a JPEG that fits in one database packet (XAMPP allows 1 MB):
    ''' turned upright (phone photos), flattened on white, at most 1600 px on the long side, and under 600 KB.
    ''' </summary>
    Public NotInheritable Class ProofImage

        Public Const JpegType As String = "image/jpeg"
        Public Const FileFilter As String = "Pictures (*.jpg; *.jpeg; *.png; *.bmp; *.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
        Public Const MaxSourceBytes As Long = 20L * 1024 * 1024
        Public Const MaxStoredBytes As Integer = 600 * 1024
        Private Const MaxSide As Integer = 1600
        Private Const ExifOrientation As Integer = &H112

        Private Shared ReadOnly Extensions As String() = {".jpg", ".jpeg", ".png", ".bmp", ".gif"}

        Private Sub New()
        End Sub

        ''' <summary>Reads and shrinks the picture at <paramref name="path"/>. Throws ProofImageException with a friendly message.</summary>
        Public Shared Function Prepare(path As String) As PreparedProof
            Dim name = IO.Path.GetFileName(path)
            If Not Extensions.Contains(IO.Path.GetExtension(path).ToLowerInvariant()) Then
                Throw New ProofImageException("Pick a picture (JPG, PNG, BMP or GIF), like a photo or screenshot of the receipt.")
            End If

            Dim bytes As Byte()
            Try
                Dim info As New FileInfo(path)
                If info.Length > MaxSourceBytes Then Throw New ProofImageException("That picture is too big (over 20 MB). Pick a smaller one.")
                bytes = File.ReadAllBytes(path)
            Catch ex As ProofImageException
                Throw
            Catch ex As Exception
                Debug.WriteLine("[ProofImage] " & ex.ToString())
                Throw New ProofImageException("PayAlert couldn't open that file. Check that it isn't open in another program.")
            End Try
            Return New PreparedProof With {.FileName = name, .Data = Shrink(bytes)}
        End Function

        ''' <summary>Re-encodes picture bytes as an upright JPEG under MaxStoredBytes.</summary>
        Public Shared Function Shrink(source As Byte()) As Byte()
            Using original = LoadImage(source)
                Upright(original)
                Dim scale = Math.Min(1.0, MaxSide / CDbl(Math.Max(original.Width, original.Height)))
                Dim quality = 85L
                Do
                    Dim w = Math.Max(1, CInt(original.Width * scale))
                    Dim h = Math.Max(1, CInt(original.Height * scale))
                    Dim encoded = EncodeJpeg(original, w, h, quality)
                    If encoded.Length <= MaxStoredBytes OrElse (w <= 200 AndAlso h <= 200) Then Return encoded
                    ' Too big: lower the quality a little first, then make it smaller.
                    If quality > 60L Then quality -= 10L Else scale *= 0.8
                Loop
            End Using
        End Function

        ''' <summary>Opens stored proof bytes as a picture (the caller disposes it), or Nothing if they are not a picture.</summary>
        Public Shared Function ToImage(data As Byte()) As Image
            If data Is Nothing OrElse data.Length = 0 Then Return Nothing
            Try
                Return LoadImage(data)
            Catch ex As ProofImageException
                Return Nothing
            End Try
        End Function

        Private Shared Function LoadImage(data As Byte()) As Bitmap
            Try
                ' Copy into a Bitmap so the stream can close (GDI+ keeps reading the stream otherwise).
                Using stream As New MemoryStream(data)
                    Using decoded = Image.FromStream(stream, True, True)
                        Dim copy As New Bitmap(decoded)
                        For Each item In decoded.PropertyItems
                            If item.Id = ExifOrientation Then copy.SetPropertyItem(item)
                        Next
                        Return copy
                    End Using
                End Using
            Catch ex As Exception
                Debug.WriteLine("[ProofImage] " & ex.ToString())
                Throw New ProofImageException("That file isn't a picture PayAlert can read. Try a JPG or PNG.")
            End Try
        End Function

        ''' <summary>Phone cameras save photos sideways with a note on how to turn them; apply it.</summary>
        Private Shared Sub Upright(picture As Bitmap)
            If Not picture.PropertyIdList.Contains(ExifOrientation) Then Return
            Dim value = picture.GetPropertyItem(ExifOrientation).Value
            If value Is Nothing OrElse value.Length = 0 Then Return
            Select Case value(0)
                Case 2 : picture.RotateFlip(RotateFlipType.RotateNoneFlipX)
                Case 3 : picture.RotateFlip(RotateFlipType.Rotate180FlipNone)
                Case 4 : picture.RotateFlip(RotateFlipType.Rotate180FlipX)
                Case 5 : picture.RotateFlip(RotateFlipType.Rotate90FlipX)
                Case 6 : picture.RotateFlip(RotateFlipType.Rotate90FlipNone)
                Case 7 : picture.RotateFlip(RotateFlipType.Rotate270FlipX)
                Case 8 : picture.RotateFlip(RotateFlipType.Rotate270FlipNone)
            End Select
            picture.RemovePropertyItem(ExifOrientation)
        End Sub

        Private Shared Function EncodeJpeg(picture As Image, width As Integer, height As Integer, quality As Long) As Byte()
            Using canvas As New Bitmap(width, height, PixelFormat.Format24bppRgb)
                Using g = Graphics.FromImage(canvas)
                    ' Screenshots may be transparent; JPEG has no transparency, so put them on white.
                    g.Clear(Theme.Surface)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality
                    g.DrawImage(picture, New Rectangle(0, 0, width, height))
                End Using
                Dim codec = ImageCodecInfo.GetImageEncoders().First(Function(c) c.MimeType = JpegType)
                Using parameters As New EncoderParameters(1)
                    parameters.Param(0) = New EncoderParameter(Encoder.Quality, quality)
                    Using stream As New MemoryStream()
                        canvas.Save(stream, codec, parameters)
                        Return stream.ToArray()
                    End Using
                End Using
            End Using
        End Function

    End Class

End Namespace
