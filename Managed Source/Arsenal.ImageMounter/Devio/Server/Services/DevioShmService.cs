//  DevioShmService.vb
//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.Services;

/// <summary>
/// Class that implements server end of Devio shared memory based communication
/// protocol. It uses an object implementing <see>IDevioProvider</see> interface as
/// storage backend for I/O requests received from client.
/// </summary>
public class DevioShmService : DevioServiceBase
{

    /// <summary>
    /// Object name of shared memory file mapping object created by this instance.
    /// </summary>
    public string ObjectName { get; }

    /// <summary>
    /// Size of the memory block that is shared between driver and this service.
    /// </summary>
    public long BufferSize { get; }

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
    /// shared memory based communication.
    /// </summary>
    /// <param name="ObjectName">Object name of shared memory file mapping object created by this instance.</param>
    /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    /// <param name="BufferSize">Buffer size to use for shared memory I/O communication between driver and this service.</param>
    public DevioShmService(string ObjectName, IDevioProvider DevioProvider, bool OwnsProvider, long BufferSize)
        : base(DevioProvider, OwnsProvider)
    {

        this.ObjectName = ObjectName;
        this.BufferSize = BufferSize;

    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A default buffer size will be used.
    /// </summary>
    /// <param name="ObjectName">Object name of shared memory file mapping object created by this instance.</param>
    /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioShmService(string ObjectName, IDevioProvider DevioProvider, bool OwnsProvider)
        : this(ObjectName, DevioProvider, OwnsProvider, DefaultBufferSize)
    {
    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A default buffer size and a random object name will be used.
    /// </summary>
    /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioShmService(IDevioProvider DevioProvider, bool OwnsProvider)
        : this(DevioProvider, OwnsProvider, DefaultBufferSize)
    {
    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// shared memory based communication. A random object name will be used.
    /// </summary>
    /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    /// <param name="BufferSize">Buffer size to use for shared memory I/O communication.</param>
    public DevioShmService(IDevioProvider DevioProvider, bool OwnsProvider, long BufferSize)
        : this($"devio-{GetNextRandomValue()}", DevioProvider, OwnsProvider, BufferSize)
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

        using var DisposableObjects = new DisposableList();

        EventWaitHandle RequestEvent;

        EventWaitHandle ResponseEvent;

        MemoryMappedFile? Mapping;

        MemoryMappedViewAccessor MapView;

        Mutex ServerMutex;

        Trace.WriteLine($"Creating objects for shared memory communication '{ObjectName}'.");

        try
        {

            RequestEvent = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Request");
            DisposableObjects.Add(RequestEvent);
            ResponseEvent = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Response");
            DisposableObjects.Add(ResponseEvent);
            ServerMutex = new Mutex(initiallyOwned: false, name: $@"Global\{ObjectName}_Server");
            DisposableObjects.Add(ServerMutex);

            if (ServerMutex.WaitOne(0) == false)
            {
                var message = $"Service name '{ObjectName}' busy.";
                Trace.WriteLine(message);
                throw new Exception(message);
            }
        }

        catch (Exception ex)
        {
            if (ex is UnauthorizedAccessException)
            {
                Exception = new Exception($"Service name '{ObjectName}' already in use or not accessible.", ex);
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
            Mapping = MemoryMappedFile.CreateNew($@"Global\{ObjectName}", BufferSize, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.None);

            DisposableObjects.Add(Mapping);

            MapView = Mapping.CreateViewAccessor();

            DisposableObjects.Add(MapView);

            MaxTransferSize = (int)(MapView.Capacity - IMDPROXY_HEADER_SIZE);

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

            using (var StopServiceThreadEvent = new ManualResetEvent(initialState: false))
            {
                var StopServiceThreadHandler = new EventHandler((sender, e) => StopServiceThreadEvent.Set());
                StopServiceThread += StopServiceThreadHandler;
                var WaitEvents = new[] { RequestEvent, StopServiceThreadEvent };
                var EventIndex = WaitHandle.WaitAny(WaitEvents);
                StopServiceThread -= StopServiceThreadHandler;

                Trace.WriteLine("Wait finished. Disposing file mapping object.");

                Mapping.Dispose();
                Mapping = null;

                if (ReferenceEquals(WaitEvents[EventIndex], StopServiceThreadEvent))
                {
                    Trace.WriteLine("Service thread exit request.");
                    return;
                }
            }

            Trace.WriteLine("Client connected, waiting for request.");

            var request_shutdown = false;

            internalShutdownRequestAction = () =>
            {
                try
                {
                    Trace.WriteLine("Emergency service thread shutdown requested, injecting close request...");
                    request_shutdown = true;
                    RequestEvent.Set();
                }
                catch { }
            };

            for (; ; )
            {
                if (request_shutdown)
                {
                    Trace.WriteLine("Emergency shutdown. Closing connection.");
                    break;
                }

                var RequestCode = MapView.SafeMemoryMappedViewHandle.Read<IMDPROXY_REQ>(0x0UL);

                // Trace.WriteLine("Got client request: " & RequestCode.ToString())

                switch (RequestCode)
                {

                    case IMDPROXY_REQ.IMDPROXY_REQ_INFO:
                        {
                            SendInfo(MapView.SafeMemoryMappedViewHandle);
                            break;
                        }

                    case IMDPROXY_REQ.IMDPROXY_REQ_READ:
                        {
                            ReadData(MapView.SafeMemoryMappedViewHandle);
                            break;
                        }

                    case IMDPROXY_REQ.IMDPROXY_REQ_WRITE:
                        {
                            WriteData(MapView.SafeMemoryMappedViewHandle);
                            break;
                        }

                    case IMDPROXY_REQ.IMDPROXY_REQ_CLOSE:
                        {
                            Trace.WriteLine("Closing connection.");
                            return;
                        }

                    case IMDPROXY_REQ.IMDPROXY_REQ_SHARED:
                        {
                            SharedKeys(MapView.SafeMemoryMappedViewHandle);
                            break;
                        }

                    default:
                        {
                            Trace.WriteLine($"Unsupported request code: {RequestCode}");
                            return;
                        }
                }

                // Trace.WriteLine("Sending response and waiting for next request.")

                if (WaitHandle.SignalAndWait(ResponseEvent, RequestEvent) == false)
                {
                    Trace.WriteLine("Synchronization failed.");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Unhandled exception in service thread: {ex}");
            OnServiceUnhandledException(new ThreadExceptionEventArgs(ex));
        }
        finally
        {
            Trace.WriteLine("Client disconnected.");
            OnServiceShutdown(EventArgs.Empty);
        }
    }

    private void SendInfo(SafeBuffer MapView)
    {
        var Info = new IMDPROXY_INFO_RESP
        {
            file_size = (ulong)DevioProvider.Length,
            req_alignment = REQUIRED_ALIGNMENT,
            flags = (DevioProvider.CanWrite ? IMDPROXY_FLAGS.IMDPROXY_FLAG_NONE : IMDPROXY_FLAGS.IMDPROXY_FLAG_RO)
                | (DevioProvider.SupportsShared ? IMDPROXY_FLAGS.IMDPROXY_FLAG_SUPPORTS_SHARED : IMDPROXY_FLAGS.IMDPROXY_FLAG_NONE)
        };

        MapView.Write(0x0UL, Info);
    }

    private int readData_largest_request = default;

    private void ReadData(SafeBuffer MapView)
    {
        var Request = MapView.Read<IMDPROXY_READ_REQ>(0x0UL);

        var Offset = (long)Request.offset;
        var ReadLength = (int)Request.length;
        if (ReadLength > readData_largest_request)
        {
            readData_largest_request = ReadLength;
            Trace.WriteLine($"Largest requested read size is now: {readData_largest_request} bytes");
        }

        var Response = default(IMDPROXY_READ_RESP);

        try
        {
            if (ReadLength > MaxTransferSize)
            {
#if DEBUG
                Trace.WriteLine($"Requested read length {ReadLength}, lowered to {MaxTransferSize} bytes.");
#endif

                ReadLength = MaxTransferSize;
            }

            Response.length = (ulong)DevioProvider.Read(MapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, ReadLength, Offset);
            Response.errorno = 0UL;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Read request at 0x{Offset:X8} for {ReadLength} bytes.");
            Response.errorno = 1UL;
            Response.length = 0UL;
        }

        MapView.Write(0x0UL, Response);
    }

    private int writeData_largest_request = default;

    private void WriteData(SafeBuffer MapView)
    {
        var Request = MapView.Read<IMDPROXY_WRITE_REQ>(0x0UL);

        var Offset = (long)Request.offset;
        var WriteLength = (int)Request.length;
        if (WriteLength > writeData_largest_request)
        {
            writeData_largest_request = WriteLength;
            Trace.WriteLine($"Largest requested write size is now: {writeData_largest_request} bytes");
        }

        var Response = default(IMDPROXY_WRITE_RESP);

        try
        {
            if (WriteLength > MaxTransferSize)
            {
                throw new Exception($"Requested write length {WriteLength}. Buffer size is {MaxTransferSize} bytes.");
            }

            var WrittenLength = DevioProvider.Write(MapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, WriteLength, Offset);
            if (WrittenLength < 0)
            {
                Trace.WriteLine($"Write request at 0x{Offset:X8} for {WriteLength} bytes, returned {WrittenLength}.");
                Response.errorno = 1UL;
                Response.length = 0UL;
            }
            else
            {
                Response.length = (ulong)WrittenLength;
                Response.errorno = 0UL;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Write request at 0x{Offset:X8} for {WriteLength} bytes.");
            Response.errorno = 1UL;
            Response.length = 0UL;
        }

        MapView.Write(0x0UL, Response);
    }

    private void SharedKeys(SafeBuffer MapView)
    {
        var Request = MapView.Read<IMDPROXY_SHARED_REQ>(0x0UL);

        var Response = default(IMDPROXY_SHARED_RESP);

        try
        {
            DevioProvider.SharedKeys(Request, out Response, out var Keys);
            if (Keys is null)
            {
                Response.length = 0UL;
            }
            else
            {
                Response.length = (ulong)(Keys.Length * sizeof(ulong));
                MapView.WriteArray(IMDPROXY_HEADER_SIZE, Keys, 0, Keys.Length);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Response.errorno = IMDPROXY_SHARED_RESP_CODE.IOError;
            Response.length = 0UL;
        }

        MapView.Write(0x0UL, Response);
    }

    protected override string ProxyObjectName => ObjectName;

    protected override DeviceFlags ProxyModeFlags => DeviceFlags.TypeProxy | DeviceFlags.ProxyTypeSharedMemory;

    protected override void EmergencyStopServiceThread() => internalShutdownRequestAction?.Invoke();
}