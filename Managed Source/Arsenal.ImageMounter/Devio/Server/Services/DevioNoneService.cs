//  DevioNoneService.cs
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
using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.IO.Native;
using System;
using System.IO;
using System.Runtime.Versioning;

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
    /// <param name="imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="diskAccess"></param>
    public DevioNoneService(string imagefile, FileAccess diskAccess)
        : this(imagefile, NativeStruct.GetFileOrDiskSize(imagefile), diskAccess)
    {
    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk image file name for direct mounting internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="imageFile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="length">Disk size to initialize dummy provider instance</param>
    /// <param name="diskAccess"></param>
    protected DevioNoneService(string imageFile, long length, FileAccess diskAccess)
        : base(new DummyProvider(length), ownsProvider: true)
    {
        Offset = NativeStruct.GetOffsetByFileExt(imageFile);

        DiskAccess = diskAccess;

        Imagefile = imageFile;

        if (!diskAccess.HasFlag(FileAccess.Write))
        {
            ProxyModeFlags = DeviceFlags.TypeFile | DeviceFlags.ReadOnly;
        }
        else
        {
            ProxyModeFlags = DeviceFlags.TypeFile;
        }

        ProxyObjectName = imageFile;
    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk image file name for direct mounting internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="imageFile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="diskAccess"></param>
    public DevioNoneService(string imageFile, DevioServiceFactory.VirtualDiskAccess diskAccess)
        : this(imageFile, DevioServiceFactory.GetDirectFileAccessFlags(diskAccess))
    {
    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk image file name for direct mounting internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="imageFile">Name and path of image file mounted by Arsenal Image Mounter.</param>
    /// <param name="length">Disk size to initialize dummy provider instance</param>
    /// <param name="diskAccess"></param>
    protected DevioNoneService(string imageFile, long length, DevioServiceFactory.VirtualDiskAccess diskAccess)
        : this(imageFile, length, DevioServiceFactory.GetDirectFileAccessFlags(diskAccess))
    {
    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just passes a disk size for directly mounting a RAM disk internally in
    /// SCSI Adapter.
    /// </summary>
    /// <param name="diskSize">Size in bytes of RAM disk to create.</param>
    public DevioNoneService(long diskSize)
        : base(new DummyProvider(diskSize), ownsProvider: true)
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

    private static long GetVhdSize(string imageFile)
    {
        using var disk = new DiscUtils.Vhd.Disk(imageFile, FileAccess.Read);

        return disk.Capacity;
    }

    /// <summary>
    /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
    /// Instead, it just requests the SCSI adapter, awealloc and vhdaccess drivers to create
    /// a dynamically expanding RAM disk based on the contents of the supplied VHD image.
    /// </summary>
    /// <param name="imageFile">Path to VHD image file to use as template for the RAM disk.</param>
    public DevioNoneService(string imageFile)
        : base(new DummyProvider(GetVhdSize(imageFile)), ownsProvider: true)
    {
        DiskAccess = FileAccess.ReadWrite;

        Imagefile = imageFile;

        ProxyObjectName = $@"\\?\vhdaccess\??\awealloc{NativeFileIO.GetNtPath(imageFile)}";
    }

    public override string? ProxyObjectName { get; }

    public override DeviceFlags ProxyModeFlags { get; }

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
    public override void RunService()
        => OnServiceReady(EventArgs.Empty);

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