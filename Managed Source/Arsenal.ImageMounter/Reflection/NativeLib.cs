using Arsenal.ImageMounter.Extensions;
using System;
using System.Runtime.InteropServices;
using static Arsenal.ImageMounter.IO.Native.NativeFileIO;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Reflection;

public static class NativeLib
{

#if NETSTANDARD || NETCOREAPP
    public static bool IsWindows { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static Delegate GetProcAddress(IntPtr hModule, string procedureName, Type delegateType)
        => Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(hModule, procedureName), delegateType);

    public static Delegate? GetProcAddressNoThrow(IntPtr hModule, string procedureName, Type delegateType)
        => NativeLibrary.TryGetExport(hModule, procedureName, out var fptr)
            ? Marshal.GetDelegateForFunctionPointer(fptr, delegateType)
            : null;

    public static Delegate GetProcAddress(string moduleName, string procedureName, Type delegateType)
    {

        var hModule = NativeLibrary.Load(moduleName);

        return Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(hModule, procedureName), delegateType);

    }

    public static IntPtr GetProcAddressNoThrow(ReadOnlyMemory<char> moduleName, string procedureName)
    {

        if (!NativeLibrary.TryLoad(moduleName.ToString(), out var hModule))
        {
            return default;
        }

        return !NativeLibrary.TryGetExport(hModule, procedureName, out var address) ? default : address;
    }

#else

    public static bool IsWindows { get; private set; } = true;

    public static Delegate GetProcAddress(IntPtr hModule, string procedureName, Type delegateType)
        => Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType);

    public static Delegate? GetProcAddressNoThrow(IntPtr hModule, string procedureName, Type delegateType)
    {

        var fptr = UnsafeNativeMethods.GetProcAddress(hModule, procedureName);

        return fptr == default ? null : Marshal.GetDelegateForFunctionPointer(fptr, delegateType);
    }

    public static Delegate GetProcAddress(ReadOnlyMemory<char> moduleName, string procedureName, Type delegateType)
    {

        var hModule = Win32Try(UnsafeNativeMethods.LoadLibraryW(moduleName.MakeNullTerminated()));

        return Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType);

    }

    public static IntPtr GetProcAddressNoThrow(ReadOnlyMemory<char> moduleName, string procedureName)
    {

        var hModule = UnsafeNativeMethods.LoadLibraryW(moduleName.MakeNullTerminated());

        return hModule == default ? default : UnsafeNativeMethods.GetProcAddress(hModule, procedureName);
    }

#endif

    public static Delegate? GetProcAddressNoThrow(ReadOnlyMemory<char> moduleName, string procedureName, Type delegateType)
    {

        var fptr = GetProcAddressNoThrow(moduleName, procedureName);

        return fptr == default ? null : Marshal.GetDelegateForFunctionPointer(fptr, delegateType);
    }
}