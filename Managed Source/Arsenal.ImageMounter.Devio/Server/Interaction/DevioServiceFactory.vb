''''' DevioServiceFactory.vb
''''' Support routines for creating provider and service instances given a known
''''' proxy provider.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
'''''

Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Devio.Server.Services
Imports Arsenal.ImageMounter.Devio.Server.SpecializedProviders
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports DiscUtils
Imports DiscUtils.Streams

Namespace Server.Interaction

    ''' <summary>
    ''' Support routines for creating provider and service instances given a known proxy provider.
    ''' </summary>
    Public NotInheritable Class DevioServiceFactory

        ''' <summary>
        ''' Supported proxy types.
        ''' </summary>
        Public Enum ProxyType
            None

            LibEwf

            DiscUtils

            MultiPartRaw

            LibAFF4
        End Enum

        ''' <summary>
        ''' Virtual disk access modes. A list of supported modes for a particular ProxyType
        ''' is obtained by calling GetSupportedVirtualDiskAccess().
        ''' </summary>
        Public Enum VirtualDiskAccess

            [ReadOnly] = 1

            ReadWriteOriginal = 3

            ReadWriteOverlay = 7

            ReadOnlyFileSystem = 9

            ReadWriteFileSystem = 11

        End Enum

        Private Shared ReadOnly SupportedVirtualDiskAccess As New Dictionary(Of ProxyType, ReadOnlyCollection(Of VirtualDiskAccess)) From
            {
                {ProxyType.None,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOriginal,
                                   VirtualDiskAccess.ReadOnlyFileSystem,
                                   VirtualDiskAccess.ReadWriteFileSystem})},
                {ProxyType.MultiPartRaw,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOriginal,
                                   VirtualDiskAccess.ReadOnlyFileSystem,
                                   VirtualDiskAccess.ReadWriteFileSystem})},
                {ProxyType.DiscUtils,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOriginal,
                                   VirtualDiskAccess.ReadWriteOverlay,
                                   VirtualDiskAccess.ReadOnlyFileSystem,
                                   VirtualDiskAccess.ReadWriteFileSystem})},
                {ProxyType.LibEwf,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOverlay,
                                   VirtualDiskAccess.ReadOnlyFileSystem})},
                {ProxyType.LibAFF4,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadOnlyFileSystem})}
            }

        Private Shared ReadOnly NotSupportedFormatsForWriteOverlay As String() =
            {
                ".vdi",
                ".xva"
            }

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file. Once that is done, this method
        ''' automatically calls Arsenal Image Mounter to create a virtual disk device for this
        ''' image file.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="Adapter">Open ScsiAdapter object for communication with Arsenal Image Mounter.</param>
        ''' <param name="Flags">Additional flags to pass to ScsiAdapter.CreateDevice(). For example,
        ''' this could specify a flag for read-only mounting.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function AutoMount(Imagefile As String, Adapter As ScsiAdapter, Proxy As ProxyType, Flags As DeviceFlags, DiskAccess As VirtualDiskAccess) As DevioServiceBase

            If Imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) OrElse
                Imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) OrElse
                Imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) Then

                Flags = Flags Or DeviceFlags.DeviceTypeCD
            End If

            Dim Service = GetService(Imagefile, DiskAccess, Proxy)

            Service.StartServiceThreadAndMount(Adapter, Flags)

            Return Service

        End Function

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file. Once that is done, this method
        ''' automatically calls Arsenal Image Mounter to create a virtual disk device for this
        ''' image file.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="Adapter">Open ScsiAdapter object for communication with Arsenal Image Mounter.</param>
        ''' <param name="Flags">Additional flags to pass to ScsiAdapter.CreateDevice(). For example,
        ''' this could specify a flag for read-only mounting.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function AutoMount(Imagefile As String, Adapter As ScsiAdapter, Proxy As ProxyType, Flags As DeviceFlags) As DevioServiceBase

            Dim DiskAccess As FileAccess

            If Not Flags.HasFlag(DeviceFlags.ReadOnly) Then
                DiskAccess = FileAccess.ReadWrite
            Else
                DiskAccess = FileAccess.Read
            End If

            If Imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) OrElse
                Imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) OrElse
                Imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) Then

                Flags = Flags Or DeviceFlags.DeviceTypeCD
            End If

            Dim Service = GetService(Imagefile, DiskAccess, Proxy)

            Service.StartServiceThreadAndMount(Adapter, Flags)

            Return Service

        End Function

        Public Shared Function GetSupportedVirtualDiskAccess(Proxy As ProxyType, imagePath As String) As ReadOnlyCollection(Of VirtualDiskAccess)

            GetSupportedVirtualDiskAccess = Nothing
            If Not SupportedVirtualDiskAccess.TryGetValue(Proxy, GetSupportedVirtualDiskAccess) Then
                Throw New ArgumentException($"Proxy type not supported: {Proxy}", NameOf(Proxy))
            End If

            If Proxy = ProxyType.DiscUtils AndAlso
                NotSupportedFormatsForWriteOverlay.Contains(
                    Path.GetExtension(imagePath), StringComparer.OrdinalIgnoreCase) Then

                GetSupportedVirtualDiskAccess = GetSupportedVirtualDiskAccess.
                    Where(Function(acc) acc <> VirtualDiskAccess.ReadWriteOverlay).
                    ToList().
                    AsReadOnly()

            End If

        End Function

        ''' <summary>
        ''' Creates an object, of a DiscUtils.VirtualDisk derived class, for any supported image files format.
        ''' For image formats not directly supported by DiscUtils.dll, this creates a devio provider first which
        ''' then is opened as a DiscUtils.VirtualDisk wrapper object so that DiscUtils virtual disk features can
        ''' be used on the image anyway.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function GetDiscUtilsVirtualDisk(Imagefile As String, DiskAccess As FileAccess, Proxy As ProxyType) As VirtualDisk

            Dim virtualdisk As VirtualDisk

            Select Case Proxy

                Case ProxyType.DiscUtils
                    If Imagefile.EndsWith(".ova", StringComparison.OrdinalIgnoreCase) Then
                        virtualdisk = OpenOVA(Imagefile, DiskAccess)
                    Else
                        virtualdisk = VirtualDisk.OpenDisk(Imagefile, DiskAccess)
                    End If

                Case Else
                    Dim provider = GetProvider(Imagefile, DiskAccess, Proxy)
                    Dim geom = Geometry.FromCapacity(provider.Length, CInt(provider.SectorSize))
                    virtualdisk = New Raw.Disk(New Client.DevioDirectStream(provider, ownsProvider:=True), Ownership.Dispose, geom)

            End Select

            Return virtualdisk

        End Function

        ''' <summary>
        ''' Opens a VMDK image file embedded in an OVA archive.
        ''' </summary>
        ''' <param name="imagefile">Path to OVA archive file</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <returns></returns>
        Public Shared Function OpenOVA(imagefile As String, diskAccess As FileAccess) As VirtualDisk

            If diskAccess.HasFlag(FileAccess.Write) Then
                Throw New NotSupportedException("Cannot modify OVA files")
            End If

            Dim ova = File.Open(imagefile, FileMode.Open, FileAccess.Read)

            Try
                Dim vmdk = Aggregate file In Archives.TarFile.EnumerateFiles(ova)
                           Into FirstOrDefault(file.Name.EndsWith(".vmdk", StringComparison.OrdinalIgnoreCase))

                If vmdk Is Nothing Then

                    Throw New NotSupportedException($"The OVA file {imagefile} does not contain an embedded vmdk file.")

                End If

                Dim virtual_disk As New Vmdk.Disk(vmdk.GetStream(), Ownership.Dispose)
                AddHandler virtual_disk.Disposed, Sub() ova.Dispose()
                Return virtual_disk

            Catch ex As Exception
                ova.Dispose()

                Throw New Exception($"Error opening {imagefile}", ex)

            End Try

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file. This does not create a DevioServiceBase
        ''' object that can actually serve incoming requests, it just creates the provider object that can
        ''' be used with a later created DevioServiceBase object.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function GetProvider(Imagefile As String, DiskAccess As FileAccess, Proxy As ProxyType) As IDevioProvider

            Dim GetProviderFunc As Func(Of String, FileAccess, IDevioProvider) = Nothing

            If _InstalledProvidersByProxyValueAndFileAccess.TryGetValue(Proxy, GetProviderFunc) Then

                Return GetProviderFunc(Imagefile, DiskAccess)

            End If

            Throw New InvalidOperationException($"Proxy {Proxy} not supported.")

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file. This does not create a DevioServiceBase
        ''' object that can actually serve incoming requests, it just creates the provider object that can
        ''' be used with a later created DevioServiceBase object.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function GetProvider(Imagefile As String, DiskAccess As VirtualDiskAccess, Proxy As ProxyType) As IDevioProvider

            Dim device_number As UInteger

            If UInteger.TryParse(Imagefile, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, device_number) Then

                Return GetProviderPhysical(device_number, DiskAccess)

            ElseIf (Imagefile.StartsWith("\\?\", StringComparison.OrdinalIgnoreCase) OrElse
                Imagefile.StartsWith("\\.\", StringComparison.OrdinalIgnoreCase)) AndAlso
                Imagefile.IndexOf("\"c, 4) < 0 Then

                Return GetProviderPhysical(Imagefile, DiskAccess)

            End If

            Dim GetProviderFunc As Func(Of String, VirtualDiskAccess, IDevioProvider) = Nothing

            If _InstalledProvidersByProxyValueAndVirtualDiskAccess.TryGetValue(Proxy, GetProviderFunc) Then

                Return GetProviderFunc(Imagefile, DiskAccess)

            End If

            Throw New InvalidOperationException($"Proxy {Proxy} not supported.")

        End Function

        Public Shared Function GetProvider(Imagefile As String, DiskAccess As FileAccess, ProviderName As String) As IDevioProvider

            Dim device_number As UInteger

            If UInteger.TryParse(Imagefile, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, device_number) Then

                Return GetProviderPhysical(device_number, DiskAccess)

            ElseIf (Imagefile.StartsWith("\\?\", StringComparison.OrdinalIgnoreCase) OrElse
                Imagefile.StartsWith("\\.\", StringComparison.OrdinalIgnoreCase)) AndAlso
                Imagefile.IndexOf("\"c, 4) < 0 Then

                Return GetProviderPhysical(Imagefile, DiskAccess)

            End If

            Dim GetProviderFunc As Func(Of String, FileAccess, IDevioProvider) = Nothing

            If _InstalledProvidersByNameAndFileAccess.TryGetValue(ProviderName, GetProviderFunc) Then

                Return GetProviderFunc(Imagefile, DiskAccess)

            End If

            Throw New NotSupportedException($"Provider '{ProviderName}' not supported. Valid values are: {String.Join(", ", _InstalledProvidersByNameAndFileAccess.Keys)}.")

        End Function

        Private Shared Function GetProviderPhysical(DeviceNumber As UInteger, DiskAccess As VirtualDiskAccess) As DevioProviderFromStream

            Return GetProviderPhysical(DeviceNumber, GetDirectFileAccessFlags(DiskAccess))

        End Function

        Private Shared Function GetProviderPhysical(DevicePath As String, DiskAccess As VirtualDiskAccess) As DevioProviderFromStream

            Return GetProviderPhysical(DevicePath, GetDirectFileAccessFlags(DiskAccess))

        End Function

        Private Shared Function GetProviderPhysical(DeviceNumber As UInteger, DiskAccess As FileAccess) As DevioProviderFromStream

            Using adapter As New ScsiAdapter

                Dim disk = adapter.OpenDevice(DeviceNumber, DiskAccess)

                Return New DevioProviderFromStream(disk.GetRawDiskStream(), ownsStream:=True) With {
                    .CustomSectorSize = CUInt(If(disk.Geometry?.BytesPerSector, 512))
                }

            End Using

        End Function

        Private Shared Function GetProviderPhysical(DevicePath As String, DiskAccess As FileAccess) As DevioProviderFromStream

            Dim disk As New DiskDevice(DevicePath, DiskAccess)

            Return New DevioProviderFromStream(disk.GetRawDiskStream(), ownsStream:=True) With {
                .CustomSectorSize = CUInt(If(disk.Geometry?.BytesPerSector, 512))
            }

        End Function

        Private Shared Function GetProviderRaw(Imagefile As String, DiskAccess As VirtualDiskAccess) As DevioProviderFromStream

            Return GetProviderRaw(Imagefile, GetDirectFileAccessFlags(DiskAccess))

        End Function

        Private Shared Function GetProviderRaw(Imagefile As String, DiskAccess As FileAccess) As DevioProviderFromStream

            Return New DevioProviderFromStream(NativeFileIO.OpenFileStream(Imagefile, FileMode.Open, DiskAccess, FileShare.Read Or FileShare.Delete), ownsStream:=True) With {
                .CustomSectorSize = API.GetSectorSizeFromFileName(Imagefile)
            }

        End Function

        Public Shared ReadOnly Property InstalledProvidersByProxyValueAndVirtualDiskAccess As New Dictionary(Of ProxyType, Func(Of String, VirtualDiskAccess, IDevioProvider))() From {
            {ProxyType.DiscUtils, AddressOf GetProviderDiscUtils},
            {ProxyType.LibEwf, AddressOf GetProviderLibEwf},
            {ProxyType.LibAFF4, AddressOf GetProviderLibAFF4},
            {ProxyType.MultiPartRaw, AddressOf GetProviderMultiPartRaw},
            {ProxyType.None, AddressOf GetProviderRaw}
        }

        Public Shared ReadOnly Property InstalledProvidersByProxyValueAndFileAccess As New Dictionary(Of ProxyType, Func(Of String, FileAccess, IDevioProvider))() From {
            {ProxyType.DiscUtils, AddressOf GetProviderDiscUtils},
            {ProxyType.LibEwf, AddressOf GetProviderLibEwf},
            {ProxyType.LibAFF4, AddressOf GetProviderLibAFF4},
            {ProxyType.MultiPartRaw, AddressOf GetProviderMultiPartRaw},
            {ProxyType.None, AddressOf GetProviderRaw}
        }

        Public Shared ReadOnly Property InstalledProvidersByNameAndVirtualDiskAccess As New Dictionary(Of String, Func(Of String, VirtualDiskAccess, IDevioProvider))(StringComparer.OrdinalIgnoreCase) From {
            {"DiscUtils", AddressOf GetProviderDiscUtils},
            {"LibEwf", AddressOf GetProviderLibEwf},
            {"LibAFF4", AddressOf GetProviderLibAFF4},
            {"MultiPartRaw", AddressOf GetProviderMultiPartRaw},
            {"None", AddressOf GetProviderRaw}
        }

        Public Shared ReadOnly Property InstalledProvidersByNameAndFileAccess As New Dictionary(Of String, Func(Of String, FileAccess, IDevioProvider))(StringComparer.OrdinalIgnoreCase) From {
            {"DiscUtils", AddressOf GetProviderDiscUtils},
            {"LibEwf", AddressOf GetProviderLibEwf},
            {"LibAFF4", AddressOf GetProviderLibAFF4},
            {"MultiPartRaw", AddressOf GetProviderMultiPartRaw},
            {"None", AddressOf GetProviderRaw}
        }

        Private Shared ReadOnly DiscUtilsAssemblies As Assembly() = {
            GetType(Vmdk.Disk).Assembly,
            GetType(Vhdx.Disk).Assembly,
            GetType(Vhd.Disk).Assembly,
            GetType(Vdi.Disk).Assembly,
            GetType(Dmg.Disk).Assembly,
            GetType(Xva.Disk).Assembly,
            GetType(OpticalDisk.Disc).Assembly,
            GetType(Raw.Disk).Assembly
        }

        Public Shared ReadOnly Property DiscUtilsInitialized As Boolean = InitializeDiscUtils()

        Private Shared Function InitializeDiscUtils() As Boolean
            Dim done = False
            For Each asm In DiscUtilsAssemblies.Distinct()
                Trace.WriteLine($"Registering DiscUtils assembly '{asm.FullName}'...")
                Setup.SetupHelper.RegisterAssembly(asm)
                done = True
            Next
            Return done
        End Function

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function GetService(Imagefile As String, DiskAccess As VirtualDiskAccess, Proxy As ProxyType) As DevioServiceBase

            Return GetService(Imagefile, DiskAccess, Proxy, FakeMBR:=False)

        End Function

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function GetService(Imagefile As String, DiskAccess As VirtualDiskAccess, Proxy As ProxyType, FakeMBR As Boolean) As DevioServiceBase

            If Proxy = ProxyType.None AndAlso Not FakeMBR Then

                Return New DevioNoneService(Imagefile, DiskAccess)

            ElseIf Proxy = ProxyType.DiscUtils AndAlso Not FakeMBR AndAlso
                    (Imagefile.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) OrElse
                    Imagefile.EndsWith(".avhd", StringComparison.OrdinalIgnoreCase)) Then

                Return New DevioNoneService("\\?\vhdaccess" & NativeFileIO.GetNtPath(Imagefile), DiskAccess)

            End If

            Dim Provider = GetProvider(Imagefile, DiskAccess, Proxy)

            If FakeMBR Then

                Provider = New DevioProviderWithFakeMBR(Provider)

            End If

            Dim Service = New DevioShmService(Provider, OwnsProvider:=True) With {
                .Description = $"Image file {Imagefile}"
            }

            Return Service

        End Function

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function GetService(Imagefile As String, DiskAccess As FileAccess, Proxy As ProxyType) As DevioServiceBase

            Dim Service As DevioServiceBase

            Select Case Proxy

                Case ProxyType.None
                    If Imagefile.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) OrElse
                        Imagefile.EndsWith(".avhd", StringComparison.OrdinalIgnoreCase) Then

                        Return New DevioNoneService("\\?\vhdaccess" & NativeFileIO.GetNtPath(Imagefile), DiskAccess)

                    Else

                        Service = New DevioNoneService(Imagefile, DiskAccess)

                    End If

                Case Else
                    Service = New DevioShmService(GetProvider(Imagefile, DiskAccess, Proxy), OwnsProvider:=True)

            End Select

            Service.Description = $"Image file {Imagefile}"

            Return Service

        End Function

        Friend Shared Function GetDirectFileAccessFlags(DiskAccess As VirtualDiskAccess) As FileAccess
            If (DiskAccess And Not FileAccess.ReadWrite) <> 0 Then
                Throw New ArgumentException($"Unsupported VirtualDiskAccess flags For direct file access: {DiskAccess}", NameOf(DiskAccess))
            End If
            Return CType(DiskAccess, FileAccess)
        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using DiscUtils library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderDiscUtils(Imagefile As String, DiskAccess As FileAccess) As IDevioProvider

            Dim VirtualDiskAccess As VirtualDiskAccess

            Select Case DiskAccess
                Case FileAccess.Read
                    VirtualDiskAccess = VirtualDiskAccess.ReadOnly

                Case FileAccess.ReadWrite
                    VirtualDiskAccess = VirtualDiskAccess.ReadWriteOriginal

                Case Else
                    Throw New ArgumentException($"Unsupported DiskAccess for DiscUtils: {DiskAccess}", NameOf(DiskAccess))

            End Select

            Return GetProviderDiscUtils(Imagefile, VirtualDiskAccess)

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using DiscUtils library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderDiscUtils(Imagefile As String, DiskAccess As VirtualDiskAccess) As IDevioProvider

            Dim FileAccess As FileAccess

            Select Case DiskAccess
                Case VirtualDiskAccess.ReadOnly
                    FileAccess = FileAccess.Read

                Case VirtualDiskAccess.ReadWriteOriginal
                    FileAccess = FileAccess.ReadWrite

                Case VirtualDiskAccess.ReadWriteOverlay
                    FileAccess = FileAccess.Read

                Case Else
                    Throw New ArgumentException($"Unsupported DiskAccess for DiscUtils: {DiskAccess}", NameOf(DiskAccess))

            End Select

            Trace.WriteLine($"Opening image {Imagefile}")

            Dim Disk = GetDiscUtilsVirtualDisk(Imagefile, FileAccess, ProxyType.DiscUtils)

            If Disk Is Nothing Then
                Dim fs As New FileStream(Imagefile, FileMode.Open, FileAccess, FileShare.Read Or FileShare.Delete)
                Try
                    Disk = New Dmg.Disk(fs, Ownership.Dispose)
                Catch
                    fs.Dispose()
                End Try
            End If

            If Disk Is Nothing Then
                Trace.WriteLine($"Image not recognized by DiscUtils.{Environment.NewLine}
{Environment.NewLine}Formats currently supported: {String.Join(", ", VirtualDiskManager.SupportedDiskTypes)}",
                                  "Error")
                Return Nothing
            End If

            Trace.WriteLine($"Image type class: {Disk.DiskTypeInfo?.Name} ({Disk.DiskTypeInfo?.Variant})")

            Dim DisposableObjects As New List(Of IDisposable) From {
                Disk
            }

            Try

                If Disk.IsPartitioned Then
                    Trace.WriteLine($"Partition table class: {Disk.Partitions.GetType()}")
                End If

                Trace.WriteLine($"Image virtual size is {Disk.Capacity} bytes")

                Dim SectorSize As UInteger

                If Disk.Geometry Is Nothing Then
                    SectorSize = 512
                    Trace.WriteLine("Image sector size is unknown, assuming 512 bytes")
                Else
                    SectorSize = CUInt(Disk.Geometry.BytesPerSector)
                    Trace.WriteLine($"Image sector size is {SectorSize} bytes")
                End If

                If DiskAccess = VirtualDiskAccess.ReadWriteOverlay Then

                    Dim DifferencingPath =
                        Path.Combine(Path.GetDirectoryName(Imagefile),
                                     $"{Path.GetFileNameWithoutExtension(Imagefile)}_aimdiff{Path.GetExtension(Imagefile)}")

                    Trace.WriteLine($"Using temporary overlay file '{DifferencingPath}'")

                    Do
                        Try
                            If File.Exists(DifferencingPath) Then
                                If UseExistingDifferencingDisk(DifferencingPath) Then
                                    Disk = VirtualDisk.OpenDisk(DifferencingPath, FileAccess.ReadWrite)
                                    Exit Do
                                End If

                                File.Delete(DifferencingPath)
                            End If

                            Disk = Disk.CreateDifferencingDisk(DifferencingPath)
                            Exit Do

                        Catch ex As Exception When _
                                Not ex.Enumerate().OfType(Of OperationCanceledException)().Any() AndAlso
                                HandleDifferencingDiskCreationError(ex, DifferencingPath)

                        End Try
                    Loop

                    DisposableObjects.Add(Disk)
                End If

                Dim DiskStream = Disk.Content
                Trace.WriteLine($"Used size is {DiskStream.Length} bytes")

                If DiskStream.CanWrite Then
                    Trace.WriteLine("Read/write mode.")
                Else
                    Trace.WriteLine("Read-only mode.")
                End If

                Dim provider As New DevioProviderFromStream(DiskStream, ownsStream:=True) With {
                    .CustomSectorSize = SectorSize
                }

                AddHandler provider.Disposed, Sub() DisposableObjects.ForEach(Sub(obj) obj.Dispose())

                Return provider

            Catch ex As Exception

                DisposableObjects.ForEach(Sub(obj) obj.Dispose())

                Throw New Exception($"Error opening {Imagefile}", ex)

            End Try

        End Function

        Public Class PathExceptionEventArgs
            Inherits EventArgs

            Public Property Exception As Exception

            Public Property Path As String

            Public Property Handled As Boolean

        End Class

        Public Shared Event DifferencingDiskCreationError As EventHandler(Of PathExceptionEventArgs)

        Private Shared Function HandleDifferencingDiskCreationError(ex As Exception, ByRef differencingPath As String) As Boolean
            Dim e As New PathExceptionEventArgs With {
                .Exception = ex,
                .Path = differencingPath
            }

            RaiseEvent DifferencingDiskCreationError(Nothing, e)

            differencingPath = e.Path

            Return e.Handled
        End Function

        Public Class PathRequestEventArgs
            Inherits EventArgs

            Public Property Path As String

            Public Property Response As Boolean

        End Class

        Public Shared Event UseExistingDifferencingDiskUserRequest As EventHandler(Of PathRequestEventArgs)

        Private Shared Function UseExistingDifferencingDisk(ByRef differencingPath As String) As Boolean
            Dim e As New PathRequestEventArgs With {
                .Path = differencingPath
            }

            RaiseEvent UseExistingDifferencingDiskUserRequest(Nothing, e)

            differencingPath = e.Path

            Return e.Response
        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified set of multi-part raw image files.
        ''' </summary>
        ''' <param name="Imagefile">First part image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderMultiPartRaw(Imagefile As String, DiskAccess As VirtualDiskAccess) As IDevioProvider

            Return GetProviderMultiPartRaw(Imagefile, GetDirectFileAccessFlags(DiskAccess))

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified set of multi-part raw image files.
        ''' </summary>
        ''' <param name="Imagefile">First part image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderMultiPartRaw(Imagefile As String, DiskAccess As FileAccess) As IDevioProvider

            Dim DiskStream As New MultiPartFileStream(Imagefile, DiskAccess)

            Return New DevioProviderFromStream(DiskStream, ownsStream:=True) With {
                .CustomSectorSize = API.GetSectorSizeFromFileName(Imagefile)
            }

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified set of multi-part raw image files.
        ''' </summary>
        ''' <param name="Imagefile">First part image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderMultiPartRaw(Imagefile As String, DiskAccess As FileAccess, ShareMode As FileShare) As IDevioProvider

            Dim DiskStream As New MultiPartFileStream(Imagefile, DiskAccess, ShareMode)

            Return New DevioProviderFromStream(DiskStream, ownsStream:=True) With {
                .CustomSectorSize = API.GetSectorSizeFromFileName(Imagefile)
            }

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using libewf library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderLibEwf(Imagefile As String, DiskAccess As VirtualDiskAccess) As IDevioProvider

            Dim FileAccess As FileAccess

            Select Case DiskAccess
                Case VirtualDiskAccess.ReadOnly
                    FileAccess = FileAccess.Read

                Case VirtualDiskAccess.ReadWriteOverlay
                    FileAccess = FileAccess.ReadWrite

                Case Else
                    Throw New ArgumentException($"Unsupported VirtualDiskAccess for libewf: {DiskAccess}", NameOf(DiskAccess))

            End Select

            Return GetProviderLibEwf(Imagefile, FileAccess)

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using libewf library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderLibEwf(Imagefile As String, DiskAccess As FileAccess) As IDevioProvider

            Dim Flags As Byte

            If DiskAccess.HasFlag(FileAccess.Read) Then
                Flags = Flags Or DevioProviderLibEwf.AccessFlagsRead
            End If

            If DiskAccess.HasFlag(FileAccess.Write) Then
                Flags = Flags Or DevioProviderLibEwf.AccessFlagsWrite
            End If

            Return New DevioProviderLibEwf(Imagefile, Flags)

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using libaff4 library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Only read access to image file supported.</param>
        Public Shared Function GetProviderLibAFF4(Imagefile As String, DiskAccess As VirtualDiskAccess) As IDevioProvider

            Select Case DiskAccess
                Case VirtualDiskAccess.ReadOnly

                Case Else
                    Throw New IOException("Only read-only mode supported with libaff4")

            End Select

            Return GetProviderLibAFF4(Imagefile, 0)

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using libaff4 library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Only read access supported.</param>
        Public Shared Function GetProviderLibAFF4(Imagefile As String, DiskAccess As FileAccess) As IDevioProvider

            If DiskAccess.HasFlag(FileAccess.Write) Then
                Throw New IOException("Only read-only mode supported with libaff4")
            End If

            Return GetProviderLibAFF4(Imagefile, 0)

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using libaff4 library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        Public Shared Function GetProviderLibAFF4(Imagefile As String) As IDevioProvider()

            Dim number_of_images = CInt(DevioProviderLibAFF4.getimagecount(Imagefile))

            Dim providers(0 To number_of_images - 1) As IDevioProvider

            Try

                For i = 0 To number_of_images - 1
                    providers(i) = GetProviderLibAFF4(Imagefile, i)
                Next

            Catch ex As Exception

                Array.ForEach(providers, Sub(p) p?.Dispose())

                Throw New Exception("Error in libaff4.dll", ex)

            End Try

            Return providers

        End Function

        ''' <summary>
        ''' Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using libaff4 library.
        ''' </summary>
        ''' <param name="containerfile">Container file containing image to mount.</param>
        ''' <param name="index">Index of image to mount within container file.</param>
        Public Shared Function GetProviderLibAFF4(containerfile As String, index As Integer) As IDevioProvider

            Return New DevioProviderLibAFF4(String.Concat(containerfile, ContainerIndexSeparator, index.ToString()))

        End Function

        Private Const ContainerIndexSeparator = ":::"

    End Class

End Namespace
