using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Extensions;

public static class ExtensionMethods
{

    public static string Join(this IEnumerable<string> strings, string separator) => string.Join(separator, strings);

    public static string Join(this string[] strings, string separator) => string.Join(separator, strings);

#if NETSTANDARD || NETCOREAPP

    public static string Join(this IEnumerable<string> strings, char separator) => string.Join(separator, strings);

    public static string Join(this string[] strings, char separator) => string.Join(separator, strings);

#else

    public static string Join(this IEnumerable<string> strings, char separator) => string.Join(separator.ToString(), strings);

    public static string Join(this string[] strings, char separator) => string.Join(separator.ToString(), strings);

    public static string[] Split(this string str, char delimiter, StringSplitOptions options) => str.Split(new[] { delimiter }, options);

#endif

    public static string Concat(this IEnumerable<string> strings) => string.Concat(strings);

    public static string Concat(this string[] strings) => string.Concat(strings);

    public static void QueueDispose(this IDisposable instance) => ThreadPool.QueueUserWorkItem(o => instance.Dispose());

    public static string ToMembersString(this object? o) => o is null
            ? "{null}"
            : typeof(Reflection.MembersStringParser<>)
                .MakeGenericType(o.GetType())
                .GetMethod("ToString", BindingFlags.Public | BindingFlags.Static)?
                .Invoke(null, new[] { o }) as string ?? "(null)";

    public static string ToMembersString<T>(this T o) where T : struct => Reflection.MembersStringParser<T>.ToString(o);

}