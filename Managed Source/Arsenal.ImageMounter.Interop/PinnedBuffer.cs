// Arsenal.ImageMounter.IO.PinnedBuffer
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Arsenal.ImageMounter.IO
{

	/// <summary>
	/// Pins a value object for unmanaged use.
	/// </summary>
	[ComVisible(false)]
	public class PinnedBuffer : SafeBuffer
	{
		protected GCHandle GCHandle { get; }

		unsafe public int Offset
		{
			get
			{
				checked
				{
					return (int)((byte*)handle.ToPointer() - (byte*)GCHandle.AddrOfPinnedObject().ToPointer());
				}
			}
		}

		public object Target => GCHandle.Target;

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
		{
			return new PinnedString(instance);
		}

		/// <summary>
		/// Initializes a new instance with an existing type T array and pins memory
		/// position.
		/// </summary>
		/// <param name="instance">Existing object to marshal to unmanaged memory.</param>
		public static PinnedBuffer<T> Create<T>(T[] instance) where T : struct
		{
			return new PinnedBuffer<T>(instance);
		}

		public static T PtrToStructure<T>(IntPtr address)
		{
			return (T)Marshal.PtrToStructure(address, typeof(T));
		}

		public static void DestroyStructure<T>(IntPtr address)
		{
			Marshal.DestroyStructure(address, typeof(T));
		}

		public static PinnedBuffer<byte> Serialize<T>(T instance) where T : struct
		{
			var pinnedBuffer = new PinnedBuffer<byte>(PinnedBuffer<T>.TypeSize);
			pinnedBuffer.Write(0uL, instance);
			return pinnedBuffer;
		}

		public static T Deserialize<T>(byte[] buffer) where T : struct
		{
			if (buffer == null || buffer.Length < PinnedBuffer<T>.TypeSize)
			{
				throw new ArgumentException("Invalid input buffer", nameof(buffer));
			}
			using var pinned = Create(buffer);
			return pinned.Read<T>(0uL);
		}

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
		/// <param name="totalObjectSize">Total number of bytes used by obj in unmanaged memory</param>
		/// <param name="byteOffset">Byte offset into unmanaged memory where this instance should start</param>
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

		protected override bool ReleaseHandle()
		{
			GCHandle.Free();
			return true;
		}

		public static PinnedBuffer operator +(PinnedBuffer existing, int offset)
		{
			return new PinnedBuffer(existing, offset);
		}

		public static PinnedBuffer operator -(PinnedBuffer existing, int offset)
		{
			return new PinnedBuffer(existing, checked(-offset));
		}

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
			: base(instance, MakeTotalByteSize(instance, arrayOffset, arrayItems), unchecked(Buffer.ByteLength(instance) / instance.Length) * arrayOffset)
		{
		}

		private static int MakeTotalByteSize(T[] obj, int arrayOffset, int arrayItems)
		{
			checked
			{
				if (arrayOffset >= obj.Length || arrayOffset + arrayItems > obj.Length)
				{
					throw new IndexOutOfRangeException($"{arrayOffset} and {arrayItems} must resolve to positions within the array");
				}
				if (arrayOffset + arrayItems < obj.Length)
				{
					return Buffer.ByteLength(obj) / obj.Length * arrayOffset + arrayItems;
				}
				return Buffer.ByteLength(obj);
			}
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
}
