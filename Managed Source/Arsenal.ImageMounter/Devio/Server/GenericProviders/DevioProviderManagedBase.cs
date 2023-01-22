// 
//  DevioProviderManagedBase.vb
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Buffers;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

/// <summary>
/// Base class for implementing <see>IDevioProvider</see> interface with a storage backend where
/// bytes to read from and write to device are provided in a managed byte array.
/// </summary>
public abstract class DevioProviderManagedBase : IDevioProvider
{

    /// <summary>
    /// Event when object is about to be disposed
    /// </summary>
    public event EventHandler? Disposing;

    /// <summary>
    /// Event when object has been disposed
    /// </summary>
    public event EventHandler? Disposed;

    /// <summary>
    /// Determines whether virtual disk is writable or read-only.
    /// </summary>
    /// <value>True if virtual disk can be written to through this instance, or False
    /// if it is opened for reading only.</value>
    /// <returns>True if virtual disk can be written to through this instance, or False
    /// if it is opened for reading only.</returns>
    public abstract bool CanWrite { get; }

    /// <summary>
    /// Indicates whether provider supports shared image operations with registrations
    /// and reservations.
    /// </summary>
    public virtual bool SupportsShared { get; }

    /// <summary>
    /// Size of virtual disk.
    /// </summary>
    /// <value>Size of virtual disk.</value>
    /// <returns>Size of virtual disk.</returns>
    public abstract long Length { get; }

    /// <summary>
    /// Sector size of virtual disk.
    /// </summary>
    /// <value>Sector size of virtual disk.</value>
    /// <returns>Sector size of virtual disk.</returns>
    public abstract uint SectorSize { get; }

    /// <summary>
    /// Reads bytes from virtual disk to a byte array.
    /// </summary>
    /// <param name="buffer">Byte array with enough size where read bytes are stored.</param>
    /// <param name="bufferoffset">Offset in array where bytes are stored.</param>
    /// <param name="count">Number of bytes to read from virtual disk device.</param>
    /// <param name="fileoffset">Offset at virtual disk device where read starts.</param>
    /// <returns>Returns number of bytes read from device that were stored in byte array.</returns>
    public abstract int Read(byte[] buffer, int bufferoffset, int count, long fileoffset);

    int IDevioProvider.Read(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {

        var _byte_buffer = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            var readlen = Read(_byte_buffer, 0, count, fileoffset);
            Marshal.Copy(_byte_buffer, 0, buffer + bufferoffset, readlen);

            return readlen;
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(_byte_buffer);

        }
    }

    int IDevioProvider.Read(Span<byte> buffer, long fileoffset)
    {

        var _byte_buffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            var readlen = Read(_byte_buffer, 0, buffer.Length, fileoffset);
            _byte_buffer.AsSpan(0, readlen).CopyTo(buffer);

            return readlen;
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(_byte_buffer);

        }
    }

    /// <summary>
    /// Writes out bytes from byte array to virtual disk device.
    /// </summary>
    /// <param name="buffer">Byte array containing bytes to write out to device.</param>
    /// <param name="bufferoffset">Offset in array where bytes to write start.</param>
    /// <param name="count">Number of bytes to write to virtual disk device.</param>
    /// <param name="fileoffset">Offset at virtual disk device where write starts.</param>
    /// <returns>Returns number of bytes written to device.</returns>
    public abstract int Write(byte[] buffer, int bufferoffset, int count, long fileoffset);

    int IDevioProvider.Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {

        var _byte_buffer = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            Marshal.Copy(buffer + bufferoffset, _byte_buffer, 0, count);

            return Write(_byte_buffer, 0, count, fileoffset);
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(_byte_buffer);

        }
    }

    int IDevioProvider.Write(ReadOnlySpan<byte> buffer, long fileoffset)
    {

        var _byte_buffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            buffer.CopyTo(_byte_buffer);

            return Write(_byte_buffer, 0, buffer.Length, fileoffset);
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(_byte_buffer);

        }
    }

    /// <summary>
    /// Manage registrations and reservation keys for shared images.
    /// </summary>
    /// <param name="Request">Request data</param>
    /// <param name="Response">Response data</param>
    /// <param name="Keys">List of currently registered keys</param>
    public virtual void SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
        => throw new NotImplementedException();

    public bool IsDisposed { get; private set; } // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        OnDisposing(EventArgs.Empty);

        if (!IsDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }

            // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
            // TODO: set large fields to null.
        }

        IsDisposed = true;

        OnDisposed(EventArgs.Empty);
    }

    // TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
    ~DevioProviderManagedBase()
    {
        // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(false);
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Raises Disposing event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposing(EventArgs e) => Disposing?.Invoke(this, e);

    /// <summary>
    /// Raises Disposed event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposed(EventArgs e) => Disposed?.Invoke(this, e);

}