//  DevioProviderLibEwf.vb
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils.Streams;
using LTRData.Extensions.Buffers;
using LTRData.Extensions.Formatting;
using LTRData.Extensions.IO;
using LTRData.Extensions.Native;
using LTRData.Extensions.Split;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET7_0_OR_GREATER
using System.Runtime.InteropServices.Marshalling;
#endif
using System.Text;

namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

public partial class DevioProviderLibEwf : DevioProviderUnmanagedBase
{
    public event EventHandler? Finishing;

    public static readonly byte AccessFlagsRead = libewf_get_access_flags_read();
    public static readonly byte AccessFlagsReadWrite = libewf_get_access_flags_read_write();
    public static readonly byte AccessFlagsWrite = libewf_get_access_flags_write();
    public static readonly byte AccessFlagsWriteResume = libewf_get_access_flags_write_resume();

    #region SafeHandles
    public sealed class SafeLibEwfFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {

        public SafeLibEwfFileHandle(nint handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        public SafeLibEwfFileHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle() => libewf_handle_close(handle, out _) >= 0;

        public override string ToString()
        {
            if (IsClosed)
            {
                return "Closed";
            }
            else
            {
                return IsInvalid ? "Invalid" : $"0x{handle:X}";
            }
        }
    }

    public sealed class SafeLibEwfErrorObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeLibEwfErrorObjectHandle(nint handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        public SafeLibEwfErrorObjectHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle() => libewf_error_free(ref handle) >= 0;

        public override string ToString()
        {
            if (IsInvalid)
            {
                return "No error";
            }

            var errmsg = ArrayPool<byte>.Shared.Rent(32000);

            try
            {
                var numChars = libewf_error_backtrace_sprint(this, out errmsg[0], errmsg.Length);

                if (numChars > 0)
                {
                    var endpos = errmsg.AsSpan(0, numChars).IndexOfTerminator();

                    var msg = Encoding.Default.GetString(errmsg, 0, endpos);

                    var msgs = msg
                        .AsMemory()
                        .TokenEnumReverse('\n')
                        .Select(msg => msg.TrimEnd('\r'));

                    return string.Join(Environment.NewLine, msgs);
                }
                else
                {
                    return $"Unknown error 0x{handle:X}";
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(errmsg);
            }
        }
    }
    #endregion

    public byte Flags { get; }

    public SafeLibEwfFileHandle SafeHandle { get; }

#if NET7_0_OR_GREATER
    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial byte libewf_get_access_flags_read();

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial byte libewf_get_access_flags_read_write();

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial byte libewf_get_access_flags_write();

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial byte libewf_get_access_flags_write_resume();

    [LibraryImport("libewf", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_notify_stream_open([MarshalAs(UnmanagedType.LPStr)] string filename, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void libewf_notify_set_verbose(int Verbose);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_initialize(out SafeLibEwfFileHandle handle, out nint errobj);

#if NET8_0_OR_GREATER
    [LibraryImport("libewf", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_open(SafeLibEwfFileHandle handle, [In, MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, int AccessFlags, out nint errobj);

    [LibraryImport("libewf", StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_open_wide(SafeLibEwfFileHandle handle, [In, MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, int AccessFlags, out nint errobj);
#else
    [LibraryImport("libewf", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_open(SafeLibEwfFileHandle handle, [MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, int AccessFlags, out nint errobj);

    [LibraryImport("libewf", StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_open_wide(SafeLibEwfFileHandle handle, [MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, int AccessFlags, out nint errobj);
#endif

    [Obsolete]
    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_get_media_size(SafeLibEwfFileHandle handle, out long media_size);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_get_media_size(SafeLibEwfFileHandle handle, out long media_size, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial long libewf_handle_seek_offset(SafeLibEwfFileHandle handle, long offset, Whence whence, out nint errobj);

    [Obsolete]
    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_read_random(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_handle_read_buffer(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_handle_read_buffer_at_offset(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset, out nint errobj);

    [Obsolete]
    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_write_random(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_handle_write_buffer(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_handle_write_buffer_at_offset(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset, out nint errobj);

    [Obsolete]
    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_write_finalize(SafeLibEwfFileHandle handle);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial nint libewf_handle_write_finalize(SafeLibEwfFileHandle handle, out nint errobj);

    [Obsolete]
    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int libewf_close(nint handle);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_close(nint handle, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_notify_stream_close(out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_notify_set_stream(nint FILE, out nint errobj);

    [Obsolete]
    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_get_bytes_per_sector(SafeLibEwfFileHandle safeLibEwfHandle, out uint SectorSize);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_get_bytes_per_sector(SafeLibEwfFileHandle safeLibEwfHandle, out uint SectorSize, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_get_chunk_size(SafeLibEwfFileHandle safeLibEwfHandle, out uint ChunkSize, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_get_sectors_per_chunk(SafeLibEwfFileHandle safeLibEwfHandle, out uint SectorsPerChunk, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_header_codepage(SafeLibEwfFileHandle safeLibEwfHandle, int codepage, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_bytes_per_sector(SafeLibEwfFileHandle safeLibEwfHandle, uint bytes_per_sector, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_media_size(SafeLibEwfFileHandle safeLibEwfHandle, ulong media_size, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_media_type(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_MEDIA_TYPE media_type, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_media_flags(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_MEDIA_FLAGS media_flags, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_format(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_FORMAT ewf_format, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_compression_method(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_COMPRESSION_METHOD compression_method, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_compression_values(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_COMPRESSION_LEVEL compression_level, LIBEWF_COMPRESSION_FLAGS compression_flags, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_maximum_segment_size(SafeLibEwfFileHandle safeLibEwfHandle, ulong maximum_segment_size, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_sectors_per_chunk(SafeLibEwfFileHandle safeLibEwfHandle, uint sectors_per_chunk, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_error_granularity(SafeLibEwfFileHandle safeLibEwfHandle, uint sectors_per_chunk, out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_error_fprint(SafeLibEwfErrorObjectHandle errobj, nint clibfile);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_error_free(ref nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_error_sprint(SafeLibEwfErrorObjectHandle errobj, out byte buffer, int length);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_error_backtrace_sprint(SafeLibEwfErrorObjectHandle errobj, out byte buffer, int length);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_get_utf16_header_value(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                   [MarshalAs(UnmanagedType.LPStr)] string identifier,
                                                                   nint identifier_length,
                                                                   [MarshalAs(UnmanagedType.LPWStr)] string utf16_string,
                                                                   nint utf16_string_length,
                                                                   out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_utf16_header_value(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                   [MarshalAs(UnmanagedType.LPStr)] string identifier,
                                                                   nint identifier_length,
                                                                   [MarshalAs(UnmanagedType.LPWStr)] string utf16_string,
                                                                   nint utf16_string_length,
                                                                   out nint errobj);

    [LibraryImport("libewf")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    private static partial int libewf_handle_set_utf8_hash_value(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                [MarshalAs(UnmanagedType.LPStr)] string hash_value_identifier,
                                                                nint hash_value_identifier_length,
                                                                [MarshalAs(UnmanagedType.LPUTF8Str)] string utf8_string,
                                                                nint utf8_string_length,
                                                                out nint errobj);

#else

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern byte libewf_get_access_flags_read();

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern byte libewf_get_access_flags_read_write();

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern byte libewf_get_access_flags_write();

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern byte libewf_get_access_flags_write_resume();

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most probably an analyzer bug")]
    private static extern int libewf_notify_stream_open([In][MarshalAs(UnmanagedType.LPStr)] string filename, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern void libewf_notify_set_verbose(int Verbose);

    [Obsolete("Use libewf_handle_open_wide instead")]
    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ThrowOnUnmappableChar = true)]
    private static extern SafeLibEwfFileHandle libewf_open_wide([In][MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, byte AccessFlags);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_initialize(out SafeLibEwfFileHandle handle, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_open(SafeLibEwfFileHandle handle, [In][MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, int AccessFlags, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_open_wide(SafeLibEwfFileHandle handle, [In][MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, int AccessFlags, out nint errobj);

    [Obsolete]
    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_get_media_size(SafeLibEwfFileHandle handle, out long media_size);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_get_media_size(SafeLibEwfFileHandle handle, out long media_size, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern long libewf_handle_seek_offset(SafeLibEwfFileHandle handle, long offset, Whence whence, out nint errobj);

    [Obsolete]
    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_read_random(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_handle_read_buffer(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_handle_read_buffer_at_offset(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset, out nint errobj);

    [Obsolete]
    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_write_random(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_handle_write_buffer(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_handle_write_buffer_at_offset(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long offset, out nint errobj);

    [Obsolete]
    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_write_finalize(SafeLibEwfFileHandle handle);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libewf_handle_write_finalize(SafeLibEwfFileHandle handle, out nint errobj);

    [Obsolete]
    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_close(nint handle);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_close(nint handle, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_notify_stream_close(out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_notify_set_stream(nint FILE, out nint errobj);

    [Obsolete]
    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_get_bytes_per_sector(SafeLibEwfFileHandle safeLibEwfHandle, out uint SectorSize);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_get_bytes_per_sector(SafeLibEwfFileHandle safeLibEwfHandle, out uint SectorSize, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_get_chunk_size(SafeLibEwfFileHandle safeLibEwfHandle, out uint ChunkSize, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_get_sectors_per_chunk(SafeLibEwfFileHandle safeLibEwfHandle, out uint SectorsPerChunk, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_header_codepage(SafeLibEwfFileHandle safeLibEwfHandle, int codepage, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_bytes_per_sector(SafeLibEwfFileHandle safeLibEwfHandle, uint bytes_per_sector, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_media_size(SafeLibEwfFileHandle safeLibEwfHandle, ulong media_size, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_media_type(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_MEDIA_TYPE media_type, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_media_flags(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_MEDIA_FLAGS media_flags, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_format(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_FORMAT ewf_format, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_compression_method(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_COMPRESSION_METHOD compression_method, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_compression_values(SafeLibEwfFileHandle safeLibEwfHandle, LIBEWF_COMPRESSION_LEVEL compression_level, LIBEWF_COMPRESSION_FLAGS compression_flags, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_maximum_segment_size(SafeLibEwfFileHandle safeLibEwfHandle, ulong maximum_segment_size, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_sectors_per_chunk(SafeLibEwfFileHandle safeLibEwfHandle, uint sectors_per_chunk, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_handle_set_error_granularity(SafeLibEwfFileHandle safeLibEwfHandle, uint sectors_per_chunk, out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_error_fprint(SafeLibEwfErrorObjectHandle errobj, nint clibfile);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libewf_error_free(ref nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl)]
    private static extern int libewf_error_sprint(SafeLibEwfErrorObjectHandle errobj, out byte buffer, int length);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl)]
    private static extern int libewf_error_backtrace_sprint(SafeLibEwfErrorObjectHandle errobj, out byte buffer, int length);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most probably an analyzer bug")]
    private static extern int libewf_handle_get_utf16_header_value(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                   [In][MarshalAs(UnmanagedType.LPStr, SizeParamIndex = 2)] string identifier,
                                                                   nint identifier_length,
                                                                   [MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] string utf16_string,
                                                                   nint utf16_string_length,
                                                                   out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most probably an analyzer bug")]
    private static extern int libewf_handle_set_utf16_header_value(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                   [In][MarshalAs(UnmanagedType.LPStr, SizeParamIndex = 2)] string identifier,
                                                                   nint identifier_length,
                                                                   [In][MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] string utf16_string,
                                                                   nint utf16_string_length,
                                                                   out nint errobj);

    [DllImport("libewf", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most probably an analyzer bug")]
    private static extern int libewf_handle_set_utf8_hash_value(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                [In][MarshalAs(UnmanagedType.LPStr, SizeParamIndex = 2)] string hash_value_identifier,
                                                                nint hash_value_identifier_length,
                                                                [In][MarshalAs(UnmanagedType.LPUTF8Str)] string utf8_string,
                                                                nint utf8_string_length,
                                                                out nint errobj);
#endif

    private delegate int f_libewf_handle_open(SafeLibEwfFileHandle handle, string[] filenames, int numberOfFiles, int AccessFlags, out nint errobj);

    private enum Whence : int
    {
        Set = 0,
        Current = 1,
        End = 2
    }

    private delegate int libewf_handle_set_header_value_func<TValue>(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                     TValue value,
                                                                     out nint errobj) where TValue : unmanaged;

    private delegate int libewf_handle_set_header_value_func<TValue1, TValue2>(SafeLibEwfFileHandle safeLibEwfHandle,
                                                                               TValue1 value1,
                                                                               TValue2 value2,
                                                                               out nint errobj)
        where TValue1 : unmanaged
        where TValue2 : unmanaged;

    public static void SetNotificationFile(string? path)
    {
        if (path is null || string.IsNullOrWhiteSpace(path))
        {
            if (libewf_notify_stream_close(out var errobj) < 0)
            {
                ThrowError(errobj, "Error closing notification stream.");
            }
        }
        else
        {
            if (libewf_notify_stream_open(path, out var errobj) < 0)
            {
                ThrowError(errobj, $"Error opening {path}.");
            }
        }
    }

    public static NamedPipeServerStream OpenNotificationStream()
    {
        var pipename = $"DevioProviderLibEwf-{Guid.NewGuid()}";
        var pipe = new NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
        if (libewf_notify_stream_open($@"\\?\PIPE\{pipename}", out var errobj) < 0)
        {
            pipe.Dispose();
            ThrowError(errobj, $"Error opening named pipe {pipename}.");
        }

        pipe.WaitForConnection();
        return pipe;
    }

    public static bool NotificationVerbose
    {
        set => libewf_notify_set_verbose(value ? 1 : 0);
    }

    public DevioProviderLibEwf(string[] filenames, byte Flags)
    {
        if (File.Exists(filenames[0]))
        {
            using var file = File.OpenRead(filenames[0]);
            var adCryptMagic = "ADCRYPT\0"u8;
            Span<byte> buffer = stackalloc byte[adCryptMagic.Length];
            if (file.ReadMaximum(buffer) >= adCryptMagic.Length
                && buffer.BinaryEqual(adCryptMagic))
            {
                throw new NotSupportedException("AD encryption is not currently supported.");
            }
        }

        this.Flags = Flags;

        if (libewf_handle_initialize(out var safeHandle, out var errobj) != 1
            || safeHandle.IsInvalid 
            || errobj != 0)
        {
            ThrowError(errobj, "Error initializing libewf handle.");
        }

        SafeHandle = safeHandle;

        f_libewf_handle_open func;

        if (IOExtensions.IsWindows)
        {
            func = libewf_handle_open_wide;
        }
        else
        {
            func = libewf_handle_open;
        }

        if (func(SafeHandle, filenames, filenames.Length, Flags, out errobj) != 1 || errobj != 0)
        {
            ThrowError(errobj, "Error opening image file(s)");
        }

        if (libewf_handle_get_bytes_per_sector(SafeHandle, out var sectorSize, out errobj) < 0 || errobj != 0)
        {
            ThrowError(errobj, "Unable to get number of bytes per sector");
        }

        SectorSize = sectorSize;

        if (libewf_handle_get_chunk_size(SafeHandle, out var chunkSize, out errobj) < 0 || errobj != 0)
        {
            ThrowError(errobj, "Unable to get chunk size");
        }

        ChunkSize = chunkSize;

        if (libewf_handle_get_sectors_per_chunk(SafeHandle, out var sectorsPerChunk, out errobj) < 0 || errobj != 0)
        {
            ThrowError(errobj, "Unable to get number of sectors per chunk");
        }

        SectorsPerChunk = sectorsPerChunk;
    }

    protected static void ThrowError(nint errobj, string message)
    {
        if (errobj != 0)
        {
            using var err = new SafeLibEwfErrorObjectHandle(errobj, ownsHandle: true);

            var errmsg = err.ToString();

            throw new IOException($"{message}: {errmsg}");
        }
        else
        {
            throw new IOException(message);
        }
    }

    public DevioProviderLibEwf(string firstfilename, byte Flags)
        : this(ProviderSupport.EnumerateMultiSegmentFiles(firstfilename).ToArray(), Flags)
    {
    }

    public override bool CanWrite => (Flags & AccessFlagsWrite) == AccessFlagsWrite;

    /// <summary>
    /// Parallel operation is only supported with newer libewf with random access
    /// read/write exports.
    /// </summary>
    public override bool SupportsParallel => ImportedReadAtOffset != 0;

    public override long Length
    {
        get
        {
            var RC = libewf_handle_get_media_size(SafeHandle, out var LengthRet, out var errobj);
            if (RC < 0 || errobj != 0)
            {
                ThrowError(errobj, "libewf_handle_get_media_size() failed");
            }

            return LengthRet;
        }
    }

    public int MaxIoSize { get; private set; } = int.MaxValue;

    private delegate nint FuncReadWriteAtOffset(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long fileoffset, out nint errobj);

    private static readonly nint ImportedReadAtOffset
#if NETFRAMEWORK || NETCOREAPP
        = NativeLib.GetProcAddressNoThrow("libewf", "libewf_handle_read_buffer_at_offset");
#else
        = 0;
#endif

    private static readonly FuncReadWriteAtOffset ReadAtOffset =
        ImportedReadAtOffset != 0
        ? libewf_handle_read_buffer_at_offset
        : InternalReadAtOffset;

    private static nint InternalReadAtOffset(SafeLibEwfFileHandle handle, nint buffer, nint buffer_size, long fileoffset, out nint errobj)
    {
        var offset = libewf_handle_seek_offset(handle, fileoffset, Whence.Set, out errobj);

        if (offset != fileoffset || errobj != 0)
        {
            return -1;
        }

        var result = libewf_handle_read_buffer(handle, buffer, buffer_size, out errobj);

        return result;
    }

    public override int Read(nint buffer, int bufferoffset, int count, long fileoffset)
    {
        var done_count = 0;

        while (done_count < count)
        {
            var bytesThisIteration = count - done_count;

            if (bytesThisIteration > MaxIoSize)
            {
                bytesThisIteration = MaxIoSize;
            }

            // Dim chunk_offset = CInt(fileoffset And (_ChunkSize - 1))

            // If chunk_offset + iteration_count > _ChunkSize Then

            // iteration_count = CInt(_ChunkSize) - chunk_offset

            // End If

            var result = (int)ReadAtOffset(SafeHandle, buffer + bufferoffset, bytesThisIteration, fileoffset, out var errobj);

            if (result < 0 || errobj != 0)
            {
                ThrowError(errobj, $"Error reading {bytesThisIteration} bytes from offset {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}");
            }

            if (result == 0)
            {
                break;
            }
            
            done_count += result;
            fileoffset += result;
            bufferoffset += result;
        }

        return done_count;
    }

    private static readonly nint ImportedSetCompressionMethod
#if NETFRAMEWORK || NETCOREAPP
        = NativeLib.GetProcAddressNoThrow("libewf", "libewf_handle_set_compression_method");
#else
        = 0;
#endif

    public override int Write(nint buffer, int bufferoffset, int count, long fileoffset)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Cannot write to read-only ewf image files");
        }

        var sizedone = 0;

        var size = (nint)count;

        var offset = libewf_handle_seek_offset(SafeHandle, fileoffset, Whence.Set, out var errobj);
        if (offset != fileoffset || errobj != 0)
        {
            ThrowError(errobj, $"Error seeking to position {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}");
        }

        while (sizedone < count)
        {
            var sizenow = size - sizedone;
            if (sizenow > 32764)
            {
                sizenow = 16384;
            }

            var retval = libewf_handle_write_buffer(SafeHandle, buffer + bufferoffset + sizedone, sizenow, out errobj);

            if (retval <= 0 || errobj != 0)
            {
                ThrowError(errobj, "Write failed");
            }

            sizedone += (int)retval;
        }

        return sizedone;
    }

    protected override void Dispose(bool disposing)
    {
        Finishing?.Invoke(this, EventArgs.Empty);

        if (disposing && SafeHandle is not null)
        {
            SafeHandle.Dispose();
        }

        base.Dispose(disposing);
    }

    public override uint SectorSize { get; }

    public uint ChunkSize { get; }

    public uint SectorsPerChunk { get; }

    public void SetOutputStringParameter(string identifier, string? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var retval = libewf_handle_set_utf16_header_value(SafeHandle, identifier, identifier.Length, value, value.Length, out var errobj);

        if (retval != 1 || errobj != 0)
        {
            ThrowError(errobj, $"Parameter set '{identifier}'='{value}' failed");
        }
    }

    public void SetOutputHashParameter(string identifier, byte[] value)
    {
        if (value is null)
        {
            return;
        }

        var valuestr = value.ToHexString();

        Trace.WriteLine($"{identifier} = {valuestr}");

        var retval = libewf_handle_set_utf8_hash_value(SafeHandle, identifier, identifier.Length, valuestr, valuestr.Length, out var errobj);

        if (retval != 1 || errobj != 0)
        {
            ThrowError(errobj, $"Hash result set '{identifier}'='{value}' failed");
        }
    }

    private void SetOutputValueParameter<TValue>(libewf_handle_set_header_value_func<TValue> func, TValue value) where TValue : unmanaged
    {
        var retval = func(SafeHandle, value, out var errobj);

        if (retval != 1 || errobj != 0)
        {
            ThrowError(errobj, $"Parameter set {func.Method.Name}({value}) failed");
        }
    }

    private void SetOutputValueParameter<TValue1, TValue2>(libewf_handle_set_header_value_func<TValue1, TValue2> func, TValue1 value1, TValue2 value2)
        where TValue1 : unmanaged
        where TValue2 : unmanaged
    {
        var retval = func(SafeHandle, value1, value2, out var errobj);

        if (retval != 1 || errobj != 0)
        {
            ThrowError(errobj, $"Parameter set {func.Method.Name}({value1}, {value2}) failed");
        }
    }

    public void SetOutputParameters(ImagingParameters ImagingParameters)
    {
        SetOutputStringParameter("case_number", ImagingParameters.CaseNumber);
        SetOutputStringParameter("description", ImagingParameters.Description);
        SetOutputStringParameter("evidence_number", ImagingParameters.EvidenceNumber);
        SetOutputStringParameter("examiner_name", ImagingParameters.ExaminerName);
        SetOutputStringParameter("notes", ImagingParameters.Notes);
        SetOutputStringParameter("acquiry_operating_system", ImagingParameters.AcquiryOperatingSystem);
        SetOutputStringParameter("acquiry_software", ImagingParameters.AcquirySoftware);

        SetOutputStringParameter("model", ImagingParameters.StorageStandardProperties.ProductId);
        SetOutputStringParameter("serial_number", ImagingParameters.StorageStandardProperties.SerialNumber);

        SetOutputValueParameter(libewf_handle_set_header_codepage, ImagingParameters.CodePage);
        SetOutputValueParameter(libewf_handle_set_bytes_per_sector, ImagingParameters.BytesPerSector);
        SetOutputValueParameter(libewf_handle_set_media_size, ImagingParameters.MediaSize);
        SetOutputValueParameter(libewf_handle_set_media_type, ImagingParameters.MediaType);
        SetOutputValueParameter(libewf_handle_set_media_flags, ImagingParameters.MediaFlags);
        SetOutputValueParameter(libewf_handle_set_format, ImagingParameters.UseEWFFileFormat);

        if (ImportedSetCompressionMethod != 0)
        {
            SetOutputValueParameter(libewf_handle_set_compression_method, ImagingParameters.CompressionMethod);
        }

        SetOutputValueParameter(libewf_handle_set_compression_values, ImagingParameters.CompressionLevel, ImagingParameters.CompressionFlags);
        SetOutputValueParameter(libewf_handle_set_maximum_segment_size, ImagingParameters.SegmentFileSize);
        SetOutputValueParameter(libewf_handle_set_sectors_per_chunk, ImagingParameters.SectorsPerChunk);

        if (ImagingParameters.SectorErrorGranularity == 0
            || ImagingParameters.SectorErrorGranularity >= ImagingParameters.SectorsPerChunk)
        {
            ImagingParameters.SectorErrorGranularity = ImagingParameters.SectorsPerChunk;
        }

        SetOutputValueParameter(libewf_handle_set_error_granularity, ImagingParameters.SectorErrorGranularity);
    }

    public class ImagingParameters
    {
        public const int LIBEWF_CODEPAGE_ASCII = 20127;

        public int CodePage { get; set; } = LIBEWF_CODEPAGE_ASCII;

        public StorageStandardProperties StorageStandardProperties { get; set; }

        public string? CaseNumber { get; set; }

        public string? Description { get; set; }

        public string? EvidenceNumber { get; set; }

        public string? ExaminerName { get; set; }

        public string? Notes { get; set; }

        public string AcquiryOperatingSystem { get; set; } = RuntimeInformation.OSDescription;

        public string AcquirySoftware { get; set; } = "aim-libewf";

        public ulong MediaSize { get; set; }

        public uint BytesPerSector { get; set; }

        public uint SectorsPerChunk { get; set; } = 64U;

        public uint SectorErrorGranularity { get; set; } = 64U;

        /// <summary>
        /// logical, physical
        /// </summary>
        public LIBEWF_MEDIA_FLAGS MediaFlags { get; set; } = LIBEWF_MEDIA_FLAGS.PHYSICAL;

        /// <summary>
        /// fixed, removable, optical, memory
        /// </summary>
        public LIBEWF_MEDIA_TYPE MediaType { get; set; } = LIBEWF_MEDIA_TYPE.FIXED;

        /// <summary>
        /// ewf, smart, ftk, encase1, encase2, encase3, encase4, encase5, encase6, encase7, encase7-v2, linen5, linen6, linen7, ewfx
        /// </summary>
        public LIBEWF_FORMAT UseEWFFileFormat { get; set; } = LIBEWF_FORMAT.ENCASE6;

        /// <summary>
        /// deflate
        /// </summary>
        public LIBEWF_COMPRESSION_METHOD CompressionMethod { get; set; } = LIBEWF_COMPRESSION_METHOD.DEFLATE;

        /// <summary>
        /// none, empty-block, fast, best
        /// </summary>
        public LIBEWF_COMPRESSION_LEVEL CompressionLevel { get; set; } = LIBEWF_COMPRESSION_LEVEL.FAST;

        public LIBEWF_COMPRESSION_FLAGS CompressionFlags { get; set; }

        public ulong SegmentFileSize { get; set; } = 2UL << 40;
    }

    public enum LIBEWF_FORMAT : byte
    {
        ENCASE1 = 0x1,
        ENCASE2 = 0x2,
        ENCASE3 = 0x3,
        ENCASE4 = 0x4,
        ENCASE5 = 0x5,
        ENCASE6 = 0x6,
        ENCASE7 = 0x7,

        SMART = 0xE,
        FTK_IMAGER = 0xF,

        LOGICAL_ENCASE5 = 0x10,
        LOGICAL_ENCASE6 = 0x11,
        LOGICAL_ENCASE7 = 0x12,

        LINEN5 = 0x25,
        LINEN6 = 0x26,
        LINEN7 = 0x27,

        V2_ENCASE7 = 0x37,

        V2_LOGICAL_ENCASE7 = 0x47,

        // The format as specified by Andrew Rosen

        EWF = 0x70,

        // Libewf eXtended EWF format

        EWFX = 0x71
    }

    public enum LIBEWF_MEDIA_TYPE : byte
    {
        REMOVABLE = 0x0,
        FIXED = 0x1,
        OPTICAL = 0x3,
        SINGLE_FILES = 0xE,
        MEMORY = 0x10
    }

    [Flags]
    public enum LIBEWF_MEDIA_FLAGS : byte
    {
        PHYSICAL = 0x2,
        FASTBLOC = 0x4,
        TABLEAU = 0x8
    }

    public enum LIBEWF_COMPRESSION_METHOD : ushort
    {
        NONE = 0,
        DEFLATE = 1,
        BZIP2 = 2
    }

    public enum LIBEWF_COMPRESSION_LEVEL : sbyte
    {
        DEFAULT = -1,
        NONE = 0,
        FAST = 1,
        BEST = 2
    }

    [Flags]
    public enum LIBEWF_COMPRESSION_FLAGS : byte
    {
        NONE = 0x0,
        USE_EMPTY_BLOCK_COMPRESSION = 0x1,
        USE_PATTERN_FILL_COMPRESSION = 0x10
    }
}
