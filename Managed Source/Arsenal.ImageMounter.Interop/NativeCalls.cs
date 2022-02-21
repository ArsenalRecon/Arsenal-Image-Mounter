using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter;

public static unsafe class NativeCalls
{
#if NETCOREAPP
	public static IntPtr CrtDllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if ((libraryName.StartsWith("msvcr", StringComparison.OrdinalIgnoreCase) ||
            libraryName.Equals("crtdll", StringComparison.OrdinalIgnoreCase)) &&
			!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return NativeLibrary.Load("c", assembly, searchPath);
        }

		return IntPtr.Zero;
	}
#endif

	[SupportedOSPlatform("windows")]
	private static class WindowsAPI
	{
		[DllImport("advapi32", CharSet = CharSet.Auto, EntryPoint = "SystemFunction036", SetLastError = true)]
		public static extern byte RtlGenRandom(IntPtr buffer, int length);

		[DllImport("advapi32", CharSet = CharSet.Auto, EntryPoint = "SystemFunction036", SetLastError = true)]
		public static extern byte RtlGenRandom(byte[] buffer, int length);

		public static void GetWindowsFunctions(out Action<byte[], int> GenRandomBytesFunc, out Action<IntPtr, int> GenRandomPtrFunc)
		{
			GenRandomBytesFunc = (buffer, length) => { if (RtlGenRandom(buffer, length) == 0) { throw new Exception("Random generation failed"); } };
			GenRandomPtrFunc = (buffer, length) => { if (RtlGenRandom(buffer, length) == 0) { throw new Exception("Random generation failed"); } };
		}
	}

	private static readonly Action<byte[], int> GenRandomBytesFunc;

	private static readonly Action<IntPtr, int> GenRandomPtrFunc;

#if NETSTANDARD || NETCOREAPP
	private static readonly Random Random = new();

	private static void InternalGenRandom(Span<byte> buffer)
    {
		lock (Random)
		{
			Random.NextBytes(buffer);
		}
    }

	static NativeCalls()
    {
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
			WindowsAPI.GetWindowsFunctions(out GenRandomBytesFunc, out GenRandomPtrFunc);
		}
		else
        {
			GenRandomBytesFunc = (buffer, length) => InternalGenRandom(buffer.AsSpan(0, length));
			GenRandomPtrFunc = (buffer, length) => InternalGenRandom(new Span<byte>(buffer.ToPointer(), length));
		}
	}
#else
	static NativeCalls()
	{
		GenRandomBytesFunc = (buffer, length) => { if (WindowsAPI.RtlGenRandom(buffer, length) == 0) { throw new Exception("Random generation failed"); } };
		GenRandomPtrFunc = (buffer, length) => { if (WindowsAPI.RtlGenRandom(buffer, length) == 0) { throw new Exception("Random generation failed"); } };
	}
#endif

	public static T GenRandomValue<T>() where T : unmanaged
	{
		T value;
		GenRandomPtrFunc(new IntPtr(&value), sizeof(T));
		return value;
	}

	public static sbyte GenRandomSByte() => GenRandomValue<sbyte>();

	public static short GenRandomInt16() => GenRandomValue<short>();

	public static int GenRandomInt32() => GenRandomValue<int>();

	public static long GenRandomInt64() => GenRandomValue<long>();

	public static byte GenRandomByte() => GenRandomValue<byte>();

	public static ushort GenRandomUInt16() => GenRandomValue<ushort>();

	public static uint GenRandomUInt32() => GenRandomValue<uint>();

	public static ulong GenRandomUInt64() => GenRandomValue<ulong>();

	public static Guid GenRandomGuid() => GenRandomValue<Guid>();

	public static byte[] GenRandomBytes(int count)
	{
		var bytes = new byte[count];
		GenRandomBytesFunc(bytes, count);
		return bytes;
	}

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
	public static void GenRandomBytes(byte[] bytes, int offset, int count) =>
		GenRandomBytes(bytes.AsSpan(offset, count));

    public static void GenRandomBytes(Span<byte> span)
    {
		fixed (byte* bytesPtr = span)
        {
			GenRandomPtrFunc(new IntPtr(bytesPtr), span.Length);
        }
    }
#else
	public static void GenRandomBytes(byte[] bytes, int offset, int count)
    {
		if (bytes is null)
        {
			throw new ArgumentNullException(nameof(bytes));
        }

		if (offset < 0 || checked(offset + count) > bytes.Length)
        {
			throw new IndexOutOfRangeException(nameof(offset));
        }

		fixed (byte* bytesPtr = &bytes[offset])
        {
			GenRandomPtrFunc(new IntPtr(bytesPtr), count);
        }
    }
#endif
}
