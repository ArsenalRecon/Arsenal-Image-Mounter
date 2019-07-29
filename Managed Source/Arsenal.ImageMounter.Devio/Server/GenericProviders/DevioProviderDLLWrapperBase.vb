
''''' DevioProviderDLLWrapperBase.vb
''''' Proxy provider that implements devio proxy service with an unmanaged DLL written
''''' for use with devio.exe command line tool.
''''' 
''''' Copyright (c) 2012-2019, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Namespace Server.GenericProviders

    ''' <summary>
    ''' Class that implements <see>IDevioProvider</see> interface with an unmanaged DLL
    ''' written for use with devio.exe command line tool.
    ''' object as storage backend.
    ''' </summary>
    Public MustInherit Class DevioProviderDLLWrapperBase
        Inherits DevioProviderUnmanagedBase

#Region "SafeHandle"
        Public Class SafeDevioProviderDLLHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Protected Friend Property DLLClose As DLLCloseMethod

            Public Sub New(handle As IntPtr, ownsHandle As Boolean)
                MyBase.New(ownsHandle)

                SetHandle(handle)
            End Sub

            Protected Sub New()
                MyBase.New(True)

            End Sub

            Protected Overrides Function ReleaseHandle() As Boolean
                If _DLLClose Is Nothing Then
                    Return True
                End If
                Return _DLLClose(handle) <> 0
            End Function
        End Class
#End Region

        Protected Sub New(open As DLLOpenMethod, filename As String, [readOnly] As Boolean)

            Dim DLLClose As DLLCloseMethod = Nothing

            _SafeHandle = open(filename, [readOnly], _DLLRead, _DLLWrite, DLLClose, _Length)

            If _SafeHandle.IsInvalid OrElse _SafeHandle.IsClosed Then
                Throw New IOException($"Error opening '{filename}'", GetLastErrorAsException())
            End If

            _SafeHandle.DLLClose = DLLClose

            _CanWrite = Not [readOnly]

        End Sub

        Public ReadOnly Property SafeHandle As SafeDevioProviderDLLHandle

        Public Overrides ReadOnly Property Length As Long

        Public Overrides ReadOnly Property CanWrite As Boolean

        Public Overridable ReadOnly Property DLLRead As DLLReadWriteMethod

        Public Overridable ReadOnly Property DLLWrite As DLLReadWriteMethod

        Public Delegate Function DLLOpenMethod(<MarshalAs(UnmanagedType.LPStr), [In]> filename As String,
                                               <MarshalAs(UnmanagedType.Bool)> read_only As Boolean,
                                               <MarshalAs(UnmanagedType.FunctionPtr), Out> ByRef dllread As DLLReadWriteMethod,
                                               <MarshalAs(UnmanagedType.FunctionPtr), Out> ByRef dllwrite As DLLReadWriteMethod,
                                               <MarshalAs(UnmanagedType.FunctionPtr), Out> ByRef dllclose As DLLCloseMethod,
                                               <Out> ByRef size As Long) As SafeDevioProviderDLLHandle

        Public Delegate Function DLLReadWriteMethod(handle As SafeDevioProviderDLLHandle,
                                                    buffer As IntPtr,
                                                    size As Integer,
                                                    offset As Long) As Integer

        Public Delegate Function DLLCloseMethod(handle As IntPtr) As Integer

        Protected Overridable Function GetLastErrorAsException() As Exception

            Return New Win32Exception()

        End Function

        Public Overrides Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            Return DLLRead(SafeHandle, buffer + bufferoffset, count, fileoffset)

        End Function

        Public Overrides Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            Return DLLWrite(SafeHandle, buffer + bufferoffset, count, fileoffset)

        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _SafeHandle?.Dispose()
            End If

            _SafeHandle = Nothing

            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
