Imports System.Runtime.InteropServices

Namespace Reflection

    <ComVisible(False)>
    Public NotInheritable Class SequenceEqualityComparer(Of T)
        Implements IEqualityComparer(Of IEnumerable(Of T))

        Public Property ItemComparer As IEqualityComparer(Of T)

        Public Sub New(comparer As IEqualityComparer(Of T))
            _ItemComparer = comparer
        End Sub

        Public Sub New()
            _ItemComparer = EqualityComparer(Of T).Default
        End Sub

        Public Overloads Function Equals(x As IEnumerable(Of T), y As IEnumerable(Of T)) As Boolean Implements IEqualityComparer(Of IEnumerable(Of T)).Equals
            Return x.SequenceEqual(y, _ItemComparer)
        End Function

        Public Overloads Function GetHashCode(obj As IEnumerable(Of T)) As Integer Implements IEqualityComparer(Of IEnumerable(Of T)).GetHashCode
            Dim result As Integer
            For Each item In obj
                result = result Xor _ItemComparer.GetHashCode(item)
            Next
            Return result
        End Function

    End Class

    <ComVisible(False)>
    Public NotInheritable Class SequenceComparer(Of T)
        Implements IComparer(Of IEnumerable(Of T))

        Public Property ItemComparer As IComparer(Of T)

        Public Sub New(comparer As IComparer(Of T))
            _ItemComparer = comparer
        End Sub

        Public Sub New()
            _ItemComparer = Comparer(Of T).Default
        End Sub

        Public Function Compare(x As IEnumerable(Of T), y As IEnumerable(Of T)) As Integer Implements IComparer(Of IEnumerable(Of T)).Compare
            Dim value As Integer
            Using enumx = x.GetEnumerator()
                Using enumy = y.GetEnumerator()
                    While enumx.MoveNext() AndAlso enumy.MoveNext()
                        value = _ItemComparer.Compare(enumx.Current, enumy.Current)
                        If value <> 0 Then
                            Exit While
                        End If
                    End While
                End Using
            End Using
            Return value
        End Function
    End Class

End Namespace
