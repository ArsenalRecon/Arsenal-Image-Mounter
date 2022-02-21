// Arsenal.ImageMounter.IO.PinnedBuffer
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Arsenal.ImageMounter.IO;

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
    public object Target => GCHandle.Target;

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
    public static PinnedString Create(string instance) => new(instance);

    /// <summary>
    /// Initializes a new instance with an existing type T array and pins memory
    /// position.
    /// </summary>
    /// <param name="instance">Existing object to marshal to unmanaged memory.</param>
    public static PinnedBuffer<T> Create<T>(T[] instance) where T : struct => new(instance);

    /// <summary>
    /// Serializes a value structure as into a new byte array
    /// </summary>
    /// <typeparam name="T">Type of managed strucute to serialize</typeparam>
    /// <param name="instance">Instance of managed structure to serialize</param>
    /// <returns></returns>
    public static PinnedBuffer<byte> Serialize<T>(in T instance) where T : struct
    {
        var pinnedBuffer = new PinnedBuffer<byte>(PinnedBuffer<T>.TypeSize);
        pinnedBuffer.Write(0uL, instance);
        return pinnedBuffer;
    }

    /// <summary>
    /// Deserializes a managed value structure from a byte array
    /// </summary>
    /// <typeparam name="T">Type of managed structure to deserialize</typeparam>
    /// <param name="buffer">Byte array containing data to form the managed structure</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is null or too small</exception>
    public static T Deserialize<T>(byte[] buffer) where T : struct
    {
        if (buffer == null || buffer.Length < PinnedBuffer<T>.TypeSize)
        {
            throw new ArgumentException("Invalid input buffer", nameof(buffer));
        }
        using var pinned = Create(buffer);
        return pinned.Read<T>(0uL);
    }

    /// <summary>
    /// Creates a new pinning for an offset into the existing pinned buffer.
    /// </summary>
    /// <param name="existing">Existing pinned object</param>
    /// <param name="offset">Offset into existing pinned objects</param>
    public PinnedBuffer(PinnedBuffer existing, int offset)
        : base(ownsHandle: true)
    {
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
    public PinnedBuffer(object instance, int totalObjectSize, int byteOffset)
        : this()
    {
        if (byteOffset > totalObjectSize)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset), "Argument byteOffset must be within total object size");
        }
        Initialize(checked((ulong)(totalObjectSize - byteOffset)));
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
    /// '{Unallocated}' if object is not initialized.
    /// </summary>
    /// <returns>Calls ToString implementation of pinned object, or returns the string
    /// '{Unallocated}' if object is not initialized.</returns>
    public override string ToString()
    {
        if (GCHandle.IsAllocated)
        {
            return GCHandle.Target.ToString();
        }
        return "{Unallocated}";
    }
}

// Arsenal.ImageMounter.IO.PinnedBuffer<T>

/// <summary>
/// Pins an array of values for unmanaged use.
/// </summary>
/// <typeparam name="T">Type of elements in array.</typeparam>
[ComVisible(false)]
public class PinnedBuffer<T> : PinnedBuffer where T : struct
{
    /// <summary>
    /// Returns associated object of this instance.
    /// </summary>
    public new T[] Target => (T[])GCHandle.Target;

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    /// <summary>
    /// Creates a Memory&lt;T&gt; representing the array pinned by this instance.
    /// </summary>
    public Memory<T> AsMemory() => MemoryMarshal.CreateFromPinnedArray(Target, 0, Target.Length);

    /// <summary>
    /// Creates a Memory&lt;T&gt; representing the array pinned by this instance.
    /// </summary>
    public Memory<T> AsMemory(int start) => MemoryMarshal.CreateFromPinnedArray(Target, start, checked(Target.Length - start));

    /// <summary>
    /// Creates a Memory&lt;T&gt; representing the array pinned by this instance.
    /// </summary>
    public Memory<T> AsMemory(int start, int length) => MemoryMarshal.CreateFromPinnedArray(Target, start, length);

    /// <summary>
    /// Creates a Memory&lt;T&gt; representing the array pinned by this instance.
    /// </summary>
    public Span<T> AsSpan() => new(Target, 0, Target.Length);

    /// <summary>
    /// Creates a Memory&lt;T&gt; representing the array pinned by this instance.
    /// </summary>
    public Span<T> AsSpan(int start) => new(Target, start, checked(Target.Length - start));

    /// <summary>
    /// Creates a Memory&lt;T&gt; representing the array pinned by this instance.
    /// </summary>
    public Span<T> AsSpan(int start, int length) => new(Target, start, length);
#endif

    /// <summary>
    /// Initializes a new instance with an new type T array and pins memory
    /// position.
    /// </summary>
    /// <param name="count">Number of items in new array.</param>
    public PinnedBuffer(int count)
        : base(new T[count], GetTypeSize() * count)
    {
    }

    /// <summary>
    /// Returns unmanaged byte size of type <typeparamref name="T"/>
    /// </summary>
    public static int TypeSize { get; } = GetTypeSize();

    private static int GetTypeSize()
    {
        if (typeof(T) == typeof(char))
        {
            return 2;
        }

#if NETFRAMEWORK && !NET451_OR_GREATER
		return Marshal.SizeOf(typeof(T));
#else
        return Marshal.SizeOf<T>();
#endif
    }

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
        : base(instance, MakeTotalByteSize(instance, arrayOffset, arrayItems), Buffer.ByteLength(instance) / instance.Length * arrayOffset)
    {
    }

    private static int MakeTotalByteSize(T[] obj, int arrayOffset, int arrayItems)
    {
        checked
        {
            if (arrayOffset < 0 || arrayItems < 0 || arrayOffset + arrayItems > obj.Length)
            {
                throw new IndexOutOfRangeException($"{arrayOffset} and {arrayItems} must resolve to positions within the array");
            }
        }
        return Buffer.ByteLength(obj);
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
    public new string Target => (string)GCHandle.Target;

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
