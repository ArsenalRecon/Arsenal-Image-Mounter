Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Arsenal.ImageMounter.Extensions
Imports Microsoft.Win32.SafeHandles

#Disable Warning IDE0079 ' Remove unnecessary suppression
#Disable Warning CA1069 ' Enums values should not be duplicated

Namespace IO

    Public Module NativeStruct

        Public ReadOnly Property IsOsWindows As Boolean = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

        Public Function GetFileSize(path As String) As Long

            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return New FileInfo(path).Length
            End If

            Return NativeFileIO.GetFileSize(path)

        End Function

        Public Function ReadAllBytes(path As String) As Byte()

            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return File.ReadAllBytes(path)
            End If

            Using stream = NativeFileIO.OpenFileStream(path.AsMemory(),
                                                       FileMode.Open,
                                                       FileAccess.Read,
                                                       FileShare.Read Or FileShare.Delete,
                                                       CType(NativeConstants.FILE_FLAG_BACKUP_SEMANTICS, FileOptions))

                Dim buffer(0 To CInt(stream.Length - 1)) As Byte

                If stream.Read(buffer, 0, buffer.Length) <> stream.Length Then

                    Throw New IOException($"Incomplete read from '{path}'")

                End If

                Return buffer

            End Using

        End Function

#If NETSTANDARD2_1_OR_GREATER OrElse NETCOREAPP Then
        Public Async Function ReadAllBytesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Byte())

            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return Await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(False)
            End If

            Using stream = NativeFileIO.OpenFileStream(path.AsMemory(),
                                                       FileMode.Open,
                                                       FileAccess.Read,
                                                       FileShare.Read Or FileShare.Delete,
                                                       CType(NativeConstants.FILE_FLAG_BACKUP_SEMANTICS, FileOptions) Or FileOptions.Asynchronous)

                Dim buffer(0 To CInt(stream.Length - 1)) As Byte

                If Await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(False) <> stream.Length Then

                    Throw New IOException($"Incomplete read from '{path}'")

                End If

                Return buffer

            End Using

        End Function

#End If

        Public Function GetFileOrDiskSize(imagefile As String) As Long

            Dim attributes As FileAttributes = Nothing

            If imagefile.StartsWith("/dev/", StringComparison.Ordinal) OrElse
                (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) AndAlso
                (imagefile.StartsWith("\\?\", StringComparison.OrdinalIgnoreCase) OrElse
                imagefile.StartsWith("\\.\", StringComparison.OrdinalIgnoreCase)) AndAlso
                (Not NativeFileIO.TryGetFileAttributes(imagefile, attributes) OrElse
                attributes.HasFlag(FileAttributes.Directory))) Then

                Return GetDiskSize(imagefile)
            Else

                Return GetFileSize(imagefile)

            End If

        End Function

        Public Function GetDiskSize(imagefile As String) As Long

            Using disk As New DiskDevice(imagefile, FileAccess.Read)

                Dim diskSize = disk.DiskSize

                If Not diskSize.HasValue Then

                    Throw New NotSupportedException($"Failed to identify size of device '{imagefile}'")

                End If

                Return diskSize.Value

            End Using

        End Function

        Public Function GetDiskSize(SafeFileHandle As SafeFileHandle) As Long?

            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then

                Return NativeFileIO.GetDiskSize(SafeFileHandle)

            Else

                Return NativeUnixIO.GetDiskSize(SafeFileHandle)

            End If

        End Function

        Public Function GetDiskGeometry(SafeFileHandle As SafeFileHandle) As DISK_GEOMETRY?

            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then

                Return NativeFileIO.GetDiskGeometry(SafeFileHandle)

            Else

                Return NativeUnixIO.GetDiskGeometry(SafeFileHandle)

            End If

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        Public Function OpenFileHandle(FileName As ReadOnlyMemory(Of Char),
                                       DesiredAccess As FileAccess,
                                       ShareMode As FileShare,
                                       CreationDisposition As FileMode,
                                       Overlapped As Boolean) As SafeFileHandle

            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then

                Return NativeFileIO.OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped)

            Else

                Return New FileStream(FileName.ToString(), CreationDisposition, DesiredAccess, ShareMode, 1, Overlapped).SafeFileHandle

            End If

        End Function


        Private ReadOnly _knownFormatsOffsets As New Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase) From {
            {"nrg", 600 << 9},
            {"sdi", 8 << 9}
        }

        ''' <summary>
        ''' Checks if filename contains a known extension for which PhDskMnt knows of a constant offset value. That value can be
        ''' later passed as Offset parameter to CreateDevice method.
        ''' </summary>
        ''' <param name="ImageFile">Name of disk image file.</param>
        Public Function GetOffsetByFileExt(ImageFile As String) As Long

            Dim Offset As Long
            If _knownFormatsOffsets.TryGetValue(Path.GetExtension(ImageFile), Offset) Then
                Return Offset
            Else
                Return 0
            End If

        End Function

        ''' <summary>
        ''' Returns sector size typically used for image file name extensions. Returns 2048 for
        ''' .iso, .nrg and .bin. Returns 512 for all other file name extensions.
        ''' </summary>
        ''' <param name="ImageFile">Name of disk image file.</param>
        Public Function GetSectorSizeFromFileName(imagefile As String) As UInteger

            imagefile.NullCheck(NameOf(imagefile))

            If imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) OrElse
                    imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) OrElse
                    imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) Then

                Return 2048
            Else
                Return 512
            End If

        End Function

        Private ReadOnly multipliers As New Dictionary(Of ULong, String) From
            {{1UL << 60, " EB"},
             {1UL << 50, " PB"},
             {1UL << 40, " TB"},
             {1UL << 30, " GB"},
             {1UL << 20, " MB"},
             {1UL << 10, " KB"}}

        Public Function FormatBytes(size As ULong) As String

            For Each m In multipliers
                If size >= m.Key Then
                    Return $"{size / m.Key:0.0}{m.Value}"
                End If
            Next

            Return $"{size} byte"

        End Function

        Public Function FormatBytes(size As ULong, precision As Integer) As String

            For Each m In multipliers
                If size >= m.Key Then
                    Return $"{(size / m.Key).ToString($"0.{New String("0"c, precision - 1)}")}{m.Value}"
                End If
            Next

            Return $"{size} byte"

        End Function

        Public Function FormatBytes(size As Long) As String

            For Each m In multipliers
                If Math.Abs(size) >= m.Key Then
                    Return $"{size / m.Key:0.000}{m.Value}"
                End If
            Next

            If size = 1 Then
                Return $"{size} byte"
            Else
                Return $"{size} bytes"
            End If

        End Function

        Public Function FormatBytes(size As Long, precision As Integer) As String

            For Each m In multipliers
                If size >= m.Key Then
                    Return $"{(size / m.Key).ToString("0." & New String("0"c, precision - 1))}{m.Value}"
                End If
            Next

            Return $"{size} byte"

        End Function

        ''' <summary>
        ''' Checks if Flags specifies a read only virtual disk.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function IsReadOnly(Flags As DeviceFlags) As Boolean

            Return Flags.HasFlag(DeviceFlags.ReadOnly)

        End Function

        ''' <summary>
        ''' Checks if Flags specifies a removable virtual disk.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function IsRemovable(Flags As DeviceFlags) As Boolean

            Return Flags.HasFlag(DeviceFlags.Removable)

        End Function

        ''' <summary>
        ''' Checks if Flags specifies a modified virtual disk.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function IsModified(Flags As DeviceFlags) As Boolean

            Return Flags.HasFlag(DeviceFlags.Modified)

        End Function

        ''' <summary>
        ''' Gets device type bits from a Flag field.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function GetDeviceType(Flags As DeviceFlags) As DeviceFlags

            Return CType(Flags And &HF0UI, DeviceFlags)

        End Function

        ''' <summary>
        ''' Gets disk type bits from a Flag field.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function GetDiskType(Flags As DeviceFlags) As DeviceFlags

            Return CType(Flags And &HF00UI, DeviceFlags)

        End Function

        ''' <summary>
        ''' Gets proxy type bits from a Flag field.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function GetProxyType(Flags As DeviceFlags) As DeviceFlags

            Return CType(Flags And &HF000UI, DeviceFlags)

        End Function

    End Module

End Namespace
