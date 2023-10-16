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
using Arsenal.ImageMounter.IO.Native;
using LTRData.Extensions.Buffers;
using Microsoft.Win32.SafeHandles;
using System;
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
#endif
}

/// <summary>
/// Class that implements server end of Devio shared memory based communication
/// protocol. It uses an object implementing <see>IDevioProvider</see> interface as
/// storage backend for I/O requests received from client.
/// </summary>
[SupportedOSPlatform("windows")]
public class DevioDrvService : DevioServiceBase
{
    /// <summary>
    /// Object name of shared memory file mapping object created by this instance.
    /// </summary>
    public string ObjectName { get; }

    /// <summary>
    /// Size of the memory block that is shared between driver and this service.
    /// </summary>
    public long BufferSize { get; private set; }

    /// <summary>
    /// Largest size of an I/O transfer between driver and this service. This
    /// number depends on the size of the memory block that is shared between
    /// driver and this service.
    /// </summary>
    public int MaxTransferSize { get; private set; }

    private Action? internalShutdownRequestAction;

    /// <summary>
    /// Buffer size that will be automatically selected on this platform when
    /// an instance is created by a constructor without a BufferSize argument.
    /// 
    /// Corresponds to MaximumTransferLength that driver reports to
    /// storage port driver. This is the largest possible size of an
    /// I/O request from the driver.
    /// </summary>
    public const long DefaultBufferSize = (64 << 10) + IMDPROXY_HEADER_SIZE;

    private static Guid GetNextRandomValue() => NativeCalls.GenRandomGuid();

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication.
    /// </summary>
    /// <param name="objectName">Object name of shared memory file mapping object created by this instance.</param>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    /// <param name="bufferSize">Buffer size to use for shared memory I/O communication between driver and this service.</param>
    public DevioDrvService(string objectName, IDevioProvider devioProvider, bool ownsProvider, long bufferSize)
        : base(devioProvider, ownsProvider)
    {
        ObjectName = objectName;
        BufferSize = bufferSize;
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
        : this(objectName, devioProvider, ownsProvider, DefaultBufferSize)
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
        : this(devioProvider, ownsProvider, DefaultBufferSize)
    {
    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A random object name will be used.
    /// </summary>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    /// <param name="BufferSize">Buffer size to use for shared memory I/O communication.</param>
    public DevioDrvService(IDevioProvider devioProvider, bool ownsProvider, long BufferSize)
        : this(GetNextRandomValue().ToString(), devioProvider, ownsProvider, BufferSize)
    {
    }

    private static readonly ulong headerOffset = (ulong)IMDPROXY_DEVIODRV_BUFFER_HEADER.SizeOf;

    /// <summary>
    /// Runs service that acts as server end in Devio shared memory based communication. It will first wait for
    /// a client to connect, then serve client I/O requests and when client finally requests service to terminate, this
    /// method returns to caller. To run service in a worker thread that automatically disposes this object after client
    /// disconnection, call StartServiceThread() instead.
    /// </summary>
    public override unsafe void RunService()
    {
        using var disposableObjects = new DisposableList();

        SafeFileHandle file;

        EventWaitHandle requestEvent;

        EventWaitHandle memoryEvent;

        MemoryMappedViewAccessor mapView;

        void* bufferId;

        Trace.WriteLine($"Creating objects for shared memory communication '{ObjectName}'.");

        try
        {
            file = NativeFileIO.CreateFile(ProxyObjectName,
                                           FileSystemRights.FullControl,
                                           FileShare.ReadWrite,
                                           SecurityAttributes: 0,
                                           NativeConstants.CREATE_NEW,
                                           FileOptions.Asynchronous,
                                           TemplateFile: 0);

            disposableObjects.Add(file);
        }
        catch (Exception ex)
        {
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

            return;
        }

        try
        {
            using var mapping = MemoryMappedFile.CreateNew(null,
                                                           BufferSize,
                                                           MemoryMappedFileAccess.ReadWrite,
                                                           MemoryMappedFileOptions.None,
                                                           HandleInheritability.None);

            mapView = mapping.CreateViewAccessor();

            disposableObjects.Add(mapView);

            bufferId = (void*)mapView.SafeMemoryMappedViewHandle.DangerousGetHandle();

            MaxTransferSize = (int)(mapView.Capacity - IMDPROXY_HEADER_SIZE);

            Trace.WriteLine($"Created shared memory object, {MaxTransferSize} bytes.");
        }
        catch (Exception ex)
        {
            if (ex is UnauthorizedAccessException)
            {
                Exception = new Exception($"This operation requires administrative privileges.", ex);
            }
            else
            {
                Exception = ex;
            }

            var message = $"Service thread initialization failed: {Exception}.";
            Trace.WriteLine(message);
            OnServiceInitFailed(EventArgs.Empty);

            return;
        }

        try
        {
            memoryEvent = new ManualResetEvent(initialState: false);

            var memoryOverlapped = new NativeOverlapped
            {
                EventHandle = memoryEvent.SafeWaitHandle.DangerousGetHandle()
            };

            requestEvent = new ManualResetEvent(initialState: false);

            var requestOverlapped = new NativeOverlapped
            {
                EventHandle = requestEvent.SafeWaitHandle.DangerousGetHandle()
            };

            Trace.WriteLine("Waiting for client to connect.");

            const uint IOCTL_DEVIODRV_LOCK_MEMORY = unchecked((uint)(((0x8372) << 16) | (((0x0001) | (0x0002)) << 14) | ((0x8D1) << 2) | (2)));
            const uint IOCTL_DEVIODRV_EXCHANGE_IO = unchecked((uint)(((0x8372) << 16) | (((0x0001) | (0x0002)) << 14) | ((0x8D0) << 2) | (2)));

            if (!DevioDrvServiceInterop.DeviceIoControl(file,
                                                            IOCTL_DEVIODRV_LOCK_MEMORY,
                                                            &bufferId,
                                                            sizeof(void*),
                                                            bufferId,
                                                            (uint)mapView.Capacity,
                                                            null,
                                                            &memoryOverlapped)
                && Marshal.GetLastWin32Error() != NativeConstants.ERROR_IO_PENDING)
            {
                throw new IOException("Lock memory request failed", new Win32Exception());
            }

            mapView.SafeMemoryMappedViewHandle.Write(0, IMDPROXY_REQ.IMDPROXY_REQ_INFO);
            mapView.SafeMemoryMappedViewHandle.Write(headerOffset, IMDPROXY_REQ.IMDPROXY_REQ_INFO);

            Trace.WriteLine("Client connected, waiting for request.");

            var requestShutdown = false;

            internalShutdownRequestAction = () =>
            {
                try
                {
                    Trace.WriteLine("Emergency service thread shutdown requested, injecting close request...");
                    requestShutdown = true;
                    requestEvent.Set();
                }
                catch { }
            };

            var initialized = false;

            for (; ; )
            {
                if (requestShutdown)
                {
                    Trace.WriteLine("Emergency shutdown. Closing connection.");
                    break;
                }

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
                        Trace.WriteLine("Closing connection.");
                        return;

                    case IMDPROXY_REQ.IMDPROXY_REQ_SHARED:
                        SharedKeys(mapView.SafeMemoryMappedViewHandle);
                        break;

                    default:
                        Trace.WriteLine($"Unsupported request code: {requestCode}");
                        mapView.SafeMemoryMappedViewHandle.Write(headerOffset, NativeConstants.ERROR_INVALID_FUNCTION);
                        break;
                }

                // Trace.WriteLine("Sending response and waiting for next request.")

                for(; ;)
                {
                    requestEvent.Reset();

                    var rc = DevioDrvServiceInterop.DeviceIoControl(file,
                                                                    IOCTL_DEVIODRV_EXCHANGE_IO,
                                                                    &bufferId,
                                                                    sizeof(void*),
                                                                    null,
                                                                    0,
                                                                    null,
                                                                    &requestOverlapped);

                    var err = Marshal.GetLastWin32Error();

                    if (!initialized)
                    {
                        initialized = true;

                        OnServiceReady(EventArgs.Empty);
                    }

                    if (rc)
                    {
                        break;
                    }

                    if (err == NativeConstants.ERROR_IO_PENDING)
                    {
                        if (DevioDrvServiceInterop.GetOverlappedResult(file, &requestOverlapped, out _, bWait: true))
                        {
                            break;
                        }

                        err = Marshal.GetLastWin32Error();
                    }

                    if (err == NativeConstants.ERROR_INSUFFICIENT_BUFFER)
                    {
                        if (!DevioDrvServiceInterop.GetOverlappedResult(file, &memoryOverlapped, out _, bWait: true)
                            && Marshal.GetLastWin32Error() != NativeConstants.ERROR_INSUFFICIENT_BUFFER)
                        {
                            throw new InternalBufferOverflowException("Memory lock or I/O request failed", new Win32Exception());
                        }

                        bufferId = null;

                        mapView.Dispose();

                        disposableObjects.Remove(mapView);

                        BufferSize <<= 1;

                        using var mapping = MemoryMappedFile.CreateNew(null,
                                                                       BufferSize,
                                                                       MemoryMappedFileAccess.ReadWrite,
                                                                       MemoryMappedFileOptions.None,
                                                                       HandleInheritability.None);

                        mapView = mapping.CreateViewAccessor();

                        disposableObjects.Add(mapView);

                        bufferId = (void*)mapView.SafeMemoryMappedViewHandle.DangerousGetHandle();

                        MaxTransferSize = (int)(mapView.Capacity - IMDPROXY_HEADER_SIZE);

                        memoryEvent.Reset();

                        if (!DevioDrvServiceInterop.DeviceIoControl(file,
                                                                    IOCTL_DEVIODRV_LOCK_MEMORY,
                                                                    &bufferId,
                                                                    sizeof(void*),
                                                                    bufferId,
                                                                    (uint)mapView.Capacity,
                                                                    null,
                                                                    &memoryOverlapped)
                            && Marshal.GetLastWin32Error() != NativeConstants.ERROR_IO_PENDING)
                        {
                            throw new IOException("Lock memory request failed", new Win32Exception());
                        }

                        Trace.WriteLine($"Created shared memory object, {MaxTransferSize} bytes.");

                        continue;
                    }

                    if (err == NativeConstants.ERROR_DEV_NOT_EXIST)
                    {
                        Trace.WriteLine("Device closed by client, shutting down.");

                        return;
                    }

                    throw new IOException("I/O exchange failed", new Win32Exception(err));
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Unhandled exception in service thread: {ex}");
            OnServiceUnhandledException(new(ex));
        }
        finally
        {
            Trace.WriteLine("Client disconnected.");
            OnServiceShutdown(EventArgs.Empty);
        }
    }

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
            if (readLength > MaxTransferSize)
            {
#if DEBUG
                Trace.WriteLine($"Requested read length {readLength}, lowered to {MaxTransferSize} bytes.");
#endif

                readLength = MaxTransferSize;
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
            if (writeLength > MaxTransferSize)
            {
                throw new Exception($"Requested write length {writeLength}. Buffer size is {MaxTransferSize} bytes.");
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
        => internalShutdownRequestAction?.Invoke();
}