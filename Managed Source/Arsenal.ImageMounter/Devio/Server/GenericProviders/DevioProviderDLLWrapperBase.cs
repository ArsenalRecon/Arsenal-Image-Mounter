//  DevioProviderDLLWrapperBase.vb
//  Proxy provider that implements devio proxy service with an unmanaged DLL written
//  for use with devio.exe command line tool.
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// 

using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1510 // Use ArgumentNullException throw helper

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
        protected internal unsafe delegate* unmanaged[Cdecl]<nint, int> DLLClose { get; set; }

        public SafeDevioProviderDLLHandle(nint handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        protected SafeDevioProviderDLLHandle()
            : base(true)
        {
        }

        protected override unsafe bool ReleaseHandle() => DLLClose is null || DLLClose(handle) != 0;
    }
    #endregion

    protected DevioProviderDLLWrapperBase(DLLOpenMethod open, string filename, bool readOnly)
        : this(open, filename, readOnly, null)
    {
    }

    protected DevioProviderDLLWrapperBase(DLLOpenMethodNIntRet open, string filename, bool readOnly)
        : this(open, filename, readOnly, null)
    {
    }

    protected unsafe DevioProviderDLLWrapperBase(DLLOpenMethodNIntRet open, string filename, bool readOnly, Func<Exception>? get_last_error)
        : this(GetDLLOpenWrapper(open), filename, readOnly, get_last_error)
    {
    }

    private static unsafe DLLOpenMethod GetDLLOpenWrapper(DLLOpenMethodNIntRet open)
    {
        SafeDevioProviderDLLHandle DLLOpenWrapper(string filename,
                                                  bool read_only,
                                                  out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllread,
                                                  out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllwrite,
                                                  out delegate* unmanaged[Cdecl]<nint, int> dllclose,
                                                  out long size)
        {
            var handle = open(filename, read_only, out dllread, out dllwrite, out dllclose, out size);
            return new(handle, ownsHandle: true);
        }

        return DLLOpenWrapper;
    }

    protected unsafe DevioProviderDLLWrapperBase(DLLOpenMethod open, string filename, bool readOnly, Func<Exception>? get_last_error)
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

    protected internal unsafe delegate* unmanaged[Cdecl]<nint, nint, int, long, int> DLLRead { get; }

    protected internal unsafe delegate* unmanaged[Cdecl]<nint, nint, int, long, int> DLLWrite { get; }

    public unsafe delegate SafeDevioProviderDLLHandle DLLOpenMethod([MarshalAs(UnmanagedType.LPStr)] string filename,
                                                                    [MarshalAs(UnmanagedType.Bool)] bool read_only,
                                                                    out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllread,
                                                                    out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllwrite,
                                                                    out delegate* unmanaged[Cdecl]<nint, int> dllclose,
                                                                    out long size);

    public unsafe delegate nint DLLOpenMethodNIntRet([MarshalAs(UnmanagedType.LPStr)] string filename,
                                                     [MarshalAs(UnmanagedType.Bool)] bool read_only,
                                                     out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllread,
                                                     out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllwrite,
                                                     out delegate* unmanaged[Cdecl]<nint, int> dllclose,
                                                     out long size);

    public override unsafe int Read(nint buffer, int bufferoffset, int count, long fileoffset)
    {
        var ptrSuccess = false;

        try
        {
            SafeHandle.DangerousAddRef(ref ptrSuccess);

            return DLLRead(SafeHandle.DangerousGetHandle(), buffer + bufferoffset, count, fileoffset);
        }
        finally
        {
            if (ptrSuccess)
            {
                SafeHandle.DangerousRelease();
            }
        }
    }

    public override unsafe int Write(nint buffer, int bufferoffset, int count, long fileoffset)
    {
        var ptrSuccess = false;

        try
        {
            SafeHandle.DangerousAddRef(ref ptrSuccess);

            return DLLWrite(SafeHandle.DangerousGetHandle(), buffer + bufferoffset, count, fileoffset);
        }
        finally
        {
            if (ptrSuccess)
            {
                SafeHandle.DangerousRelease();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SafeHandle?.Dispose();
        }

        base.Dispose(disposing);
    }
}
