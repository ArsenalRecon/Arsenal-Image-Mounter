Imports System.Linq.Expressions
Imports System.Reflection

Namespace Reflection

    Friend MustInherit Class MembersStringParser(Of T)

        Private Sub New()

        End Sub

        Public Overloads Shared Function ToString(obj As T) As String
            Dim values =
                From accessor In _accessors
                Select $"{accessor.Key} = {TryCall(accessor.Value, obj, Function(ex) $"{{{ex.GetType()}: {ex.Message}}}")}"

            Return $"{{{String.Join(", ", values)}}}"
        End Function

        Private Shared ReadOnly _accessors As KeyValuePair(Of String, Func(Of T, String))() = GetAccessors()

        Private Shared Function TryCall(method As Func(Of T, String), param As T, handler As Func(Of Exception, String)) As String

            Try
                Return method(param)

            Catch ex As Exception
                If handler Is Nothing Then
                    Return Nothing
                Else
                    Return handler(ex)
                End If

            End Try

        End Function

        Private Shared Function GetAccessors() As KeyValuePair(Of String, Func(Of T, String))()

            Dim param_this = Expression.Parameter(GetType(T), "this")

            Dim ObjectToStringMethod = GetType(Object).GetMethod("ToString")

            Dim fields =
                From member In GetType(T).GetFields(BindingFlags.Instance Or BindingFlags.Public)
                Select New KeyValuePair(Of String, MemberExpression)(member.Name, Expression.Field(param_this, member))

            Dim props =
                From member In GetType(T).GetProperties(BindingFlags.Instance Or BindingFlags.Public)
                Where
                    member.GetIndexParameters().Length = 0 AndAlso
                    member.CanRead
                Select New KeyValuePair(Of String, MemberExpression)(member.Name, Expression.Property(param_this, member))

            Dim Accessors =
                Aggregate field In fields.Concat(props)
                Let isnull = Expression.ReferenceEqual(Expression.TypeAs(field.Value, GetType(Object)), Expression.Constant(Nothing))
                Let fieldstring = Expression.Call(field.Value, ObjectToStringMethod)
                Let valueornull = Expression.Condition(isnull, Expression.Constant("(null)"), fieldstring)
                Let lambda = Expression.Lambda(Of Func(Of T, String))(valueornull, param_this)
                Select accessor = New KeyValuePair(Of String, Func(Of T, String))(field.Key, lambda.Compile())
                Order By accessor.Key
                Into ToArray()

            'Dim fieldgetters =
            '    From field In GetType(T).GetFields(BindingFlags.Instance Or BindingFlags.Public)
            '    Select New KeyValuePair(Of String, Func(Of T, String))(field.Name, Function(obj As T) field.GetValue(obj).ToString())

            'Dim propsgetters =
            '    From prop In GetType(T).GetProperties(BindingFlags.Instance Or BindingFlags.Public)
            '    Where prop.GetIndexParameters().Length = 0
            '    Let getmethod = prop.GetGetMethod()
            '    Where getmethod IsNot Nothing
            '    Select New KeyValuePair(Of String, Func(Of T, String))(prop.Name, Function(obj As T) prop.GetValue(obj, Nothing).ToString())

            Return Accessors

        End Function

    End Class

End Namespace
