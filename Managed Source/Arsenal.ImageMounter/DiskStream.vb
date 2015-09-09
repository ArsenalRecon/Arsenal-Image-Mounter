''''' DiskStream.vb
''''' Stream implementation for direct access to raw disk data.
''''' 
''''' Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.IO


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
    Public Sub New(SafeFileHandle As SafeFileHandle, AccessMode As FileAccess)
        MyBase.New(SafeFileHandle, AccessMode)
    End Sub

    ''' <summary>
    ''' Retrieves raw disk size.
    ''' </summary>
    Public Overrides ReadOnly Property Length As Long
        Get
            Dim Size As Int64
            NativeFileIO.GetDiskSize(SafeFileHandle)
            Return Size
        End Get
    End Property

    ''' <summary>
    ''' Not implemented.
    ''' </summary>
    Public Overrides Sub SetLength(value As Long)
        Throw New NotImplementedException
    End Sub

End Class



