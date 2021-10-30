''''' DevioServiceBase.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.ComponentModel
Imports System.IO
Imports System.Threading
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO

Namespace Server.Services

    ''' <summary>
    ''' Base class for classes that implement functionality for acting as server end of
    ''' Devio communication. Derived classes implement communication mechanisms and
    ''' use an object implementing <see>IDevioProvider</see> interface as storage backend
    ''' for I/O requests received from client.
    ''' </summary>
    Public MustInherit Class DevioServiceBase
        Implements IVirtualDiskService

        Public Property Exception As Exception

        Protected ReadOnly Property ServiceThread As Thread

        ''' <summary>
        ''' IDevioProvider object used by this instance.
        ''' </summary>
        Public ReadOnly Property DevioProvider As IDevioProvider

        ''' <summary>
        ''' ScsiAdapter object used when StartServiceThreadAndMount was called. This object
        ''' is used to remove the device when DismountAndStopServiceThread is called.
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ScsiAdapter As ScsiAdapter

        ''' <summary>
        ''' Indicates whether DevioProvider will be automatically closed when this instance
        ''' is disposed.
        ''' </summary>
        Public ReadOnly Property OwnsProvider As Boolean

        ''' <summary>
        ''' Size of virtual disk device.
        ''' </summary>
        ''' <value>Size of virtual disk device.</value>
        ''' <returns>Size of virtual disk device.</returns>
        Public Overridable Property DiskSize As Long Implements IVirtualDiskService.DiskSize

        ''' <summary>
        ''' Offset in disk image where this virtual disk device begins.
        ''' </summary>
        ''' <value>Offset in disk image where this virtual disk device begins.</value>
        ''' <returns>Offset in disk image where this virtual disk device begins.</returns>
        Public Overridable Property Offset As Long

        ''' <summary>
        ''' Sector size of virtual disk device.
        ''' </summary>
        ''' <value>Sector size of virtual disk device.</value>
        ''' <returns>Sector size of virtual disk device.</returns>
        Public Overridable Property SectorSize As UInteger Implements IVirtualDiskService.SectorSize

        ''' <summary>
        ''' Description of service.
        ''' </summary>
        ''' <value>Description of service.</value>
        ''' <returns>Description of service.</returns>
        Public Overridable Property Description As String Implements IVirtualDiskService.Description

        ''' <summary>
        ''' Event raised when service thread is ready to start accepting connection from a client.
        ''' </summary>
        Public Event ServiceReady As EventHandler
        Protected Overridable Sub OnServiceReady(e As EventArgs)
            RaiseEvent ServiceReady(Me, e)
        End Sub

        ''' <summary>
        ''' Event raised when service initialization fails.
        ''' </summary>
        Public Event ServiceInitFailed As EventHandler
        Protected Overridable Sub OnServiceInitFailed(e As EventArgs)
            RaiseEvent ServiceInitFailed(Me, e)
        End Sub

        ''' <summary>
        ''' Event raised when an Arsenal Image Mounter Disk Device is created by with this instance.
        ''' </summary>
        Public Event DiskDeviceCreated As EventHandler
        Protected Overridable Sub OnDiskDeviceCreated(e As EventArgs)
            RaiseEvent DiskDeviceCreated(Me, e)
        End Sub

        ''' <summary>
        ''' Event raised when any of the DismountAndStopServiceThread methods are called, before
        ''' disk device object is removed. Note that this event is not raised if device is directly
        ''' removed by some other method.
        ''' </summary>
        Public Event ServiceStopping As EventHandler Implements IVirtualDiskService.ServiceStopping
        Protected Overridable Sub OnServiceStopping(e As EventArgs)
            RaiseEvent ServiceStopping(Me, e)
        End Sub

        ''' <summary>
        ''' Event raised when service thread exits.
        ''' </summary>
        Public Event ServiceShutdown As EventHandler Implements IVirtualDiskService.ServiceShutdown
        Protected Overridable Sub OnServiceShutdown(e As EventArgs)
            RaiseEvent ServiceShutdown(Me, e)
        End Sub

        ''' <summary>
        ''' Event raised when an unhandled exception occurs in service thread and thread is about to terminate,
        ''' but before associated virtual disk device is forcefully removed, as specified by ForceRemoveDiskDeviceOnCrash
        ''' property.
        ''' </summary>
        Public Event ServiceUnhandledException As ThreadExceptionEventHandler Implements IVirtualDiskService.ServiceUnhandledException
        Protected Overridable Sub OnServiceUnhandledException(e As ThreadExceptionEventArgs)
            RaiseEvent ServiceUnhandledException(Me, e)
            If HasDiskDevice AndAlso _ForceRemoveDiskDeviceOnCrash AndAlso _ScsiAdapter IsNot Nothing Then
                _ScsiAdapter.RemoveDevice(_DiskDeviceNumber)
            End If
        End Sub

        ''' <summary>
        ''' Event raised to stop service thread. Service thread handle this event by preparing communication for
        ''' disconnection.
        ''' </summary>
        Protected Event StopServiceThread As EventHandler
        Protected Overridable Sub OnStopServiceThread(e As EventArgs)
            RaiseEvent StopServiceThread(Me, e)
        End Sub

        ''' <summary>
        ''' Creates a new service instance with enough data to later run a service that acts as server end in Devio
        ''' communication.
        ''' </summary>
        ''' <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        ''' <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        ''' instance is disposed.</param>
        Protected Sub New(DevioProvider As IDevioProvider, OwnsProvider As Boolean)

            Me.OwnsProvider = OwnsProvider

            _DevioProvider = DevioProvider.NullCheck(NameOf(DevioProvider))

            DiskSize = DevioProvider.Length

            SectorSize = DevioProvider.SectorSize

        End Sub

        ''' <summary>
        ''' When overridden in a derived class, runs service that acts as server end in Devio communication. It will
        ''' first wait for a client to connect, then serve client I/O requests and when client finally requests service to
        ''' terminate, this method returns to caller. To run service in a worker thread that automatically disposes this
        ''' object after client disconnection, call StartServiceThread() instead.
        ''' </summary>
        Public MustOverride Sub RunService()

        ''' <summary>
        ''' When overridden in a derived class, immediately stop service thread. This method will be called internally when
        ''' service base class methods for example detect that the device object no longer exists in the driver, or similar
        ''' scenarios where the driver cannot be requested to request service thread to shut down.
        ''' </summary>
        Protected MustOverride Sub EmergencyStopServiceThread()

        ''' <summary>
        ''' Creates a worker thread where RunService() method is called. After that method exits, this instance is automatically
        ''' disposed.
        ''' </summary>
        Public Overridable Function StartServiceThread() As Boolean

            Using _
                ServiceReadyEvent As New ManualResetEvent(initialState:=False),
                ServiceInitFailedEvent As New ManualResetEvent(initialState:=False)

                Dim ServiceReadyHandler As New EventHandler(Sub() ServiceReadyEvent.Set())
                AddHandler ServiceReady, ServiceReadyHandler
                Dim ServiceInitFailedHandler As New EventHandler(Sub() ServiceInitFailedEvent.Set())
                AddHandler ServiceInitFailed, ServiceInitFailedHandler

                _ServiceThread = New Thread(AddressOf ServiceThreadProcedure)
                _ServiceThread.Start()
                WaitHandle.WaitAny({ServiceReadyEvent, ServiceInitFailedEvent})

                RemoveHandler ServiceReady, ServiceReadyHandler
                RemoveHandler ServiceInitFailed, ServiceInitFailedHandler

                If ServiceReadyEvent.WaitOne(0) Then
                    Return True
                Else
                    Return False
                End If

            End Using

        End Function

        Private Sub ServiceThreadProcedure()

            Try
                RunService()

            Finally
                Dispose()

            End Try

        End Sub

        ''' <summary>
        ''' Waits for service thread created by StartServiceThread() to exit. If no service thread
        ''' has been created or if it has already exit, this method returns immediately with a
        ''' value of True.
        ''' </summary>
        ''' <param name="timeout">Timeout value, or Timeout.Infinite to wait infinitely.</param>
        ''' <returns>Returns True if service thread has exit or no service thread has been
        ''' created, or False if timeout occurred.</returns>
        Public Overridable Function WaitForServiceThreadExit(timeout As TimeSpan) As Boolean

            If _ServiceThread IsNot Nothing AndAlso
                _ServiceThread.ManagedThreadId <> Thread.CurrentThread.ManagedThreadId AndAlso
                _ServiceThread.IsAlive Then

                Trace.WriteLine($"Waiting for service thread to terminate.")

                Return _ServiceThread.Join(timeout)

            Else

                Return True

            End If

        End Function

        ''' <summary>
        ''' Waits for service thread created by StartServiceThread() to exit. If no service thread
        ''' has been created or if it has already exit, this method returns immediately.
        ''' </summary>
        Public Overridable Sub WaitForServiceThreadExit() Implements IVirtualDiskService.WaitForServiceThreadExit

            If _ServiceThread IsNot Nothing AndAlso
                _ServiceThread.ManagedThreadId <> Thread.CurrentThread.ManagedThreadId AndAlso
                _ServiceThread.IsAlive Then

                Trace.WriteLine($"Waiting for service thread to terminate.")

                _ServiceThread.Join()

            End If

        End Sub

        ''' <summary>
        ''' Combines a call to StartServiceThread() with a call to API to create a proxy type
        ''' Arsenal Image Mounter Disk Device that uses the started service as storage backend.
        ''' </summary>
        ''' <param name="Flags">Flags to pass to API.CreateDevice() combined with fixed flag
        ''' values specific to this instance. Example of such fixed flag values are flags specifying
        ''' proxy operation and which proxy communication protocol to use, which therefore do not
        ''' need to be specified in this parameter. A common value to pass however, is DeviceFlags.ReadOnly
        ''' to create a read-only virtual disk device.</param>
        Public Overridable Sub StartServiceThreadAndMount(ScsiAdapter As ScsiAdapter,
                                                          Flags As DeviceFlags)

            _ScsiAdapter = ScsiAdapter.NullCheck(NameOf(ScsiAdapter))

            If Not StartServiceThread() Then
                If _Exception Is Nothing Then
                    Throw New Exception("Service initialization failed")
                Else
                    Throw New Exception("Service initialization failed", _Exception)
                End If
            End If

            Try
                ScsiAdapter.CreateDevice(DiskSize,
                                         SectorSize,
                                         Offset,
                                         Flags Or AdditionalFlags Or ProxyModeFlags,
                                         ProxyObjectName,
                                         False,
                                         _WriteOverlayImageName,
                                         False,
                                         _DiskDeviceNumber)

                OnDiskDeviceCreated(EventArgs.Empty)

            Catch ex As Exception

                OnStopServiceThread(EventArgs.Empty)

                Throw New Exception($"Error when starting service thread or mounting {ProxyObjectName}", ex)

            End Try

        End Sub

        ''' <summary>
        ''' Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount() and waits
        ''' for service thread of this instance to exit.
        ''' </summary>
        Public Overridable Sub DismountAndStopServiceThread()

            RemoveDeviceAndStopServiceThread()

            WaitForServiceThreadExit()

        End Sub

        ''' <summary>
        ''' Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount() and waits
        ''' for service thread of this instance to exit.
        ''' </summary>
        ''' <param name="timeout">Timeout value to wait for service thread exit, or Timeout.Infinite to wait infinitely.</param>
        Public Overridable Function DismountAndStopServiceThread(timeout As TimeSpan) As Boolean

            RemoveDeviceAndStopServiceThread()

            Dim rc = WaitForServiceThreadExit(timeout)

            If rc Then
                Trace.WriteLine($"Service for device {_DiskDeviceNumber:X6} shut down successfully.")
            Else
                Trace.WriteLine($"Service for device {_DiskDeviceNumber:X6} shut down timed out.")
            End If

            Return rc

        End Function

        ''' <summary>
        ''' Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount(). If device
        ''' was already removed, it calls EmergencyStopServiceThread() to notify service thread.
        ''' </summary>
        Protected Sub RemoveDeviceAndStopServiceThread()

            Trace.WriteLine($"Notifying service stopping for device {_DiskDeviceNumber:X6}...")

            OnServiceStopping(EventArgs.Empty)

            Trace.WriteLine($"Removing device {_DiskDeviceNumber:X6}...")

            Dim i = 1
            Do
                Try
                    _ScsiAdapter.RemoveDevice(_DiskDeviceNumber)

                    Trace.WriteLine($"Device {_DiskDeviceNumber:X6} removed.")

                    Exit Do

                Catch ex As Win32Exception When (
                  i < 40 AndAlso
                  ex.NativeErrorCode = NativeFileIO.NativeConstants.ERROR_ACCESS_DENIED)

                    Trace.WriteLine($"Access denied attempting to remove device {_DiskDeviceNumber:X6}, retrying...")

                    i += 1
                    Thread.Sleep(100)
                    Continue Do

                Catch ex As Win32Exception When _
                    ex.NativeErrorCode = NativeFileIO.NativeConstants.ERROR_FILE_NOT_FOUND

                    Trace.WriteLine($"Attempt to remove non-existent device {_DiskDeviceNumber:X6}")

                    EmergencyStopServiceThread()

                    Exit Do

                End Try
            Loop

        End Sub

        ''' <summary>
        ''' Additional flags that will be passed to API.CreateDevice() in StartServiceThreadAndMount()
        ''' method. Default value of this property depends on derived class and which parameters are normally
        ''' needed for driver to start communication with this service.
        ''' </summary>
        ''' <value>Default value of this property depends on derived class and which parameters are normally
        ''' needed for driver to start communication with this service.</value>
        ''' <returns>Default value of this property depends on derived class and which parameters are normally
        ''' needed for driver to start communication with this service.</returns>
        Public Overridable Property AdditionalFlags As DeviceFlags

        ''' <summary>
        ''' When overridden in a derived class, indicates additional flags that will be passed to
        ''' API.CreateDevice() in StartServiceThreadAndMount() method. Value of this property depends
        ''' on derived class and which parameters are normally needed for driver to start communication with this
        ''' service.
        ''' </summary>
        ''' <value>Default value of this property depends on derived class and which parameters are normally
        ''' needed for driver to start communication with this service.</value>
        ''' <returns>Default value of this property depends on derived class and which parameters are normally
        ''' needed for driver to start communication with this service.</returns>
        Protected MustOverride ReadOnly Property ProxyModeFlags As DeviceFlags

        ''' <summary>
        ''' Object name that Arsenal Image Mounter can use to connect to this service.
        ''' </summary>
        ''' <value>Object name string.</value>
        ''' <returns>Object name that Arsenal Image Mounter can use to connect to this service.</returns>
        Protected MustOverride ReadOnly Property ProxyObjectName As String

        ''' <summary>
        ''' Path to write overlay image to pass to driver when a virtual disk is created for this service.
        ''' </summary>
        ''' <value>Path to write overlay image to pass to driver.</value>
        ''' <returns>Path to write overlay image to pass to driver.</returns>
        Public Property WriteOverlayImageName As String

        Private _DiskDeviceNumber As UInteger = UInteger.MaxValue

        ''' <summary>
        ''' After successful call to StartServiceThreadAndMount(), this property returns disk device
        ''' number for created Arsenal Image Mounter Disk Device. This number can be used when calling API
        ''' functions. If no Arsenal Image Mounter Disk Device has been created by this instance, an exception is
        ''' thrown. Use HasDiskDevice property to find out if a disk device has been created.
        ''' </summary>
        ''' <value>Disk device
        ''' number for created Arsenal Image Mounter Disk Device.</value>
        ''' <returns>Disk device
        ''' number for created Arsenal Image Mounter Disk Device.</returns>
        ''' <remarks></remarks>
        Public ReadOnly Property DiskDeviceNumber As UInteger
            Get
                If _DiskDeviceNumber = UInteger.MaxValue Then
                    Throw New InvalidOperationException("No Arsenal Image Mounter Disk Device currently associated with this instance.")
                End If
                Return _DiskDeviceNumber
            End Get
        End Property

        ''' <summary>
        ''' Use HasDiskDevice property to find out if a disk device has been created in a call to
        ''' StartServiceThreadAndMount() method. Use DiskDeviceNumber property to find out disk
        ''' device number for created device.
        ''' </summary>
        ''' <value>Returns True if an Arsenal Image Mounter Disk Device has been created, False otherwise.</value>
        ''' <returns>Returns True if an Arsenal Image Mounter Disk Device has been created, False otherwise.</returns>
        Public Overridable ReadOnly Property HasDiskDevice As Boolean Implements IVirtualDiskService.HasDiskDevice
            Get
                Return _DiskDeviceNumber <> UInteger.MaxValue
            End Get
        End Property

        ''' <summary>
        ''' Opens a DiskDevice object for direct access to a mounted device provided by
        ''' this service instance.
        ''' </summary>
        Public Overridable Function OpenDiskDevice(access As FileAccess) As DiskDevice
            Return _ScsiAdapter.OpenDevice(DiskDeviceNumber, access)
        End Function

        ''' <summary>
        ''' Returns a PhysicalDrive or CdRom device name for a mounted device provided by
        ''' this service instance.
        ''' </summary>
        Public Overridable Function GetDiskDeviceName() As String Implements IVirtualDiskService.GetDiskDeviceName
            Return _ScsiAdapter.GetDeviceName(DiskDeviceNumber)
        End Function

        ''' <summary>
        ''' Deletes the write overlay image file after use. Also sets this filter driver to
        ''' silently ignore flush requests to improve performance when integrity of the write
        ''' overlay image is not needed for future sessions.
        ''' </summary>
        ''' <returns>Returns 0 on success or Win32 error code on failure</returns>
        Public Function SetWriteOverlayDeleteOnClose() As Integer
            Using disk = OpenDiskDevice(FileAccess.ReadWrite)
                Return API.SetWriteOverlayDeleteOnClose(disk.SafeFileHandle)
            End Using
        End Function

        ''' <summary>
        ''' Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        ''' forcefully removed if a crash occurs in service thread of this instance. Default is True.
        ''' </summary>
        ''' <value>Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        ''' forcefully removed if a crash occurs in service thread of this instance. Default is True.</value>
        ''' <returns>Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        ''' forcefully removed if a crash occurs in service thread of this instance. Default is True.</returns>
        Public Overridable Property ForceRemoveDiskDeviceOnCrash As Boolean = True

        Public Overridable Sub RemoveDevice() Implements IVirtualDiskService.RemoveDevice

            _ScsiAdapter.RemoveDevice(DiskDeviceNumber)

        End Sub

        Public Overridable Sub RemoveDeviceSafe() Implements IVirtualDiskService.RemoveDeviceSafe

            _ScsiAdapter.RemoveDeviceSafe(DiskDeviceNumber)

        End Sub

#Region "IDisposable Support"
        Public ReadOnly Property IsDisposed As Boolean Implements IVirtualDiskService.IsDisposed ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me._IsDisposed Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    If HasDiskDevice Then
                        Try
                            DismountAndStopServiceThread()

                        Catch

                        End Try
                    Else
                        Try
                            OnStopServiceThread(EventArgs.Empty)

                        Catch

                        End Try
                    End If

                    If OwnsProvider Then
                        _DevioProvider?.Dispose()
                    End If
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                ' TODO: set large fields to null.
                _DevioProvider = Nothing
            End If
            Me._IsDisposed = True
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
#End Region

    End Class

End Namespace
