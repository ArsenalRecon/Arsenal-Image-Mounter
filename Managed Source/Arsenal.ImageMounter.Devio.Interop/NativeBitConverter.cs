using Arsenal.ImageMounter.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Extensions;

public static class NativeBitConverter
{
#if NETSTANDARD || NETCOREAPP
    static NativeBitConverter()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeBitConverter).Assembly, NativeCalls.CrtDllImportResolver);
    }
#endif

    /// <summary>
    /// Sets a bit to 1 in a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to set to 1</param>
    public static void SetBit(this byte[] data, int bitnumber) =>
        data[bitnumber >> 3] |= (byte)(1 << ((~bitnumber) & 7));

    /// <summary>
    /// Sets a bit to 0 in a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to set to 0</param>
    public static void ClearBit(this byte[] data, int bitnumber) =>
        data[bitnumber >> 3] &= unchecked((byte)~(1 << ((~bitnumber) & 7)));

    /// <summary>
    /// Gets a bit from a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to get</param>
    /// <returns>True if value of specified bit is 1, false if 0.</returns>
    public static bool GetBit(this byte[] data, int bitnumber) =>
        (data[bitnumber >> 3] & (1 << ((~bitnumber) & 7))) != 0;

    public static void FromByteArray<T>(out T result, ReadOnlySpan<byte> buffer) where T : unmanaged
    {
        if (!MemoryMarshal.TryRead(buffer, out result))
        {
            throw new ArgumentOutOfRangeException(nameof(buffer));
        }
    }

    public static void FromByteArray<T>(T[] result, ReadOnlySpan<byte> buffer) where T : unmanaged =>
        MemoryMarshal.Cast<byte, T>(buffer).CopyTo(result);

    public static byte[] ToByteArray<T>(in T source) where T : unmanaged =>
        UnmanagedStructSupport<T>.ToByteArray(source);

    public static string ToHexString<T>(in T source) where T : unmanaged =>
        UnmanagedStructSupport<T>.ToHexString(source);

    public static int StructSize<T>() where T : unmanaged => UnmanagedStructSupport<T>.SizeOfStruct;

    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool RtlIsZeroMemoryFunc(IntPtr buffer, IntPtr length);

    private static readonly RtlIsZeroMemoryFunc RtlIsZeroMemory =
        NativeLib.GetProcAddressNoThrow("ntdll", "RtlIsZeroMemory", typeof(RtlIsZeroMemoryFunc)) as RtlIsZeroMemoryFunc ??
        InternalIsZeroMemory;

    /// <summary>
    /// Determines whether all bytes in a buffer are zero. If ntdll.RtlIsZeroMemory is available it is used,
    /// otherwise it falls back to a native method that compares groups of bytes is an optimized way.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns>If all bytes are zero, buffer is empty or buffer is null, true is returned, false otherwise.</returns>
    public static bool IsBufferZero(this byte[] buffer) => buffer is null || buffer.AsSpan().IsBufferZero();

    /// <summary>
    /// Determines whether all bytes in a buffer are zero. If ntdll.RtlIsZeroMemory is available it is used,
    /// otherwise it falls back to a native method that compares groups of bytes is an optimized way.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns>If all bytes are zero, buffer is empty, true is returned, false otherwise.</returns>
    public static unsafe bool IsBufferZero(this Span<byte> buffer)
    {
        fixed (byte* ptr = buffer)
        {
            return RtlIsZeroMemory(new IntPtr(ptr), new IntPtr(buffer.Length));
        }
    }

    /// <summary>
    /// Determines whether all bytes in a buffer are zero. If ntdll.RtlIsZeroMemory is available it is used,
    /// otherwise it falls back to a native method that compares groups of bytes is an optimized way.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns>If all bytes are zero, buffer is empty, true is returned, false otherwise.</returns>
    public static unsafe bool IsBufferZero(this ReadOnlySpan<byte> buffer)
    {
        fixed (byte* ptr = buffer)
        {
            return RtlIsZeroMemory(new IntPtr(ptr), new IntPtr(buffer.Length));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool InternalIsZeroMemory(IntPtr buffer, IntPtr length)
    {
        if (length == IntPtr.Zero)
        {
            return true;
        }

        var ptr = (byte*)buffer.ToPointer();
        var pointervalue = buffer.ToInt64();

        if ((pointervalue & (sizeof(long) - 1)) == 0 &&
            (length.ToInt64() & (sizeof(long) - 1)) == 0)
        {
            for (var p = (long*)ptr; p < ptr + length.ToInt64(); p++)
            {
                if (*p != 0)
                {
                    return false;
                }
            }
        }
        else if ((pointervalue & (sizeof(int) - 1)) == 0 &&
            (length.ToInt64() & (sizeof(int) - 1)) == 0)
        {
            for (var p = (int*)ptr; p < ptr + length.ToInt64(); p++)
            {
                if (*p != 0)
                {
                    return false;
                }
            }
        }
        else if ((pointervalue & (sizeof(short) - 1)) == 0 &&
            (length.ToInt64() & (sizeof(short) - 1)) == 0)
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

        return true;
    }

    public static int Compare<T>(in T s1, in T s2) where T : unmanaged =>
        UnmanagedStructSupport<T>.Compare(in s1, in s2);

    public static int GetHashCode<T>(in T source) where T : unmanaged =>
        UnmanagedStructSupport<T>.GetHashCode(in source);

    [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern unsafe int memcmp(void* ptr1, void* ptr2, IntPtr count);

    [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern unsafe void* memcpy(void* ptrTarget, void* ptrSource, IntPtr count);

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>If sequences are both empty, true is returned. If sequences have different lengths, false is returned.
    /// If lengths are equal and byte sequences are equal, true is returned.</returns>
    public static unsafe bool SequenceEqual(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.IsEmpty && second.IsEmpty)
        {
            return true;
        }

        if (first.Length != second.Length)
        {
            return false;
        }

        fixed (byte* ptr1 = first, ptr2 = second)
        {
            return ptr1 == ptr2 || memcmp(ptr1, ptr2, new IntPtr(first.Length)) == 0;
        }
    }

    private static unsafe class UnmanagedStructSupport<T> where T : unmanaged
    {
        public static int SizeOfStruct { get; } = sizeof(T);

        public static int Compare(in T s1, in T s2)
        {
            fixed (T* ptr1 = &s1, ptr2 = &s2)
            {
                return ptr1 == ptr2 ? 0 : memcmp(ptr1, ptr2, new IntPtr(SizeOfStruct));
            }
        }

        public static int GetHashCode(in T source)
        {
            var result = 0;
            fixed (T* ptr = &source)
            {
                for (var i = 0; i < SizeOfStruct; i++)
                {
                    result ^= (((byte*)ptr)[i] << ((i & 0x3) * 8));
                }
            }
            return result;
        }

        public static byte[] ToByteArray(in T source)
        {
            var target = new byte[SizeOfStruct];
            fixed (byte* ptrTarget = target)
            {
                *(T*)ptrTarget = source;
            }
            return target;
        }

        public static string ToHexString(in T source)
        {
            var target = new StringBuilder(SizeOfStruct << 1);
            fixed (T* ptr = &source)
            {
                for (var i = 0; i < SizeOfStruct; i++)
                {
                    target.Append(((byte*)ptr)[i].ToString("x2"));
                }
            }
            return target.ToString();
        }

        public static void FromByteArray(out T target, byte[] source, int offset)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (offset < 0 || offset >= source.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must point to a position within the array");
            }

            if (source.Length - offset < SizeOfStruct)
            {
                throw new ArgumentException($"Too few bytes for a {typeof(T).Name} value", nameof(source));
            }

            fixed (byte* ptrSource = &source[offset])
            {
                target = *(T*)ptrSource;
            }
        }

        public static void FromByteArray(T[] target, byte[] source, int sourceOffset)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (sourceOffset < 0 || sourceOffset >= source.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Offset must point to a position within the array");
            }

            if (source.Length - sourceOffset < SizeOfStruct * target.Length)
            {
                throw new ArgumentException($"Too few bytes for {target.Length} {typeof(T).Name} values", nameof(source));
            }

            fixed (byte* ptrSource = &source[sourceOffset])
            fixed (T* ptrTarget = target)
            {
                if (ptrTarget != ptrSource)
                {
#if NET46_OR_GREATER || NETSTANDARD || NETCOREAPP
                    Buffer.MemoryCopy(ptrSource, ptrTarget, target.Length * SizeOfStruct, target.Length * SizeOfStruct);
#else
                    memcpy(ptrTarget, ptrSource, new IntPtr(target.Length * SizeOfStruct));
#endif
                }
            }
        }
    }
}
