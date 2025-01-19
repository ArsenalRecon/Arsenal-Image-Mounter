//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;


#pragma warning disable 0649

namespace Arsenal.ImageMounter.IO.Native;

[Flags]
public enum NtObjectAccess
{
    Query = 0x0001,
    Traverse = 0x0002,
    CreateObject = 0x0004,
    CreateSubdirectory = 0x0008,
    AllAccess = 0x000F0000 | 0xF
}

[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public partial class NtDirectoryObject : IDisposable
{
    private bool disposedValue;

#if NET7_0_OR_GREATER
    [LibraryImport("ntdll", SetLastError = false)]
    private static partial int NtOpenDirectoryObject(out SafeFileHandle handle, NtObjectAccess access, in ObjectAttributes objectAttributes);

    [LibraryImport("ntdll", SetLastError = false)]
    private static partial int NtQueryDirectoryObject(
        SafeFileHandle DirectoryHandle,
        out byte buffer,
        int length,
        [MarshalAs(UnmanagedType.U1)] bool returnSingleEntry,
        [MarshalAs(UnmanagedType.U1)] bool restartScan,
        ref int context,
        out int returnLength);
#else
    [DllImport("ntdll", SetLastError = false)]
    private static extern int NtOpenDirectoryObject(out SafeFileHandle handle, NtObjectAccess access, in ObjectAttributes objectAttributes);

    [DllImport("ntdll", SetLastError = false)]
    private static extern int NtQueryDirectoryObject(
        SafeFileHandle DirectoryHandle,
        out byte buffer,
        int length,
        [MarshalAs(UnmanagedType.U1)] bool returnSingleEntry,
        [MarshalAs(UnmanagedType.U1)] bool restartScan,
        ref int context,
        out int returnLength);
#endif

    public SafeFileHandle Handle { get; }

    public NtDirectoryObject(string path, NtObjectAccess access)
        : this(path, access, null)
    {
    }

    public NtDirectoryObject(string path, NtObjectAccess access, NtDirectoryObject? root)
    {
        using var path_native = UnicodeString.Pin(path);

        var objectAttributes = new ObjectAttributes(root?.Handle, path_native, NtObjectAttributes.OpenIf, null, null);

        NativeFileIO.NtDllTry(NtOpenDirectoryObject(out var handle, access, objectAttributes));

        Handle = handle;
    }

    private readonly struct ObjectDirectoryInformation
    {
        public readonly UNICODE_STRING Name { get; }
        public readonly UNICODE_STRING TypeName { get; }
    }

    public static IEnumerable<(string Name, string TypeName)> EnumerateObjects(string path)
    {
        using var obj = new NtDirectoryObject(path, NtObjectAccess.Query | NtObjectAccess.Traverse);
        foreach (var result in obj.EnumerateObjects())
        {
            yield return result;
        }
    }

    public IEnumerable<(string Name, string TypeName)> EnumerateObjects()
    {
        var context = 0;
        for (var restartScan = true; ; restartScan = false)
        {
            var result = EnumerateNextObject(ref context, restartScan);
            if (string.IsNullOrWhiteSpace(result.TypeName))
            {
                yield break;
            }

            yield return result;
        }
    }

    private (string Name, string TypeName) EnumerateNextObject(ref int context, bool restartScan)
    {
        const int bufferSize = 600;
        Span<byte> buffer = stackalloc byte[bufferSize];
        var result = NtQueryDirectoryObject(Handle, out buffer[0], bufferSize, returnSingleEntry: true, restartScan, ref context, out var returnLength);

        if (result < 0)
        {
            return default;
        }

        var info = MemoryMarshal.Read<ObjectDirectoryInformation>(buffer);
        return (info.Name.ToString(), string.Intern(info.TypeName.ToString()));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                Handle?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer

            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~NtDirectoryObject()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
