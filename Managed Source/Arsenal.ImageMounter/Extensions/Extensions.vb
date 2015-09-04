Imports System.Runtime.CompilerServices

Namespace Extensions

    Public Module Extensions

        <Extension>
        Public Iterator Function GetMessages(ex As Exception) As IEnumerable(Of String)

            While ex IsNot Nothing
                Yield ex.Message
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

    End Module

End Namespace
