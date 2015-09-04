Imports System.Management.Automation
Imports System.Reflection
Imports System.Linq.Expressions

Namespace PSDisk

    Friend MustInherit Class FieldAssigner(Of T)

        Private Shared ReadOnly assigners As New Dictionary(Of String, Action(Of T, Object))

        Private Sub New()
        End Sub

        Shared Sub New()

            Dim fields =
                From fld In GetType(T).GetFields(BindingFlags.NonPublic Or BindingFlags.Instance)
                    Where fld.Name.StartsWith("_", StringComparison.Ordinal)
                    Let source = Expression.Parameter(GetType(Object), "source")
                    Let target = Expression.Parameter(GetType(T), "target")
                    Let field = Expression.Field(target, fld)
                    Let nullable_base_type = If(Nullable.GetUnderlyingType(fld.FieldType),
                                                fld.FieldType)
                    Let enum_base_type = If(nullable_base_type.IsEnum,
                                            nullable_base_type.GetEnumUnderlyingType(),
                                            nullable_base_type)
                    Let compatible_compare = Expression.TypeEqual(source, enum_base_type)
                    Let source_as_nullable_base = If(fld.FieldType.IsValueType,
                                                     Expression.Convert(source, nullable_base_type),
                                                     Expression.TypeAs(source, fld.FieldType))
                    Let value = If(Nullable.GetUnderlyingType(fld.FieldType) Is Nothing,
                                   DirectCast(source_as_nullable_base, Expression),
                                   Expression.[New](fld.FieldType.GetConstructor(fld.FieldType.GetGenericArguments()),
                                                              {source_as_nullable_base}))
                    Let assign = Expression.Assign(field, value)
                    Let default_value = Expression.Default(fld.FieldType)
                    Let assign_default = Expression.Assign(field, default_value)
                    Let conditional_assign = Expression.IfThenElse(compatible_compare, assign, assign_default)
                    Let lambda = Expression.Lambda(Of Action(Of T, Object))(conditional_assign, target, source)
                    Select
                        fldname = fld.Name.Substring(1),
                        action = lambda.Compile()

            assigners =
                fields.ToDictionary(Function(o) o.fldname,
                                    Function(o) o.action)

        End Sub

        Public Shared Sub AssignFieldsFromPSObject(target As T, obj As PSObject)

            For Each commonprop In
                From prop In obj.Properties
                Join fld In assigners
                On prop.Name Equals fld.Key

                Try
                    commonprop.fld.Value.Invoke(target, commonprop.prop.Value)

                Catch ex As Exception

                End Try

            Next

        End Sub

    End Class

End Namespace
