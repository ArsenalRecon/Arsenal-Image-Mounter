Imports System.Management.Automation
Imports System.Reflection
Imports System.Linq.Expressions

Friend MustInherit Class FieldAssigner(Of T)

    Private Shared ReadOnly assigners As New Dictionary(Of String, Action(Of T, Object))

    Private Sub New()
    End Sub

    Shared Sub New()

        Dim fields =
            From fld In GetType(T).GetFields(BindingFlags.NonPublic Or BindingFlags.Instance)
                Where fld.Name.StartsWith("_", StringComparison.Ordinal)
                Let fldname = fld.Name.Substring(1)
                Let source = Expression.Parameter(GetType(Object), "source")
                Let target = Expression.Parameter(GetType(T), "target")
                Let field = Expression.Field(target, fld)
                Let targetbasetype = If(fld.FieldType.IsGenericType,
                                        fld.FieldType.GetGenericArguments()(0),
                                        fld.FieldType)
                Let compatible_compare = Expression.TypeEqual(source, targetbasetype)
                Let typedsource = If(fld.FieldType.IsValueType,
                                     Expression.Convert(source, targetbasetype),
                                     Expression.TypeAs(source, targetbasetype))
                Let value = If(fld.FieldType.IsGenericType,
                               DirectCast(Expression.[New](fld.FieldType.GetConstructor(fld.FieldType.GetGenericArguments()), {typedsource}), Expression),
                               DirectCast(typedsource, Expression))
                Let assign = Expression.Assign(field, value)
                Let default_value = Expression.Default(fld.FieldType)
                Let assign_default = Expression.Assign(field, default_value)
                Let conditional_assign = Expression.IfThenElse(compatible_compare, assign, assign_default)
                Let lambda = Expression.Lambda(Of Action(Of T, Object))(conditional_assign, target, source)
                Let action = lambda.Compile()

        assigners =
            fields.ToDictionary(Function(o) o.fldname,
                                Function(o) o.action)

    End Sub

    Public Shared Sub AssignFieldsFromPSObject(target As T, obj As PSObject)

        For Each commonprop In
            From prop In obj.Properties
            Join fld In assigners
            On prop.Name Equals fld.Key

            commonprop.fld.Value.Invoke(target, commonprop.prop.Value)

        Next

    End Sub

End Class
