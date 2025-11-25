//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using DiscUtils.Streams;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator
#pragma warning disable IDE0270 // Use coalesce expression

namespace Arsenal.ImageMounter.IO.Native;

public static partial class NativeUnixIO
{
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Specified for parameters")]
    private static partial class UnixAPI
    {
        public const uint BLKGETSIZE64 = 0x80081272;
        public const uint DIOCGSECTORSIZE = 0x40046480;
        public const uint DIOCGMEDIASIZE = 0x40086481;
        public const uint DIOCGFWSECTORS = 0x40046482;
        public const uint DIOCGFWHEADS = 0x40046483;

#if NET7_0_OR_GREATER

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
        public static partial int ioctl(SafeFileHandle handle, uint request, nint parameter);

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
        public static unsafe partial int ioctl(SafeFileHandle handle, uint request, void* parameter);

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
        public static partial int ioctl(SafeFileHandle handle, uint request, ref byte parameter);

        [LibraryImport("c", SetLastError = true)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe partial int sysctlbyname([MarshalAs(UnmanagedType.LPUTF8Str)] string name, ref byte oldp, ref nint oldtenp, in byte newp, nint newten);

        [LibraryImport("c", SetLastError = true)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe partial int getmntinfo(freebsd_statfs** mntbufp, int mode);

        [LibraryImport("c", SetLastError = true)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial nint readlink([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out byte buf, nint bufsiz);

        [LibraryImport("c", SetLastError = true)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe partial byte* realpath([MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, nint buffer = 0);

        [LibraryImport("c", SetLastError = true)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe partial nint strlen(void* s);

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe partial void free(void* mem);

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe partial int unmount([MarshalAs(UnmanagedType.LPUTF8Str)] string dir, int flags);

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe partial int mount([MarshalAs(UnmanagedType.LPUTF8Str)] string type, [MarshalAs(UnmanagedType.LPUTF8Str)] string dir, int flags, void* data);

#else

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ioctl(SafeFileHandle handle, uint request, nint parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int ioctl(SafeFileHandle handle, uint request, void* parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ioctl(SafeFileHandle handle, uint request, ref byte parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern unsafe int sysctlbyname([MarshalAs(UnmanagedType.LPUTF8Str)] string name, ref byte oldp, ref nint oldtenp, in byte newp, nint newten);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern unsafe int getmntinfo(freebsd_statfs** mntbufp, int mode);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern nint readlink([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out byte buf, nint bufsiz);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern unsafe byte* realpath([MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, nint buffer = 0);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern unsafe nint strlen(void* s);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void free(void* mem);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int unmount([MarshalAs(UnmanagedType.LPUTF8Str)] string dir, int flags);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int mount([MarshalAs(UnmanagedType.LPUTF8Str)] string type, [MarshalAs(UnmanagedType.LPUTF8Str)] string dir, int flags, void* data);

#endif
    }

    public static unsafe long? GetDiskSize(SafeFileHandle safeFileHandle)
    {
        long size;
        return UnixAPI.ioctl(safeFileHandle, UnixAPI.BLKGETSIZE64, &size) == 0 ||
            UnixAPI.ioctl(safeFileHandle, UnixAPI.DIOCGMEDIASIZE, &size) == 0
            ? size
            : null;
    }

    public static unsafe DISK_GEOMETRY? GetDiskGeometry(SafeFileHandle safeFileHandle)
    {
        int sectorSize;
        int numberOfSectors;
        int numberOfHeads;

        return UnixAPI.ioctl(safeFileHandle, UnixAPI.DIOCGSECTORSIZE, &sectorSize) == 0 &&
            UnixAPI.ioctl(safeFileHandle, UnixAPI.DIOCGFWSECTORS, &numberOfSectors) == 0 &&
            UnixAPI.ioctl(safeFileHandle, UnixAPI.DIOCGFWHEADS, &numberOfHeads) == 0
            ? new DISK_GEOMETRY(numberOfHeads, DISK_GEOMETRY.MEDIA_TYPE.FixedMedia, 255, 63, sectorSize)
            : null;
    }

    public static bool GetSysCtlByName(string name, Span<byte> buffer, out nint length)
    {
        length = buffer.Length;

        if (UnixAPI.sysctlbyname(name, ref buffer[0], ref length, Unsafe.NullRef<byte>(), 0) == 0)
        {
            return true;
        }

        if (Marshal.GetLastWin32Error() == NativeConstants.ENOMEM)
        {
            return false;
        }

        length = 0;

        return false;
    }

    public static unsafe ReadOnlySpan<freebsd_statfs> GetMntInfo(bool wait)
    {
        freebsd_statfs* bufp = null;
        var count = UnixAPI.getmntinfo(&bufp, wait ? 1 : 2);

        if (count == 0)
        {
            return default;
        }

        var array = new ReadOnlySpan<freebsd_statfs>(bufp, count);

        return array;
    }

    public static string ReadLink(string path)
    {
        Span<byte> buf = stackalloc byte[1024];
        var size = UnixAPI.readlink(path, out buf[0], buf.Length);

        if (size == -1)
        {
            throw new Win32Exception();
        }

        return Encoding.UTF8.GetString(buf.Slice(0, (int)size));
    }

    public static unsafe string RealPath(string path)
    {
        var buffer = UnixAPI.realpath(path, buffer: 0);

        if (buffer is null)
        {
            throw new Win32Exception();
        }

#if NET6_0_OR_GREATER
        var target = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(buffer));
        NativeMemory.Free(buffer);
#elif NETCOREAPP || NETSTANDARD
        var target = Marshal.PtrToStringUTF8((nint)buffer);
        UnixAPI.free(buffer);
#else
        var target = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(buffer, (int)UnixAPI.strlen(buffer)));
        UnixAPI.free(buffer);
#endif

        return target;
    }

    public static unsafe bool Unmount(string path) => UnixAPI.unmount(path, 0) == 0;
}
