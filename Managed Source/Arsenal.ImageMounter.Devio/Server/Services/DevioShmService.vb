
''''' DevioShmService.vb
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
Imports System.IO.MemoryMappedFiles
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.IO

Namespace Server.Services

    ''' <summary>
    ''' Class that implements server end of Devio shared memory based communication
    ''' protocol. It uses an object implementing <see>IDevioProvider</see> interface as
    ''' storage backend for I/O requests received from client.
    ''' </summary>
    Public Class DevioShmService
        Inherits DevioServiceBase

        ''' <summary>
        ''' Object name of shared memory file mapping object created by this instance.
        ''' </summary>
        Public ReadOnly Property ObjectName As String

        ''' <summary>
        ''' Size of the memory block that is shared between driver and this service.
        ''' </summary>
        Public ReadOnly Property BufferSize As Long

        ''' <summary>
        ''' Largest size of an I/O transfer between driver and this service. This
        ''' number depends on the size of the memory block that is shared between
        ''' driver and this service.
        ''' </summary>
        Public ReadOnly Property MaxTransferSize As Integer

        Private InternalShutdownRequestAction As Action

        ''' <summary>
        ''' Buffer size that will be automatically selected on this platform when
        ''' an instance is created by a constructor without a BufferSize argument.
        ''' 
        ''' Corresponds to MaximumTransferLength that driver reports to
        ''' storage port driver. This is the largest possible size of an
        ''' I/O request from the driver.
        ''' </summary>
        Public Const DefaultBufferSize As Long = (8 << 20) + IMDPROXY_HEADER_SIZE

        Private Shared Function GetNextRandomValue() As Guid
            Return NativeFileIO.GenRandomGuid()
        End Function

        ''' <summary>
        ''' Creates a new service instance with enough data to later run a service that acts as server end in Devio
        ''' shared memory based communication.
        ''' </summary>
        ''' <param name="ObjectName">Object name of shared memory file mapping object created by this instance.</param>
        ''' <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        ''' <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        ''' instance is disposed.</param>
        ''' <param name="BufferSize">Buffer size to use for shared memory I/O communication between driver and this service.</param>
        Public Sub New(ObjectName As String, DevioProvider As IDevioProvider, OwnsProvider As Boolean, BufferSize As Long)
            MyBase.New(DevioProvider, OwnsProvider)

            _ObjectName = ObjectName
            _BufferSize = BufferSize

        End Sub

        ''' <summary>
        ''' Creates a new service instance with enough data to later run a service that acts as server end in Devio
        ''' shared memory based communication. A default buffer size will be used.
        ''' </summary>
        ''' <param name="ObjectName">Object name of shared memory file mapping object created by this instance.</param>
        ''' <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        ''' <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        ''' instance is disposed.</param>
        Public Sub New(ObjectName As String, DevioProvider As IDevioProvider, OwnsProvider As Boolean)
            MyClass.New(ObjectName, DevioProvider, OwnsProvider, DefaultBufferSize)
        End Sub

        ''' <summary>
        ''' Creates a new service instance with enough data to later run a service that acts as server end in Devio
        ''' shared memory based communication. A default buffer size and a random object name will be used.
        ''' </summary>
        ''' <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        ''' <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        ''' instance is disposed.</param>
        Public Sub New(DevioProvider As IDevioProvider, OwnsProvider As Boolean)
            MyClass.New(DevioProvider, OwnsProvider, DefaultBufferSize)
        End Sub

        ''' <summary>
        ''' Creates a new service instance with enough data to later run a service that acts as server end in Devio
        ''' shared memory based communication. A random object name will be used.
        ''' </summary>
        ''' <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        ''' <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        ''' instance is disposed.</param>
        ''' <param name="BufferSize">Buffer size to use for shared memory I/O communication.</param>
        Public Sub New(DevioProvider As IDevioProvider, OwnsProvider As Boolean, BufferSize As Long)
            MyClass.New($"devio-{GetNextRandomValue()}", DevioProvider, OwnsProvider, BufferSize)
        End Sub

        ''' <summary>
        ''' Runs service that acts as server end in Devio shared memory based communication. It will first wait for
        ''' a client to connect, then serve client I/O requests and when client finally requests service to terminate, this
        ''' method returns to caller. To run service in a worker thread that automatically disposes this object after client
        ''' disconnection, call StartServiceThread() instead.
        ''' </summary>
        Public Overrides Sub RunService()

            Using DisposableObjects As New DisposableList

                Dim RequestEvent As EventWaitHandle

                Dim ResponseEvent As EventWaitHandle

                Dim Mapping As MemoryMappedFile

                Dim MapView As MemoryMappedViewAccessor

                Dim ServerMutex As Mutex

                Trace.WriteLine($"Creating objects for shared memory communication '{_ObjectName}'.")

                Try

                    RequestEvent = New EventWaitHandle(initialState:=False, mode:=EventResetMode.AutoReset, name:=$"Global\{_ObjectName}_Request")
                    DisposableObjects.Add(RequestEvent)
                    ResponseEvent = New EventWaitHandle(initialState:=False, mode:=EventResetMode.AutoReset, name:=$"Global\{_ObjectName}_Response")
                    DisposableObjects.Add(ResponseEvent)
                    ServerMutex = New Mutex(initiallyOwned:=False, name:=$"Global\{_ObjectName}_Server")
                    DisposableObjects.Add(ServerMutex)

                    If ServerMutex.WaitOne(0) = False Then
                        Dim message As String = $"Service name '{_ObjectName}' busy."
                        Trace.WriteLine(message)
                        Throw New Exception(message)
                    End If

                Catch ex As Exception
                    If TypeOf ex Is UnauthorizedAccessException Then
                        Exception = New Exception($"Service name '{_ObjectName}' already in use or not accessible.", ex)
                    Else
                        Exception = ex
                    End If
                    Dim message = $"Service thread initialization failed: {Exception}."
                    Trace.WriteLine(message)
                    OnServiceInitFailed(EventArgs.Empty)
                    Return

                End Try

                Try
#If NETFRAMEWORK AndAlso Not NET46_OR_GREATER Then
                    Mapping = MemoryMappedFile.CreateNew($"Global\{_ObjectName}",
                                                         _BufferSize,
                                                         MemoryMappedFileAccess.ReadWrite,
                                                         MemoryMappedFileOptions.None,
                                                         Nothing,
                                                         HandleInheritability.None)
#Else
                    Mapping = MemoryMappedFile.CreateNew($"Global\{_ObjectName}",
                                                         _BufferSize,
                                                         MemoryMappedFileAccess.ReadWrite,
                                                         MemoryMappedFileOptions.None,
                                                         HandleInheritability.None)
#End If

                    DisposableObjects.Add(Mapping)

                    MapView = Mapping.CreateViewAccessor()

                    DisposableObjects.Add(MapView)

                    _MaxTransferSize = CInt(MapView.Capacity - IMDPROXY_HEADER_SIZE)

                    Trace.WriteLine($"Created shared memory object, {_MaxTransferSize} bytes.")

                    Trace.WriteLine("Raising service ready event.")
                    OnServiceReady(EventArgs.Empty)

                Catch ex As Exception
                    If TypeOf ex Is UnauthorizedAccessException Then
                        Exception = New Exception($"This operation requires administrative privileges.", ex)
                    Else
                        Exception = ex
                    End If
                    Dim message = $"Service thread initialization failed: {Exception}."
                    Trace.WriteLine(message)
                    OnServiceInitFailed(EventArgs.Empty)
                    Return

                End Try

                Try
                    Trace.WriteLine("Waiting for client to connect.")

                    Using StopServiceThreadEvent As New ManualResetEvent(initialState:=False)
                        Dim StopServiceThreadHandler As New EventHandler(Sub() StopServiceThreadEvent.Set())
                        AddHandler StopServiceThread, StopServiceThreadHandler
                        Dim WaitEvents = {RequestEvent, StopServiceThreadEvent}
                        Dim EventIndex = WaitHandle.WaitAny(WaitEvents)
                        RemoveHandler StopServiceThread, StopServiceThreadHandler

                        Trace.WriteLine("Wait finished. Disposing file mapping object.")

                        Mapping.Dispose()
                        Mapping = Nothing

                        If WaitEvents(EventIndex) Is StopServiceThreadEvent Then
                            Trace.WriteLine("Service thread exit request.")
                            Return
                        End If
                    End Using

                    Trace.WriteLine("Client connected, waiting for request.")

                    Dim request_shutdown As Boolean

                    InternalShutdownRequestAction =
                        Sub()
                            Try
                                Trace.WriteLine("Emergency service thread shutdown requested, injecting close request...")
                                request_shutdown = True
                                RequestEvent.Set()

                            Catch

                            End Try
                        End Sub

                    Do
                        If request_shutdown Then
                            Trace.WriteLine("Emergency shutdown. Closing connection.")
                            Return
                        End If

                        Dim RequestCode = MapView.SafeMemoryMappedViewHandle.Read(Of IMDPROXY_REQ)(&H0)

                        'Trace.WriteLine("Got client request: " & RequestCode.ToString())

                        Select Case RequestCode

                            Case IMDPROXY_REQ.IMDPROXY_REQ_INFO
                                SendInfo(MapView.SafeMemoryMappedViewHandle)

                            Case IMDPROXY_REQ.IMDPROXY_REQ_READ
                                ReadData(MapView.SafeMemoryMappedViewHandle)

                            Case IMDPROXY_REQ.IMDPROXY_REQ_WRITE
                                WriteData(MapView.SafeMemoryMappedViewHandle)

                            Case IMDPROXY_REQ.IMDPROXY_REQ_CLOSE
                                Trace.WriteLine("Closing connection.")
                                Return

                            Case IMDPROXY_REQ.IMDPROXY_REQ_SHARED
                                SharedKeys(MapView.SafeMemoryMappedViewHandle)

                            Case Else
                                Trace.WriteLine($"Unsupported request code: {RequestCode}")
                                Return

                        End Select

                        'Trace.WriteLine("Sending response and waiting for next request.")

                        If WaitHandle.SignalAndWait(ResponseEvent, RequestEvent) = False Then
                            Trace.WriteLine("Synchronization failed.")
                        End If

                    Loop

                    Trace.WriteLine("Client disconnected.")

                Catch ex As Exception
                    Trace.WriteLine($"Unhandled exception in service thread: {ex}")
                    OnServiceUnhandledException(New ThreadExceptionEventArgs(ex))

                Finally
                    OnServiceShutdown(EventArgs.Empty)

                End Try

            End Using

        End Sub

        Private Sub SendInfo(MapView As SafeBuffer)

            Dim Info As New IMDPROXY_INFO_RESP With {
                .file_size = CULng(DevioProvider.Length),
                .req_alignment = CULng(REQUIRED_ALIGNMENT),
                .flags =
                If(DevioProvider.CanWrite, IMDPROXY_FLAGS.IMDPROXY_FLAG_NONE, IMDPROXY_FLAGS.IMDPROXY_FLAG_RO) Or
                If(DevioProvider.SupportsShared, IMDPROXY_FLAGS.IMDPROXY_FLAG_SUPPORTS_SHARED, IMDPROXY_FLAGS.IMDPROXY_FLAG_NONE)
            }

            MapView.Write(&H0, Info)

        End Sub

        Private Sub ReadData(MapView As SafeBuffer)

            Dim Request = MapView.Read(Of IMDPROXY_READ_REQ)(&H0)

            Dim Offset = CLng(Request.offset)
            Dim ReadLength = CInt(Request.length)

            Static largest_request As Integer
            If ReadLength > largest_request Then
                largest_request = ReadLength
                Trace.WriteLine($"Largest requested read size is now: {largest_request} bytes")
            End If

            Dim Response As IMDPROXY_READ_RESP

            Try
                If ReadLength > _MaxTransferSize Then
#If DEBUG Then
                    Trace.WriteLine($"Requested read length {ReadLength}, lowered to {_MaxTransferSize} bytes.")
#End If
                    ReadLength = _MaxTransferSize
                End If
                Response.length = CULng(DevioProvider.Read(MapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, ReadLength, Offset))
                Response.errorno = 0

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Trace.WriteLine($"Read request at 0x{Offset:X8} for {ReadLength} bytes.")
                Response.errorno = 1
                Response.length = 0

            End Try

            MapView.Write(&H0, Response)

        End Sub

        Private Sub WriteData(MapView As SafeBuffer)

            Dim Request = MapView.Read(Of IMDPROXY_WRITE_REQ)(&H0)

            Dim Offset = CLng(Request.offset)
            Dim WriteLength = CInt(Request.length)

            Static largest_request As Integer
            If WriteLength > largest_request Then
                largest_request = WriteLength
                Trace.WriteLine($"Largest requested write size is now: {largest_request} bytes")
            End If

            Dim Response As IMDPROXY_WRITE_RESP

            Try
                If WriteLength > _MaxTransferSize Then
                    Throw New Exception($"Requested write length {WriteLength}. Buffer size is {_MaxTransferSize} bytes.")
                End If
                Dim WrittenLength = DevioProvider.Write(MapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, WriteLength, Offset)
                If WrittenLength < 0 Then
                    Trace.WriteLine($"Write request at 0x{Offset:X8} for {WriteLength} bytes, returned {WrittenLength}.")
                    Response.errorno = 1
                    Response.length = 0
                    Exit Try
                End If
                Response.length = CULng(WrittenLength)
                Response.errorno = 0

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Trace.WriteLine($"Write request at 0x{Offset:X8} for {WriteLength} bytes.")
                Response.errorno = 1
                Response.length = 0

            End Try

            MapView.Write(&H0, Response)

        End Sub

        Private Shared ReadOnly SizeOfULong As Integer = PinnedBuffer(Of ULong).TypeSize

        Private Sub SharedKeys(MapView As SafeBuffer)

            Dim Request = MapView.Read(Of IMDPROXY_SHARED_REQ)(&H0)

            Dim Response As IMDPROXY_SHARED_RESP

            Try
                Dim Keys As ULong() = Nothing
                DevioProvider.SharedKeys(Request, Response, Keys)
                If Keys Is Nothing Then
                    Response.length = 0
                Else
                    Response.length = CULng(Keys.Length * SizeOfULong)
                    MapView.WriteArray(IMDPROXY_HEADER_SIZE, Keys, 0, Keys.Length)
                End If

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Response.errorno = IMDPROXY_SHARED_RESP_CODE.IOError
                Response.length = 0

            End Try

            MapView.Write(&H0, Response)

        End Sub

        Protected Overrides ReadOnly Property ProxyObjectName As String
            Get
                Return _ObjectName
            End Get
        End Property

        Protected Overrides ReadOnly Property ProxyModeFlags As DeviceFlags
            Get
                Return DeviceFlags.TypeProxy Or DeviceFlags.ProxyTypeSharedMemory
            End Get
        End Property

        Protected Overrides Sub EmergencyStopServiceThread()

            InternalShutdownRequestAction?.Invoke()

        End Sub

    End Class

End Namespace
