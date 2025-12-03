//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Formatting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

#pragma warning disable IDE0079 // Remove unnecessary suppression

#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Extensions;

public static class CollectionExtensions
{
    private static class StaticHashAlgs<THashAlgorithm> where THashAlgorithm : HashAlgorithm, new()
    {
        [ThreadStatic]
        private static THashAlgorithm? instance;

        public static THashAlgorithm Instance => instance ??= new();
    }

    public static string CalculateChecksum<THashAlgorithm>(string file) where THashAlgorithm : HashAlgorithm, new()
    {
        using var stream = File.OpenRead(file);

        var hash = StaticHashAlgs<THashAlgorithm>.Instance.ComputeHash(stream);

        return hash.ToHexString();
    }

    public static string CalculateChecksum<THashAlgorithm>(Stream stream) where THashAlgorithm : HashAlgorithm, new()
    {
        var hash = StaticHashAlgs<THashAlgorithm>.Instance.ComputeHash(stream);

        return hash.ToHexString();
    }

#if NET5_0_OR_GREATER
    [Obsolete("Use HashData on static HashAlgorithm implementation instead")]
#endif
    public static string CalculateChecksum<THashAlgorithm>(this byte[] data) where THashAlgorithm : HashAlgorithm, new()
    {
        var hash = StaticHashAlgs<THashAlgorithm>.Instance.ComputeHash(data);

        return hash.ToHexString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddRange<T>(this List<T> list, params T[] collection) => list.AddRange(collection);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsSpan(this nint ptr, int length) =>
        new((void*)ptr, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<byte> AsReadOnlySpan(this nint ptr, int length) =>
        new((void*)ptr, length);

    public static bool TryDequeue<T>(this Queue<T> queue, out T? value)
    {
        if (queue.Count > 0)
        {
            value = queue.Dequeue();
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }
}
