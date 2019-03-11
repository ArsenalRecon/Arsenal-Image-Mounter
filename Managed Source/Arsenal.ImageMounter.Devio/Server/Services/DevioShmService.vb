
''''' DevioShmService.vb
''''' 
''''' Copyright (c) 2012-2019, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders

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
        ''' Buffer size used by this instance.
        ''' </summary>
        Public ReadOnly Property BufferSize As Long

        Private InternalShutdownRequestAction As action

        ''' <summary>
        ''' Buffer size that will be automatically selected on this platform when
        ''' an instance is created by a constructor without a BufferSize argument.
        ''' </summary>
        Public Shared ReadOnly Property DefaultBufferSize As Long
            Get
                '' Corresponds to MaximumTransferLength that driver reports to
                '' storage port driver. This is the largest possible size of an
                '' I/O request from the driver.
                Return (8 << 20) + IMDPROXY_HEADER_SIZE
            End Get
        End Property

        Private Shared _random As New Random
        Private Shared Function GetNextRandomValue() As Integer
            SyncLock _random
                Return _random.Next()
            End SyncLock
        End Function

        ''' <summary>
        ''' Creates a new service instance with enough data to later run a service that acts as server end in Devio
        ''' shared memory based communication.
        ''' </summary>
        ''' <param name="ObjectName">Object name of shared memory file mapping object created by this instance.</param>
        ''' <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        ''' <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        ''' instance is disposed.</param>
        ''' <param name="BufferSize">Buffer size to use for shared memory I/O communication.</param>
        Public Sub New(ObjectName As String, DevioProvider As IDevioProvider, OwnsProvider As Boolean, BufferSize As Long)
            MyBase.New(DevioProvider, OwnsProvider)

            Me.ObjectName = ObjectName
            Me.BufferSize = BufferSize

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
            MyClass.New("devio-" & GetNextRandomValue(), DevioProvider, OwnsProvider, BufferSize)
        End Sub

        ''' <summary>
        ''' Runs service that acts as server end in Devio shared memory based communication. It will first wait for
        ''' a client to connect, then serve client I/O requests and when client finally requests service to terminate, this
        ''' method returns to caller. To run service in a worker thread that automatically disposes this object after client
        ''' disconnection, call StartServiceThread() instead.
        ''' </summary>
        Public Overrides Sub RunService()

            Using DisposableObjects As New DisposableList(Of IDisposable)

                Dim RequestEvent As WaitHandle

                Dim ResponseEvent As WaitHandle

                Dim Mapping As MemoryMappedFile

                Dim MapView As SafeMemoryMappedViewHandle

                Dim ServerMutex As Mutex

                Try
                    Trace.WriteLine("Creating objects for shared memory communication '" & ObjectName & "'.")

                    RequestEvent = New EventWaitHandle(initialState:=False, mode:=EventResetMode.AutoReset, name:="Global\" & ObjectName & "_Request")
                    ResponseEvent = New EventWaitHandle(initialState:=False, mode:=EventResetMode.AutoReset, name:="Global\" & ObjectName & "_Response")
                    ServerMutex = New Mutex(initiallyOwned:=False, name:="Global\" & ObjectName & "_Server")

                    If ServerMutex.WaitOne(0) = False Then
                        Trace.WriteLine("Service busy.")
                        Exception = New Exception("Service busy")
                        OnServiceInitFailed()
                        Return
                    End If

                    Mapping = MemoryMappedFile.CreateNew("Global\" & ObjectName,
                                                             BufferSize,
                                                             MemoryMappedFileAccess.ReadWrite,
                                                             MemoryMappedFileOptions.None,
                                                             Nothing,
                                                             HandleInheritability.None)

                    MapView = Mapping.CreateViewAccessor().SafeMemoryMappedViewHandle

                    Trace.WriteLine("Created shared memory object, " & MapView.ByteLength & " bytes.")

                    Trace.WriteLine("Raising service ready event.")
                    OnServiceReady()
                Catch ex As Exception
                    Trace.WriteLine("Service thread initialization exception: " & ex.ToString())
                    Exception = New Exception("Service thread initialization exception", ex)
                    OnServiceInitFailed()
                    Return

                End Try

                Try
                    Trace.WriteLine("Waiting for client to connect.")

                    Using StopServiceThreadEvent As New EventWaitHandle(initialState:=False, mode:=EventResetMode.ManualReset)
                        Dim StopServiceThreadHandler As New Action(AddressOf StopServiceThreadEvent.Set)
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

                    Using MapView

                        InternalShutdownRequestAction =
                            Sub()
                                Try
                                    Trace.WriteLine("Emergency service thread shutdown requested, injecting close request...")
                                    ServiceThread.Abort()

                                Catch

                                End Try
                            End Sub

                        Do
                            Dim RequestCode = MapView.Read(Of IMDPROXY_REQ)(&H0)

                            'Trace.WriteLine("Got client request: " & RequestCode.ToString())

                            Select Case RequestCode

                                Case IMDPROXY_REQ.IMDPROXY_REQ_INFO
                                    SendInfo(MapView)

                                Case IMDPROXY_REQ.IMDPROXY_REQ_READ
                                    ReadData(MapView)

                                Case IMDPROXY_REQ.IMDPROXY_REQ_WRITE
                                    WriteData(MapView)

                                Case IMDPROXY_REQ.IMDPROXY_REQ_CLOSE
                                    Trace.WriteLine("Closing connection.")
                                    Return

                                Case IMDPROXY_REQ.IMDPROXY_REQ_SHARED
                                    SharedKeys(MapView)

                                Case Else
                                    Trace.WriteLine("Unsupported request code: " & RequestCode.ToString())
                                    Return

                            End Select

                            'Trace.WriteLine("Sending response and waiting for next request.")

                            If WaitHandle.SignalAndWait(ResponseEvent, RequestEvent) = False Then
                                Trace.WriteLine("Synchronization failed.")
                            End If

                        Loop

                    End Using

                    Trace.WriteLine("Client disconnected.")

                Catch ex As Exception
                    Trace.WriteLine("Unhandled exception in service thread: " & ex.ToString())
                    OnServiceUnhandledException(New UnhandledExceptionEventArgs(ex, True))

                Finally
                    OnServiceShutdown()

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
                Trace.WriteLine("Largest requested read size is now: " & largest_request & " bytes")
            End If

            Dim Response As IMDPROXY_READ_RESP

            Try
                If ReadLength > MapView.ByteLength - IMDPROXY_HEADER_SIZE Then
                    Trace.WriteLine("Requested read length " & ReadLength & ", lowered to " & CInt(MapView.ByteLength - CInt(IMDPROXY_HEADER_SIZE)) & " bytes.")
                    ReadLength = CInt(MapView.ByteLength - CInt(IMDPROXY_HEADER_SIZE))
                End If
                Response.length = CULng(DevioProvider.Read(MapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, ReadLength, Offset))
                Response.errorno = 0

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Trace.WriteLine("Read request at " & Offset.ToString("X8") & " for " & ReadLength & " bytes.")
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
                Trace.WriteLine("Largest requested write size is now: " & largest_request & " bytes")
            End If

            Dim Response As IMDPROXY_WRITE_RESP

            Try
                If WriteLength > MapView.ByteLength - IMDPROXY_HEADER_SIZE Then
                    Throw New Exception("Requested write length " & WriteLength & ". Buffer size is " & CInt(MapView.ByteLength - CInt(IMDPROXY_HEADER_SIZE)) & " bytes.")
                End If
                Dim WrittenLength = DevioProvider.Write(MapView.DangerousGetHandle(), IMDPROXY_HEADER_SIZE, WriteLength, Offset)
                If WrittenLength < 0 Then
                    Trace.WriteLine("Write request at " & Offset.ToString("X8") & " for " & WriteLength & " bytes, returned " & WrittenLength & ".")
                    Response.errorno = 1
                    Response.length = 0
                    Exit Try
                End If
                Response.length = CULng(WrittenLength)
                Response.errorno = 0

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Trace.WriteLine("Write request at " & Offset.ToString("X8") & " for " & WriteLength & " bytes.")
                Response.errorno = 1
                Response.length = 0

            End Try

            MapView.Write(&H0, Response)

        End Sub

        Private Sub SharedKeys(MapView As SafeBuffer)

            Dim Request = MapView.Read(Of IMDPROXY_SHARED_REQ)(&H0)

            Dim Response As IMDPROXY_SHARED_RESP

            Try
                Dim Keys As ULong() = Nothing
                DevioProvider.SharedKeys(Request, Response, Keys)
                If Keys Is Nothing Then
                    Response.length = 0
                Else
                    Response.length = CULng(Keys.Length * Marshal.SizeOf(GetType(ULong)))
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
                Return ObjectName
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

        ''' <summary>
        ''' A System.Collections.Generic.List(Of T) extended with IDisposable implementation that disposes each
        ''' object in the list when the list is disposed.
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        <ComVisible(False)> _
        Private Class DisposableList(Of T As IDisposable)
            Inherits List(Of T)

            Implements IDisposable

            Private disposedValue As Boolean    ' To detect redundant calls

            ' IDisposable
            Protected Overridable Sub Dispose(disposing As Boolean)
                If Not Me.disposedValue Then
                    If disposing Then
                        ' TODO: free managed resources when explicitly called
                        For Each obj In Me
                            obj.Dispose()
                        Next
                    End If
                End If
                Me.disposedValue = True

                ' TODO: free shared unmanaged resources

                Clear()
            End Sub

            ' This code added by Visual Basic to correctly implement the disposable pattern.
            Public Sub Dispose() Implements IDisposable.Dispose
                ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
                Dispose(True)
                GC.SuppressFinalize(Me)
            End Sub

            Protected Overrides Sub Finalize()
                Dispose(False)
                MyBase.Finalize()
            End Sub
        End Class

    End Class

End Namespace
