using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using IByteCollection = System.Collections.Generic.IReadOnlyCollection<byte>;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1069 // Enums values should not be duplicated
#pragma warning disable IDE0032 // Use auto property

namespace Arsenal.ImageMounter.IO;

/// <summary>
/// Low-level string manipulation methods
/// </summary>
public static class StringExtensions
{
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
    /// <param name="chars">Memory that contains the double null terminated string</param>
    /// <returns>Each individual string in the buffer</returns>
    public static IEnumerable<string> ParseDoubleTerminatedString(this ReadOnlyMemory<byte> chars)
    {
        var endpos = MemoryMarshal.Cast<byte, char>(chars.Span).IndexOf(default(char));

        while (endpos > 0)
        {
            yield return MemoryMarshal.Cast<byte, char>(chars.Span).Slice(0, endpos).ToString();

            chars = chars.Slice((endpos + 1) << 1);

            endpos = MemoryMarshal.Cast<byte, char>(chars.Span).IndexOf(default(char));
        }
    }

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
        return Encoding.ASCII.GetString(buffer.Slice(0, endpos));
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

    public static string ToHexString(this IByteCollection data) => data.ToHexString(null);

    public static string ToHexString(this IByteCollection data, string delimiter)
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
}
