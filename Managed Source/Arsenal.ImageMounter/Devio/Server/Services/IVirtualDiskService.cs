//  DevioServiceBase.vb
//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Devices;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter.Devio.Server.Services;

public interface IVirtualDiskService : IDisposable
{
    event EventHandler? ServiceShutdown;

    event EventHandler? ServiceStopping;

    event ThreadExceptionEventHandler? ServiceUnhandledException;
    
    event EventHandler? DiffDeviceFailed;

    bool IsDisposed { get; }

    bool HasDiskDevice { get; }

    uint SectorSize { get; }

    long DiskSize { get; }

    string? Description { get; }

    DiskDevice? OpenDiskDevice(FileAccess access);

    Stream? OpenDiskStream(FileAccess access);

    Stream? OpenVolumeStream(string volumeName, FileAccess access);

    string? GetDiskDeviceName();

    void RemoveDevice();

    void RemoveDeviceSafe();

    bool WaitForExit(TimeSpan millisecondsTimeout);

    ValueTask<bool> WaitForExitAsync(TimeSpan millisecondsTimeout);
}
