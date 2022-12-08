//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int ioctl(SafeFileHandle handle, uint request, int parameter);

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe partial int ioctl(SafeFileHandle handle, uint request, void* parameter);

        [LibraryImport("c")]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int ioctl(SafeFileHandle handle, uint request, ref byte parameter);

#else

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ioctl(SafeFileHandle handle, uint request, int parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int ioctl(SafeFileHandle handle, uint request, void* parameter);

        [DllImport("c", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ioctl(SafeFileHandle handle, uint request, ref byte parameter);

#endif
    }

    public static unsafe long? GetDiskSize(SafeFileHandle safeFileHandle)
    {
        long size;
        return UnixAPI.ioctl(safeFileHandle, UnixAPI.BLKGETSIZE64, &size) == 0 &&
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
}
