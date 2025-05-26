// 
//  DevioProviderLibEwf.vb
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

#pragma warning disable IDE0079 // Remove unnecessary suppression


namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible")]
public unsafe partial class DevioProviderLibAFF4(string filename)
    : DevioProviderDLLWrapperBase(
        dllopen,
        filename,
        readOnly: true,
        get_last_error: () => new IOException(geterrormessage(getlasterrorcode())))
{
#if NET7_0_OR_GREATER
    [LibraryImport("libaff4_devio")]
    [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static unsafe partial nint dllopen([MarshalAs(UnmanagedType.LPStr)] string filename,
                                              [MarshalAs(UnmanagedType.Bool)] bool read_only,
                                              out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllread,
                                              out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllwrite,
                                              out delegate* unmanaged[Cdecl]<nint, int> dllclose,
                                              out long size);

    [LibraryImport("libaff4_devio")]
    [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static partial uint getsectorsize(SafeDevioProviderDLLHandle handle);

    [LibraryImport("libaff4_devio")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int getlasterrorcode();

    [LibraryImport("libaff4_devio", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    [return: MarshalAs(UnmanagedType.LPStr)]
    public static partial string geterrormessage(int errorcode);

    [LibraryImport("libaff4_devio")]
    [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static partial uint getimagecount([MarshalAs(UnmanagedType.LPStr)] string containerfile);
#else
    [DllImport("libaff4_devio", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most likely analyzer bug")]
    public static extern unsafe SafeDevioProviderDLLHandle dllopen([MarshalAs(UnmanagedType.LPStr)] string filename,
                                                                   [MarshalAs(UnmanagedType.Bool)] bool read_only,
                                                                   out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllread,
                                                                   out delegate* unmanaged[Cdecl]<nint, nint, int, long, int> dllwrite,
                                                                   out delegate* unmanaged[Cdecl]<nint, int> dllclose,
                                                                   out long size);

    [DllImport("libaff4_devio", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    public static extern uint getsectorsize(SafeDevioProviderDLLHandle handle);

    [DllImport("libaff4_devio", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    public static extern int getlasterrorcode();

    [DllImport("libaff4_devio", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true, CharSet = CharSet.Ansi)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most likely analyzer bug")]
    [return: MarshalAs(UnmanagedType.LPStr)]
    public static extern string geterrormessage(int errorcode);

    [DllImport("libaff4_devio", CallingConvention = CallingConvention.Cdecl, ThrowOnUnmappableChar = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Most likely analyzer bug")]
    public static extern uint getimagecount([MarshalAs(UnmanagedType.LPStr)] string containerfile);
#endif

    public override bool SupportsParallel => true;

    public override uint SectorSize
    {
        get
        {
            var SectorSizeRet = getsectorsize(SafeHandle);

            if (SectorSizeRet == 0)
            {
                SectorSizeRet = 512U;
            }

            return SectorSizeRet;
        }
    }
}
