using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Arsenal.ImageMounter.IO.Net;

public static class IPSupport
{
    public static IEnumerable<IPAddress> EnumerateLocalIPv4Addresses()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(intf => !intf.IsReceiveOnly && intf.OperationalStatus == OperationalStatus.Up)
            .SelectMany(intf => intf.GetIPProperties().UnicastAddresses
                .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork
                    && !addr.Address.GetAddressBytes().SequenceEqual(loopbackAddressBytes))
                .Select(addr => addr.Address));

    public static IEnumerable<IPAddress> EnumerateLocalIPv6Addresses()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(intf => !intf.IsReceiveOnly && intf.OperationalStatus == OperationalStatus.Up)
            .SelectMany(intf => intf.GetIPProperties().UnicastAddresses
                .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6
                    && !addr.Address.GetAddressBytes().SequenceEqual(loopbackAddressV6Bytes))
                .Select(addr => addr.Address));

    private static readonly ImmutableArray<byte> loopbackAddressBytes = [.. IPAddress.Loopback.GetAddressBytes()];

    private static readonly ImmutableArray<byte> loopbackAddressV6Bytes = [.. IPAddress.IPv6Loopback.GetAddressBytes()];

    public static IPAddress GetIPv6AllNodesLinkLocalMulticastAddress(long scopeId)
    {
        var address = IPAddress.Parse("ff02::1");
        address.ScopeId = scopeId;
        return address;
    }
}
