
''''' DebugProvider.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders

Namespace Server.SpecializedProviders

    ''' <summary>
    ''' A class to support test cases to verify that correct data is received through providers
    ''' compared to raw image files.
    ''' </summary>
    Public Class DebugProvider
        Inherits DevioProviderManagedBase

        Public ReadOnly Property BaseProvider As IDevioProvider

        Public ReadOnly Property DebugCompareStream As Stream

        Public Sub New(BaseProvider As IDevioProvider, DebugCompareStream As Stream)
            If BaseProvider Is Nothing Then
                Throw New ArgumentNullException(NameOf(BaseProvider))
            End If

            If DebugCompareStream Is Nothing Then
                Throw New ArgumentNullException(NameOf(DebugCompareStream))
            End If

            If (Not DebugCompareStream.CanSeek) OrElse (Not DebugCompareStream.CanRead) Then
                Throw New ArgumentException("Debug compare stream must support seek and read operations.", NameOf(DebugCompareStream))
            End If

            Me.BaseProvider = BaseProvider
            Me.DebugCompareStream = DebugCompareStream

        End Sub

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return BaseProvider.CanWrite
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Return BaseProvider.Length
            End Get
        End Property

        Public Overrides ReadOnly Property SectorSize As UInteger
            Get
                Return BaseProvider.SectorSize
            End Get
        End Property

        Private Declare Function memcmp Lib "msvcrt.dll" (<[In]> buf1 As Byte(), <[In]> buf2 As Byte(), count As IntPtr) As Integer
        Private Declare Function memcmp Lib "msvcrt.dll" (<[In]> buf1 As Byte(), buf2 As IntPtr, count As IntPtr) As Integer
        Private Declare Function memcmp Lib "msvcrt.dll" (buf1 As IntPtr, <[In]> buf2 As Byte(), count As IntPtr) As Integer
        Private Declare Function memcmp Lib "msvcrt.dll" (buf1 As IntPtr, buf2 As IntPtr, count As IntPtr) As Integer

        Public Overrides Function Read(buf1 As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            Static buf2 As Byte()

            Dim compareTask = Task.Factory.StartNew(
                Function()
                    If buf2 Is Nothing OrElse buf2.Length < count Then
                        Array.Resize(buf2, count)
                    End If
                    DebugCompareStream.Position = fileoffset
                    Return DebugCompareStream.Read(buf2, 0, count)
                End Function, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default)

            Dim rc1 = BaseProvider.Read(buf1, bufferoffset, count, fileoffset)
            Dim rc2 = compareTask.Result

            If rc1 <> rc2 Then
                Trace.WriteLine($"Read request at position 0x{fileoffset:X}, 0x{count:X)} bytes, returned 0x{rc1:X)} bytes from image provider and 0x{rc2:X} bytes from debug compare stream.")
            End If

            Dim cmp As Integer

            Dim handle = GCHandle.Alloc(buf1, GCHandleType.Pinned)
            Try
                cmp = memcmp(buf2, handle.AddrOfPinnedObject() + bufferoffset, New IntPtr(Math.Min(rc1, rc2)))

            Finally
                handle.Free()

            End Try

            If cmp <> 0 Then
                Trace.WriteLine($"Read request at position 0x{fileoffset:X}, 0x{count:X} bytes, returned different data from image provider than from debug compare stream.")
            End If

            Return rc1

        End Function

        Public Overrides Function Write(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer
            Return BaseProvider.Write(buffer, bufferoffset, count, fileoffset)
        End Function

        Public Overrides ReadOnly Property SupportsShared As Boolean
            Get
                Return BaseProvider.SupportsShared
            End Get
        End Property

        Public Overrides Sub SharedKeys(Request As IMDPROXY_SHARED_REQ, <Out> ByRef Response As IMDPROXY_SHARED_RESP, <Out> ByRef Keys() As ULong)
            BaseProvider.SharedKeys(Request, Response, Keys)
        End Sub

        Protected Overrides Sub OnDisposed(e As EventArgs)
            BaseProvider.Dispose()
            DebugCompareStream.Close()

            MyBase.OnDisposed(e)
        End Sub

    End Class

End Namespace