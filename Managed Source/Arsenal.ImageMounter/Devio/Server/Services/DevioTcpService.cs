using System;
using System.Diagnostics;

// '''' DevioTcpService.vb
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS;
using Arsenal.ImageMounter.Devio.Server.GenericProviders;

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
    public IPEndPoint ListenEndPoint { get; private set; }

    private Action? InternalShutdownRequestAction;

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// TCP/IP based communication.
    /// </summary>
    /// <param name="ListenAddress">IP address where service should listen for client connection.</param>
    /// <param name="ListenPort">IP port where service should listen for client connection.</param>
    /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioTcpService(IPAddress ListenAddress, int ListenPort, IDevioProvider DevioProvider, bool OwnsProvider)
        : base(DevioProvider, OwnsProvider)
    {

        ListenEndPoint = new IPEndPoint(ListenAddress, ListenPort);

    }

    /// <summary>
    /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
    /// TCP/IP based communication.
    /// </summary>
    /// <param name="ListenPort">IP port where service should listen for client connection. Instance will listen on all
    /// interfaces where this port is available.</param>
    /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
    /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioTcpService(int ListenPort, IDevioProvider DevioProvider, bool OwnsProvider)
        : base(DevioProvider, OwnsProvider)
    {

        ListenEndPoint = new IPEndPoint(IPAddress.Any, ListenPort);

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

            var Listener = new TcpListener(ListenEndPoint);

            try
            {
                Listener.ExclusiveAddressUse = false;
                Listener.Start();
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

            var StopServiceThreadHandler = new EventHandler((sender, e) => Listener.Stop());
            StopServiceThread += StopServiceThreadHandler;
            var TcpSocket = Listener.AcceptSocket();
            StopServiceThread -= StopServiceThreadHandler;
            Listener.Stop();
            Trace.WriteLine($"Connection from {TcpSocket.RemoteEndPoint}");

            using (var TcpStream = new NetworkStream(TcpSocket, ownsSocket: true))
            using (var Reader = new BinaryReader(TcpStream, Encoding.Default))
            using (var Writer = new BinaryWriter(new MemoryStream(), Encoding.Default))
            {



                InternalShutdownRequestAction = new Action(() =>
                {
                    try
                    {
                        Reader.Dispose();
                    }
                    catch { }
                });

                byte[]? ManagedBuffer = null;

                do
                {

                    IMDPROXY_REQ RequestCode;

                    try
                    {
                        RequestCode = (IMDPROXY_REQ)Reader.ReadUInt64();
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    // Trace.WriteLine("Got client request: " & RequestCode.ToString())

                    switch (RequestCode)
                    {

                        case IMDPROXY_REQ.IMDPROXY_REQ_INFO:
                            {
                                SendInfo(Writer);
                                break;
                            }

                        case IMDPROXY_REQ.IMDPROXY_REQ_READ:
                            {
                                ReadData(Reader, Writer, ref ManagedBuffer);
                                break;
                            }

                        case IMDPROXY_REQ.IMDPROXY_REQ_WRITE:
                            {
                                WriteData(Reader, Writer, ref ManagedBuffer);
                                break;
                            }

                        case IMDPROXY_REQ.IMDPROXY_REQ_CLOSE:
                            {
                                Trace.WriteLine("Closing connection.");
                                return;
                            }

                        default:
                            {
                                Trace.WriteLine($"Unsupported request code: {RequestCode}");
                                return;
                            }
                    }

                    // Trace.WriteLine("Sending response and waiting for next request.")

                    Writer.Seek(0, SeekOrigin.Begin);
                    {
                        var withBlock = (MemoryStream)Writer.BaseStream;
                        withBlock.WriteTo(TcpStream);
                        withBlock.SetLength(0L);
                    }
                }

                while (true);
            }

            Trace.WriteLine("Client disconnected.");
        }

        catch (Exception ex)
        {
            Trace.WriteLine($"Unhandled exception in service thread: {ex}");
            OnServiceUnhandledException(new ThreadExceptionEventArgs(ex));
        }

        finally
        {
            OnServiceShutdown(EventArgs.Empty);

        }
    }

    private void SendInfo(BinaryWriter Writer)
    {

        Writer.Write((ulong)DevioProvider.Length);
        Writer.Write((ulong)REQUIRED_ALIGNMENT);
        Writer.Write((ulong)(DevioProvider.CanWrite ? IMDPROXY_FLAGS.IMDPROXY_FLAG_NONE : IMDPROXY_FLAGS.IMDPROXY_FLAG_RO));

    }

    private void ReadData(BinaryReader Reader, BinaryWriter Writer, ref byte[]? Data)
    {

        var Offset = Reader.ReadInt64();
        var ReadLength = (int)Reader.ReadUInt64();
        if (Data is null || Data.Length < ReadLength)
        {
            Array.Resize(ref Data, ReadLength);
        }
        ulong WriteLength;
        ulong ErrorCode;

        try
        {
            WriteLength = (ulong)DevioProvider.Read(Data, 0, ReadLength, Offset);
            ErrorCode = 0UL;
        }

        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Read request at {Offset:X8} for {ReadLength} bytes.");
            ErrorCode = 1UL;
            WriteLength = 0UL;

        }

        Writer.Write(ErrorCode);
        Writer.Write(WriteLength);
        if (WriteLength > 0m)
        {
            Writer.Write(Data, 0, (int)WriteLength);
        }
    }

    private void WriteData(BinaryReader Reader, BinaryWriter Writer, ref byte[]? Data)
    {

        var Offset = Reader.ReadInt64();
        var Length = Reader.ReadUInt64();
        if (Data is null || Data.Length < (decimal)Length)
        {
            Array.Resize(ref Data, (int)Length);
        }

        var ReadLength = Reader.Read(Data, 0, (int)Length);
        ulong WriteLength;
        ulong ErrorCode;

        try
        {
            WriteLength = (ulong)DevioProvider.Write(Data, 0, ReadLength, Offset);
            ErrorCode = 0UL;
        }

        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Trace.WriteLine($"Write request at {Offset:X8} for {Length} bytes.");
            ErrorCode = 1UL;
            WriteLength = 0UL;

        }

        Writer.Write(ErrorCode);
        Writer.Write(WriteLength);

    }

    protected override string ProxyObjectName
    {
        get
        {
            var EndPoint = ListenEndPoint;
            if (EndPoint.Address.Equals(IPAddress.Any))
            {
                EndPoint = new IPEndPoint(IPAddress.Loopback, EndPoint.Port);
            }
            return EndPoint.ToString();
        }
    }

    protected override DeviceFlags ProxyModeFlags => DeviceFlags.TypeProxy | DeviceFlags.ProxyTypeTCP;

    protected override void EmergencyStopServiceThread() => InternalShutdownRequestAction?.Invoke();
}