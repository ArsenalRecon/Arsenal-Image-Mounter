
''''' DevioProviderUnmanagedBase.vb
''''' 
''''' Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Namespace Server.GenericProviders

  ''' <summary>
  ''' Base class for implementing <see>IDevioProvider</see> interface with a storage backend where
  ''' bytes to read from and write to device are provided in an unmanaged memory area.
  ''' </summary>
  Public MustInherit Class DevioProviderUnmanagedBase
    Implements IDevioProvider

    ''' <summary>
    ''' Determines whether virtual disk is writable or read-only.
    ''' </summary>
    ''' <value>True if virtual disk can be written to through this instance, or False
    ''' if it is opened for reading only.</value>
    ''' <returns>True if virtual disk can be written to through this instance, or False
    ''' if it is opened for reading only.</returns>
    Public MustOverride ReadOnly Property CanWrite As Boolean Implements IDevioProvider.CanWrite

    ''' <summary>
    ''' Size of virtual disk.
    ''' </summary>
    ''' <value>Size of virtual disk.</value>
    ''' <returns>Size of virtual disk.</returns>
    Public MustOverride ReadOnly Property Length As Long Implements IDevioProvider.Length

    ''' <summary>
    ''' Sector size of virtual disk.
    ''' </summary>
    ''' <value>Sector size of virtual disk.</value>
    ''' <returns>Sector size of virtual disk.</returns>
    Public MustOverride ReadOnly Property SectorSize As UInteger Implements IDevioProvider.SectorSize

    Private Function Read(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

      If buffer Is Nothing Then
        Throw New ArgumentNullException("buffer")
      ElseIf bufferoffset + count > buffer.Length Then
        Throw New ArgumentException("buffer too small")
      End If

      Dim pinptr = GCHandle.Alloc(buffer, GCHandleType.Pinned)
      Try
        Return Read(pinptr.AddrOfPinnedObject(), bufferoffset, count, fileoffset)

      Finally
        pinptr.Free()

      End Try

    End Function

    ''' <summary>
    ''' Reads bytes from virtual disk to a memory area specified by a pointer to unmanaged memory.
    ''' </summary>
    ''' <param name="buffer">Pointer to unmanaged memory where read bytes are stored.</param>
    ''' <param name="bufferoffset">Offset in unmanaged memory buffer where bytes are stored.</param>
    ''' <param name="count">Number of bytes to read from virtual disk device.</param>
    ''' <param name="fileoffset">Offset at virtual disk device where read starts.</param>
    ''' <returns>Returns number of bytes read from device that were stored at specified memory position.</returns>
    Public MustOverride Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

    Private Function Write(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

      If buffer Is Nothing Then
        Throw New ArgumentNullException("buffer")
      ElseIf bufferoffset + count > buffer.Length Then
        Throw New ArgumentException("buffer too small")
      End If

      Dim pinptr = GCHandle.Alloc(buffer, GCHandleType.Pinned)
      Try
        Return Write(pinptr.AddrOfPinnedObject(), bufferoffset, count, fileoffset)

      Finally
        pinptr.Free()

      End Try

    End Function

    ''' <summary>
    ''' Writes out bytes to virtual disk device from a memory area specified by a pointer to unmanaged memory.
    ''' </summary>
    ''' <param name="buffer">Pointer to unmanaged memory area containing bytes to write out to device.</param>
    ''' <param name="bufferoffset">Offset in unmanaged memory buffer where bytes to write are located.</param>
    ''' <param name="count">Number of bytes to write to virtual disk device.</param>
    ''' <param name="fileoffset">Offset at virtual disk device where write starts.</param>
    ''' <returns>Returns number of bytes written to device.</returns>
    Public MustOverride Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
      If Not Me.disposedValue Then
        If disposing Then
          ' TODO: dispose managed state (managed objects).
        End If

        ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
        ' TODO: set large fields to null.
      End If
      Me.disposedValue = True
    End Sub

    ' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
    Protected Overrides Sub Finalize()
      ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
      Dispose(False)
      MyBase.Finalize()
    End Sub

    ''' <summary>
    ''' Releases all resources used by this instance.
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
      ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
      Dispose(True)
      GC.SuppressFinalize(Me)
    End Sub

  End Class

End Namespace
