Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Linq.Expressions
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.Reflection

#Disable Warning CA1051 ' Do not declare visible instance fields
#Disable Warning CA1010 ' Collections should implement generic interface
#Disable Warning CA1710 ' Identifiers should have correct suffix

Namespace IO

    Public MustInherit Class CsvReader
        Inherits MarshalByRefObject
        Implements IEnumerable, IEnumerator, IDisposable

        Protected ReadOnly _Delimiters As Char() = {","c}
        Protected ReadOnly _TextQuotes As Char() = {""""c}

        Public ReadOnly Property BaseReader As TextReader

        Public ReadOnly Property Delimiters As ReadOnlyCollection(Of Char)
            Get
                Return New ReadOnlyCollection(Of Char)(_Delimiters)
            End Get
        End Property

        Public ReadOnly Property TextQuotes As ReadOnlyCollection(Of Char)
            Get
                Return New ReadOnlyCollection(Of Char)(_TextQuotes)
            End Get
        End Property

        Protected MustOverride ReadOnly Property IEnumerable_Current As Object Implements IEnumerator.Current

        Protected Sub New(Reader As TextReader, Delimiters As Char(), TextQuotes As Char())

            If Delimiters IsNot Nothing Then
                _Delimiters = DirectCast(Delimiters.Clone(), Char())
            End If

            If TextQuotes IsNot Nothing Then
                _TextQuotes = DirectCast(TextQuotes.Clone(), Char())
            End If

            _BaseReader = Reader

        End Sub

        Public Shared Function Create(Of T As {Class, New})(FilePath As String) As CsvReader(Of T)
            Return New CsvReader(Of T)(FilePath)

        End Function

        Public Shared Function Create(Of T As {Class, New})(FilePath As String, delimiters As Char()) As CsvReader(Of T)
            Return New CsvReader(Of T)(FilePath, delimiters)

        End Function

        Public Shared Function Create(Of T As {Class, New})(FilePath As String, delimiters As Char(), textquotes As Char()) As CsvReader(Of T)
            Return New CsvReader(Of T)(FilePath, delimiters, textquotes)

        End Function

        Public Shared Function Create(Of T As {Class, New})(Reader As TextReader) As CsvReader(Of T)
            Return New CsvReader(Of T)(Reader)

        End Function

        Public Shared Function Create(Of T As {Class, New})(Reader As TextReader, delimiters As Char()) As CsvReader(Of T)
            Return New CsvReader(Of T)(Reader, delimiters)

        End Function

        Public Shared Function Create(Of T As {Class, New})(Reader As TextReader, delimiters As Char(), textquotes As Char()) As CsvReader(Of T)
            Return New CsvReader(Of T)(Reader, delimiters, textquotes)

        End Function

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)

            If Not Me.disposedValue Then

                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    _BaseReader?.Dispose()

                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                ' TODO: set large fields to null.

            End If

            Me.disposedValue = True

        End Sub

        ' TODO: override Finalize() only if Dispose( disposing As Boolean) above has code to free unmanaged resources.
        Protected Overrides Sub Finalize()
            ' Do not change this code.  Put cleanup code in Dispose( disposing As Boolean) above.
            Dispose(False)
            MyBase.Finalize()
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        Protected Overridable Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return Me
        End Function

        Public MustOverride Function MoveNext() As Boolean Implements IEnumerator.MoveNext

        Protected Overridable Sub Reset() Implements IEnumerator.Reset
            Throw New NotImplementedException()
        End Sub
#End Region

    End Class

    <ComVisible(False)>
    Public Class CsvReader(Of T As {Class, New})
        Inherits CsvReader
        Implements IEnumerable(Of T), IEnumerator(Of T)

        Protected ReadOnly _Properties As Action(Of T, String)()

        Public Sub New(FilePath As String)
            Me.New(File.OpenText(FilePath))

        End Sub

        Public Sub New(FilePath As String, delimiters As Char())
            Me.New(File.OpenText(FilePath), delimiters)

        End Sub

        Public Sub New(FilePath As String, delimiters As Char(), textquotes As Char())
            Me.New(File.OpenText(FilePath), delimiters, textquotes)

        End Sub

        Public Sub New(Reader As TextReader)
            Me.New(Reader, Nothing, Nothing)

        End Sub

        Public Sub New(Reader As TextReader, delimiters As Char())
            Me.New(Reader, delimiters, Nothing)

        End Sub

        Public Sub New(Reader As TextReader, delimiters As Char(), textquotes As Char())
            MyBase.New(Reader, delimiters, textquotes)

            Dim line = BaseReader.ReadLine()

            Dim field_names = line.Split(_Delimiters, StringSplitOptions.None)

            _Properties = Array.ConvertAll(field_names, AddressOf MembersStringSetter.GenerateReferenceTypeMemberSetter(Of T))

        End Sub

        Protected Overrides ReadOnly Property IEnumerable_Current As Object
            Get
                Return _Current
            End Get
        End Property

        Public ReadOnly Property Current As T Implements IEnumerator(Of T).Current

        Public Overrides Function MoveNext() As Boolean

            Dim line = BaseReader.ReadLine()

            If line Is Nothing Then
                _Current = Nothing
                Return False
            End If

            If line.Length = 0 Then
                _Current = Nothing
                Return True
            End If

            Dim fields As New List(Of String)(_Properties.Length)
            Dim startIdx = 0

            While startIdx < line.Length

                Dim scanIdx = startIdx
                If Array.IndexOf(_TextQuotes, line(scanIdx)) >= 0 AndAlso
                    scanIdx + 1 < line.Length Then

                    scanIdx = line.IndexOfAny(_TextQuotes, scanIdx + 1)

                    If scanIdx < 0 Then
                        scanIdx = startIdx
                    End If

                End If

                Dim i = line.IndexOfAny(_Delimiters, scanIdx)

                If i < 0 Then

                    fields.Add(line.Substring(startIdx).Trim(_TextQuotes))
                    Exit While

                End If

                fields.Add(line.Substring(startIdx, i - startIdx).Trim(_TextQuotes))
                startIdx = i + 1

            End While

            Dim obj As New T

            For i = 0 To Math.Min(fields.Count - 1, _Properties.GetUpperBound(0))

                If _Properties(i) Is Nothing Then
                    Continue For
                End If

                _Properties(i)(obj, fields(i))

            Next

            _Current = obj
            Return True

        End Function

        Public Overloads Function GetEnumerator() As IEnumerator(Of T) Implements IEnumerable(Of T).GetEnumerator
            Return Me
        End Function

    End Class

End Namespace
