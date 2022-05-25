
''''' DevioProviderLibQcow.vb
''''' 
''''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Buffers
Imports System.Diagnostics.CodeAnalysis
Imports System.IO
Imports System.IO.Pipes
Imports System.Runtime.InteropServices
Imports System.Text
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Arsenal.ImageMounter.Reflection
Imports Microsoft.Win32.SafeHandles

#Disable Warning CA2101 ' Specify marshaling for P/Invoke string arguments

Namespace Server.SpecializedProviders

    Public Class DevioProviderLibQcow
        Inherits DevioProviderUnmanagedBase

        Public Event Finishing As EventHandler

        Public Shared ReadOnly AccessFlagsRead As Byte = libqcow_get_access_flags_read()
        Public Shared ReadOnly AccessFlagsReadWrite As Byte = libqcow_get_access_flags_read_write()
        Public Shared ReadOnly AccessFlagsWrite As Byte = libqcow_get_access_flags_write()

#Region "SafeHandles"
        Public NotInheritable Class SafeLibQcowFileHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Public Sub New(handle As IntPtr, ownsHandle As Boolean)
                MyBase.New(ownsHandle)

                SetHandle(handle)
            End Sub

            Public Sub New()
                MyBase.New(True)

            End Sub

            Protected Overrides Function ReleaseHandle() As Boolean
                If libqcow_file_close(MyBase.handle, Nothing) < 0 Then
                    Return False
                Else
                    Return True
                End If
            End Function

            Public Overrides Function ToString() As String
                If IsClosed Then
                    Return "Closed"
                ElseIf IsInvalid Then
                    Return "Invalid"
                Else
                    Return $"0x{handle:X}"
                End If
            End Function

        End Class

        Public NotInheritable Class SafeLibQcowErrorObjectHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Public Sub New(handle As IntPtr, ownsHandle As Boolean)
                MyBase.New(ownsHandle)

                SetHandle(handle)
            End Sub

            Public Sub New()
                MyBase.New(True)

            End Sub

            Protected Overrides Function ReleaseHandle() As Boolean
                If libqcow_error_free(handle) < 0 Then
                    Return False
                Else
                    Return True
                End If
            End Function

            Public Overrides Function ToString() As String

                If IsInvalid Then
                    Return "No error"
                End If

                Dim errmsg = ArrayPool(Of Char).Shared.Rent(32000)

                Try
                    If libqcow_error_backtrace_sprint(Me, errmsg, errmsg.Length) > 0 Then

                        Dim msgs = New ReadOnlyMemory(Of Char)(errmsg).
                            ReadNullTerminatedUnicodeString().
                            SplitReverse(vbLf(0)).
                            Select(Function(msg) msg.TrimEnd(vbCr(0)))

                        Return String.Join(Environment.NewLine, msgs)

                    Else

                        Return $"Unknown error 0x{handle:X}"

                    End If

                Finally
                    ArrayPool(Of Char).Shared.Return(errmsg)

                End Try

            End Function

        End Class
#End Region

        Public ReadOnly Property Flags As Byte

        Public ReadOnly Property SafeHandle As SafeLibQcowFileHandle

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_get_access_flags_read() As Byte
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_get_access_flags_read_write() As Byte
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_get_access_flags_write() As Byte
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_notify_stream_open(<[In], MarshalAs(UnmanagedType.LPStr)> filename As String, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Sub libqcow_notify_set_verbose(Verbose As Integer)
        End Sub

        <Obsolete, DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_open_wide(<[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), numberOfFiles As Integer, AccessFlags As Byte) As SafeLibQcowFileHandle
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_file_initialize(<Out> ByRef handle As SafeLibQcowFileHandle, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Auto, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_file_open(handle As SafeLibQcowFileHandle, <[In](), MarshalAs(UnmanagedType.LPUTF8Str)> filename As String, AccessFlags As Integer, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Auto, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_file_open_wide(handle As SafeLibQcowFileHandle, <[In](), MarshalAs(UnmanagedType.LPWStr)> filename As String, AccessFlags As Integer, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        Private Delegate Function f_libqcow_file_open(handle As SafeLibQcowFileHandle, filename As String, AccessFlags As Integer, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer

        <Obsolete, DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_get_media_size(handle As SafeLibQcowFileHandle, <Out> ByRef media_size As Long) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_file_get_media_size(handle As SafeLibQcowFileHandle, <Out> ByRef media_size As Long, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        Private Enum Whence As Integer
            [Set] = 0
            Current = 1
            [End] = 2
        End Enum

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_file_seek_offset(handle As SafeLibQcowFileHandle, offset As Long, whence As Whence, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Long
        End Function

        <Obsolete, DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_read_random(handle As SafeLibQcowFileHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_file_read_buffer(handle As SafeLibQcowFileHandle, buffer As IntPtr, buffer_size As IntPtr, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As IntPtr
        End Function

        <Obsolete, DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_close(handle As IntPtr) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_file_close(handle As IntPtr, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_notify_stream_close(<Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_notify_set_stream(FILE As IntPtr, <Out> ByRef errobj As SafeLibQcowErrorObjectHandle) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True, CharSet:=CharSet.Ansi)>
        Private Shared Function libqcow_error_sprint(errobj As SafeLibQcowErrorObjectHandle, <Out> buffer As Char(), length As Integer) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True, CharSet:=CharSet.Ansi)>
        Private Shared Function libqcow_error_backtrace_sprint(errobj As SafeLibQcowErrorObjectHandle, <Out> buffer As Char(), length As Integer) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_error_fprint(errobj As SafeLibQcowErrorObjectHandle, clibfile As IntPtr) As Integer
        End Function

        <DllImport("libqcow", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libqcow_error_free(ByRef errobj As IntPtr) As Integer
        End Function

        Public Shared Sub SetNotificationFile(path As String)
            If String.IsNullOrEmpty(path) Then
                Dim errobj As SafeLibQcowErrorObjectHandle = Nothing
                If libqcow_notify_stream_close(errobj) < 0 Then
                    ThrowError(errobj, "Error closing notification stream.")
                End If
            Else
                Dim errobj As SafeLibQcowErrorObjectHandle = Nothing
                If libqcow_notify_stream_open(path, errobj) < 0 Then
                    ThrowError(errobj, $"Error opening {path}.")
                End If
            End If
        End Sub

        Public Shared Function OpenNotificationStream() As NamedPipeServerStream
            Dim pipename = $"DevioProviderLibEwf-{Guid.NewGuid()}"
            Dim pipe As New NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None)
            Dim errobj As SafeLibQcowErrorObjectHandle = Nothing
            If libqcow_notify_stream_open($"\\?\PIPE\{pipename}", errobj) < 0 Then
                pipe.Dispose()
                ThrowError(errobj, $"Error opening named pipe {pipename}.")
            End If
            pipe.WaitForConnection()
            Return pipe
        End Function

        Public Shared WriteOnly Property NotificationVerbose As Boolean
            Set
                libqcow_notify_set_verbose(If(Value, 1, 0))
            End Set
        End Property

        Public Sub New(filenames As String, Flags As Byte)

            _Flags = Flags

            Dim errobj As SafeLibQcowErrorObjectHandle = Nothing

            If libqcow_file_initialize(_SafeHandle, errobj) <> 1 OrElse
                _SafeHandle.IsInvalid OrElse Failed(errobj) Then

                ThrowError(errobj, "Error initializing libqcow handle.")

            End If

            Dim func As f_libqcow_file_open

            If NativeLib.IsWindows Then
                func = AddressOf libqcow_file_open_wide
            Else
                func = AddressOf libqcow_file_open
            End If

            If func(_SafeHandle, filenames, Flags, errobj) <> 1 OrElse Failed(errobj) Then

                ThrowError(errobj, "Error opening image file(s)")

            End If

        End Sub

        Protected Shared Function Failed(errobj As SafeLibQcowErrorObjectHandle) As Boolean
            Return errobj IsNot Nothing AndAlso Not errobj.IsInvalid
        End Function

        Protected Shared Sub ThrowError(errobj As SafeLibQcowErrorObjectHandle, message As String)

            Using errobj

                Dim errmsg = errobj?.ToString()

                If errmsg IsNot Nothing Then
                    Throw New IOException($"{message}: {errmsg}")
                Else
                    Throw New IOException(message)
                End If

            End Using

        End Sub

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return (_Flags And AccessFlagsWrite) = AccessFlagsWrite
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Length = 0

                Dim errobj As SafeLibQcowErrorObjectHandle = Nothing

                Dim RC = libqcow_file_get_media_size(_SafeHandle, Length, errobj)
                If RC < 0 OrElse Failed(errobj) Then
                    ThrowError(errobj, "libqcow_file_get_media_size() failed")
                End If
            End Get
        End Property

        Public ReadOnly Property MaxIoSize As Integer = Integer.MaxValue

        Public Overrides ReadOnly Property SectorSize As UInteger = 512

        Public Overloads Overrides Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            Dim done_count = 0

            While done_count < count

                Dim errobj As SafeLibQcowErrorObjectHandle = Nothing

                Dim offset = libqcow_file_seek_offset(_SafeHandle, fileoffset, Whence.Set, errobj)

                If offset <> fileoffset OrElse Failed(errobj) Then

                    ThrowError(errobj, $"Error seeking to position {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}")

                End If

                Dim iteration_count = count - done_count

                If iteration_count > _MaxIoSize Then

                    iteration_count = _MaxIoSize

                End If

                'Dim chunk_offset = CInt(fileoffset And (_ChunkSize - 1))

                'If chunk_offset + iteration_count > _ChunkSize Then

                '    iteration_count = CInt(_ChunkSize) - chunk_offset

                'End If

                Dim result = libqcow_file_read_buffer(_SafeHandle, buffer + bufferoffset, New IntPtr(iteration_count), errobj).ToInt32()

                If result < 0 OrElse Failed(errobj) Then

                    ThrowError(errobj, $"Error reading {iteration_count} bytes from offset {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}")

                End If

                If result > 0 Then

                    done_count += result
                    fileoffset += result
                    bufferoffset += result

                ElseIf result = 0 Then

                    Exit While

                ElseIf iteration_count >= (_SectorSize << 1) Then

                    errobj?.Dispose()

                    _MaxIoSize = (iteration_count >> 1) And Not CInt(_SectorSize - 1)

                    Trace.WriteLine($"Lowering MaxTransferSize to {_MaxIoSize} bytes.")

                    Continue While

                Else

                    ThrowError(errobj, $"Error reading {iteration_count} bytes from offset {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}")

                End If

            End While

            Return done_count

        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)

            RaiseEvent Finishing(Me, EventArgs.Empty)

            If disposing AndAlso _SafeHandle IsNot Nothing Then
                _SafeHandle.Dispose()
            End If

            _SafeHandle = Nothing

            MyBase.Dispose(disposing)

        End Sub

        Public Overrides Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer
            Throw New NotImplementedException("Write operations not implemented in libqcow")
        End Function
    End Class

End Namespace
