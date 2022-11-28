using Arsenal.ImageMounter.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Reflection;

[ComVisible(false)]
public static class ExpressionSupport
{

    private class ParameterCompatibilityComparer : IEqualityComparer<Type>
    {

        private static readonly ParameterCompatibilityComparer Instance = new();

        private static bool Equals(Type? x, Type? y) => ReferenceEquals(x, y) || (x?.IsAssignableFrom(y) ?? false);

        bool IEqualityComparer<Type>.Equals(Type? x, Type? y) => Equals(x, y);

        private static int GetHashCode(Type obj) => obj.GetHashCode();

        int IEqualityComparer<Type>.GetHashCode(Type obj) => GetHashCode(obj);

        public static bool Compatible(MethodInfo dest, Type sourceReturnType, Type[] sourceParameters)
            => dest.ReturnType.IsAssignableFrom(sourceReturnType)
            && dest.GetParameters().Select(dparam => dparam.ParameterType).SequenceEqual(sourceParameters, Instance);
    }

    public static Delegate CreateLocalFallbackFunction(string MethodName, Type[] GenericArguments, IEnumerable<Expression> MethodArguments, Type ReturnType, bool InvertResult, bool RuntimeMethodDetection)
    {

        var staticArgs = (from arg in MethodArguments
                          select arg.NodeType == ExpressionType.Quote ? ((UnaryExpression)arg).Operand : arg).ToArray();

        var staticArgsTypes = Array.ConvertAll(staticArgs, arg => arg.Type);

        var newMethod = GetCompatibleMethod(typeof(Enumerable), true, MethodName, GenericArguments, ReturnType, staticArgsTypes);

        if (newMethod is null)
        {
            throw new NotSupportedException("Expression calls unsupported method " + MethodName + ".");
        }

        // ' Substitute first argument (extension method source object) with a parameter that
        // ' will be substituted with the result sequence when resulting lambda conversion
        // ' routine is called locally after data has been fetched from external data service.
        var sourceObject = Expression.Parameter(newMethod.GetParameters()[0].ParameterType, "source");

        staticArgs[0] = sourceObject;

        Expression newCall = Expression.Call(newMethod, staticArgs);
        if (InvertResult)
        {
            newCall = Expression.Not(newCall);
        }

        var enumerableStaticDelegate = Expression.Lambda(newCall, sourceObject).Compile();

        if (RuntimeMethodDetection)
        {

            var instanceArgs = staticArgs.Skip(1).ToArray();
            var instanceArgsTypes = staticArgsTypes.Skip(1).ToArray();

            var delegateType = enumerableStaticDelegate.GetType();
            var delegateInvokeMethod = delegateType.GetMethod("Invoke") ??
                throw new InvalidProgramException();

            Expression<Func<object, Delegate>> getDelegateInstanceOrDefault = obj =>
                GetCompatibleMethodAsDelegate(obj.GetType(),
                                              false,
                                              InvertResult,
                                              MethodName,
                                              GenericArguments,
                                              ReturnType,
                                              instanceArgsTypes,
                                              sourceObject,
                                              instanceArgs) ?? enumerableStaticDelegate;

            var exprGetDelegateInstanceOrDefault = Expression.Invoke(getDelegateInstanceOrDefault, sourceObject);

            var exprCallDelegateInvokeMethod = Expression.Call(Expression.TypeAs(exprGetDelegateInstanceOrDefault, delegateType), delegateInvokeMethod, sourceObject);

            return Expression.Lambda(exprCallDelegateInvokeMethod, sourceObject).Compile();
        }
        else
        {

            return enumerableStaticDelegate;

        }
    }

    public static Delegate? GetCompatibleMethodAsDelegate(Type TypeToSearch,
                                                          bool FindStaticMethod,
                                                          bool InvertResult,
                                                          string MethodName,
                                                          Type[] GenericArguments,
                                                          Type ReturnType,
                                                          Type[] AlternateArgsTypes,
                                                          ParameterExpression Instance,
                                                          Expression[] Args)
    {

        var dynMethod = GetCompatibleMethod(TypeToSearch, FindStaticMethod, MethodName, GenericArguments, ReturnType, AlternateArgsTypes);
        if (dynMethod is null)
        {
            return null;
        }

        Expression callExpr;
        if (FindStaticMethod)
        {
            callExpr = Expression.Call(dynMethod, Args);
        }
        else
        {
            callExpr = Expression.Call(Expression.TypeAs(Instance, dynMethod.DeclaringType!), dynMethod, Args);
        }

        if (InvertResult)
        {
            callExpr = Expression.Not(callExpr);
        }

        return Expression.Lambda(callExpr, Instance).Compile();

    }

    private static readonly Dictionary<string, MethodInfo?> _GetCompatibleMethod_methodCache = new();

    public static MethodInfo? GetCompatibleMethod(Type TypeToSearch, bool FindStaticMethod, string MethodName, Type[] GenericArguments, Type ReturnType, Type[] AlternateArgsTypes)
    {

        var key = string.Concat(ReturnType.ToString(), ":", MethodName, ":", string.Join(":", Array.ConvertAll(AlternateArgsTypes, argType => argType.ToString())));

        lock (_GetCompatibleMethod_methodCache)
        {

            if (!_GetCompatibleMethod_methodCache.TryGetValue(key, out var newMethod))
            {

                var methodNames = new[] { MethodName, "get_" + MethodName };

                newMethod = (from m in TypeToSearch.GetMethods(BindingFlags.Public | (FindStaticMethod ? BindingFlags.Static : BindingFlags.Instance))
                             where methodNames.Contains(m.Name) && m.GetParameters().Length == AlternateArgsTypes.Length && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == GenericArguments.Length
                             select m.MakeGenericMethod(GenericArguments)).FirstOrDefault(m => ParameterCompatibilityComparer.Compatible(m, ReturnType, AlternateArgsTypes));

                if (newMethod is null && !FindStaticMethod)
                {
                    foreach (var interf in from i in TypeToSearch.GetInterfaces()
                                           where i.IsGenericType && i.GetGenericArguments().Length == GenericArguments.Length
                                           select i)
                    {

                        newMethod = interf.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => methodNames.Contains(m.Name) && ParameterCompatibilityComparer.Compatible(m, ReturnType, AlternateArgsTypes));

                        if (newMethod is not null)
                        {
                            break;
                        }
                    }
                }

                _GetCompatibleMethod_methodCache.Add(key, newMethod);

            }

            return newMethod;

        }
    }

    private static readonly Dictionary<Type, Type[]> _GetListItemsType_listItemsTypes = new();

    public static Type GetListItemsType(Type type)
    {

        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }

        Type[]? i = null;

        lock (_GetListItemsType_listItemsTypes)
        {

            if (!_GetListItemsType_listItemsTypes.TryGetValue(type, out i))
            {

                i = (from ifc in type.GetInterfaces()
                     where ifc.IsGenericType && ReferenceEquals(ifc.GetGenericTypeDefinition(), typeof(IList<>))
                     select ifc.GetGenericArguments()[0]).ToArray();

                _GetListItemsType_listItemsTypes.Add(type, i);

            }
        }

        if (i.Length == 0)
        {
            return type;
        }
        else if (i.Length == 1)
        {
            return i[0];
        }

        throw new NotSupportedException("More than one element type detected for list type " + type.ToString() + ".");

    }

    private sealed class ExpressionMemberEqualityComparer : IEqualityComparer<MemberInfo>
    {

        public bool Equals(MemberInfo? x, MemberInfo? y)
            => ReferenceEquals(x, y)
            || ReferenceEquals(x?.DeclaringType, y?.DeclaringType) && x?.MetadataToken == y?.MetadataToken;

        public int GetHashCode(MemberInfo obj)
            => HashCode.Combine(obj.DeclaringType?.MetadataToken, obj.MetadataToken);
    }

    public static SequenceEqualityComparer<MemberInfo> MemberSequenceEqualityComparer { get; }
        = new SequenceEqualityComparer<MemberInfo>(new ExpressionMemberEqualityComparer());

    private static readonly Dictionary<Type, Dictionary<IEnumerable<MemberInfo>, string>> _GetDataFieldMappings_dataMappings = new();

    public static Dictionary<IEnumerable<MemberInfo>, string> GetDataFieldMappings(Type ElementType)
    {

        lock (_GetDataFieldMappings_dataMappings)
        {

            if (!_GetDataFieldMappings_dataMappings.TryGetValue(ElementType, out var mappings))
            {

                var _mappings = from prop in ElementType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                where prop.MemberType == MemberTypes.Property && ((PropertyInfo)prop).GetIndexParameters().Length == 0 && ((PropertyInfo)prop).CanRead && ((PropertyInfo)prop).CanWrite || prop.MemberType == MemberTypes.Field && !((FieldInfo)prop).IsInitOnly
                                select (Props: (IEnumerable<MemberInfo>)SingleValueEnumerable.Get(prop), prop.Name);

                mappings = _mappings.ToDictionary(m => m.Props, m => m.Name, MemberSequenceEqualityComparer);

                var submappings = from props in mappings.Keys.ToArray()
                                  let prop = props.ElementAtOrDefault(0)
                                  where prop.MemberType == MemberTypes.Property && ((PropertyInfo)prop).GetIndexParameters().Length == 0 && ((PropertyInfo)prop).CanRead && ((PropertyInfo)prop).CanWrite || prop.MemberType == MemberTypes.Field && !((FieldInfo)prop).IsInitOnly
                                  let type = GetListItemsType(prop.MemberType == MemberTypes.Property ? ((PropertyInfo)prop).PropertyType : ((FieldInfo)prop).FieldType)
                                  where !type.IsPrimitive && !ReferenceEquals(type, typeof(string))
                                  from submapping in GetDataFieldMappings(type)
                                  select (Key: submapping.Key.Concat(props), Value: $"{prop.Name}.{submapping.Value}");

                foreach (var (Key, Value) in submappings)
                {
                    mappings.Add(Key, Value);
                }

                _GetDataFieldMappings_dataMappings.Add(ElementType, mappings);

            }

            return mappings;
        }
    }

    public static ReadOnlyCollection<string> GetPropertiesWithAttributes<TAttribute>(Type type) where TAttribute : Attribute => AttributedMemberFinder<TAttribute>.GetPropertiesWithAttributes(type);

    private static readonly Dictionary<Type, ReadOnlyCollection<string>> _GetPropertiesWithAttributes_cache = new();

    private class AttributedMemberFinder<TAttribute> where TAttribute : Attribute
    {

        public static ReadOnlyCollection<string> GetPropertiesWithAttributes(Type type)
        {

            lock (_GetPropertiesWithAttributes_cache)
            {

                if (!_GetPropertiesWithAttributes_cache.TryGetValue(type, out var prop))
                {
                    prop = Array.AsReadOnly((from p in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                             where Attribute.IsDefined(p, typeof(TAttribute))
                                             select p.Name).ToArray());

                    _GetPropertiesWithAttributes_cache.Add(type, prop);
                }

                return prop;
            }
        }
    }

    public static string? GetDataTableName<TContext>(Type entityType)
        => DataContextPropertyFinder<TContext>.GetDataTableName(entityType);

    private static readonly Dictionary<Type, string?> _GetDataTableName_properties = new();

    private class DataContextPropertyFinder<TContext>
    {

        public static string? GetDataTableName(Type entityType)
        {

            string? prop = null;

            lock (_GetDataTableName_properties)
            {

                if (!_GetDataTableName_properties.TryGetValue(entityType, out prop))
                {
                    prop = typeof(TContext)
                        .GetProperty("Items")?
                        .GetCustomAttributes(true)
                        .OfType<XmlElementAttribute>()
                        .Single(attr => attr.Type?.IsAssignableFrom(entityType) ?? false)
                        .ElementName;

                    _GetDataTableName_properties.Add(entityType, prop);
                }
            }

            return prop;

        }
    }

    public static Expression GetLambdaBody(Expression expression)
        => GetLambdaBody(expression, out var argparameters);

    public static Expression GetLambdaBody(Expression expression, out ReadOnlyCollection<ParameterExpression>? parameters)
    {

        if (expression.NodeType != ExpressionType.Quote)
        {
            parameters = null;
            return expression;
        }

        var expr = (LambdaExpression)((UnaryExpression)expression).Operand;

        parameters = expr.Parameters;

        return expr.Body;

    }

    private class PropertiesAssigners<T>
    {

        public static Dictionary<string, Func<T, object?>> Getters { get; }

        public static Dictionary<string, Action<T, object?>> Setters { get; }

        public static Dictionary<string, Type> Types { get; }

        static PropertiesAssigners()
        {

            var target = Expression.Parameter(typeof(T), "targetObject");

            var props = (from m in typeof(T).GetMembers(BindingFlags.Public | BindingFlags.Instance)
                         let p = m as PropertyInfo
                         let f = m as FieldInfo
                         where p is not null && p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 || f is not null && !f.IsInitOnly
                         let proptype = p is not null ? p.PropertyType : f.FieldType
                         let name = m.Name
                         let member = Expression.PropertyOrField(target, m.Name)
                         select (name, proptype, member)).ToArray();

            var getters = from m in props
                          select (m.name, valueconverted: m.proptype.IsValueType
                            ? Expression.Convert(m.member, typeof(object))
                            : Expression.TypeAs(m.member, typeof(object)));

            Getters = getters.ToDictionary(m => m.name, m => Expression.Lambda<Func<T, object?>>(m.valueconverted, target).Compile(), StringComparer.OrdinalIgnoreCase);

            var setters = from m in props
                          let value = Expression.Parameter(typeof(object), "value")
                          let valueconverted = m.proptype.IsValueType
                            ? Expression.ConvertChecked(value, m.proptype)
                            : (Expression)Expression.Condition(Expression.TypeIs(value, m.proptype),
                                                               Expression.TypeAs(value, m.proptype),
                                                               Expression.ConvertChecked(value, m.proptype))
                          select (m.name, assign: Expression.Assign(m.member, valueconverted), value);

            Setters = setters.ToDictionary(m => m.name, m => Expression.Lambda<Action<T, object?>>(m.assign, target, m.value).Compile(), StringComparer.OrdinalIgnoreCase);

            Types = props.ToDictionary(m => m.name, m => m.proptype, StringComparer.OrdinalIgnoreCase);

        }
    }

    public static Dictionary<string, Func<T, object?>> GetPropertyGetters<T>() where T : new()
        => new(PropertiesAssigners<T>.Getters, PropertiesAssigners<T>.Getters.Comparer);

    public static Dictionary<string, Action<T, object?>> GetPropertySetters<T>() where T : new()
        => new(PropertiesAssigners<T>.Setters, PropertiesAssigners<T>.Setters.Comparer);

    public static Dictionary<string, Type> GetPropertyTypes<T>() where T : new()
        => new(PropertiesAssigners<T>.Types, PropertiesAssigners<T>.Types.Comparer);

    public static T RecordToEntityObject<T>(IDataRecord record) where T : new()
        => RecordToEntityObject(record, new T());

    public static T RecordToEntityObject<T>(IDataRecord record, T obj)
    {

        var props = PropertiesAssigners<T>.Setters;

        for (int i = 0, loopTo = record.FieldCount - 1; i <= loopTo; i++)
        {
            if (props.TryGetValue(record.GetName(i), out var prop))
            {
                prop(obj, record[i] is DBNull ? null : record[i]);
            }
        }

        return obj;

    }
}