
''''' ProviderSupport.vb
''''' 
''''' Copyright (c) 2012-2020, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Runtime.CompilerServices
Imports Arsenal.ImageMounter.IO

Namespace Server.GenericProviders

    Public Module ProviderSupport

        Public Property ImageConversionIoBufferSize As Integer = 2 << 20

        Public Function GetMultiSegmentFiles(FirstFile As String) As String()

            Dim pathpart = Path.GetDirectoryName(FirstFile)
            Dim filepart = Path.GetFileNameWithoutExtension(FirstFile)
            Dim extension = Path.GetExtension(FirstFile)
            Dim foundfiles As String() = Nothing

            If extension.EndsWith("01", StringComparison.Ordinal) OrElse
                extension.EndsWith("00", StringComparison.Ordinal) Then

                Dim start = extension.Length - 3

                While start >= 0 AndAlso Char.IsDigit(extension, start)
                    start -= 1
                End While

                start += 1

                Dim segmentnumberchars = New String("?"c, extension.Length - start)
                Dim namebase = filepart & extension.Remove(start)
                Dim pathbase = Path.Combine(Path.GetDirectoryName(FirstFile), namebase)
                Dim dir_name = Path.GetDirectoryName(FirstFile)
                Dim dir_pattern = namebase & segmentnumberchars

                If String.IsNullOrWhiteSpace(dir_name) Then
                    dir_name = "."
                End If

                Try
                    foundfiles =
                        Directory.GetFiles(dir_name, dir_pattern)

                Catch ex As Exception
                    Throw New Exception($"Failed enumerating files '{dir_pattern}' in directory '{dir_name}'", ex)

                End Try

                Array.Sort(foundfiles, StringComparer.Ordinal)

            Else

                If File.Exists(FirstFile) Then
                    foundfiles = {FirstFile}
                End If

            End If

            If foundfiles Is Nothing OrElse foundfiles.Length = 0 Then
                Throw New FileNotFoundException("Image file not found", FirstFile)
            End If

            Return foundfiles

        End Function

        <Extension>
        Public Sub ConvertToDiscUtilsImage(provider As IDevioProvider, outputImage As String, type As String, OutputImageVariant As String, cancel As CancellationToken)

            Using builder = VirtualDisk.CreateDisk(type, OutputImageVariant, outputImage, provider.Length, Geometry.FromCapacity(provider.Length, CInt(provider.SectorSize)), Nothing)

                Dim target = builder.Content

                provider.WriteToSkipEmptyBlocks(target, ImageConversionIoBufferSize, cancel)

            End Using

        End Sub

        <Extension>
        Public Sub ConvertToRawImage(provider As IDevioProvider, outputImage As String, OutputImageVariant As String, cancel As CancellationToken)

            Using target As New FileStream(outputImage, FileMode.Create, FileAccess.Write, FileShare.Delete, ImageConversionIoBufferSize)

                If "fixed".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase) Then

                ElseIf "dynamic".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase) Then

                    NativeFileIO.SetFileSparseFlag(target.SafeFileHandle, True)

                Else

                    Throw New ArgumentException($"Value {OutputImageVariant} not supported as output image variant. Valid values are fixed or dynamic.")

                End If

                provider.WriteToSkipEmptyBlocks(target, ImageConversionIoBufferSize, cancel)

            End Using

        End Sub

        <Extension>
        Public Sub WriteToSkipEmptyBlocks(source As IDevioProvider, target As Stream, buffersize As Integer, cancel As CancellationToken)

            '' 2 MB buffer
            Dim buffer(0 To buffersize - 1) As Byte

            Dim count = 0

            Dim source_position = 0L

            Do

                cancel.ThrowIfCancellationRequested()

                Dim length_to_read = CInt(Math.Min(buffer.Length, source.Length - source_position))

                If length_to_read = 0 Then

                    Exit Do

                End If

                count = source.Read(buffer, 0, length_to_read, source_position)

                If count = 0 Then

                    Throw New IOException($"Read error, {length_to_read} bytes from {source_position}")

                End If

                source_position += count

                Const zero As Byte = 0

                If Array.TrueForAll(buffer, AddressOf zero.Equals) Then

                    target.Seek(count, SeekOrigin.Current)

                Else

                    cancel.ThrowIfCancellationRequested()

                    target.Write(buffer, 0, count)

                End If

            Loop

            If target.Length <> target.Position Then

                target.SetLength(target.Position)

            End If

        End Sub

    End Module

End Namespace
