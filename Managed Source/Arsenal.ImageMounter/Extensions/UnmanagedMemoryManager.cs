using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Arsenal.ImageMounter.Extensions;

/// <summary>
/// </summary>
public static class UnmanagedMemoryExtensions
{
    /// <summary>
    /// Gets a disposable <see cref="MemoryManager{Byte}"/> for an unmanaged memory block.
    /// This can be used to get a <see cref="Memory{Byte}"/> that can be sent to asynchronous
    /// API or delegates. Remember though, that the memory is invalid after <see cref="SafeBuffer"/>
    /// has been unallocated or disposed.
    /// </summary>
    public static MemoryManager<byte> GetMemoryManager(this SafeBuffer safeBuffer)
        => new UnmanagedMemoryManager<byte>(safeBuffer.DangerousGetHandle(), (int)safeBuffer.ByteLength);
}

internal sealed class UnmanagedMemoryManager<T>(nint address, int count) : MemoryManager<T> where T : unmanaged
{
    private bool _disposed;

    public override unsafe Span<T> GetSpan()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnmanagedMemoryManager<T>));
        }

        return new((T*)address, count);
    }

    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnmanagedMemoryManager<T>));
        }

        if (elementIndex < 0 || elementIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }

        var pointer = address + elementIndex;
        return new MemoryHandle((T*)pointer, default, this);
    }

    public override void Unpin()
    {
        // No need to do anything, since we're dealing with unmanaged memory.
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            address = 0;
            count = 0;
            _disposed = true;
        }
    }

    public override unsafe string ToString()
        => $"{typeof(T).Name} 0x{address:x}[{count}]";
}
