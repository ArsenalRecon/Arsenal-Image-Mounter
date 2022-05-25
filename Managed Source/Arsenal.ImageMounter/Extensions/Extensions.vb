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
        Public Function Join(strings As String(), separator As Char) As String

            Return String.Join(separator, strings)

        End Function

#If NETFRAMEWORK Then
        <Extension>
        Public Function Split(str As String, delimiter As Char, options As StringSplitOptions) As String()
            Return str.Split({delimiter}, options)
        End Function
#End If

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

    End Module

End Namespace
