// '''' DevioProviderDLLWrapperBase.vb
// '''' Proxy provider that implements devio proxy service with an unmanaged DLL written
// '''' for use with devio.exe command line tool.
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// ''''

using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

/// <summary>
/// Class that implements <see>IDevioProvider</see> interface with an unmanaged DLL
/// written for use with devio.exe command line tool.
/// object as storage backend.
/// </summary>
public abstract class DevioProviderDLLWrapperBase : DevioProviderUnmanagedBase
{

    #region SafeHandle
    public class SafeDevioProviderDLLHandle : SafeHandleZeroOrMinusOneIsInvalid
    {

        protected internal DLLCloseMethod? DLLClose { get; set; }

        public SafeDevioProviderDLLHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {

            SetHandle(handle);
        }

        protected SafeDevioProviderDLLHandle()
            : base(true)
        {

        }

        protected override bool ReleaseHandle() => DLLClose is null || DLLClose(handle) != 0;
    }
    #endregion

    protected DevioProviderDLLWrapperBase(DLLOpenMethod open, string filename, bool readOnly)
        : this(open, filename, readOnly, null)
    {

    }

    protected DevioProviderDLLWrapperBase(DLLOpenMethod open, string filename, bool readOnly, Func<Exception>? get_last_error)
    {

        if (open is null)
        {
            throw new ArgumentNullException(nameof(open));
        }

        SafeHandle = open(filename, readOnly, out var dllRead, out var dllWrite, out var dllClose, out var length);

        if (SafeHandle.IsInvalid || SafeHandle.IsClosed)
        {
            throw new IOException($"Error opening '{filename}'", (get_last_error?.Invoke()) ?? new Win32Exception());
        }

        DLLRead = dllRead;
        DLLWrite = dllWrite;
        Length = length;
        SafeHandle.DLLClose = dllClose;

        CanWrite = !readOnly;

    }

    public SafeDevioProviderDLLHandle SafeHandle { get; }

    public override long Length { get; }

    public override bool CanWrite { get; }

    public virtual DLLReadWriteMethod DLLRead { get; }

    public virtual DLLReadWriteMethod DLLWrite { get; }

    public delegate SafeDevioProviderDLLHandle DLLOpenMethod([MarshalAs(UnmanagedType.LPStr)][In] string filename, [MarshalAs(UnmanagedType.Bool)] bool read_only, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllread, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllwrite, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLCloseMethod dllclose, out long size);

    public delegate int DLLReadWriteMethod(SafeDevioProviderDLLHandle handle, IntPtr buffer, int size, long offset);

    public delegate int DLLCloseMethod(IntPtr handle);

    public override int Read(IntPtr buffer, int bufferoffset, int count, long fileoffset)
        => DLLRead(SafeHandle, buffer + bufferoffset, count, fileoffset);

    public override int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
        => DLLWrite(SafeHandle, buffer + bufferoffset, count, fileoffset);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SafeHandle?.Dispose();
        }

        base.Dispose(disposing);
    }
}