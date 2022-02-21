Imports System.Globalization
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Threading

Namespace Extensions

    Public Module ExtensionMethods

        <Extension>
        Public Function Join(strings As IEnumerable(Of String), separator As String) As String

            Return String.Join(separator, strings)

        End Function

        <Extension>
        Public Function Join(strings As String(), separator As String) As String

            Return String.Join(separator, strings)

        End Function

        <Extension>
        Public Function Concat(strings As IEnumerable(Of String)) As String

            Return String.Concat(strings)

        End Function

        <Extension>
        Public Function Concat(strings As String()) As String

            Return String.Concat(strings)

        End Function

        <Extension>
        Public Sub QueueDispose(instance As IDisposable)

            ThreadPool.QueueUserWorkItem(Sub() instance.Dispose())

        End Sub

        <Extension>
        Public Function ToMembersString(o As Object) As String

            If o Is Nothing Then
                Return "{null}"
            Else
                Return TryCast(GetType(Reflection.MembersStringParser(Of )).
                    MakeGenericType(o.GetType()).
                    GetMethod("ToString", BindingFlags.Public Or BindingFlags.Static).
                    Invoke(Nothing, {o}), String)
            End If

        End Function

        <Extension>
        Public Function ToMembersString(Of T As Structure)(o As T) As String

            Return Reflection.MembersStringParser(Of T).ToString(o)

        End Function

        <Extension>
        Public Function ToHexString(bytes As ICollection(Of Byte), offset As Integer, count As Integer) As String

            If bytes Is Nothing OrElse offset > bytes.Count OrElse offset + count > bytes.Count Then
                Return Nothing
            End If

            Dim valuestr As New StringBuilder(count << 1)
            For i = offset To offset + count - 1
                valuestr.Append(bytes(i).ToString("x2"))
            Next

            Return valuestr.ToString()

        End Function

        <Extension>
        Public Function ToHexString(bytes As IEnumerable(Of Byte)) As String

            If bytes Is Nothing Then
                Return Nothing
            End If

            Dim valuestr As New StringBuilder
            For Each b In bytes
                valuestr.Append(b.ToString("x2"))
            Next

            Return valuestr.ToString()

        End Function

        Public Function ParseHexString(str As String) As Byte()

            Dim bytes = New Byte(0 To (str.Length >> 1) - 1) {}

            For i = 0 To bytes.Length - 1

                bytes(i) = Byte.Parse(str.Substring(i << 1, 2), NumberStyles.HexNumber)

            Next

            Return bytes

        End Function

        Public Iterator Function ParseHexString(str As IEnumerable(Of Char)) As IEnumerable(Of Byte)

            Dim buffer(0 To 1) As Char

            For Each c In str

                If buffer(0) = Nothing Then
                    buffer(0) = c
                Else
                    buffer(1) = c
                    Yield Byte.Parse(New String(buffer), NumberStyles.HexNumber)
                    Array.Clear(buffer, 0, 2)
                End If

            Next

        End Function

        Public Function ParseHexString(str As String, offset As Integer, count As Integer) As Byte()

            Dim bytes = New Byte(0 To (count >> 1) - 1) {}

            For i = 0 To count - 1

                bytes(i) = Byte.Parse(str.Substring((i + offset) << 1, 2), NumberStyles.HexNumber)

            Next

            Return bytes

        End Function

#If NETFRAMEWORK Then
        <Extension>
        Public Function Contains(str As String, chr As Char) As Boolean
            Return str.IndexOf(chr) >= 0
        End Function

        <Extension>
        Public Function Contains(str As String, substr As String) As Boolean
            Return str.IndexOf(substr) >= 0
        End Function

        <Extension>
        Public Function Contains(str As String, substr As String, comparison As StringComparison) As Boolean
            Return str.IndexOf(substr, comparison) >= 0
        End Function
#End If

    End Module

End Namespace
