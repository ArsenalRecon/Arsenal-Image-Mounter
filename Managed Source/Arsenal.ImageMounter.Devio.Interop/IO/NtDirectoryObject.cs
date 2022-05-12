using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Arsenal.ImageMounter.IO.NativeStruct;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO;

[Flags]
public enum NtObjectAccess
{
    Query = 0x0001,
    Traverse = 0x0002,
    CreateObject = 0x0004,
    CreateSubdirectory = 0x0008,
    AllAccess = 0x000F0000 | 0xF
}

public class NtDirectoryObject : IDisposable
{
    private bool disposedValue;

    [DllImport("ntdll", SetLastError = false)]
    private static extern int NtOpenDirectoryObject(out SafeFileHandle handle, NtObjectAccess access, in ObjectAttributes objectAttributes);

    [DllImport("ntdll", SetLastError = false)]
    unsafe private static extern int NtQueryDirectoryObject(
        SafeFileHandle DirectoryHandle,
        byte* buffer,
        int length,
        [MarshalAs(UnmanagedType.U1)] bool returnSingleEntry,
        [MarshalAs(UnmanagedType.U1)] bool restartScan,
        ref int context,
        out int returnLength);

    public SafeFileHandle Handle { get; }

    public NtDirectoryObject(string path, NtObjectAccess access)
        : this(path, access, null)
    { }

    unsafe public NtDirectoryObject(string path, NtObjectAccess access, NtDirectoryObject root)
    {
        using var pinnedpathstr = new PinnedString(path);

        var pathstr = pinnedpathstr.UnicodeString;

        var objectAttributes = new ObjectAttributes(
            root?.Handle?.DangerousGetHandle() ?? IntPtr.Zero,
            new IntPtr(&pathstr),
            NtObjectAttributes.OpenIf,
            IntPtr.Zero,
            IntPtr.Zero);

        NativeFileIO.NtDllTry(NtOpenDirectoryObject(out var handle, access, in objectAttributes));

        Handle = handle;
    }

    private struct ObjectDirectoryInformation
    {
        public readonly UNICODE_STRING Name;
        public readonly UNICODE_STRING TypeName;
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

    unsafe private (string Name, string TypeName) EnumerateNextObject(ref int context, bool restartScan)
    {
        const int bufferSize = 600;
        var buffer = stackalloc byte[bufferSize];
        var result = NtQueryDirectoryObject(Handle, buffer, bufferSize, returnSingleEntry: true, restartScan, ref context, out var returnLength);
        
        if (result < 0)
        {
            return default;
        }

        var info = *(ObjectDirectoryInformation*)buffer;
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
