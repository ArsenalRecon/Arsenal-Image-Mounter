//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Extensions;

public static class ExtensionMethods
{
    public static IEnumerable<string> EnumerateLines(this TextReader reader)
    {
        for (
            var line = reader.ReadLine();
            line is not null;
            line = reader.ReadLine())
        {
            yield return line;
        }
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    public static async IAsyncEnumerable<string> AsyncEnumerateLines(this TextReader reader)
    {
        for (
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            line is not null;
            line = await reader.ReadLineAsync().ConfigureAwait(false))
        {
            yield return line;
        }
    }
#endif

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
