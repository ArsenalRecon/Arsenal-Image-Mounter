Imports System.ComponentModel
Imports System.Linq.Expressions
Imports System.Reflection

Namespace Reflection

    Public MustInherit Class MembersStringSetter

        Private Sub New()
        End Sub

        Friend Shared ReadOnly _EnumParse As MethodInfo = GetType([Enum]).GetMethod("Parse", BindingFlags.Public Or BindingFlags.Static, Nothing, {GetType(Type), GetType(String)}, Nothing)

        Public Shared Function GenerateReferenceTypeMemberSetter(Of T)(member_name As String) As Action(Of T, String)
            Return MembersStringSetter(Of T).GenerateReferenceTypeMemberSetter(member_name)
        End Function

        Private MustInherit Class MembersStringSetter(Of T)

            Private Sub New()
            End Sub

            ''' <summary>Generate a specific member setter for a specific reference type</summary>
            ''' <param name="member_name">The member's name as defined in <typeparamref name="T"/></param>
            ''' <returns>A compiled lambda which can access (set> the member</returns>
            Friend Shared Function GenerateReferenceTypeMemberSetter(member_name As String) As Action(Of T, String)

                Dim param_this = Expression.Parameter(GetType(T), "this")

                Dim param_value = Expression.Parameter(GetType(String), "value")             '' the member's new value

                Dim member_info = GetType(T).
                    GetMember(member_name, BindingFlags.FlattenHierarchy Or BindingFlags.IgnoreCase Or BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic).
                    FirstOrDefault(Function(m) TypeOf m Is PropertyInfo OrElse TypeOf m Is FieldInfo)

                Dim member As Expression

                If TypeOf member_info Is FieldInfo Then
                    Dim field_info = DirectCast(member_info, FieldInfo)
                    If field_info.IsInitOnly OrElse field_info.IsLiteral Then
                        Return Nothing
                    End If
                    member = Expression.Field(param_this, field_info)
                ElseIf TypeOf member_info Is PropertyInfo Then
                    Dim property_info = DirectCast(member_info, PropertyInfo)
                    If Not property_info.CanWrite Then
                        Return Nothing
                    End If
                    member = Expression.Property(param_this, property_info)
                Else
                    Return Nothing
                End If

                Dim assign_value As Expression
                If member.Type Is GetType(String) Then
                    assign_value = param_value
                ElseIf member.Type.IsEnum Then
                    assign_value = Expression.Convert(Expression.Call(_EnumParse, Expression.Constant(member.Type), param_value), member.Type)
                Else
                    Dim method = member.Type.GetMethod("Parse", BindingFlags.Public Or BindingFlags.Static, Nothing, {GetType(String)}, Nothing)
                    If method IsNot Nothing Then
                        assign_value = Expression.Call(method, param_value)
                    Else

                        Dim can_convert_from_string = TypeDescriptor.GetConverter(member.Type)?.CanConvertFrom(GetType(String))
                        If Not can_convert_from_string.GetValueOrDefault() Then
                            Return Nothing
                        End If

                        assign_value = Expression.Convert(param_value, member.Type)
                    End If
                End If
                assign_value = Expression.Condition(Expression.ReferenceEqual(param_value, Expression.Constant(Nothing)),
                                                    Expression.Default(member.Type),
                                                    assign_value)

                Dim assign = Expression.Assign(member, assign_value)                '' i.e., 'this.member_name = value'

                Dim lambda = Expression.Lambda(Of Action(Of T, String))(assign, param_this, param_value)

                Return lambda.Compile()

            End Function

        End Class

    End Class

End Namespace
