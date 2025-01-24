//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Buffers;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable IDE0079 // Remove unnecessary suppression

namespace Arsenal.ImageMounter.IO.Native;

public static partial class NativeUnixIO
{
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

#else

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ioctl(SafeFileHandle handle, uint request, nint parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int ioctl(SafeFileHandle handle, uint request, void* parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ioctl(SafeFileHandle handle, uint request, ref byte parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Specified for parameters")]
        public static extern unsafe int sysctlbyname([MarshalAs(UnmanagedType.LPUTF8Str)] string name, ref byte oldp, ref nint oldtenp, in byte newp, nint newten);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern unsafe int getmntinfo(freebsd_statfs** mntbufp, int mode);

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
}
