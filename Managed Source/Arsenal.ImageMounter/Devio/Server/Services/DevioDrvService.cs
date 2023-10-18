//  DevioShmService.vb
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Collections;
using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils.Streams;
using LTRData.Extensions.Buffers;
using LTRData.Extensions.Formatting;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Threading;
using static Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.Services;

/// <summary>
/// Class that implements server end of Devio shared memory based communication
/// protocol. It uses an object implementing <see>IDevioProvider</see> interface as
/// storage backend for I/O requests received from client.
/// </summary>
[SupportedOSPlatform("windows")]
public partial class DevioDrvService : DevioServiceBase
{
    /// <summary>
    /// Object name of shared memory file mapping object created by this instance.
    /// </summary>
    public string ObjectName { get; }

    /// <summary>
    /// Size of the initial memory blocks that is shared between driver and this service.
    /// This will automatically be increased when needed.
    /// </summary>
    private readonly long initialBufferSize;

    /// <summary>
    /// Initial buffer size that will be automatically selected on this platform when
    /// an instance is created by a constructor without a InitialBufferSize argument.
    /// 
    /// Buffer sizes will be automatically increased when needed.
    /// </summary>
    public const long DefaultInitialBufferSize = (64 << 10) + IMDPROXY_HEADER_SIZE;

    private static Guid GetNextRandomValue() => NativeCalls.GenRandomGuid();

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication.
    /// </summary>
    /// <param name="objectName">Object name of shared memory file mapping object created by this instance.</param>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    /// <param name="initialBufferSize">Initial buffer size to use for shared memory I/O communication between driver and this service.
    /// This will be automatically increased later if needed.</param>
    public DevioDrvService(string objectName, IDevioProvider devioProvider, bool ownsProvider, long initialBufferSize)
        : base(devioProvider, ownsProvider)
    {
        ObjectName = objectName;
        this.initialBufferSize = initialBufferSize;
    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A default buffer size will be used.
    /// </summary>
    /// <param name="objectName">Object name of shared memory file mapping object created by this instance.</param>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioDrvService(string objectName, IDevioProvider devioProvider, bool ownsProvider)
        : this(objectName, devioProvider, ownsProvider, DefaultInitialBufferSize)
    {
    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A default buffer size and a random object name will be used.
    /// </summary>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioDrvService(IDevioProvider devioProvider, bool ownsProvider)
        : this(devioProvider, ownsProvider, DefaultInitialBufferSize)
    {
    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A random object name will be used.
    /// </summary>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    /// <param name="initialBufferSize">Initial buffer size to use for shared memory I/O communication between driver and this service.
    /// This will be automatically increased later if needed.</param>
    public DevioDrvService(IDevioProvider devioProvider, bool ownsProvider, long initialBufferSize)
        : this(GetNextRandomValue().ToString(), devioProvider, ownsProvider, initialBufferSize)
    {
    }

    internal static unsafe partial class DevioDrvServiceInterop
    {
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeviceIoControl(SafeFileHandle hDevice,
                                                   uint dwIoControlCode,
                                                   void* lpBufferId,
                                                   int nBufferIdSize,
                                                   void* lpDataBuffer,
                                                   uint nDataBufferSize,
                                                   uint* lpBytesReturned,
                                                   NativeOverlapped* lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetOverlappedResult(SafeFileHandle hDevice,
                                                       NativeOverlapped* lpOverlapped,
                                                       out uint lpNumberOfBytesTransferred,
                                                       [MarshalAs(UnmanagedType.Bool)] bool bWait);


        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CancelIo(SafeFileHandle device);
#else
        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice,
                                                    uint dwIoControlCode,
                                                    void* lpBufferId,
                                                    int nBufferIdSize,
                                                    void* lpDataBuffer,
                                                    uint nDataBufferSize,
                                                    uint* lpBytesReturned,
                                                    NativeOverlapped* lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetOverlappedResult(SafeFileHandle hDevice,
                                                        NativeOverlapped* lpOverlapped,
                                                        out uint lpNumberOfBytesTransferred,
                                                        [MarshalAs(UnmanagedType.Bool)] bool bWait);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CancelIo(SafeFileHandle device);
#endif
    }

    private unsafe void StartNewIoExchange()
    {
        const uint IOCTL_DEVIODRV_LOCK_MEMORY = unchecked((uint)(((0x8372) << 16) | (((0x0001) | (0x0002)) << 14) | ((0x8D1) << 2) | (2)));
        const uint IOCTL_DEVIODRV_EXCHANGE_IO = unchecked((uint)(((0x8372) << 16) | (((0x0001) | (0x0002)) << 14) | ((0x8D0) << 2) | (2)));

        device = device ?? throw new InvalidOperationException();

        MemoryMappedViewAccessor mapView = null!;

        long bufferSize;

        EventWaitHandle requestEvent = null!;

        EventWaitHandle memoryEvent = null!;

        HGlobalBuffer memoryOverlappedMem = null!;

        HGlobalBuffer requestOverlappedMem = null!;

        RegisteredWaitHandle registeredHandle = null!;

        var serviceNumber = Interlocked.Increment(ref ioExchangeBufferCounter);

        closedEvent.Reset();

        Trace.WriteLine($"Starting I/O exchange number {serviceNumber}...");

        bufferSize = initialBufferSize;

        nint bufferId;

        var initializationDone = serviceNumber == 1
            ? new ManualResetEventSlim(initialState: false)
            : null;

        try
        {
            using var mapping = MemoryMappedFile.CreateNew(null,
                                                           bufferSize,
                                                           MemoryMappedFileAccess.ReadWrite,
                                                           MemoryMappedFileOptions.None,
                                                           HandleInheritability.None);

            mapView = mapping.CreateViewAccessor();

            bufferId = mapView.SafeMemoryMappedViewHandle.DangerousGetHandle();

            var maxTransferSize = (int)(mapView.Capacity - IMDPROXY_HEADER_SIZE);

            Trace.WriteLine($"Service number {serviceNumber}: Created shared memory object, {maxTransferSize} bytes.");

            memoryEvent = new ManualResetEvent(initialState: false);

            memoryOverlappedMem = new HGlobalBuffer(sizeof(NativeOverlapped));

            requestEvent = new AutoResetEvent(initialState: false);

            requestOverlappedMem = new HGlobalBuffer(sizeof(NativeOverlapped));

            var memoryOverlapped = (NativeOverlapped*)memoryOverlappedMem.DangerousGetHandle();

            memoryOverlapped->EventHandle = memoryEvent.SafeWaitHandle.DangerousGetHandle();

            var requestOverlapped = (NativeOverlapped*)requestOverlappedMem.DangerousGetHandle();

            requestOverlapped->EventHandle = requestEvent.SafeWaitHandle.DangerousGetHandle();

            if (!DevioDrvServiceInterop.DeviceIoControl(device,
                                                        IOCTL_DEVIODRV_LOCK_MEMORY,
                                                        &bufferId,
                                                        sizeof(void*),
                                                        (void*)bufferId,
                                                        (uint)mapView.Capacity,
                                                        null,
                                                        memoryOverlapped)
                && Marshal.GetLastWin32Error() != NativeConstants.ERROR_IO_PENDING)
            {
                throw new IOException($"Service number {serviceNumber}: Lock memory request failed", new Win32Exception());
            }

            if (serviceNumber == 1)
            {
                mapView.SafeMemoryMappedViewHandle.Write(0, IMDPROXY_REQ.IMDPROXY_REQ_INFO);

                SendInfo(mapView.SafeMemoryMappedViewHandle);
            }
            else
            {
                mapView.SafeMemoryMappedViewHandle.Write(0, IMDPROXY_REQ.IMDPROXY_REQ_NULL);
            }

            var initializedWaitEvent = initializationDone;

            registeredHandle = ThreadPool.RegisterWaitForSingleObject(requestEvent,
                                                                      IoComplete,
                                                                      null,
                                                                      Timeout.Infinite,
                                                                      executeOnlyOnce: false);

            if (!DevioDrvServiceInterop.DeviceIoControl(device,
                                                        IOCTL_DEVIODRV_EXCHANGE_IO,
                                                        &bufferId,
                                                        sizeof(void*),
                                                        null,
                                                        0,
                                                        null,
                                                        requestOverlapped)
                && Marshal.GetLastWin32Error() != NativeConstants.ERROR_IO_PENDING)
            {
                if (initializedWaitEvent is not null)
                {
                    initializedWaitEvent.Wait();

                    if (registeredHandle is null)
                    {
                        throw new IOException($"Service number {serviceNumber}: I/O exchange failed", new Win32Exception());
                    }
                }
            }

            Trace.WriteLine($"Service number {serviceNumber}: I/O exchange buffer initialized, waiting for request.");
        }
        catch
        {
            CleanupIoExchange();

            throw;
        }
        
        void IoComplete(object? state, bool timeout)
        {
            var concurrentCount = Interlocked.Increment(ref concurrentRequestsCounter);

            try
            {
                if (device.IsClosed)
                {
                    Trace.WriteLine($"Service number {serviceNumber}: Device closed by server, shutting down.");

                    CleanupIoExchange();

                    return;
                }

                var memoryOverlapped = (NativeOverlapped*)memoryOverlappedMem.DangerousGetHandle();

                var requestOverlapped = (NativeOverlapped*)requestOverlappedMem.DangerousGetHandle();

                var bufferId = mapView.SafeMemoryMappedViewHandle.DangerousGetHandle();

                var err = NativeConstants.NO_ERROR;

                if (!DevioDrvServiceInterop.GetOverlappedResult(device, requestOverlapped, out _, bWait: true))
                {
                    err = Marshal.GetLastWin32Error();
                }

                for (; ; )
                {
                    if (err == NativeConstants.ERROR_DEV_NOT_EXIST)
                    {
                        Trace.WriteLine($"Service number {serviceNumber}: Device closed by client, shutting down.");

                        CleanupIoExchange();

                        return;
                    }

                    if (err == NativeConstants.ERROR_INSUFFICIENT_BUFFER)
                    {
                        if (!DevioDrvServiceInterop.GetOverlappedResult(device, memoryOverlapped, out _, bWait: true)
                            && Marshal.GetLastWin32Error() != NativeConstants.ERROR_INSUFFICIENT_BUFFER)
                        {
                            Trace.WriteLine($"Service number {serviceNumber}: Memory lock completion failed ({Marshal.GetLastWin32Error()})");

                            CleanupIoExchange();

                            return;
                        }

                        Trace.WriteLine($"Service number {serviceNumber}: Memory buffer too small, reallocating...");

                        bufferId = default;

                        mapView.Dispose();

                        mapView = null!;

                        bufferSize <<= 1;

                        using var mapping = MemoryMappedFile.CreateNew(null,
                                                                       bufferSize,
                                                                       MemoryMappedFileAccess.ReadWrite,
                                                                       MemoryMappedFileOptions.None,
                                                                       HandleInheritability.None);

                        mapView = mapping.CreateViewAccessor();

                        bufferId = mapView.SafeMemoryMappedViewHandle.DangerousGetHandle();

                        var maxTransferSize = (int)(mapView.Capacity - IMDPROXY_HEADER_SIZE);

                        memoryEvent.Reset();

                        if (!DevioDrvServiceInterop.DeviceIoControl(device,
                                                                    IOCTL_DEVIODRV_LOCK_MEMORY,
                                                                    &bufferId,
                                                                    sizeof(void*),
                                                                    (void*)bufferId,
                                                                    (uint)mapView.Capacity,
                                                                    null,
                                                                    memoryOverlapped)
                            && Marshal.GetLastWin32Error() != NativeConstants.ERROR_IO_PENDING)
                        {
                            Trace.Write($"Service number {serviceNumber}: Lock memory request failed ({Marshal.GetLastWin32Error()})");
                            CleanupIoExchange();
                            return;
                        }

                        Trace.WriteLine($"Service number {serviceNumber}: Created shared memory object, {maxTransferSize} bytes.");
                    }

                    if (err == NativeConstants.NO_ERROR)
                    {
                        var requestCode = mapView.SafeMemoryMappedViewHandle.Read<IMDPROXY_REQ>(headerOffset);

                        // Trace.WriteLine("Got client request: " & RequestCode.ToString())

                        switch (requestCode)
                        {
                            case IMDPROXY_REQ.IMDPROXY_REQ_INFO:
                                SendInfo(mapView.SafeMemoryMappedViewHandle);
                                break;

                            case IMDPROXY_REQ.IMDPROXY_REQ_READ:
                                ReadData(mapView.SafeMemoryMappedViewHandle);
                                break;

                            case IMDPROXY_REQ.IMDPROXY_REQ_WRITE:
                                WriteData(mapView.SafeMemoryMappedViewHandle);
                                break;

                            case IMDPROXY_REQ.IMDPROXY_REQ_CLOSE:
                                Trace.WriteLine($"Service number {serviceNumber}: Closing connection.");
                                CleanupIoExchange();
                                return;

                            case IMDPROXY_REQ.IMDPROXY_REQ_SHARED:
                                SharedKeys(mapView.SafeMemoryMappedViewHandle);
                                break;

                            default:
                                Trace.WriteLine($"Service number {serviceNumber}: Unsupported request code: {requestCode}");
                                mapView.SafeMemoryMappedViewHandle.Write(headerOffset, NativeConstants.ERROR_INVALID_FUNCTION);
                                break;
                        }
                    }

                    if (initializationDone is null
                        && concurrentCount >= ioExchangeBufferCounter
                        && DevioProvider.SupportsParallel
                        && !DevioProvider.ForceSingleThread)
                    {
                        try
                        {
                            StartNewIoExchange();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Service number {serviceNumber}: Exception while starting another I/O exchange buffer: {ex.JoinMessages()}");
                        }
                    }

                    // Trace.WriteLine("Sending response and waiting for next request.")

                    if (!DevioDrvServiceInterop.DeviceIoControl(device,
                                                                IOCTL_DEVIODRV_EXCHANGE_IO,
                                                                &bufferId,
                                                                sizeof(void*),
                                                                null,
                                                                0,
                                                                null,
                                                                requestOverlapped))
                    {
                        err = Marshal.GetLastWin32Error();

                        if (err != NativeConstants.ERROR_IO_PENDING)
                        {
                            continue;
                        }
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Service number {serviceNumber}: Exception while dispatching I/O request: {ex.JoinMessages()}");

                CleanupIoExchange();
            }
            finally
            {
                Interlocked.Decrement(ref concurrentRequestsCounter);

                if (initializationDone is not null)
                {
                    initializationDone.Set();
                    initializationDone = null;
                }
            }
        }

        void CleanupIoExchange()
        {
            Trace.WriteLine($"Dispoing I/O exchange number {serviceNumber}...");

            registeredHandle?.Unregister(null);
            memoryEvent?.Dispose();
            requestEvent?.Dispose();
            mapView?.Dispose();
            memoryOverlappedMem?.Dispose();
            requestOverlappedMem?.Dispose();

            if (Interlocked.Decrement(ref ioExchangeBufferCounter) == 0)
            {
                closedEvent.Set();
            }

            registeredHandle = null!;
            memoryEvent = null!;
            requestEvent = null!;
            mapView = null!;
            memoryOverlappedMem = null!;
            requestOverlappedMem = null!;
        }
    }

    private static readonly ulong headerOffset = (ulong)IMDPROXY_DEVIODRV_BUFFER_HEADER.SizeOf;

    private SafeFileHandle? device = null;

    private ManualResetEventSlim closedEvent = new(initialState: true);

    /// <summary>
    /// Runs service that acts as server end in Devio shared memory based communication. It will first wait for
    /// a client to connect, then serve client I/O requests and when client finally requests service to terminate, this
    /// method returns to caller. To run service in a worker thread that automatically disposes this object after client
    /// disconnection, call StartServiceThread() instead.
    /// </summary>
    public override bool StartServiceThread()
    {
        Trace.WriteLine($"Creating objects for shared memory communication '{ObjectName}'.");

        try
        {
            device = NativeFileIO.CreateFile(ProxyObjectName,
                                             FileSystemRights.FullControl,
                                             FileShare.ReadWrite,
                                             SecurityAttributes: 0,
                                             NativeConstants.CREATE_NEW,
                                             FileOptions.Asynchronous,
                                             TemplateFile: 0);

            StartNewIoExchange();

            OnServiceReady(EventArgs.Empty);

            return true;
        }
        catch (Exception ex)
        {
            device?.Dispose();
            device = null;

            if (ex is Win32Exception win32Exception
                && win32Exception.NativeErrorCode == NativeConstants.ERROR_ALREADY_EXISTS)
            {
                Exception = new InvalidOperationException($"Service name '{ObjectName}' already in use or not accessible.", ex);
            }
            else
            {
                Exception = ex;
            }

            var message = $"Service thread initialization failed: {Exception}.";
            Trace.WriteLine(message);
            OnServiceInitFailed(EventArgs.Empty);

            return false;
        }
    }

    public override void WaitForServiceThreadExit()
        => WaitForServiceThreadExit(Timeout.InfiniteTimeSpan);

    public override bool WaitForServiceThreadExit(TimeSpan timeout)
        => closedEvent.Wait(timeout);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            device?.Dispose();
        }

        base.Dispose(disposing);
    }

    public override void RunService()
        => throw new NotImplementedException();

    private int ioExchangeBufferCounter;

    private int concurrentRequestsCounter;

    private void SendInfo(SafeBuffer mapView)
    {
        var info = new IMDPROXY_INFO_RESP
        {
            file_size = (ulong)DevioProvider.Length,
            req_alignment = REQUIRED_ALIGNMENT,
            flags = (DevioProvider.CanWrite ? 0 : IMDPROXY_FLAGS.IMDPROXY_FLAG_RO)
                | (DevioProvider.SupportsShared ? IMDPROXY_FLAGS.IMDPROXY_FLAG_SUPPORTS_SHARED : 0)
        };

        mapView.Write(headerOffset, info);
    }

    private int readData_largest_request = default;

    private void ReadData(SafeBuffer mapView)
    {
        var request = mapView.Read<IMDPROXY_READ_REQ>(headerOffset);

        var offset = (long)request.offset;
        var readLength = (int)request.length;

        if (readLength > readData_largest_request)
        {
            readData_largest_request = readLength;
            Trace.WriteLine($"Largest requested read size is now: {readData_largest_request} bytes");
        }

        var response = default(IMDPROXY_READ_RESP);

        try
        {
            var maxTransferSize = (int)(mapView.ByteLength - IMDPROXY_HEADER_SIZE);

            if (readLength > maxTransferSize)
            {
#if DEBUG
                Trace.WriteLine($"Requested read length {readLength}, lowered to {maxTransferSize} bytes.");
#endif

                readLength = maxTransferSize;
            }

            response.length = (ulong)DevioProvider.Read(mapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, readLength, offset);
            response.errorno = 0UL;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Read request at 0x{offset:X8} for {readLength} bytes.");
            response.errorno = 1UL;
            response.length = 0UL;
        }

        mapView.Write(headerOffset, response);
    }

    private int writeData_largest_request = default;

    private void WriteData(SafeBuffer mapView)
    {
        var request = mapView.Read<IMDPROXY_WRITE_REQ>(headerOffset);

        var offset = (long)request.offset;
        var writeLength = (int)request.length;
        if (writeLength > writeData_largest_request)
        {
            writeData_largest_request = writeLength;
            Trace.WriteLine($"Largest requested write size is now: {writeData_largest_request} bytes");
        }

        var response = default(IMDPROXY_WRITE_RESP);

        try
        {
            var maxTransferSize = (int)(mapView.ByteLength - IMDPROXY_HEADER_SIZE);

            if (writeLength > maxTransferSize)
            {
                throw new Exception($"Requested write length {writeLength}. Buffer size is {maxTransferSize} bytes.");
            }

            var writtenLength = DevioProvider.Write(mapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, writeLength, offset);

            if (writtenLength < 0)
            {
                Trace.WriteLine($"Write request at 0x{offset:X8} for {writeLength} bytes, returned {writtenLength}.");
                response.errorno = 1UL;
                response.length = 0UL;
            }
            else
            {
                response.length = (ulong)writtenLength;
                response.errorno = 0UL;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Write request at 0x{offset:X8} for {writeLength} bytes.");
            response.errorno = 1UL;
            response.length = 0UL;
        }

        mapView.Write(headerOffset, response);
    }

    private void SharedKeys(SafeBuffer mapView)
    {
        var request = mapView.Read<IMDPROXY_SHARED_REQ>(headerOffset);

        var response = default(IMDPROXY_SHARED_RESP);

        try
        {
            DevioProvider.SharedKeys(request, out response, out var keys);
            if (keys is null)
            {
                response.length = 0UL;
            }
            else
            {
                response.length = (ulong)(keys.Length * sizeof(ulong));
                mapView.WriteArray(IMDPROXY_HEADER_SIZE, keys, 0, keys.Length);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            response.errorno = IMDPROXY_SHARED_RESP_CODE.IOError;
            response.length = 0UL;
        }

        mapView.Write(headerOffset, response);
    }

    protected override string ProxyObjectName
        => $@"\\?\DevIoDrv\{ObjectName}";

    protected override DeviceFlags ProxyModeFlags
        => DeviceFlags.FileTypeParallel
        | (DevioProvider.CanWrite ? DeviceFlags.None : DeviceFlags.ReadOnly);

    protected override void EmergencyStopServiceThread()
        => device?.Dispose();
}