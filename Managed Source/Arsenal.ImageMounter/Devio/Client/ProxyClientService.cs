using Arsenal.ImageMounter.Devio.Server.Services;
using System.IO;

namespace Arsenal.ImageMounter.Devio.Client;

/// <summary>
/// Client class used to instruct driver to connect to a proxy service, for example across network.
/// </summary>
/// <param name="remote">Proxy service to connect to. For <see cref="DeviceFlags.ProxyTypeTCP"/> proxy, this is the host name or ip address and port to connect to.</param>
/// <param name="proxyMode">One of the "ProxyType" flags of <see cref="DeviceFlags"/> specifying which kind of proxy service to connect to.</param>
/// <param name="description">Description of connection to display in user interfaces and similar.</param>
/// <param name="diskSize">Size of virtual disk when mounted, or specify zero to automatically get this information from proxy service.</param>
public class ProxyClientService(string remote, DeviceFlags proxyMode, string description, long diskSize)
    : DevioNoneService(remote, diskSize, FileAccess.Read)
{
    public override string? ProxyObjectName { get; } = remote;

    public override DeviceFlags ProxyModeFlags { get; } = proxyMode;

    public override string? Description { get; set; } = description;
}
