using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO;
using Arsenal.ImageMounter.Reflection;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Extensions;

public static class BufferExtensions
{
#if NETSTANDARD || NETCOREAPP
    static BufferExtensions()
    {
        NativeLibrary.SetDllImportResolver(typeof(BufferExtensions).Assembly, NativeCalls.CrtDllImportResolver);
    }
#endif

    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool RtlIsZeroMemoryFunc(in byte buffer, IntPtr length);

    private static readonly RtlIsZeroMemoryFunc RtlIsZeroMemory =
        NativeLib.GetProcAddressNoThrow("ntdll".AsMemory(), "RtlIsZeroMemory", typeof(RtlIsZeroMemoryFunc)) as RtlIsZeroMemoryFunc ??
        InternalIsZeroMemory;

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
        RtlIsZeroMemory(buffer[0], new IntPtr(buffer.Length));

    /// <summary>
    /// Determines whether all bytes in a buffer are zero. If ntdll.RtlIsZeroMemory is available it is used,
    /// otherwise it falls back to a native method that compares groups of bytes is an optimized way.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns>If all bytes are zero, buffer is empty, true is returned, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBufferZero(this ReadOnlySpan<byte> buffer) =>
        RtlIsZeroMemory(buffer[0], new IntPtr(buffer.Length));

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

