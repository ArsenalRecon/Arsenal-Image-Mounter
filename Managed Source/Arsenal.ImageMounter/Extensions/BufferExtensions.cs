//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using Arsenal.ImageMounter.Reflection;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Extensions;

public static partial class BufferExtensions
{
#if NETCOREAPP
    static BufferExtensions()
    {
        NativeLibrary.SetDllImportResolver(typeof(BufferExtensions).Assembly, NativeCalls.CrtDllImportResolver);
    }
#endif

    public static IEnumerable<Exception> Enumerate(this Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is TargetInvocationException)
            {
                ex = ex.InnerException;
            }
            else if (ex is AggregateException aex)
            {
                foreach (var iex in aex.InnerExceptions.SelectMany(Enumerate))
                {
                    yield return iex;
                }

                yield break;
            }
            else if (ex is ReflectionTypeLoadException rtlex)
            {
                yield return ex;

                foreach (var iex in rtlex.LoaderExceptions.SelectMany(Enumerate))
                {
                    yield return iex;
                }

                ex = ex.InnerException;
            }
            else
            {
                yield return ex;

                ex = ex.InnerException;
            }
        }
    }

    public static IEnumerable<string> EnumerateMessages(this Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is TargetInvocationException)
            {
                ex = ex.InnerException;
            }
            else if (ex is AggregateException agex)
            {
                foreach (var msg in agex.InnerExceptions.SelectMany(EnumerateMessages))
                {
                    yield return msg;
                }

                yield break;
            }
            else if (ex is ReflectionTypeLoadException tlex)
            {
                yield return ex.Message;

                foreach (var msg in tlex.LoaderExceptions.SelectMany(EnumerateMessages))
                {
                    yield return msg;
                }

                ex = ex.InnerException;
            }
            else if (ex is Win32Exception win32ex)
            {
                yield return $"{win32ex.Message} ({win32ex.NativeErrorCode})";

                ex = ex.InnerException;
            }
            else
            {
                yield return ex.Message;

                ex = ex.InnerException;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string JoinMessages(this Exception exception) =>
        exception.JoinMessages(Environment.NewLine + Environment.NewLine);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string JoinMessages(this Exception exception, string separator) =>
        string.Join(separator, exception.EnumerateMessages());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? FormatLogMessages(this Exception exception) =>
#if DEBUG
        Debugger.IsAttached
        ? exception.JoinMessages()
        : exception?.ToString();
#else
        exception.JoinMessages();
#endif

    /// <summary>
    /// Workaround for Visual Basic Span consumers
    /// </summary>
    /// <typeparam name="T">Type of elements of span</typeparam>
    /// <param name="span">span</param>
    /// <param name="index">index of element in span to return</param>
    /// <returns>Copy of element at position</returns>
    public static T GetItem<T>(this ReadOnlySpan<T> span, int index) => span[index];

    /// <summary>
    /// Workaround for Visual Basic Span consumers
    /// </summary>
    /// <typeparam name="T">Type of elements of span</typeparam>
    /// <param name="span">span</param>
    /// <param name="index">index of element in span to return</param>
    /// <returns>Copy of element at position</returns>
    public static T GetItem<T>(this Span<T> span, int index) => span[index];

    /// <summary>
    /// Workaround for Visual Basic Span consumers
    /// </summary>
    /// <typeparam name="T">Type of elements of span</typeparam>
    /// <param name="memory">span</param>
    /// <param name="index">index of element in span to set</param>
    /// <param name="item">reference to item to assign to index in span</param>
    public static void SetItem<T>(this Memory<T> memory, int index, in T item) => memory.Span[index] = item;

    /// <summary>
    /// Workaround for Visual Basic Span consumers
    /// </summary>
    /// <typeparam name="T">Type of elements of span</typeparam>
    /// <param name="span">span</param>
    /// <param name="index">index of element in span to set</param>
    /// <param name="item">reference to item to assign to index in span</param>
    public static void SetItem<T>(this Span<T> span, int index, in T item) => span[index] = item;

    /// <summary>
    /// Workaround for Visual Basic Span consumers
    /// </summary>
    /// <typeparam name="T">Type of elements of span</typeparam>
    /// <param name="memory">span</param>
    /// <param name="index">index of element in span to return</param>
    /// <returns>Copy of element at position</returns>
    public static T GetItem<T>(this ReadOnlyMemory<T> memory, int index) => memory.Span[index];

    /// <summary>
    /// Workaround for Visual Basic Span consumers
    /// </summary>
    /// <typeparam name="T">Type of elements of span</typeparam>
    /// <param name="memory">span</param>
    /// <param name="index">index of element in span to return</param>
    /// <returns>Copy of element at position</returns>
    public static T GetItem<T>(this Memory<T> memory, int index) => memory.Span[index];

    /// <summary>
    /// Parses a multi-string where each string is terminated by null char
    /// and the whole buffer is terminated by double null chars.
    /// </summary>
    /// <param name="bytes">Memory that contains the double null terminated string</param>
    /// <returns>Each individual string in the buffer</returns>
    public static IEnumerable<string> ParseDoubleTerminatedString(this Memory<byte> bytes) =>
        ParseDoubleTerminatedString((ReadOnlyMemory<byte>)bytes);

    /// <summary>
    /// Parses a multi-string where each string is terminated by null char
    /// and the whole buffer is terminated by double null chars.
    /// </summary>
    /// <param name="bytes">Memory that contains the double null terminated string</param>
    /// <returns>Each individual string in the buffer</returns>
    public static IEnumerable<string> ParseDoubleTerminatedString(this ReadOnlyMemory<byte> bytes)
    {
        var endpos = MemoryMarshal.Cast<byte, char>(bytes.Span).IndexOf('\0');

        while (endpos > 0)
        {
            yield return MemoryMarshal.Cast<byte, char>(bytes.Span).Slice(0, endpos).ToString();

            bytes = bytes.Slice((endpos + 1) << 1);

            endpos = MemoryMarshal.Cast<byte, char>(bytes.Span).IndexOf('\0');
        }
    }

    /// <summary>
    /// Parses a multi-string where each string is terminated by null char
    /// and the whole buffer is terminated by double null chars.
    /// </summary>
    /// <param name="chars">Memory that contains the double null terminated string</param>
    /// <returns>Each individual string in the buffer</returns>
    public static IEnumerable<ReadOnlyMemory<char>> ParseDoubleTerminatedString(this Memory<char> chars) =>
        ParseDoubleTerminatedString((ReadOnlyMemory<char>)chars);

    /// <summary>
    /// Parses a multi-string where each string is terminated by null char
    /// and the whole buffer is terminated by double null chars.
    /// </summary>
    /// <param name="chars">Memory that contains the double null terminated string</param>
    /// <returns>Each individual string in the buffer</returns>
    public static IEnumerable<ReadOnlyMemory<char>> ParseDoubleTerminatedString(this ReadOnlyMemory<char> chars)
    {
        var endpos = chars.Span.IndexOf('\0');

        while (endpos > 0)
        {
            yield return chars.Slice(0, endpos);

            chars = chars.Slice(endpos + 1);

            endpos = chars.Span.IndexOf('\0');
        }
    }

    /// <summary>
    /// Return position of first empty element, or the entire span length if
    /// no empty elements are found.
    /// </summary>
    /// <param name="buffer">Span to search</param>
    /// <returns>Position of first found empty element or entire span length if none found</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfTerminator<T>(this ReadOnlySpan<T> buffer) where T : unmanaged, IEquatable<T>
    {
        var endpos = buffer.IndexOf(default(T));
        return endpos >= 0 ? endpos : buffer.Length;
    }

    /// <summary>
    /// Return position of first empty element, or the entire span length if
    /// no empty elements are found.
    /// </summary>
    /// <param name="buffer">Span to search</param>
    /// <returns>Position of first found empty element or entire span length if none found</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfTerminator<T>(this Span<T> buffer) where T : unmanaged, IEquatable<T>
    {
        var endpos = buffer.IndexOf(default(T));
        return endpos >= 0 ? endpos : buffer.Length;
    }

    /// <summary>
    /// Reads null terminated ASCII string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <param name="offset">Offset in byte buffer where the string starts</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedAsciiString(byte[] buffer, int offset)
    {
        var endpos = buffer.AsSpan(offset).IndexOfTerminator();
        return Encoding.ASCII.GetString(buffer, offset, endpos);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP

    /// <summary>
    /// Reads null terminated ASCII string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedAsciiString(this ReadOnlySpan<byte> buffer)
    {
        var endpos = buffer.IndexOfTerminator();
        return Encoding.ASCII.GetString(buffer[..endpos]);
    }

    /// <summary>
    /// Reads null terminated ASCII string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedAsciiString(this Span<byte> buffer)
    {
        var endpos = buffer.IndexOfTerminator();
        return Encoding.ASCII.GetString(buffer[..endpos]);
    }

#else

    /// <summary>
    /// Reads null terminated ASCII string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedAsciiString(this ReadOnlySpan<byte> buffer)
    {
        var endpos = buffer.IndexOfTerminator();
        return Encoding.ASCII.GetString(buffer.Slice(0, endpos).ToArray());
    }

#endif

    /// <summary>
    /// Reads null terminated Unicode string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <param name="offset">Offset in byte buffer where the string starts</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> ReadNullTerminatedUnicode(byte[] buffer, int offset)
        => ReadNullTerminatedUnicode(MemoryMarshal.Cast<byte, char>(buffer.AsSpan(offset)));

    /// <summary>
    /// Reads null terminated Unicode string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<char> ReadNullTerminatedUnicode(this Span<byte> buffer)
        => ReadNullTerminatedUnicode(MemoryMarshal.Cast<byte, char>(buffer));

    /// <summary>
    /// Reads null terminated Unicode string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> ReadNullTerminatedUnicode(this ReadOnlySpan<byte> buffer)
        => ReadNullTerminatedUnicode(MemoryMarshal.Cast<byte, char>(buffer));

    /// <summary>
    /// Reads null terminated Unicode string from char buffer.
    /// </summary>
    /// <param name="chars">Buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> ReadNullTerminatedUnicode(this ReadOnlySpan<char> chars)
    {
        var endpos = chars.IndexOfTerminator();
        return chars.Slice(0, endpos);
    }

    /// <summary>
    /// Reads null terminated Unicode string from char buffer.
    /// </summary>
    /// <param name="chars">Buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<char> ReadNullTerminatedUnicode(this Span<char> chars)
    {
        var endpos = chars.IndexOfTerminator();
        return chars.Slice(0, endpos);
    }

    /// <summary>
    /// Reads null terminated Unicode string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <param name="offset">Offset in byte buffer where the string starts</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedUnicodeString(byte[] buffer, int offset)
        => ReadNullTerminatedUnicodeString(MemoryMarshal.Cast<byte, char>(buffer.AsSpan(offset)));

    /// <summary>
    /// Reads null terminated Unicode string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedUnicodeString(this Span<byte> buffer)
        => ReadNullTerminatedUnicodeString(MemoryMarshal.Cast<byte, char>(buffer));

    /// <summary>
    /// Reads null terminated Unicode string from byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedUnicodeString(this ReadOnlySpan<byte> buffer)
        => ReadNullTerminatedUnicodeString(MemoryMarshal.Cast<byte, char>(buffer));

    /// <summary>
    /// Reads null terminated Unicode string from char buffer.
    /// </summary>
    /// <param name="chars">Buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedUnicodeString(this ReadOnlySpan<char> chars)
    {
        var endpos = chars.IndexOfTerminator();
        return chars.Slice(0, endpos).ToString();
    }

    /// <summary>
    /// Reads null terminated Unicode string from char buffer.
    /// </summary>
    /// <param name="chars">Buffer</param>
    /// <returns>Managed string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadNullTerminatedUnicodeString(this Span<char> chars)
    {
        var endpos = chars.IndexOfTerminator();
        return chars.Slice(0, endpos).ToString();
    }

    /// <summary>
    /// Reads null terminated Unicode string from char buffer.
    /// </summary>
    /// <param name="chars">Buffer</param>
    /// <returns>Memory region up to null terminator</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<char> ReadNullTerminatedUnicodeString(this ReadOnlyMemory<char> chars)
    {
        var endpos = chars.Span.IndexOfTerminator();
        return chars.Slice(0, endpos);
    }

    /// <summary>
    /// Reads null terminated Unicode string from char buffer.
    /// </summary>
    /// <param name="chars">Buffer</param>
    /// <returns>Memory region up to null terminator</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<char> ReadNullTerminatedUnicodeString(this Memory<char> chars)
    {
        var endpos = chars.Span.IndexOfTerminator();
        return chars.Slice(0, endpos);
    }

    public static byte[] ParseHexString(string str)
    {

        var bytes = new byte[(str.Length >> 1)];

        for (int i = 0, loopTo = bytes.Length - 1; i <= loopTo; i++)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            bytes[i] = byte.Parse(str.AsSpan(i << 1, 2), NumberStyles.HexNumber);
#else
            bytes[i] = byte.Parse(str.Substring(i << 1, 2), NumberStyles.HexNumber);
#endif
        }

        return bytes;
    }

    public static byte[] ParseHexString(ReadOnlySpan<char> str)
    {
        var bytes = new byte[(str.Length >> 1)];

        for (int i = 0, loopTo = bytes.Length - 1; i <= loopTo; i++)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            bytes[i] = byte.Parse(str.Slice(i << 1, 2), NumberStyles.HexNumber);
#else
            bytes[i] = byte.Parse(str.Slice(i << 1, 2).ToString(), NumberStyles.HexNumber);
#endif
        }

        return bytes;
    }

    public static IEnumerable<byte> ParseHexString(IEnumerable<char> str)
    {
        var buffer = ArrayPool<char>.Shared.Rent(2);
        try
        {
            foreach (var c in str)
            {
                if (buffer[0] == '\0')
                {
                    buffer[0] = c;
                }
                else
                {
                    buffer[1] = c;
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                    yield return byte.Parse(buffer.AsSpan(0, 2), NumberStyles.HexNumber);
#else
                    yield return byte.Parse(new string(buffer, 0, 2), NumberStyles.HexNumber);
#endif
                    buffer[0] = '\0';
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    public static byte[] ParseHexString(string str, int offset, int count) =>
        ParseHexString(str.AsSpan(offset, count));

    public static string? ToHexString(this IReadOnlyCollection<byte> data) => data.ToHexString(null);

    public static string? ToHexString(this IReadOnlyCollection<byte> data, string? delimiter)
    {
        if (data is null)
        {
            return null;
        }

        if (data.Count == 0)
        {
            return string.Empty;
        }

        var capacity = data.Count << 1;
        if (delimiter is not null)
        {
            capacity += delimiter.Length * (data.Count - 1);
        }

#if NETCOREAPP
        var result = string.Create(capacity,
            (data, delimiter, capacity),
            static (ptr, v) =>
            {
                foreach (var b in v.data)
                {
                    if (v.delimiter is not null && ptr.Length < v.capacity)
                    {
                        v.delimiter.AsSpan().CopyTo(ptr);
                        ptr = ptr.Slice(v.delimiter.Length);
                    }

                    b.TryFormat(ptr, out _, "x2", NumberFormatInfo.InvariantInfo);
                    ptr = ptr.Slice(2);
                }
            });
#else
        var result = new string('\0', capacity);

        var ptr = MemoryMarshal.AsMemory(result.AsMemory()).Span;

        foreach (var b in data)
        {
            if (delimiter is not null && ptr.Length < capacity)
            {
                delimiter.AsSpan().CopyTo(ptr);
                ptr = ptr.Slice(delimiter.Length);
            }

            b.ToString("x2", NumberFormatInfo.InvariantInfo).AsSpan().CopyTo(ptr);
            ptr = ptr.Slice(2);
        }
#endif

        return result;
    }

    public static string ToHexString(this byte[] data) => ((ReadOnlySpan<byte>)data).ToHexString(default);

    public static string ToHexString(this byte[] data, string? delimiter) => ((ReadOnlySpan<byte>)data).ToHexString(delimiter.AsSpan());

    public static string ToHexString(this byte[] data, int offset, int count) => data.AsSpan(offset, count).ToHexString(null);

    public static string ToHexString(this byte[] data, int offset, int count, string? delimiter) => data.AsSpan(offset, count).ToHexString(delimiter);

    public static string ToHexString(this Span<byte> data) => ((ReadOnlySpan<byte>)data).ToHexString(null);

    public static string ToHexString(this Span<byte> data, string? delimiter) => ((ReadOnlySpan<byte>)data).ToHexString(delimiter.AsSpan());

    public static string ToHexString(this ReadOnlySpan<byte> data) => data.ToHexString(null);

    public static TextWriter WriteHex(this TextWriter writer, IEnumerable<byte> bytes)
    {
        var i = 0;
        foreach (var line in bytes.FormatHexLines())
        {
            writer.Write(((ushort)(i >> 16)).ToString("X4"));
            writer.Write(' ');
            writer.Write(((ushort)i).ToString("X4"));
            writer.Write("  ");
            writer.WriteLine(line);
            i += 0x10;
        }

        return writer;
    }

    public static IEnumerable<string> FormatHexLines(this IEnumerable<byte> bytes)
    {
        var sb = ArrayPool<char>.Shared.Rent(67);
        try
        {
            byte pos = 0;
            foreach (var b in bytes)
            {
                if (pos == 0)
                {
                    "                        -                                          ".CopyTo(0, sb, 0, 67);
                }

#if NETCOREAPP
                var bstr = 0;
                if ((pos & 8) == 0)
                {
                    bstr = pos * 3;
                }
                else
                {
                    bstr = 2 + pos * 3;
                }
                b.TryFormat(sb.AsSpan(bstr), out _, "X2");
#else
                var bstr = b.ToString("X2");
                if ((pos & 8) == 0)
                {
                    sb[pos * 3] = bstr[0];
                    sb[pos * 3 + 1] = bstr[1];
                }
                else
                {
                    sb[2 + pos * 3] = bstr[0];
                    sb[2 + pos * 3 + 1] = bstr[1];
                }
#endif

                sb[51 + pos] = char.IsControl((char)b) ? '.' : (char)b;

                pos++;
                pos &= 0xf;

                if (pos == 0)
                {
                    yield return new(sb, 0, 67);
                }
            }

            if (pos > 0)
            {
                yield return new(sb, 0, 67);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(sb);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> ToEnumerable<T>(this Memory<T> span) => MemoryMarshal.ToEnumerable((ReadOnlyMemory<T>)span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> ToEnumerable<T>(this ReadOnlyMemory<T> span) => MemoryMarshal.ToEnumerable(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> AsChars(this byte[] bytes) => MemoryMarshal.Cast<byte, char>(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> AsChars(this Memory<byte> bytes) => MemoryMarshal.Cast<byte, char>(bytes.Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> AsChars(this ReadOnlyMemory<byte> bytes) => MemoryMarshal.Cast<byte, char>(bytes.Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> AsChars(this Span<byte> bytes) => MemoryMarshal.Cast<byte, char>(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> AsChars(this ReadOnlySpan<byte> bytes) => MemoryMarshal.Cast<byte, char>(bytes);

    public ref struct StringSplitByCharEnumerator
    {
        private readonly char delimiter;
        private readonly StringSplitOptions options;
        private readonly bool reverse;

        private ReadOnlySpan<char> chars;

        public ReadOnlySpan<char> Current { get; private set; }

        public bool MoveNext() => reverse ? MoveNextReverse() : MoveNextForward();

        public bool MoveNextForward()
        {
            while (!chars.IsEmpty)
            {

                var i = chars.IndexOf(delimiter);
                if (i < 0)
                {
                    i = chars.Length;
                }

                Current = chars.Slice(0, i);

                if (i < chars.Length)
                {
                    chars = chars.Slice(i + 1);
                }
                else
                {
                    chars = default;
                }

#if NET5_0_OR_GREATER
                if (options.HasFlag(StringSplitOptions.TrimEntries))
                {
                    Current = Current.Trim();
                }
#endif

                if (!Current.IsEmpty ||
                    !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                {
                    return true;
                }
            }

            return false;
        }

        public bool MoveNextReverse()
        {
            while (!chars.IsEmpty)
            {
                var i = chars.LastIndexOf(delimiter);

                Current = i >= 0 ? chars.Slice(i + 1) : chars;

                if (i < 0)
                {
                    chars = default;
                }
                else
                {
                    chars = chars.Slice(0, i);
                }

#if NET5_0_OR_GREATER
                if (options.HasFlag(StringSplitOptions.TrimEntries))
                {
                    Current = Current.Trim();
                }
#endif

                if (!Current.IsEmpty ||
                    !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                {
                    return true;
                }
            }

            return false;
        }

        public ReadOnlySpan<char> First() => MoveNext() ? Current : throw new InvalidOperationException();

        public ReadOnlySpan<char> FirstOrDefault() => MoveNext() ? Current : default;

        public ReadOnlySpan<char> Last()
        {
            var found = false;
            ReadOnlySpan<char> result = default;

            while (MoveNext())
            {
                found = true;
                result = Current;
            }

            if (found)
            {
                return result;
            }

            throw new InvalidOperationException();
        }

        public ReadOnlySpan<char> LastOrDefault()
        {
            var found = false;
            ReadOnlySpan<char> result = default;

            while (MoveNext())
            {
                found = true;
                result = Current;
            }

            if (found)
            {
                return result;
            }

            return default;
        }

        public ReadOnlySpan<char> ElementAt(int pos)
        {
            for (var i = 0; i <= pos; i++)
            {
                if (!MoveNext())
                {
                    throw new ArgumentOutOfRangeException(nameof(pos));
                }
            }

            return Current;
        }

        public ReadOnlySpan<char> ElementAtOrDefault(int pos)
        {
            for (var i = 0; i <= pos; i++)
            {
                if (!MoveNext())
                {
                    return default;
                }
            }

            return Current;
        }

        public StringSplitByCharEnumerator GetEnumerator() => this;

        public StringSplitByCharEnumerator(ReadOnlySpan<char> chars, char delimiter, StringSplitOptions options, bool reverse)
        {
            Current = default;
            this.chars = chars;
            this.delimiter = delimiter;
            this.options = options;
            this.reverse = reverse;
        }
    }

    public ref struct StringSplitByStringEnumerator
    {
        private readonly ReadOnlySpan<char> delimiter;
        private readonly StringSplitOptions options;
        private readonly bool reverse;

        private ReadOnlySpan<char> chars;

        public ReadOnlySpan<char> Current { get; private set; }

        public bool MoveNext() => reverse ? MoveNextReverse() : MoveNextForward();

        public bool MoveNextForward()
        {
            while (!chars.IsEmpty)
            {

                var i = chars.IndexOf(delimiter);
                if (i < 0)
                {
                    i = chars.Length;
                }

                Current = chars.Slice(0, i);

                if (i + delimiter.Length <= chars.Length)
                {
                    chars = chars.Slice(i + delimiter.Length);
                }
                else
                {
                    chars = default;
                }

#if NET5_0_OR_GREATER
                if (options.HasFlag(StringSplitOptions.TrimEntries))
                {
                    Current = Current.Trim();
                }
#endif

                if (!Current.IsEmpty ||
                    !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                {
                    return true;
                }
            }

            return false;
        }

        public bool MoveNextReverse()
        {
            while (!chars.IsEmpty)
            {
                var i = chars.LastIndexOf(delimiter);

                Current = i >= 0 ? chars.Slice(i + delimiter.Length) : chars;

                if (i < 0)
                {
                    chars = default;
                }
                else
                {
                    chars = chars.Slice(0, i);
                }

#if NET5_0_OR_GREATER
                if (options.HasFlag(StringSplitOptions.TrimEntries))
                {
                    Current = Current.Trim();
                }
#endif

                if (!Current.IsEmpty ||
                    !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                {
                    return true;
                }
            }

            return false;
        }

        public ReadOnlySpan<char> First() => MoveNext() ? Current : throw new InvalidOperationException();

        public ReadOnlySpan<char> FirstOrDefault() => MoveNext() ? Current : default;

        public ReadOnlySpan<char> Last()
        {
            var found = false;
            ReadOnlySpan<char> result = default;

            while (MoveNext())
            {
                found = true;
                result = Current;
            }

            if (found)
            {
                return result;
            }

            throw new InvalidOperationException();
        }

        public ReadOnlySpan<char> LastOrDefault()
        {
            var found = false;
            ReadOnlySpan<char> result = default;

            while (MoveNext())
            {
                found = true;
                result = Current;
            }

            if (found)
            {
                return result;
            }

            return default;
        }

        public ReadOnlySpan<char> ElementAt(int pos)
        {
            for (var i = 0; i <= pos; i++)
            {
                if (!MoveNext())
                {
                    throw new ArgumentOutOfRangeException(nameof(pos));
                }
            }

            return Current;
        }

        public ReadOnlySpan<char> ElementAtOrDefault(int pos)
        {
            for (var i = 0; i <= pos; i++)
            {
                if (!MoveNext())
                {
                    return default;
                }
            }

            return Current;
        }

        public StringSplitByStringEnumerator GetEnumerator() => this;

        public StringSplitByStringEnumerator(ReadOnlySpan<char> chars, ReadOnlySpan<char> delimiter, StringSplitOptions options, bool reverse)
        {
            Current = default;
            this.chars = chars;
            this.delimiter = delimiter;
            this.options = options;
            this.reverse = reverse;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringSplitByCharEnumerator Split(this ReadOnlySpan<char> chars, char delimiter, StringSplitOptions options = StringSplitOptions.None) =>
        new(chars, delimiter, options, reverse: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringSplitByStringEnumerator Split(this ReadOnlySpan<char> chars, ReadOnlySpan<char> delimiter, StringSplitOptions options = StringSplitOptions.None) =>
        new(chars, delimiter, options, reverse: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringSplitByCharEnumerator SplitReverse(this ReadOnlySpan<char> chars, char delimiter, StringSplitOptions options = StringSplitOptions.None) =>
        new(chars, delimiter, options, reverse: true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringSplitByStringEnumerator SplitReverse(this ReadOnlySpan<char> chars, ReadOnlySpan<char> delimiter, StringSplitOptions options = StringSplitOptions.None) =>
        new(chars, delimiter, options, reverse: true);

    public static IEnumerable<ReadOnlyMemory<char>> Split(this ReadOnlyMemory<char> chars, char delimiter, StringSplitOptions options = StringSplitOptions.None)
    {
        while (!chars.IsEmpty)
        {
            var i = chars.Span.IndexOf(delimiter);
            if (i < 0)
            {
                i = chars.Length;
            }

            var value = chars.Slice(0, i);

#if NET5_0_OR_GREATER
            if (options.HasFlag(StringSplitOptions.TrimEntries))
            {
                value = value.Trim();
            }
#endif

            if (!value.IsEmpty ||
                !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            {
                yield return value;
            }

            if (i >= chars.Length)
            {
                break;
            }

            chars = chars.Slice(i + 1);
        }
    }

    public static IEnumerable<ReadOnlyMemory<char>> Split(this ReadOnlyMemory<char> chars, char delimiter1, char delimiter2, StringSplitOptions options = StringSplitOptions.None)
    {
        while (!chars.IsEmpty)
        {
            var i = chars.Span.IndexOfAny(delimiter1, delimiter2);
            if (i < 0)
            {
                i = chars.Length;
            }

            var value = chars.Slice(0, i);

#if NET5_0_OR_GREATER
            if (options.HasFlag(StringSplitOptions.TrimEntries))
            {
                value = value.Trim();
            }
#endif

            if (!value.IsEmpty ||
                !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            {
                yield return value;
            }

            if (i >= chars.Length)
            {
                break;
            }

            chars = chars.Slice(i + 1);
        }
    }

    public static IEnumerable<ReadOnlyMemory<char>> SplitReverse(this ReadOnlyMemory<char> chars, char delimiter, StringSplitOptions options = StringSplitOptions.None)
    {
        while (!chars.IsEmpty)
        {
            var i = chars.Span.LastIndexOf(delimiter);

            var value = chars.Slice(i + 1);

#if NET5_0_OR_GREATER
            if (options.HasFlag(StringSplitOptions.TrimEntries))
            {
                value = value.Trim();
            }
#endif

            if (!value.IsEmpty ||
                !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            {
                yield return value;
            }

            if (i < 0)
            {
                break;
            }

            chars = chars.Slice(0, i);
        }
    }

    public static IEnumerable<ReadOnlyMemory<char>> Split(this ReadOnlyMemory<char> chars, ReadOnlyMemory<char> delimiter, StringSplitOptions options = StringSplitOptions.None)
    {
        while (!chars.IsEmpty)
        {
            var i = chars.Span.IndexOf(delimiter.Span);
            if (i < 0)
            {
                i = chars.Length;
            }

            var value = chars.Slice(0, i);

#if NET5_0_OR_GREATER
            if (options.HasFlag(StringSplitOptions.TrimEntries))
            {
                value = value.Trim();
            }
#endif

            if (!value.IsEmpty ||
                !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            {
                yield return value;
            }

            if (i >= chars.Length)
            {
                break;
            }

            chars = chars.Slice(i + delimiter.Length);
        }
    }

    public static IEnumerable<ReadOnlyMemory<char>> SplitReverse(this ReadOnlyMemory<char> chars, ReadOnlyMemory<char> delimiter, StringSplitOptions options = StringSplitOptions.None)
    {
        while (!chars.IsEmpty)
        {
            var i = chars.Span.LastIndexOf(delimiter.Span);

            var value = i >= 0 ? chars.Slice(i + delimiter.Length) : chars;

#if NET5_0_OR_GREATER
            if (options.HasFlag(StringSplitOptions.TrimEntries))
            {
                value = value.Trim();
            }
#endif

            if (!value.IsEmpty ||
                !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            {
                yield return value;
            }

            if (i < 0)
            {
                break;
            }

            chars = chars.Slice(0, i);
        }
    }

    public static IEnumerable<ReadOnlyMemory<char>> Split(this ReadOnlyMemory<char> chars, ReadOnlyMemory<char> delimiter, StringSplitOptions options, StringComparison comparison)
    {
        while (!chars.IsEmpty)
        {
            var i = chars.Span.IndexOf(delimiter.Span, comparison);
            if (i < 0)
            {
                i = chars.Length;
            }

            var value = chars.Slice(0, i);

#if NET5_0_OR_GREATER
            if (options.HasFlag(StringSplitOptions.TrimEntries))
            {
                value = value.Trim();
            }
#endif

            if (!value.IsEmpty ||
                !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            {
                yield return value;
            }

            if (i >= chars.Length)
            {
                break;
            }

            chars = chars.Slice(i + delimiter.Length);
        }
    }

    /// <summary>
    /// Checks if reference is null and in that case throws an <see cref="ArgumentNullException"/> with supplied argument name.
    /// </summary>
    /// <typeparam name="T">Type of reference to check</typeparam>
    /// <param name="obj">Reference to check</param>
    /// <param name="param">Name of parameter in calling code</param>
    /// <returns>Reference in <paramref name="obj"/> parameter, if not null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> is null</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NullCheck<T>(this T? obj, string? param) where T : class => obj ?? throw new ArgumentNullException(param);

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

    public static IAsyncResult AsAsyncResult<T>(this Task<T> task, AsyncCallback? callback, object? state)
    {
        var returntask = task.ContinueWith((t, _) => t.Result, state, TaskScheduler.Default);

        if (callback is not null)
        {
            returntask.ContinueWith(callback.Invoke, TaskScheduler.Default);
        }

        return returntask;
    }

    public static IAsyncResult AsAsyncResult(this Task task, AsyncCallback? callback, object? state)
    {
        var returntask = task.ContinueWith((t, _) => { }, state, TaskScheduler.Default);

        if (callback is not null)
        {
            returntask.ContinueWith(callback.Invoke, TaskScheduler.Default);
        }

        return returntask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddRange<T>(this List<T> list, params T[] collection) => list.AddRange(collection);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsSpan(this IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<byte> AsReadOnlySpan(this IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsSpan(this SafeBuffer ptr) =>
        new(ptr.DangerousGetHandle().ToPointer(), (int)ptr.ByteLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsSpan(this MemoryStream memoryStream) =>
        memoryStream.GetBuffer().AsSpan(0, checked((int)memoryStream.Length));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsSpan(this UnmanagedMemoryStream memoryStream) =>
        new(memoryStream.PositionPointer - memoryStream.Position, (int)memoryStream.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<byte> AsMemory(this MemoryStream memoryStream) =>
        memoryStream.GetBuffer().AsMemory(0, checked((int)memoryStream.Length));

#if !NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T CastRef<T>(this ReadOnlySpan<byte> bytes) where T : unmanaged =>
        ref MemoryMarshal.Cast<byte, T>(bytes)[0];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T CastRef<T>(this Span<byte> bytes) where T : unmanaged =>
        ref MemoryMarshal.Cast<byte, T>(bytes)[0];
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T CastRef<T>(this ReadOnlySpan<byte> bytes) where T : unmanaged =>
        ref MemoryMarshal.AsRef<T>(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T CastRef<T>(this Span<byte> bytes) where T : unmanaged =>
        ref MemoryMarshal.AsRef<T>(bytes);
#endif

    /// <summary>
    /// Sets a bit to 1 in a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to set to 1</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBit(this Span<byte> data, int bitnumber) =>
        data[bitnumber >> 3] |= (byte)(1 << ((~bitnumber) & 7));

    /// <summary>
    /// Sets a bit to 0 in a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to set to 0</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearBit(this Span<byte> data, int bitnumber) =>
        data[bitnumber >> 3] &= unchecked((byte)~(1 << ((~bitnumber) & 7)));

    /// <summary>
    /// Gets a bit from a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to get</param>
    /// <returns>True if value of specified bit is 1, false if 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetBit(this ReadOnlySpan<byte> data, int bitnumber) =>
        (data[bitnumber >> 3] & (1 << ((~bitnumber) & 7))) != 0;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> CreateReadOnlySpan<T>(in T source, int length) =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(source), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> CreateSpan<T>(ref T source, int length) =>
        MemoryMarshal.CreateSpan(ref source, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BinaryCompare<T>(in T s1, in T s2) where T : unmanaged =>
        BinaryCompare(CreateReadOnlySpan(s1, 1), CreateReadOnlySpan(s2, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BinaryEqual<T>(in T s1, in T s2) where T : unmanaged =>
        BinaryEqual(CreateReadOnlySpan(s1, 1), CreateReadOnlySpan(s2, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(in T source) where T : unmanaged =>
        MemoryMarshal.AsBytes(CreateReadOnlySpan(source, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsBytes<T>(ref T source) where T : unmanaged =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref source, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToHexString<T>(in T source) where T : unmanaged =>
        ToHexString(AsReadOnlyBytes(source));

    public static int GetHashCode<T>(in T source) where T : unmanaged =>
        GetHashCode(AsReadOnlyBytes(source));

#else

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<T> CreateReadOnlySpan<T>(in T source, int length) =>
        new(Unsafe.AsPointer(ref Unsafe.AsRef(source)), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<T> CreateSpan<T>(ref T source, int length) =>
        new(Unsafe.AsPointer(ref source), length);

#endif

    public static string ToHexString(this ReadOnlySpan<byte> data, ReadOnlySpan<char> delimiter)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var capacity = data.Length << 1;
        if (!delimiter.IsEmpty)
        {
            capacity += delimiter.Length * (data.Length - 1);
        }

        var result = new string('\0', capacity);

        var ptr = MemoryMarshal.AsMemory(result.AsMemory()).Span;

        foreach (var b in data)
        {
            if (!delimiter.IsEmpty && ptr.Length < capacity)
            {
                delimiter.CopyTo(ptr);
                ptr = ptr.Slice(delimiter.Length);
            }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            b.TryFormat(ptr, out _, "x2", NumberFormatInfo.InvariantInfo);
#else
            b.ToString("x2", NumberFormatInfo.InvariantInfo).AsSpan().CopyTo(ptr);
#endif
            ptr = ptr.Slice(2);
        }

        return result;
    }

    public static int GetHashCode(ReadOnlySpan<byte> ptr)
    {
        var result = 0;
        for (var i = 0; i < ptr.Length; i++)
        {
            result ^= ptr[i] << ((i & 0x3) * 8);
        }

        return result;
    }

#if NET7_0_OR_GREATER
    [LibraryImport("msvcrt", SetLastError = false)]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int memcmp(in byte ptr1, in byte ptr2, IntPtr count);
#else
    [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern int memcmp(in byte ptr1, in byte ptr2, IntPtr count);
#endif

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>If sequences are both empty, true is returned. If sequences have different lengths, false is returned.
    /// If lengths are equal and byte sequences are equal, true is returned.</returns>
    public static bool BinaryEqual(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.IsEmpty && second.IsEmpty)
        {
            return true;
        }

        return first.Length == second.Length
            && (first == second ||
            memcmp(first[0], second[0], new IntPtr(first.Length)) == 0);
    }

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>If sequences are both empty, true is returned. If sequences have different lengths, false is returned.
    /// If lengths are equal and byte sequences are equal, true is returned.</returns>
    public static bool BinaryEqual(this Span<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.IsEmpty && second.IsEmpty)
        {
            return true;
        }

        return first.Length == second.Length
            && (first == second ||
            memcmp(first[0], second[0], new IntPtr(first.Length)) == 0);
    }

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        => (first.IsEmpty && second.IsEmpty) || (first == second)
        ? 0 : memcmp(first[0], second[0], new IntPtr(first.Length));

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare(this Span<byte> first, ReadOnlySpan<byte> second)
        => (first.IsEmpty && second.IsEmpty) || (first == second)
        ? 0 : memcmp(first[0], second[0], new IntPtr(first.Length));

    /// <summary>
    /// Compares two spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>If sequences are both empty, true is returned. If sequences have different lengths, false is returned.
    /// If lengths are equal and byte sequences are equal, true is returned.</returns>
    public static bool BinaryEqual<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : unmanaged
        => BinaryEqual(MemoryMarshal.AsBytes(first), MemoryMarshal.AsBytes(second));

    /// <summary>
    /// Compares two spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : unmanaged
        => BinaryCompare(MemoryMarshal.AsBytes(first), MemoryMarshal.AsBytes(second));

#if !NETCOREAPP

    public static ReadOnlyMemory<char> TrimEnd(this ReadOnlyMemory<char> str, char chr)
        => str.Slice(0, str.Span.TrimEnd(chr).Length);

    public static ReadOnlyMemory<char> TrimStart(this ReadOnlyMemory<char> str, char chr)
        => str.Slice(str.Span.TrimStart(chr).Length);

    public static bool Contains(this ReadOnlySpan<char> str, char chr)
        => str.IndexOf(chr) >= 0;

    public static bool Contains(this string str, char chr)
        => str.IndexOf(chr) >= 0;

    public static bool Contains(this string str, string substr)
        => str.IndexOf(substr) >= 0;

    public static bool Contains(this string str, string substr, StringComparison comparison)
        => str.IndexOf(substr, comparison) >= 0;

    public static bool StartsWith(this string str, char chr)
        => str is not null && str.Length > 0 && str[0] == chr;

    public static bool EndsWith(this string str, char chr)
        => str is not null && str.Length > 0 && str[str.Length - 1] == chr;

#endif

    /// <summary>
    /// Return a managed reference to Span, or a managed null reference
    /// if Span is empty.
    /// </summary>
    /// <param name="span">Span to return reference for or null</param>
    /// <returns>Managed reference</returns>
    public static ref readonly T AsRef<T>(this ReadOnlySpan<T> span)
    {
        if (span.IsEmpty)
        {
            return ref Unsafe.NullRef<T>();
        }
        else
        {
            return ref span[0];
        }
    }

    /// <summary>
    /// Return a managed reference to Span, or a managed null reference
    /// if Span is empty.
    /// </summary>
    /// <param name="span">Span to return reference for or null</param>
    /// <returns>Managed reference</returns>
    public static ref T AsRef<T>(this Span<T> span)
    {
        if (span.IsEmpty)
        {
            return ref Unsafe.NullRef<T>();
        }
        else
        {
            return ref span[0];
        }
    }

    /// <summary>
    /// Return a managed reference to string, or a managed null reference
    /// if given a null reference.
    /// </summary>
    /// <param name="str">String to return reference for or null</param>
    /// <returns>Managed reference</returns>
    public static ref readonly char AsRef(this string? str)
    {
        if (str is null)
        {
            return ref Unsafe.NullRef<char>();
        }
        else
        {
            return ref MemoryMarshal.GetReference(str.AsSpan());
        }
    }

    /// <summary>
    /// Returns a reference to a character string guaranteed to be null
    /// terminated. If the supplied buffer is null terminated, a reference
    /// to the first character in buffer is returned. Otherwise, a new char
    /// array is created, data copied to it, and a reference to the first
    /// character in the new array is returned.
    /// </summary>
    /// <param name="strMemory">Input string</param>
    /// <returns>Reference to output string with characters equal to
    /// input string, but guaranteed to be null terminated.</returns>
    public static ref readonly char MakeNullTerminated(this ReadOnlyMemory<char> strMemory)
    {
        if (strMemory.IsEmpty)
        {
            return ref Unsafe.NullRef<char>();
        }

        if (MemoryMarshal.TryGetString(strMemory, out var text, out var start, out var length) &&
            start + length == text.Length)
        {
            return ref MemoryMarshal.GetReference(strMemory.Span);
        }

        if (MemoryMarshal.TryGetArray(strMemory, out var chars) &&
            chars.Array is not null &&
            chars.Offset + chars.Count < chars.Array.Length &&
            chars.Array[chars.Offset + chars.Count] == '\0')
        {
            return ref chars.Array[chars.Offset];
        }

        var buffer = new char[strMemory.Length + 1];
        strMemory.CopyTo(buffer);
        return ref buffer[0];
    }

    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool RtlIsZeroMemoryFunc(in byte buffer, IntPtr length);

    private static readonly RtlIsZeroMemoryFunc FuncRtlIsZeroMemory =
        GetRtlIsZeroMemory() ??
        InternalIsZeroMemory;

    private static unsafe RtlIsZeroMemoryFunc? GetRtlIsZeroMemory()
    {
        nint fptr = 0;

        try
        {
            fptr = NativeLib.GetProcAddressNoThrow("ntdll", "RtlIsZeroMemory");
        }
        catch
        {
        }

        if (fptr == default)
        {
            return null;
        }

        var ptr = (delegate* unmanaged[Stdcall]<byte*, nint, byte>)fptr;

        return (in byte buffer, nint length) =>
        {
            fixed (byte* bytes = &buffer)
            {
                return ptr(bytes, length) != 0;
            }
        };
    }

    /// <summary>
    /// Determines whether all bytes in a buffer are zero. If ntdll.RtlIsZeroMemory is available it is used,
    /// otherwise it falls back to a native method that compares groups of bytes is an optimized way.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns>If all bytes are zero, buffer is empty or buffer is null, true is returned, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBufferZero(this byte[] buffer) => buffer is null || buffer.AsSpan().IsBufferZero();

    /// <summary>
    /// Determines whether all bytes in a buffer are zero. If ntdll.RtlIsZeroMemory is available it is used,
    /// otherwise it falls back to a native method that compares groups of bytes is an optimized way.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns>If all bytes are zero, buffer is empty, true is returned, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBufferZero(this Span<byte> buffer) =>
        FuncRtlIsZeroMemory(MemoryMarshal.GetReference(buffer), new(buffer.Length));

    /// <summary>
    /// Determines whether all bytes in a buffer are zero. If ntdll.RtlIsZeroMemory is available it is used,
    /// otherwise it falls back to a managed method that compares groups of bytes is an optimized way.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns>If all bytes are zero, buffer is empty, true is returned, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBufferZero(this ReadOnlySpan<byte> buffer) =>
        FuncRtlIsZeroMemory(MemoryMarshal.GetReference(buffer), new(buffer.Length));

    private static unsafe bool InternalIsZeroMemory(in byte buffer, IntPtr length)
    {
        if (length == IntPtr.Zero)
        {
            return true;
        }

        fixed (byte* ptr = &buffer)
        {
            var pointervalue = new IntPtr(ptr).ToInt64();

            if ((pointervalue & sizeof(long) - 1) == 0 &&
                (length.ToInt64() & sizeof(long) - 1) == 0)
            {
                for (var p = (long*)ptr; p < ptr + length.ToInt64(); p++)
                {
                    if (*p != 0)
                    {
                        return false;
                    }
                }
            }
            else if ((pointervalue & sizeof(int) - 1) == 0 &&
                (length.ToInt64() & sizeof(int) - 1) == 0)
            {
                for (var p = (int*)ptr; p < ptr + length.ToInt64(); p++)
                {
                    if (*p != 0)
                    {
                        return false;
                    }
                }
            }
            else if ((pointervalue & sizeof(short) - 1) == 0 &&
                (length.ToInt64() & sizeof(short) - 1) == 0)
            {
                for (var p = (short*)ptr; p < ptr + length.ToInt64(); p++)
                {
                    if (*p != 0)
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (var p = ptr; p < ptr + length.ToInt64(); p++)
                {
                    if (*p != 0)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
