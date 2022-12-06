using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Reflection;

public static class MembersStringSetter
{
    internal static readonly MethodInfo EnumParseMethod = typeof(Enum)
        .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type), typeof(string) }, null)!;

    public static Action<T, string>? GenerateReferenceTypeMemberSetter<T>(string member_name)
        => MembersStringSetterType<T>.GenerateReferenceTypeMemberSetter(member_name);

    private static class MembersStringSetterType<T>
    {

        /// <summary>Generate a specific member setter for a specific reference type</summary>
        /// <param name="member_name">The member's name as defined in <typeparamref name="T"/></param>
        /// <returns>A compiled lambda which can access (set> the member</returns>
        internal static Action<T, string>? GenerateReferenceTypeMemberSetter(string member_name)
        {

            var param_this = Expression.Parameter(typeof(T), "this");

            var param_value = Expression.Parameter(typeof(string), "value");             // ' the member's new value

            var member_info = typeof(T).GetMember(member_name, BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => m is PropertyInfo or FieldInfo);

            Expression member;

            if (member_info is FieldInfo field_info)
            {
                if (field_info.IsInitOnly || field_info.IsLiteral)
                {
                    return null;
                }

                member = Expression.Field(param_this, field_info);
            }
            else if (member_info is PropertyInfo property_info)
            {
                if (!property_info.CanWrite)
                {
                    return null;
                }

                member = Expression.Property(param_this, property_info);
            }
            else
            {
                return null;
            }

            Expression assign_value;
            if (ReferenceEquals(member.Type, typeof(string)))
            {
                assign_value = param_value;
            }
            else if (member.Type.IsEnum)
            {
                assign_value = Expression.Convert(Expression.Call(EnumParseMethod, Expression.Constant(member.Type), param_value), member.Type);
            }
            else
            {
                var method = member.Type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (method is not null)
                {
                    assign_value = Expression.Call(method, param_value);
                }
                else
                {

                    var can_convert_from_string = TypeDescriptor.GetConverter(member.Type)?.CanConvertFrom(typeof(string));
                    if (!can_convert_from_string.GetValueOrDefault())
                    {
                        return null;
                    }

                    assign_value = Expression.Convert(param_value, member.Type);
                }
            }

            assign_value = Expression.Condition(Expression.ReferenceEqual(param_value, Expression.Constant(null)), Expression.Default(member.Type), assign_value);

            var assign = Expression.Assign(member, assign_value);                // ' i.e., 'this.member_name = value'

            var lambda = Expression.Lambda<Action<T, string>>(assign, param_this, param_value);

            return lambda.Compile();

        }
    }
}