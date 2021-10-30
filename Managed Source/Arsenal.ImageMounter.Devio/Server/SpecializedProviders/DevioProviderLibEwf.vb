
''''' DevioProviderLibEwf.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Diagnostics.CodeAnalysis
Imports System.IO
Imports System.IO.Pipes
Imports System.Runtime.InteropServices
Imports System.Text
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32.SafeHandles

#Disable Warning CA2101 ' Specify marshaling for P/Invoke string arguments

Namespace Server.SpecializedProviders

    Public Class DevioProviderLibEwf
        Inherits DevioProviderUnmanagedBase

        Public Event Finishing As EventHandler

        Public Shared ReadOnly AccessFlagsRead As Byte = libewf_get_access_flags_read()
        Public Shared ReadOnly AccessFlagsReadWrite As Byte = libewf_get_access_flags_read_write()
        Public Shared ReadOnly AccessFlagsWrite As Byte = libewf_get_access_flags_write()
        Public Shared ReadOnly AccessFlagsWriteResume As Byte = libewf_get_access_flags_write_resume()

#Region "SafeHandles"
        Public NotInheritable Class SafeLibEwfFileHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Public Sub New(handle As IntPtr, ownsHandle As Boolean)
                MyBase.New(ownsHandle)

                SetHandle(handle)
            End Sub

            Private Sub New()
                MyBase.New(True)

            End Sub

            Protected Overrides Function ReleaseHandle() As Boolean
                If libewf_handle_close(MyBase.handle, Nothing) < 0 Then
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

        Public NotInheritable Class SafeLibEwfErrorObjectHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Public Sub New(handle As IntPtr, ownsHandle As Boolean)
                MyBase.New(ownsHandle)

                SetHandle(handle)
            End Sub

            Private Sub New()
                MyBase.New(True)

            End Sub

            Protected Overrides Function ReleaseHandle() As Boolean
                If libewf_error_free(handle) < 0 Then
                    Return False
                Else
                    Return True
                End If
            End Function

            Public Overrides Function ToString() As String

                If IsInvalid Then
                    Return "No error"
                End If

                Dim errmsg As New StringBuilder(32000)

                If libewf_error_sprint(Me, errmsg, errmsg.Capacity) > 0 Then
                    Return errmsg.ToString()
                Else
                    Return $"Unknown error 0x{handle:X}"
                End If

            End Function

        End Class
#End Region

        Public ReadOnly Property Flags As Byte

        Public ReadOnly Property SafeHandle As SafeLibEwfFileHandle

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_access_flags_read() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_access_flags_read_write() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_access_flags_write() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_access_flags_write_resume() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_stream_open(<[In], MarshalAs(UnmanagedType.LPStr)> filename As String, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Sub libewf_notify_set_verbose(Verbose As Integer)
        End Sub

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_open_wide(<[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), numberOfFiles As Integer, AccessFlags As Byte) As SafeLibEwfFileHandle
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_initialize(<Out> ByRef handle As SafeLibEwfFileHandle, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_open_wide(handle As SafeLibEwfFileHandle, <[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), numberOfFiles As Integer, AccessFlags As Integer, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_media_size(handle As SafeLibEwfFileHandle, <Out> ByRef media_size As Long) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_get_media_size(handle As SafeLibEwfFileHandle, <Out> ByRef media_size As Long, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        Private Enum Whence As Integer
            [Set] = 0
            Current = 1
            [End] = 2
        End Enum

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_seek_offset(handle As SafeLibEwfFileHandle, offset As Long, whence As Whence, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Long
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_read_random(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_read_buffer(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_write_random(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_write_buffer(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_write_finalize(handle As SafeLibEwfFileHandle) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_write_finalize(handle As SafeLibEwfFileHandle, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_close(handle As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_close(handle As IntPtr, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_stream_close(<Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_set_stream(FILE As IntPtr, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_bytes_per_sector(safeLibEwfHandle As SafeLibEwfFileHandle, <Out> ByRef SectorSize As UInteger) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_get_bytes_per_sector(safeLibEwfHandle As SafeLibEwfFileHandle, <Out> ByRef SectorSize As UInteger, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_get_chunk_size(safeLibEwfHandle As SafeLibEwfFileHandle, <Out> ByRef ChunkSize As UInteger, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function


        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_get_sectors_per_chunk(safeLibEwfHandle As SafeLibEwfFileHandle, <Out> ByRef SectorsPerChunk As UInteger, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_utf16_header_value(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                     <[In], MarshalAs(UnmanagedType.LPStr, SizeParamIndex:=2)> identifier As String,
                                                                     identifier_length As IntPtr,
                                                                     <[In], MarshalAs(UnmanagedType.LPWStr, SizeParamIndex:=4)> utf16_string As String,
                                                                     utf16_string_length As IntPtr,
                                                                     <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_utf8_hash_value(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                     <[In], MarshalAs(UnmanagedType.LPStr, SizeParamIndex:=2)> hash_value_identifier As String,
                                                                     hash_value_identifier_length As IntPtr,
                                                                     <[In]> utf8_string As IntPtr,
                                                                     utf8_string_length As IntPtr,
                                                                     <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_header_codepage(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  codepage As Integer,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_bytes_per_sector(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  bytes_per_sector As UInteger,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_media_size(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  media_size As ULong,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_media_type(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  media_type As LIBEWF_MEDIA_TYPE,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_media_flags(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  media_flags As LIBEWF_MEDIA_FLAGS,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_format(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  ewf_format As LIBEWF_FORMAT,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_compression_method(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  compression_method As LIBEWF_COMPRESSION_METHOD,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_compression_values(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  compression_level As LIBEWF_COMPRESSION_LEVEL,
                                                                  compression_flags As LIBEWF_COMPRESSION_FLAGS,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_maximum_segment_size(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  maximum_segment_size As ULong,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_sectors_per_chunk(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  sectors_per_chunk As UInteger,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_set_error_granularity(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                  sectors_per_chunk As UInteger,
                                                                  <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        Private Delegate Function libewf_handle_set_header_value_func(Of TValue As Structure)(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                                              value As TValue,
                                                                                              <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer

        Private Delegate Function libewf_handle_set_header_value_func(Of TValue1 As Structure, TValue2 As Structure)(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                                                                     value1 As TValue1,
                                                                                                                     value2 As TValue2,
                                                                                                                     <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_get_utf16_header_value(safeLibEwfHandle As SafeLibEwfFileHandle,
                                                                     <[In], MarshalAs(UnmanagedType.LPStr, SizeParamIndex:=2)> identifier As String,
                                                                     identifier_length As IntPtr,
                                                                     <MarshalAs(UnmanagedType.LPWStr, SizeParamIndex:=4)> utf16_string As String,
                                                                     utf16_string_length As IntPtr,
                                                                     <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True, CharSet:=CharSet.Ansi)>
        Private Shared Function libewf_error_sprint(errobj As SafeLibEwfErrorObjectHandle, buffer As StringBuilder, length As Integer) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_error_fprint(errobj As SafeLibEwfErrorObjectHandle, clibfile As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_error_free(ByRef errobj As IntPtr) As Integer
        End Function

        Public Shared Sub SetNotificationFile(path As String)
            If String.IsNullOrEmpty(path) Then
                Dim errobj As SafeLibEwfErrorObjectHandle = Nothing
                If libewf_notify_stream_close(errobj) < 0 Then
                    ThrowError(errobj, "Error closing notification stream.")
                End If
            Else
                Dim errobj As SafeLibEwfErrorObjectHandle = Nothing
                If libewf_notify_stream_open(path, errobj) < 0 Then
                    ThrowError(errobj, $"Error opening {path}.")
                End If
            End If
        End Sub

        Public Shared Function OpenNotificationStream() As NamedPipeServerStream
            Dim pipename = $"DevioProviderLibEwf-{Guid.NewGuid()}"
            Dim pipe As New NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.None)
            Dim errobj As SafeLibEwfErrorObjectHandle = Nothing
            If libewf_notify_stream_open($"\\?\PIPE\{pipename}", errobj) < 0 Then
                pipe.Dispose()
                ThrowError(errobj, $"Error opening named pipe {pipename}.")
            End If
            pipe.WaitForConnection()
            Return pipe
        End Function

        Public Shared WriteOnly Property NotificationVerbose As Boolean
            Set
                libewf_notify_set_verbose(If(Value, 1, 0))
            End Set
        End Property

        Public Sub New(filenames As String(), Flags As Byte)
            _Flags = Flags

            Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

            If libewf_handle_initialize(_SafeHandle, errobj) <> 1 Then

                ThrowError(errobj, "Error initializing libewf handle.")

            End If

            If libewf_handle_open_wide(_SafeHandle, filenames, filenames.Length, Flags, errobj) <> 1 OrElse
                _SafeHandle.IsInvalid Then

                ThrowError(errobj, "Error opening image file(s)")

            End If

            If libewf_handle_get_bytes_per_sector(_SafeHandle, _SectorSize, errobj) < 0 Then

                ThrowError(errobj, "Unable to get number of bytes per sector")

            End If

            If libewf_handle_get_chunk_size(_SafeHandle, _ChunkSize, errobj) < 0 Then

                ThrowError(errobj, "Unable to get chunk size")

            End If

            If libewf_handle_get_sectors_per_chunk(_SafeHandle, _SectorsPerChunk, errobj) < 0 Then

                ThrowError(errobj, "Unable to get number of sectors per chunk")

            End If

        End Sub

        Protected Shared Sub ThrowError(errobj As SafeLibEwfErrorObjectHandle, message As String)

            Using errobj

                Dim errmsg = errobj?.ToString()

                If errmsg IsNot Nothing Then
                    Throw New IOException($"{message}: {errmsg}")
                Else
                    Throw New IOException(message)
                End If

            End Using

        End Sub

        Public Sub New(firstfilename As String, Flags As Byte)
            Me.New(GetMultiSegmentFiles(firstfilename), Flags)
        End Sub

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return (_Flags And AccessFlagsWrite) = AccessFlagsWrite
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Length = 0

                Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

                Dim RC = libewf_handle_get_media_size(_SafeHandle, Length, errobj)
                If RC < 0 Then
                    ThrowError(errobj, "libewf_handle_get_media_size() failed")
                End If
            End Get
        End Property

        Public ReadOnly Property MaxIoSize As Integer = Integer.MaxValue

        Public Overloads Overrides Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            Dim done_count = 0

            While done_count < count

                Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

                Dim offset = libewf_handle_seek_offset(_SafeHandle, fileoffset, Whence.Set, errobj)

                If offset <> fileoffset Then

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

                Dim result = libewf_handle_read_buffer(_SafeHandle, buffer + bufferoffset, New IntPtr(iteration_count), errobj).ToInt32()

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

        Public Overloads Overrides Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            Dim sizedone As Integer

            Dim retval As IntPtr

            Dim size As New IntPtr(count)

            Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

            Dim offset = libewf_handle_seek_offset(_SafeHandle, fileoffset, Whence.Set, errobj)
            If offset <> fileoffset Then
                ThrowError(errobj, $"Error seeking to position {fileoffset} to offset {bufferoffset} in buffer 0x{buffer:X}")
            End If

            While sizedone < count
                Dim sizenow = size - sizedone
                If sizenow.ToInt32() > 32764 Then
                    sizenow = New IntPtr(16384)
                End If

                retval = libewf_handle_write_buffer(_SafeHandle, buffer + bufferoffset + sizedone, sizenow, errobj)

                If retval.ToInt64() <= 0 Then
                    ThrowError(errobj, "Write failed")
                End If

                sizedone += retval.ToInt32()
            End While

            If sizedone <= 0 Then
                Return retval.ToInt32()
            Else
                Return sizedone
            End If

        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)

            RaiseEvent Finishing(Me, EventArgs.Empty)

            If disposing AndAlso _SafeHandle IsNot Nothing Then
                _SafeHandle.Dispose()
            End If

            _SafeHandle = Nothing

            MyBase.Dispose(disposing)

        End Sub

        Public Overrides ReadOnly Property SectorSize As UInteger

        Public ReadOnly Property ChunkSize As UInteger

        Public ReadOnly Property SectorsPerChunk As UInteger

        Public Sub SetOutputStringParameter(identifier As String, value As String)

            If String.IsNullOrWhiteSpace(value) Then
                Return
            End If

            Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

            Dim retval = libewf_handle_set_utf16_header_value(SafeHandle, identifier, New IntPtr(identifier.Length), value, New IntPtr(value.Length), errobj)

            If retval <> 1 Then
                ThrowError(errobj, $"Parameter set '{identifier}'='{value}' failed")
            End If

        End Sub

        Public Sub SetOutputHashParameter(identifier As String, value As Byte())

            If value Is Nothing Then
                Return
            End If

            Dim valuestr = value.ToHexString()

            Trace.WriteLine($"{identifier} = {valuestr}")

            Using utf8 = PinnedBuffer.Create(Encoding.UTF8.GetBytes(valuestr))

                Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

                Dim retval = libewf_handle_set_utf8_hash_value(SafeHandle, identifier, New IntPtr(identifier.Length), utf8.DangerousGetHandle(), New IntPtr(valuestr.Length), errobj)

                If retval <> 1 Then
                    ThrowError(errobj, $"Hash result set '{identifier}'='{value}' failed")
                End If

            End Using

        End Sub

        Private Sub SetOutputValueParameter(Of TValue As Structure)(func As libewf_handle_set_header_value_func(Of TValue), value As TValue)

            Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

            Dim retval = func(SafeHandle, value, errobj)

            If retval <> 1 Then
                ThrowError(errobj, $"Parameter set {func.Method.Name}({value}) failed")
            End If

        End Sub

        Private Sub SetOutputValueParameter(Of TValue1 As Structure, TValue2 As Structure)(func As libewf_handle_set_header_value_func(Of TValue1, TValue2), value1 As TValue1, value2 As TValue2)

            Dim errobj As SafeLibEwfErrorObjectHandle = Nothing

            Dim retval = func(SafeHandle, value1, value2, errobj)

            If retval <> 1 Then
                ThrowError(errobj, $"Parameter set {func.Method.Name}({value1}, {value2}) failed")
            End If

        End Sub

        Public Sub SetOutputParameters(ImagingParameters As ImagingParameters)

            SetOutputStringParameter("case_number", ImagingParameters.CaseNumber)
            SetOutputStringParameter("description", ImagingParameters.Description)
            SetOutputStringParameter("evidence_number", ImagingParameters.EvidenceNumber)
            SetOutputStringParameter("examiner_name", ImagingParameters.ExaminerName)
            SetOutputStringParameter("notes", ImagingParameters.Notes)
            SetOutputStringParameter("acquiry_operating_system", ImagingParameters.AcquiryOperatingSystem)
            SetOutputStringParameter("acquiry_software", ImagingParameters.AcquirySoftware)

            SetOutputStringParameter("model", ImagingParameters.StorageStandardProperties.ProductId)
            SetOutputStringParameter("serial_number", ImagingParameters.StorageStandardProperties.SerialNumber)

            SetOutputValueParameter(AddressOf libewf_handle_set_header_codepage, ImagingParameters.CodePage)
            SetOutputValueParameter(AddressOf libewf_handle_set_bytes_per_sector, ImagingParameters.BytesPerSector)
            SetOutputValueParameter(AddressOf libewf_handle_set_media_size, ImagingParameters.MediaSize)
            SetOutputValueParameter(AddressOf libewf_handle_set_media_type, ImagingParameters.MediaType)
            SetOutputValueParameter(AddressOf libewf_handle_set_media_flags, ImagingParameters.MediaFlags)
            SetOutputValueParameter(AddressOf libewf_handle_set_format, ImagingParameters.UseEWFFileFormat)
            SetOutputValueParameter(AddressOf libewf_handle_set_compression_method, ImagingParameters.CompressionMethod)
            SetOutputValueParameter(AddressOf libewf_handle_set_compression_values, ImagingParameters.CompressionLevel, ImagingParameters.CompressionFlags)
            SetOutputValueParameter(AddressOf libewf_handle_set_maximum_segment_size, ImagingParameters.SegmentFileSize)
            SetOutputValueParameter(AddressOf libewf_handle_set_sectors_per_chunk, ImagingParameters.SectorsPerChunk)

            If ImagingParameters.SectorErrorGranularity = 0 OrElse
                ImagingParameters.SectorErrorGranularity >= ImagingParameters.SectorsPerChunk Then

                ImagingParameters.SectorErrorGranularity = ImagingParameters.SectorsPerChunk
            End If

            SetOutputValueParameter(AddressOf libewf_handle_set_error_granularity, ImagingParameters.SectorErrorGranularity)

        End Sub

        Public Class ImagingParameters

            Public Const LIBEWF_CODEPAGE_ASCII = 20127

            Public Property CodePage As Integer = LIBEWF_CODEPAGE_ASCII

            Public Property StorageStandardProperties As NativeFileIO.StorageStandardProperties

            Public Property CaseNumber As String

            Public Property Description As String

            Public Property EvidenceNumber As String

            Public Property ExaminerName As String

            Public Property Notes As String

            Public Property AcquiryOperatingSystem As String = $"Windows {DriverSetup.OSVersion}"

            Public Property AcquirySoftware As String = $"aim-libewf"

            Public Property MediaSize As ULong

            Public Property BytesPerSector As UInteger

            Public Property SectorsPerChunk As UInteger = 64

            Public Property SectorErrorGranularity As UInteger = 64

            ''' <summary>
            ''' logical, physical
            ''' </summary>
            Public Property MediaFlags As LIBEWF_MEDIA_FLAGS = LIBEWF_MEDIA_FLAGS.PHYSICAL

            ''' <summary>
            ''' fixed, removable, optical, memory
            ''' </summary>
            Public Property MediaType As LIBEWF_MEDIA_TYPE = LIBEWF_MEDIA_TYPE.FIXED

            ''' <summary>
            ''' ewf, smart, ftk, encase1, encase2, encase3, encase4, encase5, encase6, encase7, encase7-v2, linen5, linen6, linen7, ewfx
            ''' </summary>
            Public Property UseEWFFileFormat As LIBEWF_FORMAT = LIBEWF_FORMAT.ENCASE6

            ''' <summary>
            ''' deflate
            ''' </summary>
            Public Property CompressionMethod As LIBEWF_COMPRESSION_METHOD = LIBEWF_COMPRESSION_METHOD.DEFLATE

            ''' <summary>
            ''' none, empty-block, fast, best
            ''' </summary>
            Public Property CompressionLevel As LIBEWF_COMPRESSION_LEVEL = LIBEWF_COMPRESSION_LEVEL.FAST

            Public Property CompressionFlags As LIBEWF_COMPRESSION_FLAGS

            Public Property SegmentFileSize As ULong = 2UL << 40

        End Class

        Public Enum LIBEWF_FORMAT As Byte

            ENCASE1 = &H1
            ENCASE2 = &H2
            ENCASE3 = &H3
            ENCASE4 = &H4
            ENCASE5 = &H5
            ENCASE6 = &H6
            ENCASE7 = &H7

            SMART = &HE
            FTK_IMAGER = &HF

            LOGICAL_ENCASE5 = &H10
            LOGICAL_ENCASE6 = &H11
            LOGICAL_ENCASE7 = &H12

            LINEN5 = &H25
            LINEN6 = &H26
            LINEN7 = &H27

            V2_ENCASE7 = &H37

            V2_LOGICAL_ENCASE7 = &H47

            '' The format as specified by Andrew Rosen

            EWF = &H70

            '' Libewf eXtended EWF format

            EWFX = &H71
        End Enum

        Public Enum LIBEWF_MEDIA_TYPE As Byte
            REMOVABLE = &H0
            FIXED = &H1
            OPTICAL = &H3
            SINGLE_FILES = &HE
            MEMORY = &H10
        End Enum

        <Flags>
        Public Enum LIBEWF_MEDIA_FLAGS As Byte

            PHYSICAL = &H2
            FASTBLOC = &H4
            TABLEAU = &H8

        End Enum

        Public Enum LIBEWF_COMPRESSION_METHOD As UShort
            NONE = 0
            DEFLATE = 1
            BZIP2 = 2
        End Enum

        Public Enum LIBEWF_COMPRESSION_LEVEL As SByte
            [DEFAULT] = -1
            NONE = 0
            FAST = 1
            BEST = 2
        End Enum

        <Flags>
        Public Enum LIBEWF_COMPRESSION_FLAGS As Byte
            NONE = &H0
            USE_EMPTY_BLOCK_COMPRESSION = &H1
            USE_PATTERN_FILL_COMPRESSION = &H10
        End Enum


    End Class

End Namespace
