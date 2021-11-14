using System;
using System.Runtime.InteropServices;

namespace Arsenal.ImageMounter.IO
{
	/// <summary>
	/// Structure for counted Unicode strings used in NT API calls
	/// </summary>
	public struct UNICODE_STRING
	{
		/// <summary>
		/// Length in bytes of Unicode string pointed to by Buffer
		/// </summary>
		public ushort Length { get; }

		/// <summary>
		/// Maximum length in bytes of string memory pointed to by Buffer
		/// </summary>
		public ushort MaximumLength { get; }

		/// <summary>
		/// Unicode character buffer in unmanaged memory
		/// </summary>
		public IntPtr Buffer { get; }

		public UNICODE_STRING(IntPtr str, ushort byte_count)
		{
			Length = byte_count;
			MaximumLength = byte_count;
			Buffer = str;
		}

		/// <summary>
		/// Creates a managed string object from UNICODE_STRING instance.
		/// </summary>
		/// <returns>Managed string</returns>
		public override string ToString()
		{
			if (Length == 0)
			{
				return string.Empty;
			}
			return Marshal.PtrToStringUni(Buffer, Length >> 1);
		}
	}
}
