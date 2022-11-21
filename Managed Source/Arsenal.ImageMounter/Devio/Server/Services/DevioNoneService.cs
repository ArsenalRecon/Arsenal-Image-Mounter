using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using System;
// '''' DevioNoneService.vb
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
using System.Runtime.Versioning;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.Services;

/// <summary>
/// Class deriving from DevioServiceBase, but without providing a proxy service. Instead,
/// it just passes a disk image file name or RAM disk information for direct mounting
/// internally in Arsenal Image Mounter SCSI Adapter.
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public class DevioNoneService : DevioServiceBase
{

    /// <summary>
    /// Name and path of image file mounted by Arsenal Image Mounter.
    /// </summary>
    public string? Imagefile { get; }

    /// <summary>
    /// FileAccess flags specifying whether to mount read-only or read-write.
    /// </summary>
    public FileAccess DiskAccess { get; }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk image file name for direct mounting internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="DiskAccess"></param>
    public DevioNoneService(string Imagefile, FileAccess DiskAccess)
        : this(Imagefile, NativeStruct.GetFileOrDiskSize(Imagefile), DiskAccess)
    {

    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk image file name for direct mounting internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="length">Disk size to initialize dummy provider instance</param>
    /// <param name="DiskAccess"></param>
    protected DevioNoneService(string Imagefile, long length, FileAccess DiskAccess)
        : base(new DummyProvider(length), OwnsProvider: true)
    {

        Offset = NativeStruct.GetOffsetByFileExt(Imagefile);

        this.DiskAccess = DiskAccess;

        this.Imagefile = Imagefile;

        if (!DiskAccess.HasFlag(FileAccess.Write))
        {
            ProxyModeFlags = DeviceFlags.TypeFile | DeviceFlags.ReadOnly;
        }
        else
        {
            ProxyModeFlags = DeviceFlags.TypeFile;
        }

        ProxyObjectName = Imagefile;

    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk image file name for direct mounting internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="DiskAccess"></param>
    public DevioNoneService(string Imagefile, DevioServiceFactory.VirtualDiskAccess DiskAccess)
        : this(Imagefile, DevioServiceFactory.GetDirectFileAccessFlags(DiskAccess))
    {

    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk image file name for direct mounting internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="length">Disk size to initialize dummy provider instance</param>
    /// <param name="DiskAccess"></param>
    protected DevioNoneService(string Imagefile, long length, DevioServiceFactory.VirtualDiskAccess DiskAccess)
        : this(Imagefile, length, DevioServiceFactory.GetDirectFileAccessFlags(DiskAccess))
    {

    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk size for directly mounting a RAM disk internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="DiskSize">Size in bytes of RAM disk to create.</param>
    public DevioNoneService(long DiskSize)
        : base(new DummyProvider(DiskSize), OwnsProvider: true)
    {

        DiskAccess = FileAccess.ReadWrite;

        if (NativeFileIO.TestFileOpen(@"\\?\awealloc"))
        {
            AdditionalFlags = DeviceFlags.TypeFile | DeviceFlags.FileTypeAwe;
        }
        else
        {
            AdditionalFlags = DeviceFlags.TypeVM;
        }
    }

    private static long GetVhdSize(string Imagefile)
    {

        using var disk = VirtualDisk.OpenDisk(Imagefile, FileAccess.Read);

        return disk.Capacity;

    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just requests the SCSI adapter, awealloc and vhdaccess drivers to create
    /// a dynamically expanding RAM disk based on the contents of the supplied VHD image.
    /// </summary>
    /// <param name="Imagefile">Path to VHD image file to use as template for the RAM disk.</param>
    public DevioNoneService(string Imagefile)
        : base(new DummyProvider(GetVhdSize(Imagefile)), OwnsProvider: true)
    {

        DiskAccess = FileAccess.ReadWrite;

        this.Imagefile = Imagefile;

        ProxyObjectName = $@"\\?\vhdaccess\??\awealloc{NativeFileIO.GetNtPath(Imagefile)}";

    }

    protected override string? ProxyObjectName { get; }

    protected override DeviceFlags ProxyModeFlags { get; }

    /// <summary>
    /// Dummy implementation that always returns True.
    /// </summary>
    /// <returns>Fixed value of True.</returns>
    public override bool StartServiceThread()
    {
        RunService();
        return true;
    }

    /// <summary>
    /// Dummy implementation that just raises ServiceReady event.
    /// </summary>
    public override void RunService() => OnServiceReady(EventArgs.Empty);

    public override void DismountAndStopServiceThread()
    {
        base.DismountAndStopServiceThread();
        OnServiceShutdown(EventArgs.Empty);
    }

    public override bool DismountAndStopServiceThread(TimeSpan timeout)
    {
        var rc = base.DismountAndStopServiceThread(timeout);
        OnServiceShutdown(EventArgs.Empty);
        return rc;
    }

    protected override void EmergencyStopServiceThread()
    {

    }
}