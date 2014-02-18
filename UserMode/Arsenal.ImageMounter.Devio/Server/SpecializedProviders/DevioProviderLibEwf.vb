
''''' DevioProviderLibEwf.vb
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
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

        Shared Sub New()

            AccessFlagsRead = libewf_get_flags_read()
            AccessFlagsReadWrite = libewf_get_flags_read_write()
            AccessFlagsWrite = libewf_get_flags_write()
            AccessFlagsWriteResume = libewf_get_flags_write_resume()

        End Sub

        Public Shared ReadOnly AccessFlagsRead As Byte
        Public Shared ReadOnly AccessFlagsReadWrite As Byte
        Public Shared ReadOnly AccessFlagsWrite As Byte
        Public Shared ReadOnly AccessFlagsWriteResume As Byte

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
                If libewf_close(MyBase.handle) < 0 Then
                    Return False
                Else
                    Return True
                End If
            End Function

        End Class
#End Region

        Private ReadOnly Flags As Byte

        Public ReadOnly Property SafeHandle As SafeLibEwfHandle
            Get
                Return handle
            End Get
        End Property

        Private handle As SafeLibEwfHandle

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_flags_read() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_flags_read_write() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_flags_write() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_flags_write_resume() As Byte
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_set_notify_values(c_libstream As IntPtr, Verbose As Integer) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_stream_open(<[In](), MarshalAs(UnmanagedType.LPStr)> filename As String, err As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Sub libewf_notify_set_verbose(Verbose As Integer)
        End Sub

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_open(<[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), AmountOfFiles As Integer, AccessFlags As Byte) As SafeLibEwfHandle
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_open_wide(<[In](), MarshalAs(UnmanagedType.LPArray)> filenames As String(), AmountOfFiles As Integer, AccessFlags As Byte) As SafeLibEwfHandle
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_media_size(handle As SafeLibEwfHandle, ByRef media_size As Long) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_read_random(handle As SafeLibEwfHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_write_random(handle As SafeLibEwfHandle, buffer As IntPtr, buffer_size As IntPtr, offset As Long) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_write_finalize(handle As SafeLibEwfHandle) As IntPtr
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_close(handle As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_stream_close(errobj As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_notify_set_stream(FILE As IntPtr, errobj As IntPtr) As Integer
        End Function

        <DllImport("libewf.dll", CallingConvention:=CallingConvention.Cdecl, SetLastError:=True, ThrowOnUnmappableChar:=True)>
        Private Shared Function libewf_get_bytes_per_sector(safeLibEwfHandle As SafeLibEwfHandle, ByRef SectorSize As UInteger) As Integer
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

        Private Shared Function GetMultiSegmentFiles(FirstFile As String) As String()
            Dim pathpart = Path.GetDirectoryName(FirstFile)
            Dim filepart = Path.GetFileNameWithoutExtension(FirstFile)
            Dim extension = Path.GetExtension(FirstFile)
            If extension.StartsWith(".e", StringComparison.InvariantCultureIgnoreCase) AndAlso
              Integer.TryParse(extension.Substring(2), Nothing) Then

                Dim mask As New String("0"c, extension.Length - 2)

                Dim foundfiles As New List(Of String)
                Dim filenumber = Integer.Parse(extension.Substring(2))
                Do
                    Dim thisfile = Path.Combine(pathpart, filepart & ".e" & filenumber.ToString(mask))
                    If Not File.Exists(thisfile) Then
                        Exit Do
                    End If

                    foundfiles.Add(thisfile)
                    filenumber += 1
                Loop
                Return foundfiles.ToArray()

            Else

                Return {FirstFile}

            End If

        End Function

        Public Sub New(filenames As String(), Flags As Byte)
            Me.Flags = Flags

            handle = libewf_open_wide(filenames, filenames.Length, Flags)
            If handle.IsInvalid Then
                Throw New Exception("Error opening image file(s).")
            End If
        End Sub

        Public Sub New(firstfilename As String, Flags As Byte)
            Me.New(GetMultiSegmentFiles(firstfilename), Flags)
        End Sub

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return (Flags And AccessFlagsWrite) = AccessFlagsWrite
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Length = 0

                Dim RC = libewf_get_media_size(handle, Length)
                If RC < 0 Then
                    Throw New Exception("libewf_get_media_size() failed.")
                End If
            End Get
        End Property

        Public Overloads Overrides Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer
            Dim result = libewf_read_random(handle, buffer + bufferoffset, New IntPtr(count), fileoffset).ToInt32()
            If result >= 0 Then
                Return result
            Else
                Throw New IOException("Error reading " & count & " bytes from offset " & fileoffset & " to offset " & bufferoffset & " in buffer " & buffer.ToString())
            End If
        End Function

        Public Overloads Overrides Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, offset As Long) As Integer

            Dim sizedone As Integer

            Dim retval As IntPtr

            Dim size As New IntPtr(count)

            While sizedone < count
                Dim sizenow = size - sizedone
                If sizenow.ToInt32() > 32764 Then
                    sizenow = New IntPtr(16384)
                End If

                retval = libewf_write_random(handle, buffer + bufferoffset + sizedone, sizenow, offset + sizedone)

                If retval.ToInt64() <= 0 Then
                    Exit While
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
            If handle IsNot Nothing Then
                handle.Dispose()
                handle = Nothing
            End If

            MyBase.Dispose(disposing)
        End Sub

        Public Overrides ReadOnly Property SectorSize As UInteger
            Get
                Dim _SectorSize As UInteger
                If libewf_get_bytes_per_sector(SafeHandle, _SectorSize) < 0 Then
                    Throw New Exception("Unable to get number of bytes per sector.")
                End If
                Return _SectorSize
            End Get
        End Property

    End Class

End Namespace
