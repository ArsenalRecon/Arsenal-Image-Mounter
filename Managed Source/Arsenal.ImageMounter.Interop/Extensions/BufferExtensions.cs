using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO;
using Microsoft.Win32.SafeHandles;
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
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Extensions;

public static class BufferExtensions
{
#if NETCOREAPP
    static BufferExtensions()
    {
        NativeLibrary.SetDllImportResolver(typeof(BufferExtensions).Assembly, NativeCalls.CrtDllImportResolver);
    }
#endif

    public static IEnumerable<Exception> Enumerate(this Exception ex)
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

    public static IEnumerable<string> EnumerateMessages(this Exception ex)
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
    public static string FormatLogMessages(this Exception exception) =>
#if DEBUG
            System.Diagnostics.Debugger.IsAttached ? exception.JoinMessages() : exception?.ToString();
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
        var endpos = MemoryMarshal.Cast<byte, char>(bytes.Span).IndexOf(default(char));

        while (endpos > 0)
        {
            yield return MemoryMarshal.Cast<byte, char>(bytes.Span).Slice(0, endpos).ToString();

            bytes = bytes.Slice((endpos + 1) << 1);

            endpos = MemoryMarshal.Cast<byte, char>(bytes.Span).IndexOf(default(char));
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
        var endpos = chars.Span.IndexOf(default(char));

        while (endpos > 0)
        {
            yield return chars.Slice(0, endpos);

            chars = chars.Slice(endpos + 1);

            endpos = chars.Span.IndexOf(default(char));
        }
    }

    /// <summary>
    /// Return position of first empty element, or the entire span length if
    /// no empty elements are found.
    /// </summary>
    /// <param name="buffer">Span to search</param>
    /// <returns>Position of first found empty element or entire span length if none found</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfTerminator<T>(this ReadOnlySpan<T> buffer) where T : struct, IEquatable<T>
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
    public static int IndexOfTerminator<T>(this Span<T> buffer) where T : struct, IEquatable<T>
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

#endif

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

    public static string ToHexString(this IReadOnlyCollection<byte> data) => data.ToHexString(default);

    public static string ToHexString(this IReadOnlyCollection<byte> data, string delimiter)
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

        var result = new StringBuilder(capacity);

        foreach (var b in data)
        {
            if (delimiter is not null && result.Length > 0)
            {
                result.Append(delimiter);
            }
            result.Append(b.ToString("x2", NumberFormatInfo.InvariantInfo));
        }

        return result.ToString();
    }

    public static string ToHexString(this byte[] data) => ((ReadOnlySpan<byte>)data).ToHexString(null);

    public static string ToHexString(this byte[] data, string delimiter) => ((ReadOnlySpan<byte>)data).ToHexString(delimiter);

    public static string ToHexString(this byte[] data, int offset, int count) => data.AsSpan(offset, count).ToHexString(null);

    public static string ToHexString(this byte[] data, int offset, int count, string delimiter) => data.AsSpan(offset, count).ToHexString(delimiter);

    public static string ToHexString(this Span<byte> data) => ((ReadOnlySpan<byte>)data).ToHexString(null);

    public static string ToHexString(this Span<byte> data, string delimiter) => ((ReadOnlySpan<byte>)data).ToHexString(delimiter);

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
        var sb = new StringBuilder(67);
        byte pos = 0;
        foreach (var b in bytes)
        {
            if (pos == 0)
            {
                sb.Append($"                        -                                          ");
            }

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

            sb[51 + pos] = char.IsControl((char)b) ? '.' : (char)b;

            pos++;
            pos &= 0xf;

            if (pos == 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
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
        private ReadOnlySpan<char> current;

        public ReadOnlySpan<char> Current => current;

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

                current = chars.Slice(0, i);

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
                    current = current.Trim();
                }
#endif

                if (!current.IsEmpty ||
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

                current = i >= 0 ? chars.Slice(i + 1) : chars;

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
                    current = current.Trim();
                }
#endif

                if (!current.IsEmpty ||
                    !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                {
                    return true;
                }
            }

            return false;
        }

        public ReadOnlySpan<char> First() => MoveNext() ? current : default;

        public ReadOnlySpan<char> ElementAt(int pos)
        {
            for (var i = 0; i <= pos; i++)
            {
                if (!MoveNext())
                {
                    throw new ArgumentOutOfRangeException(nameof(pos));
                }
            }

            return current;
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

            return current;
        }

        public StringSplitByCharEnumerator GetEnumerator() => this;

        public StringSplitByCharEnumerator(ReadOnlySpan<char> chars, char delimiter, StringSplitOptions options, bool reverse)
        {
            current = default;
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
        private ReadOnlySpan<char> current;

        public ReadOnlySpan<char> Current => current;

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

                current = chars.Slice(0, i);

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
                    current = current.Trim();
                }
#endif

                if (!current.IsEmpty ||
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

                current = i >= 0 ? chars.Slice(i + delimiter.Length) : chars;

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
                    current = current.Trim();
                }
#endif

                if (!current.IsEmpty ||
                    !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                {
                    return true;
                }
            }

            return false;
        }

        public ReadOnlySpan<char> First() => MoveNext() ? current : default;

        public ReadOnlySpan<char> ElementAt(int pos)
        {
            for (var i = 0; i <= pos; i++)
            {
                if (!MoveNext())
                {
                    throw new ArgumentOutOfRangeException(nameof(pos));
                }
            }

            return current;
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

            return current;
        }

        public StringSplitByStringEnumerator GetEnumerator() => this;

        public StringSplitByStringEnumerator(ReadOnlySpan<char> chars, ReadOnlySpan<char> delimiter, StringSplitOptions options, bool reverse)
        {
            current = default;
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
    public static T NullCheck<T>(this T obj, string param) where T : class => obj ?? throw new ArgumentNullException(param);

    public static string CalculateChecksum<THashAlgorithm>(string file) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var stream = File.OpenRead(file))
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(stream);
        }

        return hash.ToHexString();
    }

    public static string CalculateChecksum<THashAlgorithm>(Stream stream) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(stream);
        }

        return hash.ToHexString();
    }

    public static string CalculateChecksum<THashAlgorithm>(this byte[] data) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(data);
        }

        return hash.ToHexString();
    }

    public static IAsyncResult AsAsyncResult<T>(this Task<T> task, AsyncCallback callback, object state)
    {
        var returntask = task.ContinueWith((t, _) => t.Result, state, TaskScheduler.Default);

        if (callback is not null)
        {
            returntask.ContinueWith(callback.Invoke, TaskScheduler.Default);
        }

        return returntask;
    }

    public static IAsyncResult AsAsyncResult(this Task task, AsyncCallback callback, object state)
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
    public static unsafe Span<byte> AsSpan(IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<byte> AsReadOnlySpan(IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsSpan(this SafeBuffer ptr) =>
        new(ptr.DangerousGetHandle().ToPointer(), (int)ptr.ByteLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsSpan(this MemoryStream memoryStream) =>
        memoryStream.GetBuffer().AsSpan(0, checked((int)memoryStream.Length));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<byte> AsMemory(this MemoryStream memoryStream) =>
        memoryStream.GetBuffer().AsMemory(0, checked((int)memoryStream.Length));

#if !NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T AsRef<T>(this ReadOnlySpan<byte> bytes) where T : struct =>
        ref MemoryMarshal.Cast<byte, T>(bytes)[0];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(this Span<byte> bytes) where T : struct =>
        ref MemoryMarshal.Cast<byte, T>(bytes)[0];
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T AsRef<T>(this ReadOnlySpan<byte> bytes) where T : struct =>
        ref MemoryMarshal.AsRef<T>(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(this Span<byte> bytes) where T : struct =>
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
    public static int BinaryCompare<T>(in T s1, in T s2) where T : struct =>
        BinaryCompare(CreateReadOnlySpan(s1, 1), CreateReadOnlySpan(s2, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BinaryEqual<T>(in T s1, in T s2) where T : struct =>
        BinaryEqual(CreateReadOnlySpan(s1, 1), CreateReadOnlySpan(s2, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(in T source) where T : struct =>
        MemoryMarshal.AsBytes(CreateReadOnlySpan(source, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsBytes<T>(ref T source) where T : struct =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref source, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToHexString<T>(in T source) where T : struct =>
        ToHexString(AsReadOnlyBytes(source));

    public static string ToHexString(this ReadOnlySpan<byte> bytes, ReadOnlySpan<char> delimiter)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var delimiter_length = delimiter.Length;
        var str = new string('\0', bytes.Length << 1 + delimiter_length * (bytes.Length - 1));

        var target = MemoryMarshal.AsMemory(str.AsMemory()).Span;

        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i].TryFormat(target.Slice(i * (2 + delimiter_length), 2), out _, "x2");
            if (delimiter_length > 0 && i < bytes.Length - 1)
            {
                delimiter.CopyTo(target[(i * (2 + delimiter_length) + 2)..]);
            }
        }

        return target.ToString();
    }

    public static int GetHashCode<T>(in T source) where T : struct =>
        GetHashCode(AsReadOnlyBytes(source));

#else

    public static string ToHexString(this ReadOnlySpan<byte> data, string delimiter)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var capacity = data.Length << 1;
        if (delimiter is not null)
        {
            capacity += delimiter.Length * (data.Length - 1);
        }

        var result = new StringBuilder(capacity);

        foreach (var b in data)
        {
            if (delimiter is not null && result.Length > 0)
            {
                result.Append(delimiter);
            }
            result.Append(b.ToString("x2", NumberFormatInfo.InvariantInfo));
        }

        return result.ToString();
    }

#endif

    public static int GetHashCode(ReadOnlySpan<byte> ptr)
    {
        var result = 0;
        for (var i = 0; i < ptr.Length; i++)
        {
            result ^= ptr[i] << ((i & 0x3) * 8);
        }
        return result;
    }

    [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern int memcmp(in byte ptr1, in byte ptr2, IntPtr count);

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

        if (first.Length != second.Length)
        {
            return false;
        }

        return first == second ||
            memcmp(first[0], second[0], new IntPtr(first.Length)) == 0;
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

        if (first.Length != second.Length)
        {
            return false;
        }

        return first == second ||
            memcmp(first[0], second[0], new IntPtr(first.Length)) == 0;
    }

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if ((first.IsEmpty && second.IsEmpty) || first == second)
        {
            return 0;
        }

        return memcmp(first[0], second[0], new IntPtr(first.Length));
    }

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare(this Span<byte> first, ReadOnlySpan<byte> second)
    {
        if ((first.IsEmpty && second.IsEmpty) || first == second)
        {
            return 0;
        }

        return memcmp(first[0], second[0], new IntPtr(first.Length));
    }

    /// <summary>
    /// Compares two spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>If sequences are both empty, true is returned. If sequences have different lengths, false is returned.
    /// If lengths are equal and byte sequences are equal, true is returned.</returns>
    public static bool BinaryEqual<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : struct
        => BinaryEqual(MemoryMarshal.AsBytes(first), MemoryMarshal.AsBytes(second));

    /// <summary>
    /// Compares two spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : struct
        => BinaryCompare(MemoryMarshal.AsBytes(first), MemoryMarshal.AsBytes(second));

#if !NETCOREAPP

    public static ReadOnlyMemory<char> TrimEnd(this ReadOnlyMemory<char> str, char chr) =>
        str.Slice(0, str.Span.TrimEnd(chr).Length);

    public static ReadOnlyMemory<char> TrimStart(this ReadOnlyMemory<char> str, char chr) =>
        str.Slice(str.Span.TrimStart(chr).Length);

    public static bool Contains(this ReadOnlySpan<char> str, char chr) =>
        str.IndexOf(chr) >= 0;

    public static bool Contains(this string str, char chr) =>
        str.IndexOf(chr) >= 0;

    public static bool Contains(this string str, string substr) =>
        str.IndexOf(substr) >= 0;

    public static bool Contains(this string str, string substr, StringComparison comparison) =>
        str.IndexOf(substr, comparison) >= 0;

#endif

    public static ref char MakeNullTerminated(this ReadOnlyMemory<char> str)
    {
        if (MemoryMarshal.TryGetArray(str, out var chars) &&
            chars.Offset + chars.Count < chars.Array.Length &&
            chars.Array[chars.Offset + chars.Count] == '\0')
        {
            return ref chars.Array[chars.Offset];
        }

        if (MemoryMarshal.TryGetString(str, out var text, out var start, out var length) &&
            start + length == text.Length)
        {
            return ref MemoryMarshal.GetReference(str.Span);
        }

        var buffer = new char[str.Length + 1];
        str.CopyTo(buffer);
        return ref buffer[0];
    }
}
