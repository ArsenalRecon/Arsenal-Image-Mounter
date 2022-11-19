
// '''' WriteFilterStatistics.vb
// '''' Statistics data from write filter driver.
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http:''www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http:''ArsenalRecon.com/contact/
// ''''

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arsenal.ImageMounter.IO.Native;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO.Devices;

[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public struct WriteFilterStatistics
{

    public static WriteFilterStatistics GetNew()
        => new()
        {
            Version = (uint)PinnedBuffer<WriteFilterStatistics>.TypeSize
        };

    // '
    // ' Version of structure. Set to sizeof(AIMWRFLTR_DEVICE_STATISTICS)
    // '
    public uint Version { get; private set; }

    private readonly uint Flags;

    // '
    // ' TRUE if volume is protected by filter driver, FALSE otherwise.
    // '
    public bool IsProtected => (Flags & 0x1U) == 0x1U;

    // '
    // ' TRUE if all initialization is complete for protection of this
    // ' device
    // '
    public bool Initialized => (Flags & 0x100U) == 0x100U;

    // '
    // ' TRUE if all IRP_MJ_FLUSH_BUFFERS requests are silently ignored
    // ' And returned as successful by this filter driver. This is useful
    // ' to gain performance in cases where the write overlay image is
    // ' temporary And contents of it does Not need to be reliably
    // ' maintained for another session.
    // '
    public bool IgnoreFlushBuffers => (Flags & 0x10000U) == 0x10000U;

    // '
    // ' TRUE if filter driver reports non-removable storage device
    // ' properties even if underlying physical disk reports removable
    // ' media.
    // '
    public bool FakeNonRemovable => (Flags & 0x1000000U) == 0x1000000U;

    // '
    // ' Last NTSTATUS error code if failed to attach a diff device.
    // '
    public int LastErrorCode { get; private set; }

    // '
    // ' Value of AllocationTableBlocks converted to bytes instead
    // ' of number of allocation blocks.
    // '
    public long AllocationTableSize => (long)AllocationTableBlocks << DiffBlockBits;

    // '
    // ' Value of LastAllocatedBlock converted to bytes instead of
    // ' number of allocation block. This gives the total number of
    // ' bytes currently in use at diff device.
    // '
    public long UsedDiffSize => (long)LastAllocatedBlock << DiffBlockBits;

    // '
    // ' Calculates allocation block size.
    // '
    public int DiffBlockSize => 1 << DiffBlockBits;

    // '
    // ' Number of next allocation block at diff device that will
    // ' receive a TRIM request while the filter driver is idle.
    // '
    public int NextIdleTrimBlock { get; private set; }

    // '
    // ' Number of read requests.
    // '
    public long ReadRequests { get; private set; }

    // '
    // ' Total number of bytes for all read requests.
    // '
    public long ReadBytes { get; private set; }

    // '
    // ' Largest requested read operation.
    // '
    public uint LargestReadSize { get; private set; }

    // '
    // ' Number of read requests redirected to original device.
    // '
    public long ReadRequestsReroutedToOriginal { get; private set; }

    // '
    // ' Total number of bytes for read requests redirected to
    // ' original device.
    // '
    public long ReadBytesReroutedToOriginal { get; private set; }

    // '
    // ' Number of read requests split into smaller requests due
    // ' to fragmentation at diff device Or to fetch data from
    // ' both original device And diff device to fill a complete
    // ' request.
    // '
    public long SplitReads { get; private set; }

    // '
    // ' Number of bytes read from original device in split requests.
    // '
    public long ReadBytesFromOriginal { get; private set; }

    // '
    // ' Number of bytes read from diff device.
    // '
    public long ReadBytesFromDiff { get; private set; }

    // '
    // ' Number of read requests deferred to worker thread due
    // to call at raised IRQL.
    // '
    public long DeferredReadRequests { get; private set; }

    // '
    // ' Total number of bytes in DeferredReadRequests.
    // '
    public long DeferredReadBytes { get; private set; }

    // '
    // ' Number of write requests.
    // '
    public long WriteRequests { get; private set; }

    // '
    // ' Total number of bytes written.
    // '
    public long WrittenBytes { get; private set; }

    // '
    // ' Largest requested write operation.
    // '
    public uint LargestWriteSize { get; private set; }

    // '
    // ' Number of write requests split into smaller requests due
    // ' to fragmentation at diff device Or where parts of request
    // ' need to allocate New allocation blocks.
    // '
    public long SplitWrites { get; private set; }

    // '
    // ' Number of write requests sent directly to diff device.
    // ' (All blocks already allocated in previous writes.)
    // '
    public long DirectWriteRequests { get; private set; }

    // '
    // ' Total number of bytes in DirectWriteRequests.
    // '
    public long DirectWrittenBytes { get; private set; }

    // '
    // ' Number of write requests deferred to worker thread due
    // ' to needs to allocate New blocks.
    // '
    public long DeferredWriteRequests { get; private set; }

    // '
    // ' Total number of bytes in DeferredWriteRequests.
    // '
    public long DeferredWrittenBytes { get; private set; }

    // '
    // ' Number of read requests issued to original device as
    // ' part of allocating New blocks at diff device. This is
    // ' done to fill up complete allocation blocks with both
    // ' data to write And padding with data from original device.
    // '
    public long FillReads { get; private set; }

    // '
    // ' Total number of bytes read in FillReads requests.
    // '
    public long FillReadBytes { get; private set; }

    // '
    // ' Number of TRIM requests sent from filesystem drivers above.
    // '
    public long TrimRequests { get; private set; }

    // '
    // ' Total number of bytes for TRIM requests forwarded to diff
    // ' device.
    // '
    public long TrimBytesForwarded { get; private set; }

    // '
    // ' Total number of bytes for TRIM requests ignored. This
    // ' happens when TRIM requests are received for areas Not yet
    // ' allocated at diff device. That is, Not yet written to.
    // '
    public long TrimBytesIgnored { get; private set; }

    // '
    // ' Number of TRIM requests split due to fragmentation at diff
    // ' device.
    // '
    public long SplitTrims { get; private set; }

    // '
    // ' Number of paging files, hibernation files And similar at
    // ' filtered device.
    // '
    public int PagingPathCount { get; private set; }

    // '
    // ' Number of requests read from cache queue.
    // '
    public long ReadRequestsFromCache { get; private set; }

    // '
    // ' Number of bytes read from cache queue.
    // '
    public long ReadBytesFromCache { get; private set; }

    // '
    // ' Copy of diff device volume boot record. This structure holds
    // ' information about offset to private data/log data/etc.
    // '

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    private readonly byte[] Magic;        // ' Bytes 0xF4 0xEB followed by the String "AIMWriteFilter"

    public int MajorVersion { get; private set; }    // ' will be increased if there's significant, backward incompatible changes in the format
    public int MinorVersion { get; private set; }    // ' will be increased for each change that is backward compatible within the current MajorVersion

    // ' All sizes And offsets in 512 byte units.

    public long OffsetToPrivateData { get; private set; }
    public long SizeOfPrivateData { get; private set; }

    public long OffsetToLogData { get; private set; }
    public long SizeOfLogData { get; private set; }

    public long OffsetToAllocationTable { get; private set; }
    public long SizeOfAllocationTable { get; private set; }

    public long OffsetToFirstAllocatedBlock { get; private set; }

    // '
    // ' Total size of protected volume in bytes.
    // '
    public long Size { get; private set; }

    // '
    // ' Number of allocation blocks reserved at the beginning of
    // ' diff device for future use for saving allocation table
    // ' between reboots.
    // '
    public int AllocationTableBlocks { get; private set; }

    // '
    // ' Last allocated block at diff device.
    // '
    public int LastAllocatedBlock { get; private set; }

    // '
    // ' Number of bits in block size calculations.
    // '
    public byte DiffBlockBits { get; private set; }

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 408)]
    private readonly byte[] Unused;

    public ushort VbrSignature { get; private set; }
}