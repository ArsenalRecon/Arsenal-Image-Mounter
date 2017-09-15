''''' DevioNoneService.vb
''''' 
''''' Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Devio.Server.Interaction
Imports Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory

Namespace Server.Services

    ''' <summary>
    ''' Class deriving from DevioServiceBase, but without providing a proxy service. Instead,
    ''' it just passes a disk image file name for direct mounting internally in Arsenal Image Mounter
    ''' SCSI Adapter.
    ''' </summary>
    Public Class DevioNoneService
        Inherits DevioServiceBase

        ''' <summary>
        ''' Name and path of image file mounted by Arsenal Image Mounter.
        ''' </summary>
        Public ReadOnly Imagefile As String

        ''' <summary>
        ''' FileAccess flags specifying whether to mount read-only or read-write.
        ''' </summary>
        Public ReadOnly DiskAccess As FileAccess

        ''' <summary>
        ''' Creates a DevioServiceBase compatible object, but without providing a proxy service.
        ''' Instead, it just passes a disk image file name for direct mounting internally in
        ''' SCSI Adapter.
        ''' </summary>
        ''' <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
        Public Sub New(Imagefile As String, DiskAccess As FileAccess)
            MyBase.New(New DevioProviderFromStream(New FileStream(Imagefile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete), ownsStream:=True), OwnsProvider:=True)

            Offset = API.GetOffsetByFileExt(Imagefile)
            SectorSize = API.GetSectorSizeFromFileName(Imagefile)
            Me.DiskAccess = DiskAccess
            Me.Imagefile = Imagefile

        End Sub

        ''' <summary>
        ''' Creates a DevioServiceBase compatible object, but without providing a proxy service.
        ''' Instead, it just passes a disk image file name for direct mounting internally in
        ''' SCSI Adapter.
        ''' </summary>
        ''' <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
        Public Sub New(Imagefile As String, DiskAccess As VirtualDiskAccess)
            Me.New(Imagefile, DevioServiceFactory.GetDirectFileAccessFlags(DiskAccess))

        End Sub

        Protected Overrides ReadOnly Property ProxyObjectName As String
            Get
                Return Imagefile
            End Get
        End Property

        Protected Overrides ReadOnly Property ProxyModeFlags As DeviceFlags
            Get
                If (DiskAccess And FileAccess.Write) = 0 Then
                    Return DeviceFlags.TypeFile Or DeviceFlags.ReadOnly
                Else
                    Return DeviceFlags.TypeFile
                End If
            End Get
        End Property

        ''' <summary>
        ''' Dummy implementation that always returns True.
        ''' </summary>
        ''' <returns>Fixed value of True.</returns>
        Public Overrides Function StartServiceThread() As Boolean
            Return True
        End Function

        ''' <summary>
        ''' Dummy implementation that just raises ServiceReady event.
        ''' </summary>
        Public Overrides Sub RunService()
            OnServiceReady()
        End Sub

        Protected Overrides Sub EmergencyStopServiceThread()

        End Sub

    End Class

End Namespace
