using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Arsenal.ImageMounter.Reflection;

internal static class MembersStringParser<T>
{

    public static string ToString(T obj)
    {
        var values = from accessor in _accessors
                     select $"{accessor.Key} = {TryCall(accessor.Value, obj, ex => $"{{{ex.GetType()}: {ex.Message}}}")}";

        return $"{{{string.Join(", ", values)}}}";
    }

    private static readonly KeyValuePair<string, Func<T, string>>[] _accessors = GetAccessors();

    private static string? TryCall(Func<T, string> method, T param, Func<Exception, string> handler)
    {

        try
        {
            return method(param);
        }

        catch (Exception ex)
        {
            return handler is null ? null : handler(ex);

        }
    }

    private static KeyValuePair<string, Func<T, string>>[] GetAccessors()
    {

        var param_this = Expression.Parameter(typeof(T), "this");

        var ObjectToStringMethod = typeof(object).GetMethod("ToString")!;

        var fields = from member in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public)
                     select new KeyValuePair<string, MemberExpression>(member.Name, Expression.Field(param_this, member));

        var props = from member in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    where member.GetIndexParameters().Length == 0 && member.CanRead
                    select new KeyValuePair<string, MemberExpression>(member.Name, Expression.Property(param_this, member));

        var Accessors = (from field in fields.Concat(props)
                         let isnull = Expression.ReferenceEqual(Expression.TypeAs(field.Value, typeof(object)), Expression.Constant(null))
                         let fieldstring = Expression.Call(field.Value, ObjectToStringMethod)
                         let valueornull = Expression.Condition(isnull, Expression.Constant("(null)"), fieldstring)
                         let lambda = Expression.Lambda<Func<T, string>>(valueornull, param_this)
                         orderby field.Key
                         select new KeyValuePair<string, Func<T, string>>(field.Key, lambda.Compile())).ToArray();

        // Dim fieldgetters =
        // From field In GetType(T).GetFields(BindingFlags.Instance Or BindingFlags.Public)
        // Select New KeyValuePair(Of String, Func(Of T, String))(field.Name, Function(obj As T) field.GetValue(obj).ToString())

        // Dim propsgetters =
        // From prop In GetType(T).GetProperties(BindingFlags.Instance Or BindingFlags.Public)
        // Where prop.GetIndexParameters().Length = 0
        // Let getmethod = prop.GetGetMethod()
        // Where getmethod IsNot Nothing
        // Select New KeyValuePair(Of String, Func(Of T, String))(prop.Name, Function(obj As T) prop.GetValue(obj, Nothing).ToString())

        return Accessors;

    }
}