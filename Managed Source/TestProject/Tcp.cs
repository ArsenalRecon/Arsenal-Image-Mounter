using Arsenal.ImageMounter.Devio.Client;
using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Devio.Server.Services;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class Tcp
{
    [Fact]
    public async Task TestTcpStream()
    {
        using var storage = new MemoryStream(new byte[200 << 20]);

        using var provider = new DevioProviderFromStream(storage, ownsStream: true);

        using var server = new DevioTcpService(9000, provider, ownsProvider: true);

        server.StartServiceThread();

        using var client = await DevioTcpStream.ConnectAsync("localhost", 9000, read_only: false, CancellationToken.None);

        var testdata1 = "TESTDATA1"u8.ToArray();

        client.Position = 512;
        client.Write(testdata1);

        storage.Position = 512;
        var buffer = storage.ReadExactly(testdata1.Length);
        Assert.Equal(testdata1, buffer);

        var testdata2 = "TESTDATA2"u8.ToArray();

        storage.Position = 1024;
        storage.Write(testdata2);

        client.Position = 1024;
        buffer = client.ReadExactly(testdata2.Length);
        Assert.Equal(testdata2, buffer);
    }

    [Fact]
    public async Task TestTcpV6Stream()
    {
        using var storage = new MemoryStream(new byte[200 << 20]);

        using var provider = new DevioProviderFromStream(storage, ownsStream: true);

        using var server = new DevioTcpService(IPAddress.IPv6Any, 9000, provider, ownsProvider: true);

        server.StartServiceThread();

        using var tcpClient = new TcpClient(AddressFamily.InterNetworkV6);

        await tcpClient.ConnectAsync(IPAddress.IPv6Loopback, 9000);

        using var client = new DevioTcpStream(tcpClient, read_only: false);

        var testdata1 = "TESTDATA1"u8.ToArray();

        client.Position = 512;
        client.Write(testdata1);

        storage.Position = 512;
        var buffer = storage.ReadExactly(testdata1.Length);
        Assert.Equal(testdata1, buffer);

        var testdata2 = "TESTDATA2"u8.ToArray();

        storage.Position = 1024;
        storage.Write(testdata2);

        client.Position = 1024;
        buffer = client.ReadExactly(testdata2.Length);
        Assert.Equal(testdata2, buffer);
    }
}
