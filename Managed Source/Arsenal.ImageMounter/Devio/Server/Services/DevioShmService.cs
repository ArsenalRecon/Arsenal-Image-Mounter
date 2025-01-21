//  DevioShmService.vb
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using static Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS;

namespace Arsenal.ImageMounter.Devio.Server.Services;

/// <summary>
/// Class that implements server end of Devio shared memory based communication
/// protocol. It uses an object implementing <see>IDevioProvider</see> interface as
/// storage backend for I/O requests received from client.
/// </summary>
/// <remarks>
/// Creates a new service instance with enough data to later run a service that acts as server end in Devio
/// shared memory based communication.
/// </remarks>
/// <param name="objectName">Object name of shared memory file mapping object created by this instance.</param>
/// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
/// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
/// instance is disposed.</param>
/// <param name="bufferSize">Buffer size to use for shared memory I/O communication between driver and this service.</param>
public class DevioShmService(string objectName, IDevioProvider devioProvider, bool ownsProvider, long bufferSize) : DevioServiceBase(devioProvider, ownsProvider)
{
    /// <summary>
    /// Object name of shared memory file mapping object created by this instance.
    /// </summary>
    public string ObjectName { get; } = objectName;

    /// <summary>
    /// Size of the memory block that is shared between driver and this service.
    /// </summary>
    public long BufferSize { get; } = bufferSize;

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
    public const long DefaultBufferSize = (8 << 20) + IMDPROXY_HEADER_SIZE;

    private static Guid GetNextRandomValue() => NativeCalls.GenRandomGuid();

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A default buffer size will be used.
    /// </summary>
    /// <param name="objectName">Object name of shared memory file mapping object created by this instance.</param>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioShmService(string objectName, IDevioProvider devioProvider, bool ownsProvider)
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
    public DevioShmService(IDevioProvider devioProvider, bool ownsProvider)
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
    public DevioShmService(IDevioProvider devioProvider, bool ownsProvider, long BufferSize)
        : this($"devio-{GetNextRandomValue()}", devioProvider, ownsProvider, BufferSize)
    {
    }

    /// <summary>
    /// Runs service that acts as server end in Devio shared memory based communication. It will first wait for
    /// a client to connect, then serve client I/O requests and when client finally requests service to terminate, this
    /// method returns to caller. To run service in a worker thread that automatically disposes this object after client
    /// disconnection, call StartServiceThread() instead.
    /// </summary>
    public override void RunService()
    {
        using var disposableObjects = new DisposableList();

        EventWaitHandle requestEvent;

        EventWaitHandle responseEvent;

        MemoryMappedFile? mapping;

        MemoryMappedViewAccessor mapView;

        Mutex serverMutex;

        Trace.WriteLine($"Creating objects for shared memory communication '{ObjectName}'.");

        try
        {
            requestEvent = new(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Request");
            disposableObjects.Add(requestEvent);
            responseEvent = new(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Response");
            disposableObjects.Add(responseEvent);
            serverMutex = new(initiallyOwned: false, name: $@"Global\{ObjectName}_Server");
            disposableObjects.Add(serverMutex);

            if (serverMutex.WaitOne(0) == false)
            {
                var message = $"Service name '{ObjectName}' busy.";
                Trace.WriteLine(message);
                throw new InvalidOperationException(message);
            }
        }
        catch (Exception ex)
        {
            if (ex is UnauthorizedAccessException)
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
            mapping = MemoryMappedFile.CreateNew($@"Global\{ObjectName}",
                                                 BufferSize,
                                                 MemoryMappedFileAccess.ReadWrite,
                                                 MemoryMappedFileOptions.None,
                                                 HandleInheritability.None);

            disposableObjects.Add(mapping);

            mapView = mapping.CreateViewAccessor();

            disposableObjects.Add(mapView);

            MaxTransferSize = (int)(mapView.Capacity - IMDPROXY_HEADER_SIZE);

            Trace.WriteLine($"Created shared memory object, {MaxTransferSize} bytes.");

            Trace.WriteLine("Raising service ready event.");
         
            OnServiceReady(EventArgs.Empty);
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
            Trace.WriteLine("Waiting for client to connect.");

            using (var stopServiceThreadEvent = new ManualResetEvent(initialState: false))
            {
                var stopServiceThreadHandler = new EventHandler((sender, e) => stopServiceThreadEvent.Set());
                StopServiceThread += stopServiceThreadHandler;
                var waitEvents = new[] { requestEvent, stopServiceThreadEvent };
                var eventIndex = WaitHandle.WaitAny(waitEvents);
                StopServiceThread -= stopServiceThreadHandler;

                Trace.WriteLine("Wait finished. Disposing file mapping object.");

                mapping.Dispose();
                mapping = null;

                if (ReferenceEquals(waitEvents[eventIndex], stopServiceThreadEvent))
                {
                    Trace.WriteLine("Service thread exit request.");
                    return;
                }
            }

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

            OnClientConnected(EventArgs.Empty);

            for (; ; )
            {
                if (requestShutdown)
                {
                    Trace.WriteLine("Emergency shutdown. Closing connection.");
                    break;
                }

                var RequestCode = mapView.SafeMemoryMappedViewHandle.Read<IMDPROXY_REQ>(0x0UL);

                // Trace.WriteLine("Got client request: " & RequestCode.ToString())

                switch (RequestCode)
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
                        Trace.WriteLine($"Unsupported request code: {RequestCode}");
                        return;
                }

                // Trace.WriteLine("Sending response and waiting for next request.")

                if (!WaitHandle.SignalAndWait(responseEvent, requestEvent))
                {
                    Trace.WriteLine("Synchronization failed.");
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
            OnClientDisconnected(EventArgs.Empty);
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

        mapView.Write(0x0UL, info);
    }

    private int readData_largest_request = default;

    private void ReadData(SafeBuffer mapView)
    {
        var request = mapView.Read<IMDPROXY_READ_REQ>(0x0UL);

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

        mapView.Write(0x0UL, response);
    }

    private int writeData_largest_request = default;

    private void WriteData(SafeBuffer mapView)
    {
        var request = mapView.Read<IMDPROXY_WRITE_REQ>(0x0UL);

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

        mapView.Write(0x0UL, response);
    }

    private void SharedKeys(SafeBuffer mapView)
    {
        var request = mapView.Read<IMDPROXY_SHARED_REQ>(0x0UL);

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

        mapView.Write(0x0UL, response);
    }

    public override string ProxyObjectName
        => ObjectName;

    public override DeviceFlags ProxyModeFlags
        => DeviceFlags.TypeProxy | DeviceFlags.ProxyTypeSharedMemory;

    protected override void EmergencyStopServiceThread()
        => internalShutdownRequestAction?.Invoke();
}