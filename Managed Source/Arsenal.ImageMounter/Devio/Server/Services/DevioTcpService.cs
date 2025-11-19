//  DevioTcpService.vb
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils.Streams;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Arsenal.ImageMounter.Devio.Server.Services;

/// <summary>
/// Class that implements server end of Devio TCP/IP based communication protocol.
/// It uses an object implementing <see>IDevioProvider</see> interface as storage backend
/// for I/O requests received from client.
/// </summary>
public class DevioTcpService : DevioServiceBase
{
    /// <summary>
    /// Server endpoint where this service listens for client connection.
    /// </summary>
    public IPEndPoint ListenEndPoint { get; }

    private Action? internalShutdownRequestAction;

    private string? clientName;

    public override string? ClientName => clientName;

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// TCP/IP based communication.
    /// </summary>
    /// <param name="listenAddress">IP address where service should listen for client connection.</param>
    /// <param name="listenPort">IP port where service should listen for client connection.</param>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioTcpService(IPAddress listenAddress, int listenPort, IDevioProvider devioProvider, bool ownsProvider)
        : base(devioProvider, ownsProvider)
    {
        ListenEndPoint = new(listenAddress, listenPort);
    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// TCP/IP based communication.
    /// </summary>
    /// <param name="listenPort">IP port where service should listen for client connection. Instance will listen on all
    /// interfaces where this port is available.</param>
    /// <param name="devioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="ownsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioTcpService(int listenPort, IDevioProvider devioProvider, bool ownsProvider)
        : base(devioProvider, ownsProvider)
    {
        ListenEndPoint = new(IPAddress.Any, listenPort);
    }

    /// <summary>
    /// Runs service that acts as server end in Devio TCP/IP based communication. It will first wait for
    /// a client to connect, then serve client I/O requests and when client finally requests service to terminate, this
    /// method returns to caller. To run service in a worker thread that automatically disposes this object after client
    /// disconnection, call StartServiceThread() instead.
    /// </summary>
    public override void RunService()
    {
        byte[]? managedBuffer = null;
        TcpListener? listener = null;

        try
        {
            Trace.WriteLine($"Setting up listener at {ListenEndPoint}");

            listener = new TcpListener(ListenEndPoint);

            try
            {
                listener.ExclusiveAddressUse = false;
                listener.Start();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Listen failed: {ex}");
                Exception = new Exception("Listen failed on tcp port", ex);
                OnServiceInitFailed(EventArgs.Empty);
                return;
            }

            Trace.WriteLine("Raising service ready event.");
            OnServiceReady(EventArgs.Empty);

            StopServiceThread += (sender, e) => EmergencyStopServiceThread();

            var stopServiceThreadHandler = new EventHandler((sender, e) => listener.Stop());

            StopServiceThread += stopServiceThreadHandler;

            do
            {
                Socket tcpSocket;

                try
                {
                    tcpSocket = listener.AcceptSocket();
                    tcpSocket.NoDelay = true;
                }
                catch (SocketException ex)
                when (ex.ErrorCode is NativeConstants.WSAEINTR or NativeConstants.EINTR)
                {
                    Trace.WriteLine($"TCP listener stopped.");
                    return;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"TCP listener failed: {ex}");
                    return;
                }
                finally
                {
                    if (!Persistent)
                    {
                        listener.Stop();
                        listener = null;
                        StopServiceThread -= stopServiceThreadHandler;
                    }
                }

                var remoteEndPoint = tcpSocket.RemoteEndPoint;

                clientName = remoteEndPoint?.ToString();

                Trace.WriteLine($"Connection from {clientName}");

                using var tcpStream = new NetworkStream(tcpSocket, ownsSocket: true);
                using var outBuffer = new MemoryStream();

                internalShutdownRequestAction = () =>
                {
                    try
                    {
                        tcpStream.Dispose();
                    }
                    catch { }
                };

                OnClientConnected(EventArgs.Empty);

                for (bool closing = false; !closing; )
                {
                    IMDPROXY_REQ requestCode;

                    try
                    {
                        requestCode = tcpStream.Read<IMDPROXY_REQ>();
                    }
                    catch (SocketException ex)
                    when (ex.ErrorCode is NativeConstants.WSAECONNRESET or NativeConstants.ECONNRESET)
                    {
                        Trace.WriteLine("Connection reset by client.");
                        break;
                    }
                    catch (EndOfStreamException)
                    {
                        Trace.WriteLine("Connection closed.");
                        break;
                    }

                    // Trace.WriteLine("Got client request: " & RequestCode.ToString())

                    switch (requestCode)
                    {
                        case IMDPROXY_REQ.IMDPROXY_REQ_INFO:
                            SendInfo(tcpStream);
                            break;

                        case IMDPROXY_REQ.IMDPROXY_REQ_READ:
                            ReadData(tcpStream, outBuffer, ref managedBuffer);
                            break;

                        case IMDPROXY_REQ.IMDPROXY_REQ_WRITE:
                            WriteData(tcpStream, ref managedBuffer);
                            break;

                        case IMDPROXY_REQ.IMDPROXY_REQ_CLOSE:
                            Trace.WriteLine("Closing connection.");
                            closing = true;
                            break;

                        default:
                            Trace.WriteLine($"Unsupported request code: {requestCode}");
                            return;
                    }
                }

                OnClientDisconnected(EventArgs.Empty);
            }
            while (Persistent && listener?.Server?.LocalEndPoint is not null);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Unhandled exception in service thread: {ex}");
            OnServiceUnhandledException(new ThreadExceptionEventArgs(ex));
        }
        finally
        {
            if (managedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(managedBuffer);
            }

            listener?.Stop();

            Trace.WriteLine("Client disconnected.");
            OnServiceShutdown(EventArgs.Empty);
        }
    }

    private void SendInfo(NetworkStream stream)
    {
        var response = new IMDPROXY_INFO_RESP
        {
            file_size = (ulong)DevioProvider.Length,
            req_alignment = DevioProvider.SectorSize,
            flags = (DevioProvider.CanWrite ? 0 : IMDPROXY_FLAGS.IMDPROXY_FLAG_RO)
                | (DevioProvider.SupportsShared ? IMDPROXY_FLAGS.IMDPROXY_FLAG_SUPPORTS_SHARED : 0)
                | (Persistent ? IMDPROXY_FLAGS.IMDPROXY_FLAG_KEEP_OPEN : 0)
        };

        stream.Write(response);
    }

    private static byte[] EnsureBufferSize(byte[]? data, int readLength)
    {
        if (data is null || data.Length < readLength)
        {
            if (data is not null)
            {
                ArrayPool<byte>.Shared.Return(data);
            }

            data = ArrayPool<byte>.Shared.Rent(readLength);
        }

        return data;
    }

    private void ReadData(NetworkStream stream, MemoryStream outBuffer, ref byte[]? data)
    {
        var offset = stream.Read<long>();
        var readLength = (int)stream.Read<ulong>();

        data = EnsureBufferSize(data, readLength);

        ulong writeLength;
        ulong errorCode;

        try
        {
            writeLength = (ulong)DevioProvider.Read(data, 0, readLength, offset);
            errorCode = 0UL;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Read request failed at {offset:X8} for {readLength} bytes: {ex}");
            errorCode = 1UL;
            writeLength = 0UL;
        }

        outBuffer.SetLength(0);

        var response = new IMDPROXY_READ_RESP
        {
            errorno = errorCode,
            length = writeLength
        };

        outBuffer.Write(response);

        if (writeLength > 0)
        {
            outBuffer.Write(data, 0, (int)writeLength);
        }

        outBuffer.WriteTo(stream);
    }

    private void WriteData(NetworkStream stream, ref byte[]? data)
    {
        var offset = stream.Read<long>();
        var length = (int)stream.Read<ulong>();

        data = EnsureBufferSize(data, length);

        stream.ReadExactly(data, 0, length);

        ulong writeLength;
        ulong errorCode;

        try
        {
            writeLength = (ulong)DevioProvider.Write(data, 0, length, offset);
            errorCode = 0UL;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Write request failed at {offset:X8} for {length} bytes: {ex}");
            errorCode = 1UL;
            writeLength = 0UL;
        }

        var response = new IMDPROXY_WRITE_RESP
        {
            errorno = errorCode,
            length = writeLength,
        };

        stream.Write(response);
    }

    public override string ProxyObjectName
    {
        get
        {
            var endPoint = ListenEndPoint;

            if (endPoint.Address.Equals(IPAddress.Any))
            {
                endPoint = new IPEndPoint(IPAddress.Loopback, endPoint.Port);
            }

            return endPoint.ToString();
        }
    }

    public override DeviceFlags ProxyModeFlags => DeviceFlags.TypeProxy | DeviceFlags.ProxyTypeTCP;

    protected override void EmergencyStopServiceThread() => internalShutdownRequestAction?.Invoke();
}