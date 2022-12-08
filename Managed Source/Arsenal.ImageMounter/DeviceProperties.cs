//  ScsiAdapter.vb
//  Class for controlling Arsenal Image Mounter Devices.
//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using System.Runtime.Versioning;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter;

/// <summary>
/// Object storing properties for a virtual disk device. Returned by
/// QueryDevice() method.
/// </summary>
public sealed class DeviceProperties
{
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public DeviceProperties(ScsiAdapter adapter, uint deviceNumber)
    {
        adapter.QueryDevice(deviceNumber, out var diskSize, out var bytesPerSector, out var imageOffset, out var flags, out var filename, out var writeOverlayImageFile);

        DeviceNumber = deviceNumber;
        DiskSize = diskSize;
        BytesPerSector = bytesPerSector;
        ImageOffset = imageOffset;
        Flags = flags;
        Filename = filename;
        WriteOverlayImageFile = writeOverlayImageFile;
    }

    /// <summary>Device number of virtual disk.</summary>
    public uint DeviceNumber { get; }

    /// <summary>Size of virtual disk.</summary>
    public long DiskSize { get; }
    /// <summary>Number of bytes per sector for virtual disk geometry.</summary>
    public uint BytesPerSector { get; }
    /// <summary>A skip offset if virtual disk data does not begin immediately at start of disk image file.
    /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    /// or Windows filesystem drivers.</summary>
    public long ImageOffset { get; }
    /// <summary>Flags specifying properties for virtual disk. See comments for each flag value.</summary>
    public DeviceFlags Flags { get; }
    /// <summary>Name of disk image file holding storage for file type virtual disk or used to create a
    /// virtual memory type virtual disk.</summary>
    public string? Filename { get; set; }

    /// <summary>Path to differencing file used in write-temporary mode.</summary>
    public string? WriteOverlayImageFile { get; }
}
