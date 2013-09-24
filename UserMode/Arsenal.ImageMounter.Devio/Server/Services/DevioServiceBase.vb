''''' DevioServiceBase.vb
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.Devio.Server.GenericProviders

Namespace Server.Services

    ''' <summary>
    ''' Base class for classes that implement functionality for acting as server end of
    ''' Devio communication. Derived classes implement communication mechanisms and
    ''' use an object implementing <see>IDevioProvider</see> interface as storage backend
    ''' for I/O requests received from client.
    ''' </summary>
    Public MustInherit Class DevioServiceBase
        Implements IDisposable

        Private _ScsiAdapter As ScsiAdapter

        Private _ServiceThread As Thread

        Protected ReadOnly Property ServiceThread As Thread
            Get
                Return _ServiceThread
            End Get
        End Property

        Private _DevioProvider As IDevioProvider

        ''' <summary>
        ''' IDevioProvider object used by this instance.
        ''' </summary>
        Public ReadOnly Property DevioProvider As IDevioProvider
            Get
                Return _DevioProvider
            End Get
        End Property

        ''' <summary>
        ''' Indicates whether DevioProvider will be automatically closed when this instance
        ''' is disposed.
        ''' </summary>
        Public ReadOnly OwnsProvider As Boolean

        ''' <summary>
        ''' Size of virtual disk device.
        ''' </summary>
        ''' <value>Size of virtual disk device.</value>
        ''' <returns>Size of virtual disk device.</returns>
        Public Overridable Property DiskSize As Long

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
        Public Overridable Property SectorSize As UInteger

        ''' <summary>
        ''' Description of service.
        ''' </summary>
        ''' <value>Description of service.</value>
        ''' <returns>Description of service.</returns>
        Public Overridable Property Description As String

        ''' <summary>
        ''' Event raised when service thread is ready to start accepting connection from a client.
        ''' </summary>
        Public Event ServiceReady As Action
        Protected Overridable Sub OnServiceReady()
            RaiseEvent ServiceReady()
        End Sub

        ''' <summary>
        ''' Event raised when service initialization fails.
        ''' </summary>
        Public Event ServiceInitFailed As Action
        Protected Overridable Sub OnServiceInitFailed()
            RaiseEvent ServiceInitFailed()
        End Sub

        ''' <summary>
        ''' Event raised when an Arsenal Image Mounter Disk Device is created by with this instance.
        ''' </summary>
        Public Event DiskDeviceCreated As Action
        Protected Overridable Sub OnDiskDeviceCreated()
            RaiseEvent DiskDeviceCreated()
        End Sub

        ''' <summary>
        ''' Event raised when service thread exits.
        ''' </summary>
        Public Event ServiceShutdown As Action
        Protected Overridable Sub OnServiceShutdown()
            RaiseEvent ServiceShutdown()
        End Sub

        ''' <summary>
        ''' Event raised when an unhandled exception occurs in service thread and thread is about to terminate,
        ''' but before associated virtual disk device is forcefully removed, as specified by ForceRemoveDiskDeviceOnCrash
        ''' property.
        ''' </summary>
        Public Event ServiceUnhandledException As UnhandledExceptionEventHandler
        Protected Overridable Sub OnServiceUnhandledException(e As UnhandledExceptionEventArgs)
            RaiseEvent ServiceUnhandledException(Me, e)
            If HasDiskDevice AndAlso ForceRemoveDiskDeviceOnCrash AndAlso _ScsiAdapter IsNot Nothing Then
                _ScsiAdapter.RemoveDevice(DiskDeviceNumber)
            End If
        End Sub

        ''' <summary>
        ''' Event raised to stop service thread. Service thread handle this event by preparing commnunication for
        ''' disconnection.
        ''' </summary>
        Protected Event StopServiceThread As Action
        Protected Overridable Sub OnStopServiceThread()
            RaiseEvent StopServiceThread()
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

            _DevioProvider = DevioProvider

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
        ''' service base class methods for eample detect that the device object no longer exists in the driver, or similar
        ''' scenarios where the driver cannot be requested to request service thread to shut down.
        ''' </summary>
        Protected MustOverride Sub EmergencyStopServiceThread()

        ''' <summary>
        ''' Creates a worker thread where RunService() method is called. After that method exits, this instance is automatically
        ''' disposed.
        ''' </summary>
        Public Overridable Function StartServiceThread() As Boolean

            Using _
              ServiceReadyEvent As New EventWaitHandle(initialState:=False, mode:=EventResetMode.ManualReset),
              ServiceInitFailedEvent As New EventWaitHandle(initialState:=False, mode:=EventResetMode.ManualReset)

                Dim ServiceReadyHandler As New Action(AddressOf ServiceReadyEvent.Set)
                AddHandler ServiceReady, ServiceReadyHandler
                Dim ServiceInitFailedHandler As New Action(AddressOf ServiceInitFailedEvent.Set)
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
        ''' created, or False if timeout occured.</returns>
        Public Overridable Function WaitForServiceThreadExit(timeout As TimeSpan) As Boolean

            If _ServiceThread IsNot Nothing AndAlso _ServiceThread.IsAlive Then
                Return _ServiceThread.Join(timeout)
            Else
                Return True
            End If

        End Function

        ''' <summary>
        ''' Waits for service thread created by StartServiceThread() to exit. If no service thread
        ''' has been created or if it has already exit, this method returns immediately.
        ''' </summary>
        Public Overridable Sub WaitForServiceThreadExit()

            If _ServiceThread IsNot Nothing AndAlso
                _ServiceThread.ManagedThreadId <> Thread.CurrentThread.ManagedThreadId AndAlso
                _ServiceThread.IsAlive Then

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

            _ScsiAdapter = ScsiAdapter

            If Not StartServiceThread() Then
                Throw New Exception("Service initialization failed.")
            End If

            Try
                ScsiAdapter.CreateDevice(DiskSize,
                                           SectorSize,
                                           Offset,
                                           Flags Or AdditionalFlags Or ProxyModeFlags,
                                           ProxyObjectName,
                                           False,
                                           _DiskDeviceNumber)

                OnDiskDeviceCreated()

            Catch
                OnStopServiceThread()
                Throw

            End Try

        End Sub

        ''' <summary>
        ''' Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount() and waits
        ''' for service thread of this instance to exit.
        ''' </summary>
        Public Overridable Sub DismountAndStopServiceThread()

            Dim i = 1
            Do
                Try
                    _ScsiAdapter.RemoveDevice(_DiskDeviceNumber)
                    Exit Do

                Catch ex As Win32Exception When (
                  i < 40 AndAlso
                  ex.NativeErrorCode = NativeFileIO.Win32API.ERROR_ACCESS_DENIED)

                    i += 1
                    Thread.Sleep(100)
                    Continue Do

                Catch ex As Win32Exception When _
                    ex.NativeErrorCode = NativeFileIO.Win32API.ERROR_FILE_NOT_FOUND

                    Trace.WriteLine("Attempt to remove device " & _DiskDeviceNumber.ToString("X6") & " which has already disappeared.")

                    If _ServiceThread IsNot Nothing AndAlso
                        _ServiceThread.ManagedThreadId <> Thread.CurrentThread.ManagedThreadId Then

                        EmergencyStopServiceThread()

                    End If

                    Exit Do

                End Try
            Loop

            WaitForServiceThreadExit()

        End Sub

        ''' <summary>
        ''' Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount() and waits
        ''' for service thread of this instance to exit.
        ''' </summary>
        ''' <param name="timeout">Timeout value to wait for service thread exit, or Timeout.Infinite to wait infinitely.</param>
        Public Overridable Function DismountAndStopServiceThread(timeout As TimeSpan) As Boolean

            Dim i = 1
            Do
                Try
                    _ScsiAdapter.RemoveDevice(_DiskDeviceNumber)
                    Exit Do

                Catch ex As Win32Exception When (
                  i < 40 AndAlso
                  ex.NativeErrorCode = NativeFileIO.Win32API.ERROR_ACCESS_DENIED)

                    i += 1
                    Thread.Sleep(100)
                    Continue Do

                Catch ex As Win32Exception When _
                    ex.NativeErrorCode = NativeFileIO.Win32API.ERROR_FILE_NOT_FOUND

                    Trace.WriteLine("Attempt to remove non-existent device " & _DiskDeviceNumber.ToString("X6"))

                    EmergencyStopServiceThread()

                    Exit Do

                End Try
            Loop

            Return WaitForServiceThreadExit(timeout)

        End Function

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
        Public Overridable ReadOnly Property DiskDeviceNumber As UInteger
            Get
                If _DiskDeviceNumber = UInteger.MaxValue Then
                    Throw New IOException("No Arsenal Image Mounter Disk Device currently associated with this instance.")
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
        Public Overridable ReadOnly Property HasDiskDevice As Boolean
            Get
                Return _DiskDeviceNumber <> UInteger.MaxValue
            End Get
        End Property

        ''' <summary>
        ''' Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        ''' forcefully removed if a crash occurs in service thread of this instance. Default is True.
        ''' </summary>
        ''' <value>Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        ''' forcefully removed if a crash occurs in service thread of this instance. Default is True.</value>
        ''' <returns>Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        ''' forcefully removed if a crash occurs in service thread of this instance. Default is True.</returns>
        Public Overridable Property ForceRemoveDiskDeviceOnCrash As Boolean = True

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    If HasDiskDevice Then
                        Try
                            DismountAndStopServiceThread()

                        Catch

                        End Try
                    End If

                    If _DevioProvider IsNot Nothing Then
                        If OwnsProvider Then
                            _DevioProvider.Dispose()
                        End If
                        _DevioProvider = Nothing
                    End If
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
#End Region

    End Class

End Namespace
