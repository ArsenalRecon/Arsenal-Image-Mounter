
// '''' WriteFilterStatistics.vb
// '''' Statistics data from write filter driver.
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using Arsenal.ImageMounter.IO.Native;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO.Devices;

[StructLayout(LayoutKind.Sequential)]
public struct WriteFilterStatistics
{

    public WriteFilterStatistics()
    {
        Version = (uint)PinnedBuffer<WriteFilterStatistics>.TypeSize;
    }

    // '
    // ' Version of structure. Set to sizeof(AIMWRFLTR_DEVICE_STATISTICS)
    // '
    public uint Version { get; }

    public uint Flags { get; }

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
    public int LastErrorCode { get; }

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
    public int NextIdleTrimBlock { get; }

    // '
    // ' Number of read requests.
    // '
    public long ReadRequests { get; }

    // '
    // ' Total number of bytes for all read requests.
    // '
    public long ReadBytes { get; }

    // '
    // ' Largest requested read operation.
    // '
    public uint LargestReadSize { get; }

    // '
    // ' Number of read requests redirected to original device.
    // '
    public long ReadRequestsReroutedToOriginal { get; }

    // '
    // ' Total number of bytes for read requests redirected to
    // ' original device.
    // '
    public long ReadBytesReroutedToOriginal { get; }

    // '
    // ' Number of read requests split into smaller requests due
    // ' to fragmentation at diff device Or to fetch data from
    // ' both original device And diff device to fill a complete
    // ' request.
    // '
    public long SplitReads { get; }

    // '
    // ' Number of bytes read from original device in split requests.
    // '
    public long ReadBytesFromOriginal { get; }

    // '
    // ' Number of bytes read from diff device.
    // '
    public long ReadBytesFromDiff { get; }

    // '
    // ' Number of read requests deferred to worker thread due
    // to call at raised IRQL.
    // '
    public long DeferredReadRequests { get; }

    // '
    // ' Total number of bytes in DeferredReadRequests.
    // '
    public long DeferredReadBytes { get; }

    // '
    // ' Number of write requests.
    // '
    public long WriteRequests { get; }

    // '
    // ' Total number of bytes written.
    // '
    public long WrittenBytes { get; }

    // '
    // ' Largest requested write operation.
    // '
    public uint LargestWriteSize { get; }

    // '
    // ' Number of write requests split into smaller requests due
    // ' to fragmentation at diff device Or where parts of request
    // ' need to allocate New allocation blocks.
    // '
    public long SplitWrites { get; }

    // '
    // ' Number of write requests sent directly to diff device.
    // ' (All blocks already allocated in previous writes.)
    // '
    public long DirectWriteRequests { get; }

    // '
    // ' Total number of bytes in DirectWriteRequests.
    // '
    public long DirectWrittenBytes { get; }

    // '
    // ' Number of write requests deferred to worker thread due
    // ' to needs to allocate New blocks.
    // '
    public long DeferredWriteRequests { get; }

    // '
    // ' Total number of bytes in DeferredWriteRequests.
    // '
    public long DeferredWrittenBytes { get; }

    // '
    // ' Number of read requests issued to original device as
    // ' part of allocating New blocks at diff device. This is
    // ' done to fill up complete allocation blocks with both
    // ' data to write And padding with data from original device.
    // '
    public long FillReads { get; }

    // '
    // ' Total number of bytes read in FillReads requests.
    // '
    public long FillReadBytes { get; }

    // '
    // ' Number of TRIM requests sent from filesystem drivers above.
    // '
    public long TrimRequests { get; }

    // '
    // ' Total number of bytes for TRIM requests forwarded to diff
    // ' device.
    // '
    public long TrimBytesForwarded { get; }

    // '
    // ' Total number of bytes for TRIM requests ignored. This
    // ' happens when TRIM requests are received for areas Not yet
    // ' allocated at diff device. That is, Not yet written to.
    // '
    public long TrimBytesIgnored { get; }

    // '
    // ' Number of TRIM requests split due to fragmentation at diff
    // ' device.
    // '
    public long SplitTrims { get; }

    // '
    // ' Number of paging files, hibernation files And similar at
    // ' filtered device.
    // '
    public int PagingPathCount { get; }

    // '
    // ' Number of requests read from cache queue.
    // '
    public long ReadRequestsFromCache { get; }

    // '
    // ' Number of bytes read from cache queue.
    // '
    public long ReadBytesFromCache { get; }

    // '
    // ' Copy of diff device volume boot record. This structure holds
    // ' information about offset to private data/log data/etc.
    // '

    private unsafe fixed byte magic[16];        // ' Bytes 0xF4 0xEB followed by the String "AIMWriteFilter"

    public int MajorVersion { get; }    // ' will be increased if there's significant, backward incompatible changes in the format
    public int MinorVersion { get; }    // ' will be increased for each change that is backward compatible within the current MajorVersion

    // ' All sizes And offsets in 512 byte units.

    public long OffsetToPrivateData { get; }
    public long SizeOfPrivateData { get; }

    public long OffsetToLogData { get; }
    public long SizeOfLogData { get; }

    public long OffsetToAllocationTable { get; }
    public long SizeOfAllocationTable { get; }

    public long OffsetToFirstAllocatedBlock { get; }

    // '
    // ' Total size of protected volume in bytes.
    // '
    public long Size { get; }

    // '
    // ' Number of allocation blocks reserved at the beginning of
    // ' diff device for future use for saving allocation table
    // ' between reboots.
    // '
    public int AllocationTableBlocks { get; }

    // '
    // ' Last allocated block at diff device.
    // '
    public int LastAllocatedBlock { get; }

    // '
    // ' Number of bits in block size calculations.
    // '
    public byte DiffBlockBits { get; }

    private unsafe fixed byte unused[408];

    public ushort VbrSignature { get; }
}