''''' DevioServiceFactory.vb
''''' Support routines for creating provider and service instances given a known
''''' proxy provider.
''''' 
''''' Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.Devio.Server.Services
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Devio.Server.SpecializedProviders

Namespace Server.Interaction

    ''' <summary>
    ''' Support routines for creating provider and service instances given a known proxy provider.
    ''' </summary>
    Public Class DevioServiceFactory

        ''' <summary>
        ''' Supported proxy types.
        ''' </summary>
        Public Enum ProxyType
            None
            LibEwf
            DiscUtils

            MultiPartRaw

        End Enum

        ''' <summary>
        ''' Virtual disk access modes. A list of supported modes for a particular ProxyType
        ''' is obtained by calling GetSupportedVirtualDiskAccess().
        ''' </summary>
        <Flags>
        Public Enum VirtualDiskAccess

            [ReadOnly] = 1

            ReadWriteOriginal = 3

            ReadWriteOverlay = 7

        End Enum

        Private Shared SupportedVirtualDiskAccess As New Dictionary(Of ProxyType, ReadOnlyCollection(Of VirtualDiskAccess)) From
            {
                {ProxyType.None,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOriginal})},
                {ProxyType.MultiPartRaw,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOriginal})},
                {ProxyType.DiscUtils,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOriginal,
                                   VirtualDiskAccess.ReadWriteOverlay})},
                {ProxyType.LibEwf,
                 Array.AsReadOnly({VirtualDiskAccess.ReadOnly,
                                   VirtualDiskAccess.ReadWriteOverlay})}
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

            If (Flags And DeviceFlags.ReadOnly) = 0 Then
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

        Public Shared Function GetSupportedVirtualDiskAccess(Proxy As ProxyType) As ReadOnlyCollection(Of VirtualDiskAccess)

            GetSupportedVirtualDiskAccess = Nothing
            If Not SupportedVirtualDiskAccess.TryGetValue(Proxy, GetSupportedVirtualDiskAccess) Then
                Throw New ArgumentException("Proxy type not supported: " & Proxy.ToString(), "Proxy")
            End If

        End Function

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        ''' <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        Public Shared Function GetService(Imagefile As String, DiskAccess As VirtualDiskAccess, Proxy As ProxyType) As DevioServiceBase

            Dim Service As DevioServiceBase

            Select Case Proxy

                Case ProxyType.MultiPartRaw
                    Service = New DevioShmService(GetProviderMultiPartRaw(Imagefile, DiskAccess), OwnsProvider:=True)

                Case ProxyType.DiscUtils
                    Service = New DevioShmService(GetProviderDiscUtils(Imagefile, DiskAccess), OwnsProvider:=True)

                Case ProxyType.LibEwf
                    Service = New DevioShmService(GetProviderLibEwf(Imagefile, DiskAccess), OwnsProvider:=True)

                Case ProxyType.None
                    Service = New DevioNoneService(Imagefile, DiskAccess)

                Case Else
                    Throw New NotSupportedException("Proxy " & Proxy.ToString() & " not supported.")

            End Select

            Service.Description = "Image file " & Imagefile

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

                Case ProxyType.MultiPartRaw
                    Service = New DevioShmService(GetProviderMultiPartRaw(Imagefile, DiskAccess), OwnsProvider:=True)

                Case ProxyType.DiscUtils
                    Service = New DevioShmService(GetProviderDiscUtils(Imagefile, DiskAccess), OwnsProvider:=True)

                Case ProxyType.LibEwf
                    Service = New DevioShmService(GetProviderLibEwf(Imagefile, DiskAccess), OwnsProvider:=True)

                Case ProxyType.None
                    Service = New DevioNoneService(Imagefile, DiskAccess)

                Case Else
                    Throw New NotSupportedException("Proxy " & Proxy.ToString() & " not supported.")

            End Select

            Service.Description = "Image file " & Imagefile

            Return Service

        End Function

        Friend Shared Function GetDirectFileAccessFlags(DiskAccess As VirtualDiskAccess) As FileAccess
            If (DiskAccess And Not FileAccess.ReadWrite) <> 0 Then
                Throw New ArgumentException("Unsupported VirtualDiskAccess flags for direct file access: " & DiskAccess.ToString(), "DiskAccess")
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
                    Throw New ArgumentException("Unsupported DiskAccess for DiscUtils: " & DiskAccess.ToString(), "DiskAccess")

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
                    Throw New ArgumentException("Unsupported DiskAccess for DiscUtils: " & DiskAccess.ToString(), "DiskAccess")

            End Select

            Trace.WriteLine("Opening image " & Imagefile)

            Dim Disk As VirtualDisk = VirtualDisk.OpenDisk(Imagefile, FileAccess)

            If Disk Is Nothing Then
                Dim fs As New FileStream(Imagefile, FileMode.Open, FileAccess, FileShare.Read Or FileShare.Delete)
                Try
                    Disk = New Dmg.Disk(fs, Ownership.Dispose)
                Catch
                    fs.Dispose()
                End Try
            End If

            If Disk Is Nothing Then
                Trace.WriteLine("Image not recognized by DiscUtils." & Environment.NewLine &
                                  Environment.NewLine &
                                  "Formats currently supported: " & String.Join(", ", VirtualDisk.SupportedDiskTypes),
                                  "Error")
                Return Nothing
            End If
            Trace.WriteLine("Image type class: " & Disk.GetType().ToString())

            Dim DisposableObjects As New List(Of IDisposable)

            DisposableObjects.Add(Disk)

            Try

                If Disk.IsPartitioned Then
                    Trace.WriteLine("Partition table class: " & Disk.Partitions.GetType().ToString())
                End If

                Trace.WriteLine("Image virtual size is " & Disk.Capacity & " bytes")

                Dim SectorSize As UInteger

                If Disk.Geometry Is Nothing Then
                    SectorSize = 512
                    Trace.WriteLine("Image sector size is unknown")
                Else
                    SectorSize = CUInt(Disk.Geometry.BytesPerSector)
                    Trace.WriteLine("Image sector size is " & Disk.Geometry.BytesPerSector & " bytes")
                End If

                If DiskAccess = VirtualDiskAccess.ReadWriteOverlay Then
                    Dim DifferencingPath =
                        Path.Combine(Path.GetDirectoryName(Imagefile),
                                     Path.GetFileNameWithoutExtension(Imagefile) & "_aimdiff" & Path.GetExtension(Imagefile))

                    Trace.WriteLine("Using temporary overlay file '" & DifferencingPath & "'")

                    If File.Exists(DifferencingPath) Then
                        File.Delete(DifferencingPath)
                    End If

                    Disk = Disk.CreateDifferencingDisk(DifferencingPath)
                    DisposableObjects.Add(Disk)
                End If

                Dim DiskStream = Disk.Content
                Trace.WriteLine("Used size is " & DiskStream.Length & " bytes")

                If DiskStream.CanWrite Then
                    Trace.WriteLine("Read/write mode.")
                Else
                    Trace.WriteLine("Read-only mode.")
                End If

                Dim provider As New DevioProviderFromStream(DiskStream, ownsStream:=True) With {
                    .CustomSectorSize = SectorSize
                }

                AddHandler provider.Disposed,
                    Sub() DisposableObjects.ForEach(Sub(obj) obj.Dispose())

                Return provider

            Catch When (Function()
                            DisposableObjects.ForEach(Sub(obj) obj.Dispose())
                            Return False
                        End Function)()
                Throw

            End Try

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
        Public Shared Function GetProviderMultiPartRaw(Imagefile As String, DiskAccess As FileAccess, ShareMode As FileShare?) As IDevioProvider

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
                    Throw New ArgumentException("Unsupported VirtualDiskAccess for libewf: " & DiskAccess.ToString(), "DiskAccess")

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

            If (DiskAccess And FileAccess.Read) = FileAccess.Read Then
                Flags = Flags Or DevioProviderLibEwf.AccessFlagsRead
            End If

            If (DiskAccess And FileAccess.Write) = FileAccess.Write Then
                Flags = Flags Or DevioProviderLibEwf.AccessFlagsWrite
            End If

            Return New DevioProviderLibEwf(Imagefile, Flags)

        End Function

    End Class

End Namespace
