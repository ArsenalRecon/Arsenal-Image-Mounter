using Arsenal.ImageMounter.Extensions;
using System;
using System.Runtime.InteropServices;
using static Arsenal.ImageMounter.IO.Native.NativeFileIO;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Reflection;

public static class NativeLib
{

#if NETSTANDARD || NETCOREAPP
    public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static TDelegate GetProcAddress<TDelegate>(IntPtr hModule, string procedureName) where TDelegate : Delegate
        => Marshal.GetDelegateForFunctionPointer<TDelegate>(NativeLibrary.GetExport(hModule, procedureName));

    public static TDelegate? GetProcAddressNoThrow<TDelegate>(IntPtr hModule, string procedureName) where TDelegate : Delegate
        => NativeLibrary.TryGetExport(hModule, procedureName, out var fptr)
            ? Marshal.GetDelegateForFunctionPointer<TDelegate>(fptr)
            : null;

    public static TDelegate GetProcAddress<TDelegate>(string moduleName, string procedureName)
    {
        var hModule = NativeLibrary.Load(moduleName);

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(NativeLibrary.GetExport(hModule, procedureName));
    }

    public static IntPtr GetProcAddressNoThrow(string moduleName, string procedureName)
    {
        if (!NativeLibrary.TryLoad(moduleName, out var hModule))
        {
            return default;
        }

        return !NativeLibrary.TryGetExport(hModule, procedureName, out var address) ? default : address;
    }

#else

    public static bool IsWindows { get; } = true;

    public static TDelegate GetProcAddress<TDelegate>(IntPtr hModule, string procedureName) where TDelegate : Delegate
        => Marshal.GetDelegateForFunctionPointer<TDelegate>(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)));

    public static TDelegate? GetProcAddressNoThrow<TDelegate>(IntPtr hModule, string procedureName) where TDelegate : Delegate
    {
        var fptr = UnsafeNativeMethods.GetProcAddress(hModule, procedureName);

        return fptr == default ? null : Marshal.GetDelegateForFunctionPointer<TDelegate>(fptr);
    }

    public static TDelegate GetProcAddress<TDelegate>(ReadOnlyMemory<char> moduleName, string procedureName) where TDelegate : Delegate
    {
        var hModule = Win32Try(UnsafeNativeMethods.LoadLibraryW(moduleName.MakeNullTerminated()));

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)));
    }

    public static IntPtr GetProcAddressNoThrow(string moduleName, string procedureName)
    {
        var hModule = UnsafeNativeMethods.LoadLibraryW(MemoryMarshal.GetReference(moduleName.AsSpan()));

        return hModule == default ? default : UnsafeNativeMethods.GetProcAddress(hModule, procedureName);
    }

#endif

    public static TDelegate? GetProcAddressNoThrow<TDelegate>(string moduleName, string procedureName) where TDelegate : Delegate
    {
        var fptr = GetProcAddressNoThrow(moduleName, procedureName);

        return fptr == default ? null : Marshal.GetDelegateForFunctionPointer<TDelegate>(fptr);
    }
}