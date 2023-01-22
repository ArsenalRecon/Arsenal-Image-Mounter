// 
//  DevioProviderLibEwf.vb
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible")]
public partial class DevioProviderLibAFF4 : DevioProviderDLLWrapperBase
{
#if NET7_0_OR_GREATER
    [LibraryImport("libaff4_devio")]
    [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static partial SafeDevioProviderDLLHandle dllopen([MarshalAs(UnmanagedType.LPStr)] string filename, [MarshalAs(UnmanagedType.Bool)] bool read_only, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllread, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllwrite, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLCloseMethod dllclose, out long size);

    [LibraryImport("libaff4_devio")]
    [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static partial uint getsectorsize(SafeDevioProviderDLLHandle handle);

    [LibraryImport("libaff4_devio")]
    [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
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
    public static extern SafeDevioProviderDLLHandle dllopen([MarshalAs(UnmanagedType.LPStr)][In] string filename, [MarshalAs(UnmanagedType.Bool)] bool read_only, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllread, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllwrite, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLCloseMethod dllclose, out long size);

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
    public static extern uint getimagecount([MarshalAs(UnmanagedType.LPStr)][In] string containerfile);
#endif

    public DevioProviderLibAFF4(string filename)
        : base(dllopen, filename, readOnly: true, () => new IOException(geterrormessage(getlasterrorcode())))
    {

    }

    public override uint SectorSize
    {
        get
        {
            var SectorSizeRet = getsectorsize(SafeHandle);

            if (SectorSizeRet == 0L)
            {
                SectorSizeRet = 512U;
            }

            return SectorSizeRet;
        }
    }
}