''''' WriteFilterStatistics.vb
''''' Statistics data from write filter driver.
''''' 
''''' Copyright (c) 2012-2020, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Namespace IO
    <StructLayout(LayoutKind.Sequential)>
    Public Structure WriteFilterStatistics

        Public Sub Initialize()
            _Version = Marshal.SizeOf(Me)
        End Sub

        ''
        '' Version of structure. Set to sizeof(AIMWRFLTR_DEVICE_STATISTICS)
        ''
        Public ReadOnly Property Version As Integer

        ''
        '' TRUE if volume Is protected by filter driver, FALSE otherwise.
        ''
        Public ReadOnly Property IsProtected As Byte

        ''
        '' TRUE if all initialization Is complete for protection of this
        '' device
        ''
        Public ReadOnly Property Initialized As Byte

        ''
        '' Last NTSTATUS error code if failed to attach a diff device.
        ''
        Public ReadOnly Property LastErrorCode As Integer

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
        '' Value of AllocationTableBlocks converted to bytes instead
        '' of number of allocation blocks.
        ''
        Public ReadOnly Property AllocationTableSize As Long
            Get
                Return CLng(_AllocationTableBlocks) << _DiffBlockBits
            End Get
        End Property

        ''
        '' Last allocated block at diff device.
        ''
        Public ReadOnly Property LastAllocatedBlock As Integer

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
        '' Number of bits in block size calculations.
        ''
        Public ReadOnly Property DiffBlockBits As Byte

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
        '' receive a TRIM request while the filter driver Is idle.
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
        '' part of allocating New blocks at diff device. This Is
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
        '' allocated at diff device. That Is, Not yet written to.
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
        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=512)>
        Private ReadOnly DiffDeviceVbr As Byte()

    End Structure

End Namespace
