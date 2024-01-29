//  DevioProviderLibQcow.vb
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Extensions;
using LTRData.Extensions.Buffers;
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
using System.Runtime.InteropServices;
using System.Text;



namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

public partial class DevioProviderLibQcow : DevioProviderUnmanagedBase
{
    public event EventHandler? Finishing;

    public static readonly byte AccessFlagsRead = libqcow_get_access_flags_read();
    public static readonly byte AccessFlagsReadWrite = libqcow_get_access_flags_read_write();
    public static readonly byte AccessFlagsWrite = libqcow_get_access_flags_write();

    #region SafeHandles
    public sealed class SafeLibQcowFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {

        public SafeLibQcowFileHandle(nint handle, bool ownsHandle)
            : base(ownsHandle)
        {

            SetHandle(handle);
        }

        public SafeLibQcowFileHandle()
            : base(true)
        {

        }

        protected override bool ReleaseHandle() => libqcow_file_close(handle, out _) >= 0;

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

    public sealed class SafeLibQcowErrorObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
    {

        public SafeLibQcowErrorObjectHandle(nint handle, bool ownsHandle)
            : base(ownsHandle)
        {

            SetHandle(handle);
        }

        public SafeLibQcowErrorObjectHandle()
            : base(true)
        {

        }

        protected override bool ReleaseHandle() => libqcow_error_free(ref handle) >= 0;

        public override string ToString()
        {
            if (IsInvalid)
            {
                return "No error";
            }

            var errmsg = ArrayPool<byte>.Shared.Rent(32000);

            try
            {
                var numChars = libqcow_error_backtrace_sprint(this, out errmsg[0], errmsg.Length);

                if (numChars > 0)
                {
                    var endpos = errmsg.AsSpan(0, numChars).IndexOfTerminator();

                    var msg = Encoding.Default.GetString(errmsg, 0, endpos);

                    var msgs = msg
                        .AsMemory()
                        .SplitReverse('\n')
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

    public override bool SupportsParallel => true;

    public byte Flags { get; }

    public SafeLibQcowFileHandle SafeHandle { get; }

#if NET7_0_OR_GREATER
    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial byte libqcow_get_access_flags_read();

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial byte libqcow_get_access_flags_read_write();

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial byte libqcow_get_access_flags_write();

    [LibraryImport("libqcow", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_notify_stream_open([MarshalAs(UnmanagedType.LPStr)] string filename, out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void libqcow_notify_set_verbose(int Verbose);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_file_initialize(out SafeLibQcowFileHandle handle, out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_file_open(SafeLibQcowFileHandle handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, int AccessFlags, out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_file_open_wide(SafeLibQcowFileHandle handle, [MarshalAs(UnmanagedType.LPWStr)] string filename, int AccessFlags, out nint errobj);

    [Obsolete]
    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_get_media_size(SafeLibQcowFileHandle handle, out long media_size);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_file_get_media_size(SafeLibQcowFileHandle handle, out long media_size, out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial long libqcow_file_seek_offset(SafeLibQcowFileHandle handle, long offset, Whence whence, out nint errobj);

    [Obsolete]
    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial nint libqcow_read_random(SafeLibQcowFileHandle handle, nint buffer, nint buffer_size, long offset);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial nint libqcow_file_read_buffer(SafeLibQcowFileHandle handle, nint buffer, nint buffer_size, out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial nint libqcow_file_read_buffer_at_offset(SafeLibQcowFileHandle handle, nint buffer, nint buffer_size, long offset, out nint errobj);

    [Obsolete]
    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial int libqcow_close(nint handle);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_file_close(nint handle, out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_notify_stream_close(out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_notify_set_stream(nint FILE, out nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_error_fprint(SafeLibQcowErrorObjectHandle errobj, nint clibfile);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_error_free(ref nint errobj);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_error_sprint(SafeLibQcowErrorObjectHandle errobj, out byte buffer, int length);

    [LibraryImport("libqcow")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial int libqcow_error_backtrace_sprint(SafeLibQcowErrorObjectHandle errobj, out byte buffer, int length);
#else
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern byte libqcow_get_access_flags_read();

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern byte libqcow_get_access_flags_read_write();

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern byte libqcow_get_access_flags_write();

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most likely analyzer bug")]
    private static extern int libqcow_notify_stream_open([In][MarshalAs(UnmanagedType.LPStr)] string filename, out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern void libqcow_notify_set_verbose(int Verbose);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ThrowOnUnmappableChar = true)]
    private static extern SafeLibQcowFileHandle libqcow_open_wide([In][MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, byte AccessFlags);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_initialize(out SafeLibQcowFileHandle handle, out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most likely analyzer bug")]
    private static extern int libqcow_file_open(SafeLibQcowFileHandle handle, [In][MarshalAs(UnmanagedType.LPUTF8Str)] string filename, int AccessFlags, out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_open_wide(SafeLibQcowFileHandle handle, [In][MarshalAs(UnmanagedType.LPWStr)] string filename, int AccessFlags, out nint errobj);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_get_media_size(SafeLibQcowFileHandle handle, out long media_size);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_get_media_size(SafeLibQcowFileHandle handle, out long media_size, out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern long libqcow_file_seek_offset(SafeLibQcowFileHandle handle, long offset, Whence whence, out nint errobj);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libqcow_read_random(SafeLibQcowFileHandle handle, nint buffer, nint buffer_size, long offset);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libqcow_file_read_buffer(SafeLibQcowFileHandle handle, nint buffer, nint buffer_size, out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern nint libqcow_file_read_buffer_at_offset(SafeLibQcowFileHandle handle, nint buffer, nint buffer_size, long offset, out nint errobj);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_close(nint handle);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_close(nint handle, out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_notify_stream_close(out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_notify_set_stream(nint FILE, out nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_error_fprint(SafeLibQcowErrorObjectHandle errobj, nint clibfile);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_error_free(ref nint errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl)]
    private static extern int libqcow_error_sprint(SafeLibQcowErrorObjectHandle errobj, out byte buffer, int length);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl)]
    private static extern int libqcow_error_backtrace_sprint(SafeLibQcowErrorObjectHandle errobj, out byte buffer, int length);
#endif

    private delegate int f_libqcow_file_open(SafeLibQcowFileHandle handle, string filename, int AccessFlags, out nint errobj);

    private enum Whence : int
    {
        Set = 0,
        Current = 1,
        End = 2
    }

    public static void SetNotificationFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            if (libqcow_notify_stream_close(out var errobj) < 0)
            {
                ThrowError(errobj, "Error closing notification stream.");
            }
        }
        else
        {
            if (libqcow_notify_stream_open(path, out var errobj) < 0)
            {
                ThrowError(errobj, $"Error opening {path}.");
            }
        }
    }

    public static NamedPipeServerStream OpenNotificationStream()
    {
        var pipename = $"DevioProviderLibEwf-{Guid.NewGuid()}";
        var pipe = new NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
        if (libqcow_notify_stream_open($@"\\?\PIPE\{pipename}", out var errobj) < 0)
        {
            pipe.Dispose();
            ThrowError(errobj, $"Error opening named pipe {pipename}.");
        }

        pipe.WaitForConnection();
        return pipe;
    }

    public static bool NotificationVerbose
    {
        set => libqcow_notify_set_verbose(value ? 1 : 0);
    }

    public DevioProviderLibQcow(string filenames, byte Flags)
    {
        this.Flags = Flags;

        if (libqcow_file_initialize(out var safeHandle, out var errobj) != 1 || safeHandle.IsInvalid || errobj != 0)
        {
            ThrowError(errobj, "Error initializing libqcow handle.");
        }

        SafeHandle = safeHandle;

        f_libqcow_file_open func;

        if (IOExtensions.IsWindows)
        {
            func = libqcow_file_open_wide;
        }
        else
        {
            func = libqcow_file_open;
        }

        if (func(SafeHandle, filenames, Flags, out errobj) != 1 || errobj != 0)
        {

            ThrowError(errobj, "Error opening image file(s)");

        }
    }

    protected static void ThrowError(nint errobj, string message)
    {
        if (errobj != 0)
        {
            using var err = new SafeLibQcowErrorObjectHandle(errobj, ownsHandle: true);

            var errmsg = err.ToString();

            throw new IOException($"{message}: {errmsg}");
        }
        else
        {
            throw new IOException(message);
        }
    }

    public override bool CanWrite => (Flags & AccessFlagsWrite) == AccessFlagsWrite;

    public override long Length
    {
        get
        {
            var RC = libqcow_file_get_media_size(SafeHandle, out var LengthRet, out var errobj);
            if (RC < 0 || errobj != 0)
            {
                ThrowError(errobj, "libqcow_file_get_media_size() failed");
            }

            return LengthRet;
        }
    }

    public int MaxIoSize { get; private set; } = int.MaxValue;

    public override uint SectorSize { get; } = 512U;

    public override int Read(nint buffer, int bufferoffset, int count, long fileoffset)
    {
        var done_count = 0;

        while (done_count < count)
        {
            var iteration_count = count - done_count;

            if (iteration_count > MaxIoSize)
            {
                iteration_count = MaxIoSize;
            }

            // Dim chunk_offset = CInt(fileoffset And (_ChunkSize - 1))

            // If chunk_offset + iteration_count > _ChunkSize Then

            // iteration_count = CInt(_ChunkSize) - chunk_offset

            // End If

            var result = (int)libqcow_file_read_buffer_at_offset(SafeHandle, buffer + bufferoffset, iteration_count, fileoffset, out var errobj);

            if (result < 0 || errobj != 0)
            {
                ThrowError(errobj, $"Error reading {iteration_count} bytes from offset {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}");
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

    protected override void Dispose(bool disposing)
    {
        Finishing?.Invoke(this, EventArgs.Empty);

        if (disposing && SafeHandle is not null)
        {
            SafeHandle.Dispose();
        }

        base.Dispose(disposing);
    }

    public override int Write(nint buffer, int bufferoffset, int count, long fileoffset)
        => throw new NotImplementedException("Write operations not implemented in libqcow");
}
