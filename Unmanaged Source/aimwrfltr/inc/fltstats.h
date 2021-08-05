
//
// AIMWrFltr
// fltstats.h - Kernel/User mode global definitions
//

#ifndef _NTDEF_
typedef LONG NTSTATUS, *PNTSTATUS;
#endif

//
// Basic name of diff device full event.
//

#define AIMWRFLTR_DIFF_FULL_EVENT_NAME L"AIMWrFltrDiffFullEvent"

//
// Path to diff device full event that can be used in calls to OpenEvent.
//

#define AIMWRFLTR_DIFF_FULL_EVENT_PATH L"Global\\" AIMWRFLTR_DIFF_FULL_EVENT_NAME

//
// Driver name and file path
//

#define AIMWRFLTR_SERVICE_NAME L"aimwrfltr"
#define AIMWRFLTR_SERVICE_PATH L"system32\\drivers\\" AIMWRFLTR_SERVICE_NAME L".sys"

#define IOCTL_AIMWRFLTR_BASE                    0x8844UL

//
// IOCTL_AIMWRFLTR_GET_DEVICE_DATA
//
// This IOCTL is used to request a copy of the AIMWRFLTR_DEVICE_STATISTICS object
// that the filter driver is currently using for a filtered device.
//
// Size of output buffer for this request need to be at least
// sizeof(AIMWRFLTR_DEVICE_STATISTICS).
//

#define IOCTL_AIMWRFLTR_GET_DEVICE_DATA         CTL_CODE(IOCTL_AIMWRFLTR_BASE, 0xD01UL, METHOD_BUFFERED, FILE_ANY_ACCESS)

//
// IOCTL_AIMWRFLTR_DELETE_ON_CLOSE
//
// Deletes the write overlay image file after use. Also sets this filter driver to
// silently ignore flush requests to improve performance when integrity of the write
// overlay image is not needed for future sessions.
//

#define IOCTL_AIMWRFLTR_DELETE_ON_CLOSE         CTL_CODE(IOCTL_AIMWRFLTR_BASE, 0xD01UL, METHOD_NEITHER, FILE_READ_ACCESS | FILE_WRITE_ACCESS)

//
// IOCTL_AIMWRFLTR_READ_PRIVATE_DATA
//
// This IOCTL is used to request data from private data block stored
// at diff volume.
//
// Input data: LONGLONG value that specifies offset in bytes within
// private data where data read should begin.
//
// Input data length: sizeof(LONGLONG) = 8
//
// Output data: Buffer large enough to receive the read data.
//
// Output data length: Number of bytes to read.
//
// Bytes returned: Upon return, the number of bytes actually read and
// placed in output buffer.
//

#define IOCTL_AIMWRFLTR_READ_PRIVATE_DATA       CTL_CODE(IOCTL_AIMWRFLTR_BASE, 0xD02UL, METHOD_OUT_DIRECT, FILE_READ_ACCESS)

//
// IOCTL_AIMWRFLTR_WRITE_PRIVATE_DATA
//
// This IOCTL is used to write data to private data block stored
// at diff volume. Note that data and length to write are passed as
// "output" to this IOCTL. "Input" buffer holds the byte offset where
// write should start.
//
// Input data: LONGLONG value that specifies offset in bytes within
// private data where data should be written.
//
// Input data length: sizeof(LONGLONG) = 8
//
// Output data: Buffer that holds data to be written.
//
// Output data length: Number of bytes to write from buffer.
//
// Bytes returned: Upon return, the number of bytes actually written.
//

#define IOCTL_AIMWRFLTR_WRITE_PRIVATE_DATA      CTL_CODE(IOCTL_AIMWRFLTR_BASE, 0xD03UL, METHOD_IN_DIRECT, FILE_READ_ACCESS | FILE_WRITE_ACCESS)

//
// IOCTL_AIMWRFLTR_READ_LOG_DATA
//
// This IOCTL is used to request data from log data block stored
// at diff volume.
//
// Input data: LONGLONG value that specifies offset in bytes within
// log data where data read should begin.
//
// Input data length: sizeof(LONGLONG) = 8
//
// Output data: Buffer large enough to receive the read data.
//
// Output data length: Number of bytes to read.
//
// Bytes returned: Upon return, the number of bytes actually read and
// placed in output buffer.
//

#define IOCTL_AIMWRFLTR_READ_LOG_DATA           CTL_CODE(IOCTL_AIMWRFLTR_BASE, 0xD04UL, METHOD_OUT_DIRECT, FILE_READ_ACCESS)

//
// IOCTL_AIMWRFLTR_WRITE_LOG_DATA
//
// This IOCTL is used to write data to log data block stored
// at diff volume. Note that data and length to write are passed as
// "output" to this IOCTL. "Input" buffer holds the byte offset where
// write should start.
//
// Input data: LONGLONG value that specifies offset in bytes within
// log data where data should be written.
//
// Input data length: sizeof(LONGLONG) = 8
//
// Output data: Buffer that holds data to be written.
//
// Output data length: Number of bytes to write from buffer.
//
// Bytes returned: Upon return, the number of bytes actually written.
//

#define IOCTL_AIMWRFLTR_WRITE_LOG_DATA          CTL_CODE(IOCTL_AIMWRFLTR_BASE, 0xD05UL, METHOD_IN_DIRECT, FILE_READ_ACCESS | FILE_WRITE_ACCESS)

//
// Fields at the beginning of diff volume 512 byte VBR
//

typedef struct _AIMWRFLTR_VBR_HEAD_FIELDS
{
    UCHAR Magic[16];        // Bytes 0xF4 0xEB followed by the string "AIMWriteFilter"

    ULONG MajorVersion;     // will be increased if there's significant, backward incompatible changes in the format
    ULONG MinorVersion;     // will be increased for each change that is backward compatible within the current MajorVersion

    // All sizes and offsets in 512 byte units.

    LONGLONG OffsetToPrivateData;
    LONGLONG SizeOfPrivateData;

    LONGLONG OffsetToLogData;
    LONGLONG SizeOfLogData;

    LONGLONG OffsetToAllocationTable;
    LONGLONG SizeOfAllocationTable;

    LONGLONG OffsetToFirstAllocatedBlock;

    //
    // Total size of protected volume in bytes.
    //
    LARGE_INTEGER Size;

    //
    // Number of allocation blocks reserved at the beginning of
    // diff device for future use for saving allocation table
    // between reboots.
    //
    LONG AllocationTableBlocks;

    //
    // Last allocated block at diff device.
    //
    LONG LastAllocatedBlock;

    //
    // Number of bits in block size calculations.
    //
    UCHAR DiffBlockBits;

} AIMWRFLTR_VBR_HEAD_FIELDS, *PAIMWRFLTR_VBR_HEAD_FIELDS;

//
// Fields at the end of diff volume 512 byte VBR
//

typedef union _AIMWRFLTR_VBR_FOOT_FIELDS
{
    UCHAR Bytes[2];

    USHORT VbrSignature;

} AIMWRFLTR_VBR_FOOT_FIELDS, *PAIMWRFLTR_VBR_FOOT_FIELDS;

//
// Fields at the end of diff volume 512 byte VBR
//

typedef struct _AIMWRFLTR_VBR_RAW
{
    UCHAR Bytes[512];

} AIMWRFLTR_VBR_RAW, *PAIMWRFLTR_VBR_RAW;

//
// Complete diff volume 512 byte VBR
//

typedef union _AIMWRFLTR_VBR
{
    AIMWRFLTR_VBR_RAW Raw;

    struct
    {
        AIMWRFLTR_VBR_HEAD_FIELDS Head;

        CHAR NotUsed[sizeof(AIMWRFLTR_VBR_RAW) -
            sizeof(AIMWRFLTR_VBR_HEAD_FIELDS) -
            sizeof(AIMWRFLTR_VBR_FOOT_FIELDS)];

        AIMWRFLTR_VBR_FOOT_FIELDS Foot;

    } Fields;

} AIMWRFLTR_VBR, *PAIMWRFLTR_VBR;

//
// Device statistics
//

typedef struct _AIMWRFLTR_DEVICE_STATISTICS
{
    //
    // Version of structure. Set to sizeof(AIMWRFLTR_DEVICE_STATISTICS)
    //
    ULONG Version;

    //
    // TRUE if volume is protected by filter driver, FALSE otherwise.
    //
    ULONG IsProtected : 1;

    ULONG Reserved1 : 7;

    //
    // TRUE if all initialization is complete for protection of this
    // device
    //
    ULONG Initialized : 1;

    ULONG Reserved2 : 7;

    //
    // TRUE if all IRP_MJ_FLUSH_BUFFERS requests are silently ignored
    // and returned as successful by this filter driver. This is useful
    // to gain performance in cases where the write overlay image is
    // temporary and contents of it does not need to be reliably
    // maintained for another session.
    //
    ULONG IgnoreFlushBuffers : 1;

    ULONG Reserved3 : 7;

    //
    // TRUE if filter driver reports non-removable storage device
    // properties even if underlying physical disk reports removable
    // media.
    //
    ULONG FakeNonRemovable : 1;

    ULONG Reserved4 : 7;

    //
    // Last NTSTATUS error code if failed to attach a diff device.
    //
    NTSTATUS LastErrorCode;

#ifdef __cplusplus
    //
    // Value of AllocationTableBlocks converted to bytes instead
    // of number of allocation blocks.
    //
    LONGLONG AllocationTableSize() const
    {
        return (LONGLONG)DiffDeviceVbr.Fields.Head.AllocationTableBlocks <<
            DiffDeviceVbr.Fields.Head.DiffBlockBits;
    }
#endif

#ifdef __cplusplus
    //
    // Value of LastAllocatedBlock converted to bytes instead of
    // number of allocation block. This gives the total number of
    // bytes currently in use at diff device.
    //
    LONGLONG UsedDiffSize() const
    {
        return (LONGLONG)DiffDeviceVbr.Fields.Head.LastAllocatedBlock <<
            DiffDeviceVbr.Fields.Head.DiffBlockBits;
    }
#endif

#ifdef __cplusplus
    //
    // Calculates allocation block size.
    //
    LONG DiffBlockSize() const
    {
        return 1L << DiffDeviceVbr.Fields.Head.DiffBlockBits;
    }
#endif

    //
    // Number of next allocation block at diff device that will
    // receive a TRIM request while the filter driver is idle.
    //
    LONG NextIdleTrimBlock;

    //
    // Number of read requests.
    //
    LONGLONG ReadRequests;

    //
    // Total number of bytes for all read requests.
    //
    LONGLONG ReadBytes;

    //
    // Largest requested read operation.
    //
    ULONG LargestReadSize;

    //
    // Number of read requests redirected to original device.
    //
    LONGLONG ReadRequestsReroutedToOriginal;

    //
    // Total number of bytes for read requests redirected to
    // original device.
    //
    LONGLONG ReadBytesReroutedToOriginal;

    //
    // Number of read requests split into smaller requests due
    // to fragmentation at diff device or to fetch data from
    // both original device and diff device to fill a complete
    // request.
    //
    LONGLONG SplitReads;

    //
    // Number of bytes read from original device in split requests.
    //
    LONGLONG ReadBytesFromOriginal;

    //
    // Number of bytes read from diff device.
    //
    LONGLONG ReadBytesFromDiff;

    //
    // Number of read requests deferred to worker thread due
    // to call at raised IRQL.
    //
    LONGLONG DeferredReadRequests;

    //
    // Total number of bytes in DeferredReadRequests.
    //
    LONGLONG DeferredReadBytes;

    //
    // Number of write requests.
    //
    LONGLONG WriteRequests;

    //
    // Total number of bytes written.
    //
    LONGLONG WrittenBytes;

    //
    // Largest requested write operation.
    //
    ULONG LargestWriteSize;

    //
    // Number of write requests split into smaller requests due
    // to fragmentation at diff device or where parts of request
    // need to allocate new allocation blocks.
    //
    LONGLONG SplitWrites;

    //
    // Number of write requests sent directly to diff device.
    // (All blocks already allocated in previous writes.)
    //
    LONGLONG DirectWriteRequests;

    //
    // Total number of bytes in DirectWriteRequests.
    //
    LONGLONG DirectWrittenBytes;

    //
    // Number of write requests deferred to worker thread due
    // to needs to allocate new blocks or called at raised
    // IRQL.
    //
    LONGLONG DeferredWriteRequests;

    //
    // Total number of bytes in DeferredWriteRequests.
    //
    LONGLONG DeferredWrittenBytes;

    //
    // Number of read requests issued to original device as
    // part of allocating new blocks at diff device. This is
    // done to fill up complete allocation blocks with both
    // data to write and padding with data from original device.
    //
    LONGLONG FillReads;

    //
    // Total number of bytes read in FillReads requests.
    //
    LONGLONG FillReadBytes;

    //
    // Number of TRIM requests sent from filesystem drivers above.
    //
    LONGLONG TrimRequests;

    //
    // Total number of bytes for TRIM requests forwarded to diff
    // device.
    //
    LONGLONG TrimBytesForwarded;

    //
    // Total number of bytes for TRIM requests ignored. This
    // happens when TRIM requests are received for areas not yet
    // allocated at diff device. That is, not yet written to.
    //
    LONGLONG TrimBytesIgnored;

    //
    // Number of TRIM requests split due to fragmentation at diff
    // device.
    //
    LONGLONG SplitTrims;

    //
    // Number of paging files, hibernation files and similar at
    // filtered device.
    //
    LONG PagingPathCount;

    //
    // Number of requests read from cache queue.
    //
    LONGLONG ReadRequestsFromCache;

    //
    // Number of bytes read from cache queue.
    //
    LONGLONG ReadBytesFromCache;

    //
    // Copy of diff device volume boot record. This structure holds
    // information about offset to private data/log data/etc.
    //
    AIMWRFLTR_VBR DiffDeviceVbr;

} AIMWRFLTR_DEVICE_STATISTICS, *PAIMWRFLTR_DEVICE_STATISTICS;

//
// Value of AllocationTableBlocks converted to bytes instead
// of number of allocation blocks.
//
FORCEINLINE
LONGLONG AIMWrFltrStatisticsAllocationTableSize(PAIMWRFLTR_DEVICE_STATISTICS DeviceStatistics)
{
    return (LONGLONG)
        DeviceStatistics->DiffDeviceVbr.Fields.Head.AllocationTableBlocks <<
        DeviceStatistics->DiffDeviceVbr.Fields.Head.DiffBlockBits;
}

//
// Value of LastAllocatedBlock converted to bytes instead of
// number of allocation block. This gives the total number of
// bytes currently in use at diff device.
//
FORCEINLINE
LONGLONG AIMWrFltrStatisticsUsedDiffSize(PAIMWRFLTR_DEVICE_STATISTICS DeviceStatistics)
{
    return (LONGLONG)
        DeviceStatistics->DiffDeviceVbr.Fields.Head.LastAllocatedBlock <<
        DeviceStatistics->DiffDeviceVbr.Fields.Head.DiffBlockBits;
}

//
// Calculates allocation block size.
//
FORCEINLINE
LONG AIMWrFltrStatisticsDiffBlockSize(PAIMWRFLTR_DEVICE_STATISTICS DeviceStatistics)
{
    return 1L << DeviceStatistics->DiffDeviceVbr.Fields.Head.DiffBlockBits;
}

