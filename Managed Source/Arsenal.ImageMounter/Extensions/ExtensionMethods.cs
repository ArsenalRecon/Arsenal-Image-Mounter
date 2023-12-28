//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Async;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Arsenal.ImageMounter.Extensions;

public static class ExtensionMethods
{
    /// <summary>
    /// Queues dispose on a worker thread to avoid blocking calling thread.
    /// </summary>
    /// <param name="instance">Instance to dispose</param>
    public static void QueueDispose(this IDisposable instance)
        => ThreadPool.QueueUserWorkItem(_ =>
        {
            if (instance is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.QueueDispose();
                return;
            }

            try
            {
                instance.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception in {instance.GetType().FullName}.QueueDispose: {ex}");
            }
        });

    /// <summary>
    /// Queues dispose on a worker thread to avoid blocking calling thread.
    /// </summary>
    /// <param name="instance">Instance to dispose</param>
    public static void QueueDispose(this IAsyncDisposable instance)
        => ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                await instance.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception in {instance.GetType().FullName}.QueueDispose: {ex}");
            }
        });

    /// <summary>
    /// Creates a string with formatted values of all members of an object.
    /// </summary>
    /// <param name="o">Object</param>
    /// <returns>Formatted string</returns>
    public static string ToMembersString(this object? o)
        => o is null
        ? "{null}"
        : typeof(Reflection.MembersStringParser<>)
            .MakeGenericType(o.GetType())
            .GetMethod("ToString", BindingFlags.Public | BindingFlags.Static)?
            .Invoke(null, new[] { o }) as string ?? "(null)";

    /// <summary>
    /// Creates a string with formatted values of all members of an object.
    /// </summary>
    /// <typeparam name="T">Type of object to check for members</typeparam>
    /// <param name="o">Object</param>
    /// <returns>Formatted string</returns>
    public static string ToMembersString<T>(this T o) where T : struct
        => Reflection.MembersStringParser<T>.ToString(o);

}
