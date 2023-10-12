//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Extensions;

public static class ExtensionMethods
{
    public static void QueueDispose(this IDisposable instance) => ThreadPool.QueueUserWorkItem(o => instance.Dispose());

    public static string ToMembersString(this object? o) => o is null
            ? "{null}"
            : typeof(Reflection.MembersStringParser<>)
                .MakeGenericType(o.GetType())
                .GetMethod("ToString", BindingFlags.Public | BindingFlags.Static)?
                .Invoke(null, new[] { o }) as string ?? "(null)";

    public static string ToMembersString<T>(this T o) where T : struct => Reflection.MembersStringParser<T>.ToString(o);
}
