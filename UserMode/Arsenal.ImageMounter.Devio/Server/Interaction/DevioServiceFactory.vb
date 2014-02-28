''''' DevioServiceFactory.vb
''''' Support routines for creating provider and service instances given a known
''''' proxy provider.
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
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
        Public Shared Function AutoMount(Imagefile As String, Adapter As ScsiAdapter, Proxy As ProxyType, Flags As DeviceFlags) As DevioServiceBase

            Dim DiskAccess As FileAccess

            If (Flags And DeviceFlags.ReadOnly) = 0 Then
                DiskAccess = FileAccess.ReadWrite
            Else
                DiskAccess = FileAccess.Read
            End If

            If Imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.DeviceTypeCD
            End If

            Dim Service = GetService(Imagefile, DiskAccess, Proxy)

            Service.StartServiceThreadAndMount(Adapter, Flags)

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

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified image file using DiscUtils library.
        ''' </summary>
        ''' <param name="Imagefile">Image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderDiscUtils(Imagefile As String, DiskAccess As FileAccess) As IDevioProvider

            Trace.WriteLine("Opening image " & Imagefile)

            Dim Device = VirtualDisk.OpenDisk(Imagefile, DiskAccess)

            If Device Is Nothing Then
                Dim fs As New FileStream(Imagefile, FileMode.Open, DiskAccess, FileShare.Read Or FileShare.Delete)
                Try
                    Device = New Dmg.Disk(fs, Ownership.Dispose)
                Catch
                    fs.Dispose()
                End Try
            End If

            If Device Is Nothing Then
                Trace.WriteLine("Image not recognized by DiscUtils." & Environment.NewLine &
                                  Environment.NewLine &
                                  "Formats currently supported: " & String.Join(", ", VirtualDisk.SupportedDiskTypes.ToArray()),
                                  "Error")
                Return Nothing
            End If
            Trace.WriteLine("Image type class: " & Device.GetType().ToString())

            If Device.IsPartitioned Then
                Trace.WriteLine("Partition table class: " & Device.Partitions.GetType().ToString())
            End If

            Trace.WriteLine("Image virtual size is " & Device.Capacity & " bytes")

            If Device.Geometry Is Nothing Then
                Trace.WriteLine("Image sector size is unknown")
            Else
                Trace.WriteLine("Image sector size is " & Device.Geometry.BytesPerSector & " bytes")
            End If

            Dim DiskStream = Device.Content
            Trace.WriteLine("Used size is " & DiskStream.Length & " bytes")

            If DiskStream.CanWrite Then
                Trace.WriteLine("Read/write mode.")
            Else
                Trace.WriteLine("Read-only mode.")
            End If

            Dim provider As New DevioProviderFromStream(DiskStream, ownsStream:=True)

            AddHandler provider.Disposed, Sub() Device.Dispose()

            Return provider

        End Function

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        ''' for servicing I/O requests to a specified set of multi-part raw image files.
        ''' </summary>
        ''' <param name="Imagefile">First part image file.</param>
        ''' <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        Public Shared Function GetProviderMultiPartRaw(Imagefile As String, DiskAccess As FileAccess) As IDevioProvider

            Dim DiskStream As New MultiPartFileStream(Imagefile, DiskAccess)

            Return New DevioProviderFromStream(DiskStream, ownsStream:=True)

        End Function

        ''' <summary>
        ''' Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
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
