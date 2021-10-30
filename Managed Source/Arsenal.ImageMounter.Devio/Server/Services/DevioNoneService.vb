''''' DevioNoneService.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.IO
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Devio.Server.Interaction
Imports Arsenal.ImageMounter.IO
Imports DiscUtils

Namespace Server.Services

    ''' <summary>
    ''' Class deriving from DevioServiceBase, but without providing a proxy service. Instead,
    ''' it just passes a disk image file name or RAM disk information for direct mounting
    ''' internally in Arsenal Image Mounter SCSI Adapter.
    ''' </summary>
    Public Class DevioNoneService
        Inherits DevioServiceBase

        ''' <summary>
        ''' Name and path of image file mounted by Arsenal Image Mounter.
        ''' </summary>
        Public ReadOnly Property Imagefile As String

        ''' <summary>
        ''' FileAccess flags specifying whether to mount read-only or read-write.
        ''' </summary>
        Public ReadOnly Property DiskAccess As FileAccess

        ''' <summary>
        ''' Creates a DevioServiceBase compatible object, but without providing a proxy service.
        ''' Instead, it just passes a disk image file name for direct mounting internally in
        ''' SCSI Adapter.
        ''' </summary>
        ''' <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
        Public Sub New(Imagefile As String, DiskAccess As FileAccess)
            MyBase.New(New DummyProvider(NativeFileIO.GetFileSize(Imagefile)), OwnsProvider:=True)

            Offset = API.GetOffsetByFileExt(Imagefile)

            _DiskAccess = DiskAccess

            _Imagefile = Imagefile

            If Not DiskAccess.HasFlag(FileAccess.Write) Then
                _ProxyModeFlags = DeviceFlags.TypeFile Or DeviceFlags.ReadOnly
            Else
                _ProxyModeFlags = DeviceFlags.TypeFile
            End If

            _ProxyObjectName = Imagefile

        End Sub

        ''' <summary>
        ''' Creates a DevioServiceBase compatible object, but without providing a proxy service.
        ''' Instead, it just passes a disk image file name for direct mounting internally in
        ''' SCSI Adapter.
        ''' </summary>
        ''' <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
        Public Sub New(Imagefile As String, DiskAccess As DevioServiceFactory.VirtualDiskAccess)
            Me.New(Imagefile, DevioServiceFactory.GetDirectFileAccessFlags(DiskAccess))

        End Sub

        ''' <summary>
        ''' Creates a DevioServiceBase compatible object, but without providing a proxy service.
        ''' Instead, it just passes a disk size for directly mounting a RAM disk internally in
        ''' SCSI Adapter.
        ''' </summary>
        ''' <param name="DiskSize">Size in bytes of RAM disk to create.</param>
        Public Sub New(DiskSize As Long)
            MyBase.New(New DummyProvider(DiskSize), OwnsProvider:=True)

            DiskAccess = FileAccess.ReadWrite

            If NativeFileIO.TestFileOpen("\\?\awealloc") Then
                AdditionalFlags = DeviceFlags.TypeFile Or DeviceFlags.FileTypeAwe
            Else
                AdditionalFlags = DeviceFlags.TypeVM
            End If

        End Sub

        Private Shared Function GetVhdSize(Imagefile As String) As Long

            Using disk = VirtualDisk.OpenDisk(Imagefile, FileAccess.Read)

                Return disk.Capacity

            End Using

        End Function

        ''' <summary>
        ''' Creates a DevioServiceBase compatible object, but without providing a proxy service.
        ''' Instead, it just requests the SCSI adapter, awealloc and vhdaccess drivers to create
        ''' a dynamically expanding RAM disk based on the contents of the supplied VHD image.
        ''' </summary>
        ''' <param name="Imagefile">Path to VHD image file to use as template for the RAM disk.</param>
        Public Sub New(Imagefile As String)
            MyBase.New(New DummyProvider(GetVhdSize(Imagefile)), OwnsProvider:=True)

            DiskAccess = FileAccess.ReadWrite

            _Imagefile = Imagefile

            _ProxyObjectName = "\\?\vhdaccess\??\awealloc" & NativeFileIO.GetNtPath(Imagefile)

        End Sub

        Protected Overrides ReadOnly Property ProxyObjectName As String

        Protected Overrides ReadOnly Property ProxyModeFlags As DeviceFlags

        ''' <summary>
        ''' Dummy implementation that always returns True.
        ''' </summary>
        ''' <returns>Fixed value of True.</returns>
        Public Overrides Function StartServiceThread() As Boolean
            RunService()
            Return True
        End Function

        ''' <summary>
        ''' Dummy implementation that just raises ServiceReady event.
        ''' </summary>
        Public Overrides Sub RunService()
            OnServiceReady(EventArgs.Empty)
        End Sub

        Public Overrides Sub DismountAndStopServiceThread()
            MyBase.DismountAndStopServiceThread()
            OnServiceShutdown(EventArgs.Empty)
        End Sub

        Public Overrides Function DismountAndStopServiceThread(timeout As TimeSpan) As Boolean
            Dim rc = MyBase.DismountAndStopServiceThread(timeout)
            OnServiceShutdown(EventArgs.Empty)
            Return rc
        End Function

        Protected Overrides Sub EmergencyStopServiceThread()

        End Sub

    End Class

End Namespace
