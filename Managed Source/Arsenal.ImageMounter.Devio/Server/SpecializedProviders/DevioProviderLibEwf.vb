
''''' DevioProviderLibEwf.vb
''''' 
''''' Copyright (c) 2012-2017, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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

    Public Class DevioProviderLibEwf
        Inherits DevioProviderUnmanagedBase

        Public Shared ReadOnly AccessFlagsRead As Byte = libewf_get_access_flags_read()
        Public Shared ReadOnly AccessFlagsReadWrite As Byte = libewf_get_access_flags_read_write()
        Public Shared ReadOnly AccessFlagsWrite As Byte = libewf_get_access_flags_write()
        Public Shared ReadOnly AccessFlagsWriteResume As Byte = libewf_get_access_flags_write_resume()

#Region "SafeHandle"
        Public NotInheritable Class SafeLibEwfHandle
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

        End Class
#End Region

        Private ReadOnly Flags As Byte

        Public ReadOnly Property SafeHandle As SafeLibEwfHandle

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
        Private Shared Function libewf_notify_stream_open(<[In], MarshalAs(UnmanagedType.LPStr)> filename As String, err As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Sub libewf_notify_set_verbose(Verbose As Integer)
        End Sub

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_open_wide(<[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), numberOfFiles As Integer, AccessFlags As Byte) As SafeLibEwfHandle
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_initialize(<Out> ByRef handle As SafeLibEwfHandle, ByRef errobj As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_open_wide(handle As SafeLibEwfHandle, <[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), numberOfFiles As Integer, AccessFlags As Integer, ByRef errobj As IntPtr) As Integer
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_media_size(handle As SafeLibEwfHandle, ByRef media_size As Long) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_get_media_size(handle As SafeLibEwfHandle, ByRef media_size As Long, ByRef errobj As IntPtr) As Integer
        End Function

        Private Enum Whence As Integer
            [Set] = 0
            Current = 1
            [End] = 2
        End Enum

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_seek_offset(handle As SafeLibEwfHandle, offset As Long, whence As Whence, ByRef errobj As IntPtr) As Long
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_read_random(handle As SafeLibEwfHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_read_buffer(handle As SafeLibEwfHandle, buffer As IntPtr, buffer_size As IntPtr, ByRef errobj As IntPtr) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_write_random(handle As SafeLibEwfHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_write_buffer(handle As SafeLibEwfHandle, buffer As IntPtr, buffer_size As IntPtr, ByRef errobj As IntPtr) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_write_finalize(handle As SafeLibEwfHandle) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_write_finalize(handle As SafeLibEwfHandle, ByRef errobj As IntPtr) As IntPtr
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_close(handle As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_close(handle As IntPtr, ByRef errobj As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_stream_close(errobj As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_set_stream(FILE As IntPtr, errobj As IntPtr) As Integer
        End Function

        <Obsolete, DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_bytes_per_sector(safeLibEwfHandle As SafeLibEwfHandle, ByRef SectorSize As UInteger) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_handle_get_bytes_per_sector(safeLibEwfHandle As SafeLibEwfHandle, ByRef SectorSize As UInteger, ByRef errobj As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True, CharSet:=CharSet.Ansi)>
        Private Shared Function libewf_error_sprint(errobj As IntPtr, buffer As StringBuilder, length As Integer) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_error_fprint(errobj As IntPtr, clibfile As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_error_free(ByRef errobj As IntPtr) As Integer
        End Function

        Public Shared WriteOnly Property NotificationFile As String
            Set(value As String)
                If String.IsNullOrEmpty(value) Then
                    If libewf_notify_stream_close(Nothing) < 0 Then
                        Throw New IOException("Error closing notification stream.")
                    End If
                Else
                    If libewf_notify_stream_open(value, Nothing) < 0 Then
                        Throw New IOException("Error opening " & value & ".")
                    End If
                End If
            End Set
        End Property

        Public Shared Function OpenNotificationStream() As NamedPipeServerStream
            Dim pipename = "DevioProviderLibEwf-" & Guid.NewGuid().ToString()
            Dim pipe As New NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.None)
            If libewf_notify_stream_open("\\?\PIPE\" & pipename, Nothing) < 0 Then
                pipe.Dispose()
                Throw New IOException("Error opening named pipe " & pipename & ".")
            End If
            pipe.WaitForConnection()
            Return pipe
        End Function

        Public Shared WriteOnly Property NotificationVerbose As Boolean
            Set(value As Boolean)
                libewf_notify_set_verbose(If(value, 1, 0))
            End Set
        End Property

        Public Sub New(filenames As String(), Flags As Byte)
            Me.Flags = Flags

            Dim errobj As IntPtr

            If libewf_handle_initialize(_SafeHandle, errobj) <> 1 Then

                Dim errmsg As New StringBuilder(32000)

                If errobj <> Nothing AndAlso libewf_error_sprint(errobj, errmsg, errmsg.Capacity) > 0 Then
                    Throw New Exception("Error initializing libewf handle: " & errmsg.ToString())
                Else
                    Throw New Exception("Error initializing libewf handle.")
                End If

            End If

            If libewf_handle_open_wide(_SafeHandle, filenames, filenames.Length, Flags, errobj) <> 1 OrElse
                _SafeHandle.IsInvalid Then

                ThrowError(errobj, "Error opening image file(s)")

            End If
        End Sub

        Protected Shared Function GetErrorMessage(errobj As IntPtr) As String

            If errobj = Nothing Then
                Return Nothing
            End If

            Dim errmsg As New StringBuilder(32000)

            If libewf_error_sprint(errobj, errmsg, errmsg.Capacity) > 0 Then
                Return errmsg.ToString()
            Else
                Return Nothing
            End If

        End Function

        Protected Shared Sub ThrowError(errobj As IntPtr, message As String)

            Dim errmsg = GetErrorMessage(errobj)

            If errmsg IsNot Nothing Then
                Throw New Exception(message & ": " & errmsg.ToString())
            Else
                Throw New Exception(message)
            End If

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

                Dim errobj As IntPtr

                Dim RC = libewf_handle_get_media_size(_SafeHandle, Length, errobj)
                If RC < 0 Then
                    ThrowError(errobj, "libewf_handle_get_media_size() failed")
                End If
            End Get
        End Property

        Public Overloads Overrides Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer
            Dim errobj As IntPtr

            Dim offset = libewf_handle_seek_offset(_SafeHandle, fileoffset, Whence.Set, errobj)
            If offset <> fileoffset Then
                ThrowError(errobj, "Error seeking to position " & fileoffset.ToString() & " to offset " & bufferoffset.ToString() & " in buffer " & buffer.ToString())
            End If

            Dim result = libewf_handle_read_buffer(_SafeHandle, buffer + bufferoffset, New IntPtr(count), errobj).ToInt32()
            If result >= 0 Then
                Return result
            Else
                ThrowError(errobj, "Error reading " & count.ToString() & " bytes from offset " & fileoffset.ToString() & " to offset " & bufferoffset.ToString() & " in buffer " & buffer.ToString())
            End If
            Return 0
        End Function

        Public Overloads Overrides Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            Dim sizedone As Integer

            Dim retval As IntPtr

            Dim size As New IntPtr(count)

            Dim errobj As IntPtr

            Dim offset = libewf_handle_seek_offset(_SafeHandle, fileoffset, Whence.Set, errobj)
            If offset <> fileoffset Then
                ThrowError(errobj, "Error seeking to position " & fileoffset.ToString() & " to offset " & bufferoffset.ToString() & " in buffer " & buffer.ToString())
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
            Get
                Dim _SectorSize As UInteger
                Dim errobj As IntPtr
                If libewf_handle_get_bytes_per_sector(_SafeHandle, _SectorSize, errobj) < 0 Then
                    ThrowError(errobj, "Unable to get number of bytes per sector")
                End If
                Return _SectorSize
            End Get
        End Property

    End Class

End Namespace
