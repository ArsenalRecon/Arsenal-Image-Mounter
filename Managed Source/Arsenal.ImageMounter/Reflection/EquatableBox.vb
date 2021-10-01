Namespace Reflection

    Public NotInheritable Class EquatableBox(Of T As {Structure, IEquatable(Of T)})
        Implements IEquatable(Of T), IEquatable(Of EquatableBox(Of T))

        Public Property Value As T

        Public Sub New()
        End Sub

        Public Sub New(value As T)
            _Value = value
        End Sub

        Public ReadOnly Property HasDefaultValue As Boolean
            Get
                Return _Value.Equals(New T)
            End Get
        End Property

        Public Sub ClearValue()
            _Value = New T
        End Sub

        Shared Widening Operator CType(value As T) As EquatableBox(Of T)
            Return New EquatableBox(Of T)(value)
        End Operator

        Shared Widening Operator CType(box As EquatableBox(Of T)) As T
            Return box._Value
        End Operator

        Public Overrides Function ToString() As String
            Return _Value.ToString()
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return _Value.GetHashCode()
        End Function

        Public Overloads Function Equals(other As EquatableBox(Of T)) As Boolean Implements IEquatable(Of EquatableBox(Of T)).Equals
            Return _Value.Equals(other._Value)
        End Function

        Public Overloads Function Equals(other As T) As Boolean Implements IEquatable(Of T).Equals
            Return _Value.Equals(other)
        End Function

        Public Overloads Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj Is EquatableBox(Of T) Then
                Return _Value.Equals(DirectCast(obj, EquatableBox(Of T))._Value)
            ElseIf TypeOf obj Is T Then
                Return _Value.Equals(DirectCast(obj, T))
            Else
                Return MyBase.Equals(obj)
            End If
        End Function

    End Class

End Namespace
