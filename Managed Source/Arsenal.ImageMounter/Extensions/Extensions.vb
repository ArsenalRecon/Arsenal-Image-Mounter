Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports Microsoft.Win32

Namespace Extensions

    Public Module Extensions

        <Extension>
        Public Iterator Function GetMessages(ex As Exception) As IEnumerable(Of String)

            While ex IsNot Nothing
                If TypeOf ex Is TargetInvocationException Then

                ElseIf TypeOf ex Is AggregateException Then

                    Dim agex = DirectCast(ex, AggregateException)
                    For Each msg In agex.InnerExceptions.SelectMany(AddressOf GetMessages)
                        Yield msg
                    Next
                    Return

                ElseIf TypeOf ex Is ReflectionTypeLoadException Then

                    Dim ldex = DirectCast(ex, ReflectionTypeLoadException)
                    Yield ex.Message
                    For Each msg In ldex.LoaderExceptions.SelectMany(AddressOf GetMessages)
                        Yield msg
                    Next

                Else

                    Yield ex.Message

                End If

                ex = ex.InnerException
            End While

        End Function

        <Extension>
        Public Function JoinMessages(ex As Exception) As String

            Return ex.JoinMessages(" -> ")

        End Function

        <Extension>
        Public Function JoinMessages(ex As Exception, separator As String) As String

            Return String.Join(separator, ex.GetMessages())

        End Function

        <Extension>
        Public Iterator Function Enumerate(ex As Exception) As IEnumerable(Of Exception)

            While ex IsNot Nothing
                Yield ex

                If TypeOf ex Is AggregateException Then
                    Dim agex = DirectCast(ex, AggregateException)
                    For Each inner In agex.InnerExceptions.SelectMany(AddressOf Enumerate)
                        Yield inner
                    Next

                    Return
                End If

                ex = ex.InnerException
            End While

        End Function

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
        Public Sub QueueDispose(obj As IDisposable)

            ThreadPool.QueueUserWorkItem(Sub() obj.Dispose())

        End Sub

        <Extension>
        Public Sub SetValueSafe(Of T As Class)(RegKey As RegistryKey, name As String, value As T)

            If value Is Nothing Then

                RegKey.DeleteValue(name, throwOnMissingValue:=False)

            Else

                RegKey.SetValue(name, value)

            End If

        End Sub

        <Extension>
        Public Sub SetValueSafe(Of T As Class)(RegKey As RegistryKey, name As String, value As T, valueKind As RegistryValueKind)

            If value Is Nothing Then

                RegKey.DeleteValue(name, throwOnMissingValue:=False)

            Else

                RegKey.SetValue(name, value, valueKind)

            End If

        End Sub

    End Module

End Namespace
