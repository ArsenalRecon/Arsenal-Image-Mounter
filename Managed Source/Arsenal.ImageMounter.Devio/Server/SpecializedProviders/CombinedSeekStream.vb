''''' CombinedSeekStream.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.IO
Imports System.Threading.Tasks
Imports Arsenal.ImageMounter.IO

Namespace Server.SpecializedProviders

    Public Class CombinedSeekStream
        Inherits Stream

        Private ReadOnly _streams As DisposableDictionary(Of Long, Stream)

        Private _current As KeyValuePair(Of Long, Stream)

        Public ReadOnly Property Extendable() As Boolean

        Public ReadOnly Property BaseStreams() As ICollection(Of Stream)
            Get
                Return _streams.Values
            End Get
        End Property

        Public ReadOnly Property CurrentBaseStream() As Stream
            Get
                Return _current.Value
            End Get
        End Property

        Public Sub New()
            Me.New(True)
        End Sub

        Public Sub New(ParamArray ByVal inputStreams() As Stream)
            Me.New(False, inputStreams)
        End Sub

        Public Sub New(ByVal writable As Boolean, ParamArray ByVal inputStreams() As Stream)
            If inputStreams Is Nothing OrElse inputStreams.Length = 0 Then
                _streams = New DisposableDictionary(Of Long, Stream)()

                _Extendable = True
            Else
                _streams = New DisposableDictionary(Of Long, Stream)(inputStreams.Length)

                Array.ForEach(inputStreams, AddressOf AddStream)

                Seek(0, SeekOrigin.Begin)
            End If

            CanWrite = writable
        End Sub

        Private Sub AddStream(ByVal stream As Stream)
            If Not stream.CanSeek OrElse Not stream.CanRead Then
                Throw New NotSupportedException("Needs seekable and readable streams")
            End If

            'INSTANT VB TODO TASK: There is no equivalent to a 'checked' block in VB:
            '			checked
            _Length += stream.Length
            'INSTANT VB TODO TASK: End of the original C# 'checked' block.

            _streams.Add(_Length, stream)
        End Sub

        Public Overrides Function Read(ByVal buffer() As Byte, ByVal index As Integer, ByVal count As Integer) As Integer
            Dim num = 0

            Do While _current.Value IsNot Nothing AndAlso count > 0
                Dim r = _current.Value.Read(buffer, index, count)

                If r <= 0 Then
                    Exit Do
                End If

                Seek(r, SeekOrigin.Current)

                num += r
                index += r
                count -= r
            Loop

            Return num
        End Function

        Public Overrides Sub Write(ByVal buffer() As Byte, ByVal index As Integer, ByVal count As Integer)
            If Not CanWrite Then
                Throw New NotSupportedException()
            End If

            If _position = _Length AndAlso count > 0 AndAlso _Extendable Then
                AddStream(New MemoryStream(buffer, index, count, writable:=True, publiclyVisible:=True))

                Seek(count, SeekOrigin.Current)

                Return
            End If

            If _position >= _Length AndAlso count > 0 Then
                Throw New EndOfStreamException()
            End If

            Do While _current.Value IsNot Nothing AndAlso count > 0
                Dim current_count = CInt(Math.Min(count, _current.Value.Length - _current.Value.Position))

                _current.Value.Write(buffer, index, current_count)

                Seek(current_count, SeekOrigin.Current)

                index += current_count
                count -= current_count
            Loop
        End Sub

        Public Overrides Sub Flush()
            _current.Value?.Flush()
        End Sub

        Private _position As Long

        Public Overrides Property Position() As Long
            Get
                Return _position
            End Get
            Set(ByVal value As Long)
                Seek(value, SeekOrigin.Begin)
            End Set
        End Property

        Public Overrides ReadOnly Property Length() As Long

        Public Overrides Sub SetLength(ByVal value As Long)
            Throw New NotSupportedException()
        End Sub

        Public Overrides ReadOnly Property CanRead() As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property CanSeek() As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property CanWrite() As Boolean

        Public Overrides Function Seek(ByVal offset As Long, ByVal origin As SeekOrigin) As Long
            Select Case origin
                Case SeekOrigin.Current
                    offset += _position

                Case SeekOrigin.End
                    offset = Length + offset
            End Select

            If offset < 0 Then
                Throw New ArgumentException("Negative stream positions not supported")
            End If

            _current = _streams.FirstOrDefault(Function(s) s.Key > offset)

            If _current.Value IsNot Nothing Then
                _current.Value.Position = _current.Value.Length - (_current.Key - offset)
            End If

            _position = offset

            Return offset
        End Function

        Public Overrides Sub Close()
            _streams?.Dispose()

            MyBase.Close()
        End Sub
    End Class
End Namespace
