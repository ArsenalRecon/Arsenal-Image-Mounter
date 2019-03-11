
''''' DevioProviderLibEwf.vb
''''' 
''''' Copyright (c) 2012-2019, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports System.IO.Pipes

Namespace Server.SpecializedProviders

    Public Class DevioProviderLibAFF4
        Inherits DevioProviderDLLWrapperBase

        <DllImport("libaff4_devio.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Public Shared Function dllopen(<MarshalAs(UnmanagedType.LPStr), [In]> filename As String,
                                       <MarshalAs(UnmanagedType.Bool)> read_only As Boolean,
                                       <MarshalAs(UnmanagedType.FunctionPtr), Out> ByRef dllread As DLLReadWriteMethod,
                                       <MarshalAs(UnmanagedType.FunctionPtr), Out> ByRef dllwrite As DLLReadWriteMethod,
                                       <MarshalAs(UnmanagedType.FunctionPtr), Out> ByRef dllclose As DLLCloseMethod,
                                       <Out> ByRef size As Long) As SafeDevioProviderDLLHandle
        End Function

        <DllImport("libaff4_devio.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Public Shared Function getsectorsize(handle As SafeDevioProviderDLLHandle) As UInteger
        End Function

        <DllImport("libaff4_devio.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Public Shared Function getlasterrorcode() As Integer
        End Function

        <DllImport("libaff4_devio.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Public Shared Function geterrormessage(errorcode As Integer) As <MarshalAs(UnmanagedType.LPStr)> String
        End Function

        <DllImport("libaff4_devio.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Public Shared Function getimagecount(<MarshalAs(UnmanagedType.LPStr), [In]> containerfile As String) As UInteger
        End Function

        Public Sub New(filename As String)
            MyBase.New(AddressOf dllopen, filename, [readOnly]:=True)

            _SectorSize = getsectorsize(SafeHandle)
        End Sub

        Public Overrides ReadOnly Property SectorSize As UInteger

        Protected Overrides Function GetLastErrorAsException() As Exception

            Return New IOException(geterrormessage(getlasterrorcode()))

        End Function

    End Class

End Namespace
