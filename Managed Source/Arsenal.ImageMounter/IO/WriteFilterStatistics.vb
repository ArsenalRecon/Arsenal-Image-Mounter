''''' WriteFilterStatistics.vb
''''' Statistics data from write filter driver.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http:''www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http:''ArsenalRecon.com/contact/
'''''

Imports System.Runtime.InteropServices

Namespace IO
    <StructLayout(LayoutKind.Sequential)>
    Public Structure WriteFilterStatistics

        Public Shared Function Initialize() As WriteFilterStatistics
            Return New WriteFilterStatistics With {
                ._Version = CUInt(PinnedBuffer(Of WriteFilterStatistics).TypeSize)
            }
        End Function

        ''
        '' Version of structure. Set to sizeof(AIMWRFLTR_DEVICE_STATISTICS)
        ''
        Public ReadOnly Property Version As UInteger

        Private ReadOnly Flags As UInteger

        ''
        '' TRUE if volume is protected by filter driver, FALSE otherwise.
        ''
        Public ReadOnly Property IsProtected As Boolean
            Get
                Return (Flags And &H1UI) = &H1UI
            End Get
        End Property

        ''
        '' TRUE if all initialization is complete for protection of this
        '' device
        ''
        Public ReadOnly Property Initialized As Boolean
            Get
                Return (Flags And &H100UI) = &H100UI
            End Get
        End Property

        ''
        '' TRUE if all IRP_MJ_FLUSH_BUFFERS requests are silently ignored
        '' And returned as successful by this filter driver. This is useful
        '' to gain performance in cases where the write overlay image is
        '' temporary And contents of it does Not need to be reliably
        '' maintained for another session.
        ''
        Public ReadOnly Property IgnoreFlushBuffers As Boolean
            Get
                Return (Flags And &H10000UI) = &H10000UI
            End Get
        End Property

        ''
        '' TRUE if filter driver reports non-removable storage device
        '' properties even if underlying physical disk reports removable
        '' media.
        ''
        Public ReadOnly Property FakeNonRemovable As Boolean
            Get
                Return (Flags And &H1000000UI) = &H1000000UI
            End Get
        End Property

        ''
        '' Last NTSTATUS error code if failed to attach a diff device.
        ''
        Public ReadOnly Property LastErrorCode As Integer

        ''
        '' Value of AllocationTableBlocks converted to bytes instead
        '' of number of allocation blocks.
        ''
        Public ReadOnly Property AllocationTableSize As Long
            Get
                Return CLng(_AllocationTableBlocks) << _DiffBlockBits
            End Get
        End Property

        ''
        '' Value of LastAllocatedBlock converted to bytes instead of
        '' number of allocation block. This gives the total number of
        '' bytes currently in use at diff device.
        ''
        Public ReadOnly Property UsedDiffSize As Long
            Get
                Return CLng(_LastAllocatedBlock) << _DiffBlockBits
            End Get
        End Property

        ''
        '' Calculates allocation block size.
        ''
        Public ReadOnly Property DiffBlockSize As Integer
            Get
                Return 1 << _DiffBlockBits
            End Get
        End Property

        ''
        '' Number of next allocation block at diff device that will
        '' receive a TRIM request while the filter driver is idle.
        ''
        Public ReadOnly Property NextIdleTrimBlock As Integer

        ''
        '' Number of read requests.
        ''
        Public ReadOnly Property ReadRequests As Long

        ''
        '' Total number of bytes for all read requests.
        ''
        Public ReadOnly Property ReadBytes As Long

        ''
        '' Largest requested read operation.
        ''
        Public ReadOnly Property LargestReadSize As UInteger

        ''
        '' Number of read requests redirected to original device.
        ''
        Public ReadOnly Property ReadRequestsReroutedToOriginal As Long

        ''
        '' Total number of bytes for read requests redirected to
        '' original device.
        ''
        Public ReadOnly Property ReadBytesReroutedToOriginal As Long

        ''
        '' Number of read requests split into smaller requests due
        '' to fragmentation at diff device Or to fetch data from
        '' both original device And diff device to fill a complete
        '' request.
        ''
        Public ReadOnly Property SplitReads As Long

        ''
        '' Number of bytes read from original device in split requests.
        ''
        Public ReadOnly Property ReadBytesFromOriginal As Long

        ''
        '' Number of bytes read from diff device.
        ''
        Public ReadOnly Property ReadBytesFromDiff As Long

        ''
        '' Number of read requests deferred to worker thread due
        ' to call at raised IRQL.
        ''
        Public ReadOnly Property DeferredReadRequests As Long

        ''
        '' Total number of bytes in DeferredReadRequests.
        ''
        Public ReadOnly Property DeferredReadBytes As Long

        ''
        '' Number of write requests.
        ''
        Public ReadOnly Property WriteRequests As Long

        ''
        '' Total number of bytes written.
        ''
        Public ReadOnly Property WrittenBytes As Long

        ''
        '' Largest requested write operation.
        ''
        Public ReadOnly Property LargestWriteSize As UInteger

        ''
        '' Number of write requests split into smaller requests due
        '' to fragmentation at diff device Or where parts of request
        '' need to allocate New allocation blocks.
        ''
        Public ReadOnly Property SplitWrites As Long

        ''
        '' Number of write requests sent directly to diff device.
        '' (All blocks already allocated in previous writes.)
        ''
        Public ReadOnly Property DirectWriteRequests As Long

        ''
        '' Total number of bytes in DirectWriteRequests.
        ''
        Public ReadOnly Property DirectWrittenBytes As Long

        ''
        '' Number of write requests deferred to worker thread due
        '' to needs to allocate New blocks.
        ''
        Public ReadOnly Property DeferredWriteRequests As Long

        ''
        '' Total number of bytes in DeferredWriteRequests.
        ''
        Public ReadOnly Property DeferredWrittenBytes As Long

        ''
        '' Number of read requests issued to original device as
        '' part of allocating New blocks at diff device. This is
        '' done to fill up complete allocation blocks with both
        '' data to write And padding with data from original device.
        ''
        Public ReadOnly Property FillReads As Long

        ''
        '' Total number of bytes read in FillReads requests.
        ''
        Public ReadOnly Property FillReadBytes As Long

        ''
        '' Number of TRIM requests sent from filesystem drivers above.
        ''
        Public ReadOnly Property TrimRequests As Long

        ''
        '' Total number of bytes for TRIM requests forwarded to diff
        '' device.
        ''
        Public ReadOnly Property TrimBytesForwarded As Long

        ''
        '' Total number of bytes for TRIM requests ignored. This
        '' happens when TRIM requests are received for areas Not yet
        '' allocated at diff device. That is, Not yet written to.
        ''
        Public ReadOnly Property TrimBytesIgnored As Long

        ''
        '' Number of TRIM requests split due to fragmentation at diff
        '' device.
        ''
        Public ReadOnly Property SplitTrims As Long

        ''
        '' Number of paging files, hibernation files And similar at
        '' filtered device.
        ''
        Public ReadOnly Property PagingPathCount As Integer

        ''
        '' Number of requests read from cache queue.
        ''
        Public ReadOnly Property ReadRequestsFromCache As Long

        ''
        '' Number of bytes read from cache queue.
        ''
        Public ReadOnly Property ReadBytesFromCache As Long

        ''
        '' Copy of diff device volume boot record. This structure holds
        '' information about offset to private data/log data/etc.
        ''

        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=16)>
        Private ReadOnly Magic As Byte()        '' Bytes 0xF4 0xEB followed by the String "AIMWriteFilter"

        Public ReadOnly Property MajorVersion As Integer    '' will be increased if there's significant, backward incompatible changes in the format
        Public ReadOnly Property MinorVersion As Integer    '' will be increased for each change that is backward compatible within the current MajorVersion

        '' All sizes And offsets in 512 byte units.

        Public ReadOnly Property OffsetToPrivateData As Long
        Public ReadOnly Property SizeOfPrivateData As Long

        Public ReadOnly Property OffsetToLogData As Long
        Public ReadOnly Property SizeOfLogData As Long

        Public ReadOnly Property OffsetToAllocationTable As Long
        Public ReadOnly Property SizeOfAllocationTable As Long

        Public ReadOnly Property OffsetToFirstAllocatedBlock As Long

        ''
        '' Total size of protected volume in bytes.
        ''
        Public ReadOnly Property Size As Long

        ''
        '' Number of allocation blocks reserved at the beginning of
        '' diff device for future use for saving allocation table
        '' between reboots.
        ''
        Public ReadOnly Property AllocationTableBlocks As Integer

        ''
        '' Last allocated block at diff device.
        ''
        Public ReadOnly Property LastAllocatedBlock As Integer

        ''
        '' Number of bits in block size calculations.
        ''
        Public ReadOnly Property DiffBlockBits As Byte

        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=408)>
        Private ReadOnly Unused As Byte()

        Public ReadOnly Property VbrSignature As UShort

    End Structure

End Namespace
