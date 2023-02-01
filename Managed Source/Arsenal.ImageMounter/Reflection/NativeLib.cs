//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using System;
using System.Runtime.InteropServices;
using static Arsenal.ImageMounter.IO.Native.NativeFileIO;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Reflection;

public static class NativeLib
{
    public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

#if NETSTANDARD || NETCOREAPP

    public static TDelegate GetProcAddress<TDelegate>(nint hModule, string procedureName) where TDelegate : Delegate
        => Marshal.GetDelegateForFunctionPointer<TDelegate>(NativeLibrary.GetExport(hModule, procedureName));

    public static TDelegate? GetProcAddressNoThrow<TDelegate>(nint hModule, string procedureName) where TDelegate : Delegate
        => NativeLibrary.TryGetExport(hModule, procedureName, out var fptr)
            ? Marshal.GetDelegateForFunctionPointer<TDelegate>(fptr)
            : null;

    public static TDelegate GetProcAddress<TDelegate>(string moduleName, string procedureName)
    {
        var hModule = NativeLibrary.Load(moduleName);

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(NativeLibrary.GetExport(hModule, procedureName));
    }

    public static nint GetProcAddressNoThrow(string moduleName, string procedureName)
    {
        if (!NativeLibrary.TryLoad(moduleName, out var hModule))
        {
            return default;
        }

        return !NativeLibrary.TryGetExport(hModule, procedureName, out var address) ? default : address;
    }

#else

    public static TDelegate GetProcAddress<TDelegate>(nint hModule, string procedureName) where TDelegate : Delegate
        => Marshal.GetDelegateForFunctionPointer<TDelegate>(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)));

    public static TDelegate? GetProcAddressNoThrow<TDelegate>(nint hModule, string procedureName) where TDelegate : Delegate
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        var fptr = UnsafeNativeMethods.GetProcAddress(hModule, procedureName);

        return fptr == default ? null : Marshal.GetDelegateForFunctionPointer<TDelegate>(fptr);
    }

    public static TDelegate GetProcAddress<TDelegate>(string moduleName, string procedureName) where TDelegate : Delegate
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException();
        }

        var hModule = Win32Try(UnsafeNativeMethods.LoadLibraryW(moduleName.AsRef()));

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)));
    }

    public static nint GetProcAddressNoThrow(string moduleName, string procedureName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return 0;
        }

        var hModule = UnsafeNativeMethods.LoadLibraryW(moduleName.AsRef());

        return hModule == default ? default : UnsafeNativeMethods.GetProcAddress(hModule, procedureName);
    }

#endif

    public static TDelegate? GetProcAddressNoThrow<TDelegate>(string moduleName, string procedureName) where TDelegate : Delegate
    {
        var fptr = GetProcAddressNoThrow(moduleName, procedureName);

        return fptr == default ? null : Marshal.GetDelegateForFunctionPointer<TDelegate>(fptr);
    }
}