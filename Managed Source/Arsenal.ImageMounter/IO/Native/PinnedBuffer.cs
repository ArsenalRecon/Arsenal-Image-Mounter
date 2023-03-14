// Arsenal.ImageMounter.IO.PinnedBuffer
using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Arsenal.ImageMounter.IO.Native;

/// <summary>
/// Pins a value object for unmanaged use.
/// </summary>
[ComVisible(false)]
public class PinnedBuffer : SafeBuffer
{
    /// <summary>
    /// Contains GCHandle that holds managed data pinned
    /// </summary>
    protected GCHandle GCHandle { get; }

    /// <summary>
    /// Offset into managed buffer where unmanaged pointer starts
    /// </summary>
    public unsafe int Offset => checked((int)((byte*)handle.ToPointer() - (byte*)GCHandle.AddrOfPinnedObject().ToPointer()));

    /// <summary>
    /// Target managed object pinned by this instance
    /// </summary>
    public object? Target => GCHandle.Target;

    /// <summary>
    /// Initializes a new empty object
    /// </summary>
    protected PinnedBuffer()
        : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Initializes a new instance with an existing type T object and pins memory
    /// position.
    /// </summary>
    /// <param name="instance">Existing object to marshal to unmanaged memory.</param>
    public static PinnedString Create(string instance)
        => new(instance);

    /// <summary>
    /// Initializes a new instance with an existing type T array and pins memory
    /// position.
    /// </summary>
    /// <param name="instance">Existing object to marshal to unmanaged memory.</param>
    public static PinnedBuffer<T> Create<T>(T[] instance) where T : unmanaged => new(instance);

    /// <summary>
    /// Creates a new pinning for an offset into the existing pinned buffer.
    /// </summary>
    /// <param name="existing">Existing pinned object</param>
    /// <param name="offset">Offset into existing pinned objects</param>
    public PinnedBuffer(PinnedBuffer existing, int offset)
        : base(ownsHandle: true)
    {
        Initialize(checked(existing.ByteLength - (ulong)offset));
        GCHandle = GCHandle.Alloc(existing.GCHandle.Target, GCHandleType.Pinned);
        SetHandle(GCHandle.AddrOfPinnedObject() + existing.Offset + offset);
    }

    /// <summary>
    /// Initializes a new instance with an existing object and pins memory
    /// position.
    /// </summary>
    /// <param name="instance">Existing object to pin in memory.</param>
    /// <param name="totalObjectSize">Total number of bytes used by obj</param>
    /// <param name="byteOffset">Byte offset into memory where this instance should start</param>
    /// <param name="byteLength">Number of bytes from byteOffset to map into this instance</param>
    public PinnedBuffer(object instance, int totalObjectSize, int byteOffset, int byteLength)
        : this()
    {
        checked
        {
            if (byteOffset < 0 || byteLength < 0 || byteOffset + byteLength > totalObjectSize)
            {
                throw new IndexOutOfRangeException($"{byteOffset} and {byteLength} must resolve to positions within the array");
            }
        }

        Initialize(checked((ulong)byteLength));
        GCHandle = GCHandle.Alloc(instance, GCHandleType.Pinned);
        SetHandle(GCHandle.AddrOfPinnedObject() + byteOffset);
    }

    /// <summary>
    /// Initializes a new instance with an existing object and pins memory
    /// position.
    /// </summary>
    /// <param name="instance">Existing object to pin in memory.</param>
    /// <param name="size">Number of bytes in unmanaged memory</param>
    public PinnedBuffer(object instance, int size)
        : this()
    {
        Initialize(checked((ulong)size));
        GCHandle = GCHandle.Alloc(instance, GCHandleType.Pinned);
        SetHandle(GCHandle.AddrOfPinnedObject());
    }

    /// <summary>
    /// Implementation of <see cref="SafeHandle.ReleaseHandle"/> that releases
    /// the pinning GCHandle.
    /// </summary>
    /// <returns>Always returns true</returns>
    protected override bool ReleaseHandle()
    {
        GCHandle.Free();
        return true;
    }

    /// <summary>
    /// Creates a new pinned object for an offset into existing pinned object.
    /// </summary>
    /// <param name="existing">Existing pinned object</param>
    /// <param name="offset">Offset into existing pinned objects</param>
    /// <returns>New pinned object</returns>
    public static PinnedBuffer operator +(PinnedBuffer existing, int offset) =>
        new(existing, offset);

    /// <summary>
    /// Creates a new pinned object for an offset into existing pinned object.
    /// </summary>
    /// <param name="existing">Existing pinned object</param>
    /// <param name="offset">Offset into existing pinned objects</param>
    /// <returns>New pinned object</returns>
    public static PinnedBuffer operator -(PinnedBuffer existing, int offset) =>
        new(existing, checked(-offset));

    /// <summary>
    /// Calls ToString implementation of pinned object, or returns the string
    ///{Unallocated}' if object is not initialized.
    /// </summary>
    /// <returns>Calls ToString implementation of pinned object, or returns the string
    ///{Unallocated}' if object is not initialized.</returns>
    public override string? ToString()
        => GCHandle.IsAllocated
        ? GCHandle.Target?.ToString()
        : "{Unallocated}";
}

// Arsenal.ImageMounter.IO.PinnedBuffer<T>

/// <summary>
/// Pins an array of values for unmanaged use.
/// </summary>
/// <typeparam name="T">Type of elements in array.</typeparam>
[ComVisible(false)]
public class PinnedBuffer<T> : PinnedBuffer, IMemoryOwner<T> where T : unmanaged
{
    /// <summary>
    /// Returns associated object of this instance.
    /// </summary>
    public new T[] Target => (T[])GCHandle.Target!;

    /// <summary>
    /// Creates a <see cref="Memory{T}"/> representing the array pinned by this instance.
    /// </summary>
    public Memory<T> Memory
        => MemoryMarshal.CreateFromPinnedArray(Target, 0, Target.Length);

    /// <summary>
    /// Creates a <see cref="Memory{T}"/> representing a portion of the array pinned by this instance.
    /// </summary>
    public Memory<T> Slice(int start)
        => MemoryMarshal.CreateFromPinnedArray(Target, start, checked(Target.Length - start));

    /// <summary>
    /// Creates a <see cref="Memory{T}"/> representing a portion of the array pinned by this instance.
    /// </summary>
    public Memory<T> Slice(int start, int length)
        => MemoryMarshal.CreateFromPinnedArray(Target, start, length);

    /// <summary>
    /// Creates a <see cref="Span{T}"/> representing the array pinned by this instance.
    /// </summary>
    public Span<T> AsSpan()
        => new(Target, 0, Target.Length);

    /// <summary>
    /// Creates a <see cref="Span{T}"/> representing a portion of the array pinned by this instance.
    /// </summary>
    public Span<T> AsSpan(int start)
        => new(Target, start, checked(Target.Length - start));

    /// <summary>
    /// Creates a <see cref="Span{T}"/> representing a portion of the array pinned by this instance.
    /// </summary>
    public Span<T> AsSpan(int start, int length)
        => new(Target, start, length);

    /// <summary>
    /// Initializes a new instance with an new type T array and pins memory
    /// position.
    /// </summary>
    /// <param name="count">Number of items in new array.</param>
    public PinnedBuffer(int count)
        : base(new T[count], TypeSize * count)
    {
    }

    /// <summary>
    /// Returns unmanaged byte size of type <typeparamref name="T"/>
    /// </summary>
    public static unsafe int TypeSize { get; } = sizeof(T);

    /// <summary>
    /// Initializes a new instance with an existing type T array and pins memory
    /// position.
    /// </summary>
    /// <param name="instance">Existing object to marshal to unmanaged memory.</param>
    public PinnedBuffer(T[] instance)
        : base(instance, Buffer.ByteLength(instance))
    {
    }

    /// <summary>
    /// Initializes a new instance with an existing type T array and pins memory
    /// position.
    /// </summary>
    /// <param name="instance">Existing object to marshal to unmanaged memory.</param>
    /// <param name="arrayOffset">Offset in the existing object where this PinnedBuffer should begin.</param>
    /// <param name="arrayItems">Number of items in the array to cover with this PinnedBuffer instance.</param>
    public PinnedBuffer(T[] instance, int arrayOffset, int arrayItems)
        : base(
            instance,
            totalObjectSize: Buffer.ByteLength(instance),
            byteOffset: Buffer.ByteLength(instance) / instance.Length * arrayOffset,
            byteLength: Buffer.ByteLength(instance) / instance.Length * arrayItems)
    {
    }
}

// Arsenal.ImageMounter.IO.PinnedString

/// <summary>
/// Pins a managed string for unmanaged use.
/// </summary>
[ComVisible(false)]
public class PinnedString : PinnedBuffer
{
    /// <summary>
    /// Returns managed object pinned by this instance.
    /// </summary>
    public new string Target => (string)GCHandle.Target!;

    /// <summary>
    /// Creates a UNICODE_STRING structure pointing to the string buffer
    /// pinned by this instance. Useful for calls into ntdll.dll, LSA and
    /// similar native operating system components.
    /// </summary>
    public UNICODE_STRING UnicodeString => new(handle, checked((ushort)ByteLength));

    /// <summary>
    /// Initializes a new instance with an existing managed string and pins memory
    /// position.
    /// </summary>
    /// <param name="str">Managed string to pin in unmanaged memory.</param>
    public PinnedString(string str)
        : base(str, str.Length << 1)
    {
    }

    /// <summary>
    /// Initializes a new instance with a new managed string and pins memory
    /// position.
    /// </summary>
    /// <param name="count">Size in characters of managed string to pin in unmanaged memory.</param>
    public PinnedString(int count)
        : this(new string('\0', count))
    {
    }
}
