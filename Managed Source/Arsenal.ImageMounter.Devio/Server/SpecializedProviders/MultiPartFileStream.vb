''''' DevioProviderLibEwf.vb
''''' 
''''' Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Namespace Server.SpecializedProviders

    Public Class MultiPartFileStream
        Inherits Stream

        Private ReadOnly _SegmentSize As Long

        Private ReadOnly _Streams As New List(Of FileStream)

        Public Sub New(Imagefiles As String(), DiskAccess As FileAccess)
            If Imagefiles Is Nothing Then
                Throw New ArgumentNullException("Imagefiles")
            End If
            If Imagefiles.Length = 0 Then
                Throw New ArgumentException("No image files found.", "Imagefiles")
            End If

            Try
                For Each Imagefile In Imagefiles
                    Trace.WriteLine("Opening image " & Imagefile)
                    _Streams.Add(New FileStream(Imagefile, FileMode.Open, DiskAccess, FileShare.Read Or FileShare.Delete))
                Next

                _SegmentSize = _Streams(0).Length
                _Length = _Streams(0).Length * (_Streams.Count - 1) + _Streams.Last().Length

                Trace.WriteLine("Segment size: " & _SegmentSize)
                Trace.WriteLine("Length: " & _Length)

            Catch ex As Exception
                Close()
                Throw New TargetInvocationException(ex)

            End Try
        End Sub

        Public Sub New(FirstImagefile As String, DiskAccess As FileAccess)
            Me.New(GetMultiSegmentFiles(FirstImagefile), DiskAccess)
        End Sub

        Private Shared Function GetMultiSegmentFiles(FirstFile As String) As String()
            Trace.WriteLine("Finding files starting with " & FirstFile)

            Dim pathpart = Path.GetDirectoryName(FirstFile)
            Dim filepart = Path.GetFileNameWithoutExtension(FirstFile)
            Dim extension = Path.GetExtension(FirstFile)
            If extension.StartsWith(".0", StringComparison.OrdinalIgnoreCase) AndAlso
              Integer.TryParse(extension.Substring(1), Nothing) Then

                Dim mask As New String("0"c, extension.Length - 1)

                Dim foundfiles As New List(Of String)
                Dim filenumber = Integer.Parse(extension.Substring(1))
                Do
                    Dim thisfile = Path.Combine(pathpart, filepart & "." & filenumber.ToString(mask))
                    If Not File.Exists(thisfile) Then
                        Exit Do
                    End If

                    foundfiles.Add(thisfile)
                    filenumber += 1
                Loop
                Return foundfiles.ToArray()

            Else

                Return {FirstFile}

            End If

        End Function

        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return _Streams(0).CanRead
            End Get
        End Property

        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return _Streams(0).CanWrite
            End Get
        End Property

        Public Overrides Sub Flush()
            For Each Stream In _Streams
                Stream.Flush()
            Next
        End Sub

        Private ReadOnly _Length As Long
        Public Overrides ReadOnly Property Length As Long
            Get
                Return _Length
            End Get
        End Property

        Private _Position As Long
        Public Overrides Property Position As Long
            Get
                Return _Position
            End Get
            Set(value As Long)
                If value >= 0 AndAlso value < _Length Then
                    _Position = value
                Else
                    Throw New EndOfStreamException
                End If
            End Set
        End Property

        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Select Case origin
                Case SeekOrigin.Begin
                    Position = offset
                Case SeekOrigin.Current
                    Position += offset
                Case SeekOrigin.End
                    Position = _Length + offset
                Case Else
                    Throw New ArgumentException
            End Select
            Return _Position
        End Function

        Public Overrides Sub SetLength(value As Long)
            If value <> _Length Then
                Throw New NotSupportedException("Cannot set length of multi-part file stream.")
            End If
        End Sub

        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Dim doneBytes As Integer
            While count > 0
                Dim fileIndex = CInt(_Position \ _SegmentSize)
                Dim fileOffset = _Position Mod _SegmentSize
                Dim fileLength = CInt(Math.Min(count, _SegmentSize - fileOffset))
                _Streams(fileIndex).Position = fileOffset
                Dim readBytes = _Streams(fileIndex).Read(buffer, offset, fileLength)
                doneBytes += fileLength
                Position += fileLength
                offset += fileLength
                count -= fileLength
                If readBytes <> fileLength Then
                    Exit While
                End If
            End While
            Return doneBytes
        End Function

        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            While count > 0
                Dim fileIndex = CInt(_Position \ _SegmentSize)
                Dim fileOffset = _Position Mod _SegmentSize
                Dim fileLength = CInt(Math.Min(count, _SegmentSize - fileOffset))
                _Streams(fileIndex).Position = fileOffset
                _Streams(fileIndex).Write(buffer, offset, fileLength)
                Position += fileLength
                offset += fileLength
                count -= fileLength
            End While
        End Sub

        Public Overrides Sub Close()
            For Each Stream In _Streams
                Stream.Dispose()
            Next
            _Streams.Clear()

            MyBase.Close()
        End Sub

    End Class

End Namespace
