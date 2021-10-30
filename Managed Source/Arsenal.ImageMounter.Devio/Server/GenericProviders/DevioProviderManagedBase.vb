
''''' DevioProviderManagedBase.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Runtime.InteropServices

Namespace Server.GenericProviders

    ''' <summary>
    ''' Base class for implementing <see>IDevioProvider</see> interface with a storage backend where
    ''' bytes to read from and write to device are provided in a managed byte array.
    ''' </summary>
    Public MustInherit Class DevioProviderManagedBase
        Implements IDevioProvider

        ''' <summary>
        ''' Event when object is about to be disposed
        ''' </summary>
        Public Event Disposing As EventHandler Implements IDevioProvider.Disposing

        ''' <summary>
        ''' Event when object has been disposed
        ''' </summary>
        Public Event Disposed As EventHandler Implements IDevioProvider.Disposed

        ''' <summary>
        ''' Determines whether virtual disk is writable or read-only.
        ''' </summary>
        ''' <value>True if virtual disk can be written to through this instance, or False
        ''' if it is opened for reading only.</value>
        ''' <returns>True if virtual disk can be written to through this instance, or False
        ''' if it is opened for reading only.</returns>
        Public MustOverride ReadOnly Property CanWrite As Boolean Implements IDevioProvider.CanWrite

        ''' <summary>
        ''' Indicates whether provider supports shared image operations with registrations
        ''' and reservations.
        ''' </summary>
        Public Overridable ReadOnly Property SupportsShared As Boolean Implements IDevioProvider.SupportsShared

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

        ''' <summary>
        ''' Reads bytes from virtual disk to a byte array.
        ''' </summary>
        ''' <param name="buffer">Byte array with enough size where read bytes are stored.</param>
        ''' <param name="bufferoffset">Offset in array where bytes are stored.</param>
        ''' <param name="count">Number of bytes to read from virtual disk device.</param>
        ''' <param name="fileoffset">Offset at virtual disk device where read starts.</param>
        ''' <returns>Returns number of bytes read from device that were stored in byte array.</returns>
        Public MustOverride Function Read(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

        Private ReadOnly _buffers As New List(Of WeakReference)

        Private Function GetByteBuffer(size As Integer) As Byte()

#If TRACE_PERFORMANCE Then
            Static tid As String = Thread.CurrentThread.ManagedThreadId.ToString()
            Static counter As Long
            Static alloc_counter As Long
            Static free_counter As Long

            counter += 1
#End If

            Dim buffer = _buffers.
                Select(Function(ref) TryCast(ref.Target, Byte())).
                FirstOrDefault(Function(buf) buf IsNot Nothing AndAlso buf.Length >= size)

            If buffer Is Nothing Then

#If TRACE_PERFORMANCE Then
                alloc_counter += 1
#End If

                buffer = New Byte(0 To size - 1) {}

                Dim wr = _buffers.FirstOrDefault(Function(ref) Not ref.IsAlive)

                If wr Is Nothing Then

                    _buffers.Add(New WeakReference(buffer))

                Else

                    wr.Target = buffer

#If TRACE_PERFORMANCE Then
                    free_counter += 1
                    Trace.WriteLine($"[{tid}] Reallocated freed buffer ({size} bytes) {alloc_counter}/{counter}. Freed {free_counter}")
#End If

                End If

            End If

            Return buffer

        End Function

        Private Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

            Dim _byte_buffer = GetByteBuffer(count)

            Dim readlen = Read(_byte_buffer, 0, count, fileoffset)
            Marshal.Copy(_byte_buffer, 0, buffer + bufferoffset, readlen)

            Return readlen

        End Function

        ''' <summary>
        ''' Writes out bytes from byte array to virtual disk device.
        ''' </summary>
        ''' <param name="buffer">Byte array containing bytes to write out to device.</param>
        ''' <param name="bufferoffset">Offset in array where bytes to write start.</param>
        ''' <param name="count">Number of bytes to write to virtual disk device.</param>
        ''' <param name="fileoffset">Offset at virtual disk device where write starts.</param>
        ''' <returns>Returns number of bytes written to device.</returns>
        Public MustOverride Function Write(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

        Private Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

            Dim _byte_buffer = GetByteBuffer(count)

            Marshal.Copy(buffer + bufferoffset, _byte_buffer, 0, count)

            Return Write(_byte_buffer, 0, count, fileoffset)

        End Function

        ''' <summary>
        ''' Manage registrations and reservation keys for shared images.
        ''' </summary>
        ''' <param name="Request">Request data</param>
        ''' <param name="Response">Response data</param>
        ''' <param name="Keys">List of currently registered keys</param>
        Public Overridable Sub SharedKeys(Request As IMDPROXY_SHARED_REQ, <Out> ByRef Response As IMDPROXY_SHARED_RESP, <Out> ByRef Keys() As ULong) Implements IDevioProvider.SharedKeys

            Throw New NotImplementedException()

        End Sub

        Public ReadOnly Property IsDisposed As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            OnDisposing(EventArgs.Empty)

            If Not _IsDisposed Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TODO: set large fields to null.
            End If
            _IsDisposed = True

            OnDisposed(EventArgs.Empty)
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

        ''' <summary>
        ''' Raises Disposing event.
        ''' </summary>
        ''' <param name="e">Event arguments</param>
        Protected Overridable Sub OnDisposing(e As EventArgs)
            RaiseEvent Disposing(Me, e)
        End Sub

        ''' <summary>
        ''' Raises Disposed event.
        ''' </summary>
        ''' <param name="e">Event arguments</param>
        Protected Overridable Sub OnDisposed(e As EventArgs)
            RaiseEvent Disposed(Me, e)
        End Sub

    End Class

End Namespace
