using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.Reflection;
using Microsoft.Win32.SafeHandles;
using System;
// '''' DevioProviderLibQcow.vb
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

public class DevioProviderLibQcow : DevioProviderUnmanagedBase
{
    public event EventHandler? Finishing;

    public static readonly byte AccessFlagsRead = libqcow_get_access_flags_read();
    public static readonly byte AccessFlagsReadWrite = libqcow_get_access_flags_read_write();
    public static readonly byte AccessFlagsWrite = libqcow_get_access_flags_write();

    #region SafeHandles
    public sealed class SafeLibQcowFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {

        public SafeLibQcowFileHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {

            SetHandle(handle);
        }

        public SafeLibQcowFileHandle()
            : base(true)
        {

        }

        protected override bool ReleaseHandle() => libqcow_file_close(handle, out var argerrobj) >= 0;

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

        public SafeLibQcowErrorObjectHandle(IntPtr handle, bool ownsHandle)
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

            var errmsg = ArrayPool<char>.Shared.Rent(32000);

            try
            {
                if (libqcow_error_backtrace_sprint(this, errmsg, errmsg.Length) > 0)
                {
                    var msgs = new ReadOnlyMemory<char>(errmsg)
                        .ReadNullTerminatedUnicodeString()
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
                ArrayPool<char>.Shared.Return(errmsg);
            }
        }
    }
    #endregion

    public byte Flags { get; }

    public SafeLibQcowFileHandle SafeHandle { get; }

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern byte libqcow_get_access_flags_read();

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern byte libqcow_get_access_flags_read_write();

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern byte libqcow_get_access_flags_write();

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most likely analyzer bug")]
    private static extern int libqcow_notify_stream_open([In][MarshalAs(UnmanagedType.LPStr)] string filename, out SafeLibQcowErrorObjectHandle errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern void libqcow_notify_set_verbose(int Verbose);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern SafeLibQcowFileHandle libqcow_open_wide([In][MarshalAs(UnmanagedType.LPArray)] string[] filenames, int numberOfFiles, byte AccessFlags);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_initialize(out SafeLibQcowFileHandle handle, out SafeLibQcowErrorObjectHandle errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most likely analyzer bug")]
    private static extern int libqcow_file_open(SafeLibQcowFileHandle handle, [In][MarshalAs(UnmanagedType.LPUTF8Str)] string filename, int AccessFlags, out SafeLibQcowErrorObjectHandle errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_open_wide(SafeLibQcowFileHandle handle, [In][MarshalAs(UnmanagedType.LPWStr)] string filename, int AccessFlags, out SafeLibQcowErrorObjectHandle errobj);

    private delegate int f_libqcow_file_open(SafeLibQcowFileHandle handle, string filename, int AccessFlags, out SafeLibQcowErrorObjectHandle errobj);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_get_media_size(SafeLibQcowFileHandle handle, out long media_size);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_get_media_size(SafeLibQcowFileHandle handle, out long media_size, out SafeLibQcowErrorObjectHandle errobj);

    private enum Whence : int
    {
        Set = 0,
        Current = 1,
        End = 2
    }

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern long libqcow_file_seek_offset(SafeLibQcowFileHandle handle, long offset, Whence whence, out SafeLibQcowErrorObjectHandle errobj);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern IntPtr libqcow_read_random(SafeLibQcowFileHandle handle, IntPtr buffer, IntPtr buffer_size, long offset);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern IntPtr libqcow_file_read_buffer(SafeLibQcowFileHandle handle, IntPtr buffer, IntPtr buffer_size, out SafeLibQcowErrorObjectHandle errobj);

    [Obsolete]
    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_close(IntPtr handle);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_file_close(IntPtr handle, out SafeLibQcowErrorObjectHandle errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_notify_stream_close(out SafeLibQcowErrorObjectHandle errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_notify_set_stream(IntPtr FILE, out SafeLibQcowErrorObjectHandle errobj);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Ansi)]
    private static extern int libqcow_error_sprint(SafeLibQcowErrorObjectHandle errobj, char[] buffer, int length);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Ansi)]
    private static extern int libqcow_error_backtrace_sprint(SafeLibQcowErrorObjectHandle errobj, char[] buffer, int length);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_error_fprint(SafeLibQcowErrorObjectHandle errobj, IntPtr clibfile);

    [DllImport("libqcow", CallingConvention = CallingConvention.Cdecl, SetLastError = true, ThrowOnUnmappableChar = true)]
    private static extern int libqcow_error_free(ref IntPtr errobj);

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

        if (libqcow_file_initialize(out var safeHandle, out var errobj) != 1 || safeHandle.IsInvalid || Failed(errobj))
        {
            ThrowError(errobj, "Error initializing libqcow handle.");
        }

        SafeHandle = safeHandle;

        f_libqcow_file_open func;

        if (NativeLib.IsWindows)
        {
            func = libqcow_file_open_wide;
        }
        else
        {
            func = libqcow_file_open;
        }

        if (func(SafeHandle, filenames, Flags, out errobj) != 1 || Failed(errobj))
        {

            ThrowError(errobj, "Error opening image file(s)");

        }
    }

    protected static bool Failed(SafeLibQcowErrorObjectHandle errobj)
        => errobj is not null && !errobj.IsInvalid;

    protected static void ThrowError(SafeLibQcowErrorObjectHandle errobj, string message)
    {

        using (errobj)
        {

            var errmsg = errobj?.ToString();

            if (errmsg is not null)
            {
                throw new IOException($"{message}: {errmsg}");
            }
            else
            {
                throw new IOException(message);
            }
        }
    }

    public override bool CanWrite => (Flags & AccessFlagsWrite) == AccessFlagsWrite;

    public override long Length
    {
        get
        {
            var RC = libqcow_file_get_media_size(SafeHandle, out var LengthRet, out var errobj);
            if (RC < 0 || Failed(errobj))
            {
                ThrowError(errobj, "libqcow_file_get_media_size() failed");
            }

            return LengthRet;
        }
    }

    public int MaxIoSize { get; private set; } = int.MaxValue;

    public override uint SectorSize { get; } = 512U;

    public override int Read(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {

        var done_count = 0;

        while (done_count < count)
        {

            var offset = libqcow_file_seek_offset(SafeHandle, fileoffset, Whence.Set, out var errobj);

            if (offset != fileoffset || Failed(errobj))
            {

                ThrowError(errobj, $"Error seeking to position {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}");

            }

            var iteration_count = count - done_count;

            if (iteration_count > MaxIoSize)
            {

                iteration_count = MaxIoSize;

            }

            // Dim chunk_offset = CInt(fileoffset And (_ChunkSize - 1))

            // If chunk_offset + iteration_count > _ChunkSize Then

            // iteration_count = CInt(_ChunkSize) - chunk_offset

            // End If

            var result = libqcow_file_read_buffer(SafeHandle, buffer + bufferoffset, new IntPtr(iteration_count), out errobj).ToInt32();

            if (result < 0 || Failed(errobj))
            {

                ThrowError(errobj, $"Error reading {iteration_count} bytes from offset {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}");

            }

            if (result > 0)
            {

                done_count += result;
                fileoffset += result;
                bufferoffset += result;
            }

            else if (result == 0)
            {

                break;
            }

            else if (iteration_count >= SectorSize << 1)
            {

                errobj?.Dispose();

                MaxIoSize = iteration_count >> 1 & ~(int)(SectorSize - 1L);

                Trace.WriteLine($"Lowering MaxTransferSize to {MaxIoSize} bytes.");

                continue;
            }

            else
            {

                ThrowError(errobj, $"Error reading {iteration_count} bytes from offset {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}");

            }
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

    public override int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
        => throw new NotImplementedException("Write operations not implemented in libqcow");
}