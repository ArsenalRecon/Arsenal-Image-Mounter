''''' DiskStream.vb
''''' Stream implementation for direct access to raw disk data.
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
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32.SafeHandles

''' <summary>
''' A FileStream derived class that represents disk devices by overriding properties and methods
''' where FileStream base implementation rely on file API not directly compatible with disk device
''' objects.
''' </summary>
Public Class DiskStream
    Inherits FileStream

    ''' <summary>
    ''' Initializes an DiskStream object for an open disk device.
    ''' </summary>
    ''' <param name="SafeFileHandle">Open file handle for disk device.</param>
    ''' <param name="AccessMode">Access to request for stream.</param>
    Protected Friend Sub New(SafeFileHandle As SafeFileHandle, AccessMode As FileAccess, bufferSize As Integer)
        MyBase.New(SafeFileHandle, AccessMode, bufferSize)
    End Sub

    Private _CachedLength As Long?

    ''' <summary>
    ''' Initializes an DiskStream object for an open disk device.
    ''' </summary>
    ''' <param name="SafeFileHandle">Open file handle for disk device.</param>
    ''' <param name="AccessMode">Access to request for stream.</param>
    ''' <param name="DiskSize">Size that should be returned by Length property</param>
    Protected Friend Sub New(SafeFileHandle As SafeFileHandle, AccessMode As FileAccess, bufferSize As Integer, DiskSize As Long)
        MyBase.New(SafeFileHandle, AccessMode, bufferSize)

        _CachedLength = DiskSize
    End Sub

    ''' <summary>
    ''' Retrieves raw disk size.
    ''' </summary>
    Public Overrides ReadOnly Property Length As Long
        Get
            _CachedLength = If(_CachedLength, NativeFileIO.GetDiskSize(SafeFileHandle))

            Return _CachedLength.Value
        End Get
    End Property

    Private _size_from_vbr As Boolean

    Public Property SizeFromVBR As Boolean
        Get
            Return _size_from_vbr
        End Get
        Set
            If Value Then
                _CachedLength = GetVBRPartitionLength()
                If Not _CachedLength.HasValue Then
                    Throw New NotSupportedException
                End If
            Else
                _CachedLength = NativeFileIO.GetDiskSize(SafeFileHandle)
                If Not _CachedLength.HasValue Then
                    Throw New NotSupportedException
                End If
            End If
            _size_from_vbr = Value
        End Set
    End Property

    ''' <summary>
    ''' Not implemented.
    ''' </summary>
    Public Overrides Sub SetLength(value As Long)
        Throw New NotImplementedException
    End Sub

    ''' <summary>
    ''' Get partition length as indicated by VBR. Valid for volumes with formatted file system.
    ''' </summary>
    Public Function GetVBRPartitionLength() As Long?

        Dim vbr(0 To CInt(NativeFileIO.GetDiskGeometry(SafeFileHandle).Value.BytesPerSector - 1UI)) As Byte

        Position = 0

        If Read(vbr, 0, vbr.Length) < vbr.Length Then
            Return Nothing
        End If

        Dim vbr_sector_size = BitConverter.ToInt16(vbr, &HB)

        If vbr_sector_size <= 0 Then
            Return Nothing
        End If

        Dim total_sectors As Long

        total_sectors = BitConverter.ToUInt16(vbr, &H13)

        If total_sectors = 0 Then

            total_sectors = BitConverter.ToUInt32(vbr, &H20)

        End If

        If total_sectors = 0 Then

            total_sectors = BitConverter.ToInt64(vbr, &H28)

        End If

        If total_sectors < 0 Then

            Return Nothing

        End If

        Return total_sectors * vbr_sector_size

    End Function

End Class



