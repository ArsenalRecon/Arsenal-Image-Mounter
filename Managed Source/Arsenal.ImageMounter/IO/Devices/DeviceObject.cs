//  DeviceObject.vb
//  Base class for Arsenal Image Mounter SCSI Miniport objects.
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;



namespace Arsenal.ImageMounter.IO.Devices;

/// <summary>
/// Base class that represents Arsenal Image Mounter SCSI miniport created device objects.
/// </summary>
public abstract class DeviceObject : IDisposable
{

    public SafeFileHandle SafeFileHandle { get; }

    public FileAccess AccessMode { get; }

    /// <summary>
    /// Opens specified Path with CreateFile Win32 API and encapsulates the returned handle
    /// in a new DeviceObject.
    /// </summary>
    /// <param name="Path">Path to pass to CreateFile API</param>
    protected DeviceObject(string Path)
        : this(NativeStruct.OpenFileHandle(Path, 0, FileShare.ReadWrite, FileMode.Open, Overlapped: false), 0)
    {
    }

    /// <summary>
    /// Opens specified Path with CreateFile Win32 API and encapsulates the returned handle
    /// in a new DeviceObject.
    /// </summary>
    /// <param name="Path">Path to pass to CreateFile API</param>
    /// <param name="AccessMode">Access mode for opening and for underlying FileStream</param>
    protected DeviceObject(string Path, FileAccess AccessMode)
        : this(NativeStruct.OpenFileHandle(Path, AccessMode, FileShare.ReadWrite, FileMode.Open, Overlapped: false), AccessMode)
    {
    }

    /// <summary>
    /// Encapsulates a handle in a new DeviceObject.
    /// </summary>
    /// <param name="Handle">Existing handle to use</param>
    /// <param name="Access">Access mode for underlying FileStream</param>
    protected DeviceObject(SafeFileHandle Handle, FileAccess Access)
    {
        SafeFileHandle = Handle;
        AccessMode = Access;
    }

    #region IDisposable Support
    private bool disposedValue; // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                SafeFileHandle?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

            // TODO: set large fields to null.
        }

        disposedValue = true;
    }

    // TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    ~DeviceObject()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(false);
    }

    /// <summary>
    /// Close device object.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion

}