
''''' DevioProviderLibEwf.vb
''''' 
''''' Copyright (c) 2012-2020, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports System.Diagnostics.CodeAnalysis
Imports System.IO.Pipes

Namespace Server.SpecializedProviders

    <SuppressMessage("Design", "CA1060:Move pinvokes to native methods class")>
    Public Class DevioProviderLibEwf
        Inherits DevioProviderUnmanagedBase

        Public Shared ReadOnly AccessFlagsRead As Byte = libewf_get_access_flags_read()
        Public Shared ReadOnly AccessFlagsReadWrite As Byte = libewf_get_access_flags_read_write()
        Public Shared ReadOnly AccessFlagsWrite As Byte = libewf_get_access_flags_write()
        Public Shared ReadOnly AccessFlagsWriteResume As Byte = libewf_get_access_flags_write_resume()

#Region "SafeHandles"
        <SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification:="<Pending>")>
        Public NotInheritable Class SafeLibEwfFileHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Public Sub New(handle As IntPtr, ownsHandle As Boolean)
                MyBase.New(ownsHandle)

                SetHandle(handle)
            End Sub

            Protected Sub New()
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

        <SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification:="<Pending>")>
        Public NotInheritable Class SafeLibEwfErrorObjectHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Public Sub New(handle As IntPtr, ownsHandle As Boolean)
                MyBase.New(ownsHandle)

                SetHandle(handle)
            End Sub

            Protected Sub New()
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

        Private ReadOnly Flags As Byte

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
        <SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")>
        Private Shared Function libewf_open_wide(<[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), numberOfFiles As Integer, AccessFlags As Byte) As SafeLibEwfFileHandle
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_initialize(<Out> ByRef handle As SafeLibEwfFileHandle, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_open_wide(handle As SafeLibEwfFileHandle, <[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), numberOfFiles As Integer, AccessFlags As Integer, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As Integer
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        <SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")>
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
        <SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")>
        Private Shared Function libewf_read_random(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_read_buffer(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        <SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")>
        Private Shared Function libewf_write_random(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_write_buffer(handle As SafeLibEwfFileHandle, buffer As IntPtr, buffer_size As IntPtr, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        <SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")>
        Private Shared Function libewf_write_finalize(handle As SafeLibEwfFileHandle) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_write_finalize(handle As SafeLibEwfFileHandle, <Out> ByRef errobj As SafeLibEwfErrorObjectHandle) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        <SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")>
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
        <SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")>
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

        <SuppressMessage("Design", "CA1044:Properties should not be write only")>
        Public Shared WriteOnly Property NotificationVerbose As Boolean
            Set
                libewf_notify_set_verbose(If(Value, 1, 0))
            End Set
        End Property

        Public Sub New(filenames As String(), Flags As Byte)
            Me.Flags = Flags

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
            Me.New(ProviderSupport.GetMultiSegmentFiles(firstfilename), Flags)
        End Sub

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return (Flags And AccessFlagsWrite) = AccessFlagsWrite
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
            _SafeHandle?.Dispose()
            _SafeHandle = Nothing

            MyBase.Dispose(disposing)
        End Sub

        Public Overrides ReadOnly Property SectorSize As UInteger

        Public ReadOnly Property ChunkSize As UInteger

        Public ReadOnly Property SectorsPerChunk As UInteger

    End Class

End Namespace
