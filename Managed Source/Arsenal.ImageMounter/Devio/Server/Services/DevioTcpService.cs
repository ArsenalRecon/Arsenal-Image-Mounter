//  DevioTcpService.vb
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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
        try
        {
            Trace.WriteLine($"Setting up listener at {ListenEndPoint}");

            var listener = new TcpListener(ListenEndPoint);

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

            var stopServiceThreadHandler = new EventHandler((sender, e) => listener.Stop());
            StopServiceThread += stopServiceThreadHandler;
            var tcpSocket = listener.AcceptSocket();
            StopServiceThread -= stopServiceThreadHandler;
            listener.Stop();
            Trace.WriteLine($"Connection from {tcpSocket.RemoteEndPoint}");

            using var tcpStream = new NetworkStream(tcpSocket, ownsSocket: true);
            using var reader = new BinaryReader(tcpStream, Encoding.Default);
            using var writer = new BinaryWriter(new MemoryStream(), Encoding.Default);

            internalShutdownRequestAction = () =>
            {
                try
                {
                    reader.Dispose();
                }
                catch { }
            };

            byte[]? managedBuffer = null;

            for (; ; )
            {
                IMDPROXY_REQ requestCode;

                try
                {
                    requestCode = (IMDPROXY_REQ)reader.ReadUInt64();
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                // Trace.WriteLine("Got client request: " & RequestCode.ToString())

                switch (requestCode)
                {
                    case IMDPROXY_REQ.IMDPROXY_REQ_INFO:
                        SendInfo(writer);
                        break;

                    case IMDPROXY_REQ.IMDPROXY_REQ_READ:
                        ReadData(reader, writer, ref managedBuffer);
                        break;

                    case IMDPROXY_REQ.IMDPROXY_REQ_WRITE:
                        WriteData(reader, writer, ref managedBuffer);
                        break;

                    case IMDPROXY_REQ.IMDPROXY_REQ_CLOSE:
                        Trace.WriteLine("Closing connection.");
                        return;

                    default:
                        Trace.WriteLine($"Unsupported request code: {requestCode}");
                        return;
                }

                // Trace.WriteLine("Sending response and waiting for next request.")

                writer.Seek(0, SeekOrigin.Begin);

                var baseStream = (MemoryStream)writer.BaseStream;
                baseStream.WriteTo(tcpStream);
                baseStream.SetLength(0);
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

    private void SendInfo(BinaryWriter writer)
    {
        writer.Write((ulong)DevioProvider.Length);
        writer.Write((ulong)REQUIRED_ALIGNMENT);
        writer.Write((ulong)(DevioProvider.CanWrite ? IMDPROXY_FLAGS.IMDPROXY_FLAG_NONE : IMDPROXY_FLAGS.IMDPROXY_FLAG_RO));
    }

    private void ReadData(BinaryReader reader, BinaryWriter writer, ref byte[]? data)
    {
        var offset = reader.ReadInt64();
        var readLength = (int)reader.ReadUInt64();

        if (data is null || data.Length < readLength)
        {
            Array.Resize(ref data, readLength);
        }

        ulong writeLength;
        ulong errorCode;

        try
        {
            writeLength = (ulong)DevioProvider.Read(data, 0, readLength, offset);
            errorCode = 0UL;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Read request at {offset:X8} for {readLength} bytes.");
            errorCode = 1UL;
            writeLength = 0UL;
        }

        writer.Write(errorCode);
        writer.Write(writeLength);
        if (writeLength > 0m)
        {
            writer.Write(data, 0, (int)writeLength);
        }
    }

    private void WriteData(BinaryReader reader, BinaryWriter writer, ref byte[]? data)
    {
        var offset = reader.ReadInt64();
        var length = reader.ReadUInt64();
        if (data is null || (ulong)data.Length < length)
        {
            Array.Resize(ref data, (int)length);
        }

        var readLength = reader.Read(data, 0, (int)length);
        ulong writeLength;
        ulong errorCode;

        try
        {
            writeLength = (ulong)DevioProvider.Write(data, 0, readLength, offset);
            errorCode = 0UL;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Write request at {offset:X8} for {length} bytes.");
            errorCode = 1UL;
            writeLength = 0UL;
        }

        writer.Write(errorCode);
        writer.Write(writeLength);
    }

    protected override string ProxyObjectName
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

    protected override DeviceFlags ProxyModeFlags => DeviceFlags.TypeProxy | DeviceFlags.ProxyTypeTCP;

    protected override void EmergencyStopServiceThread() => internalShutdownRequestAction?.Invoke();
}