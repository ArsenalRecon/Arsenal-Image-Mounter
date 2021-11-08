''''' SystemNotificationEvent.vb
''''' Represents a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Security.AccessControl
Imports System.Threading

Namespace IO
    ''' <summary>
    ''' Represents a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
    ''' </summary>
    Public Class SystemNotificationEvent
        Inherits WaitHandle

        ''' <summary>
        ''' Opens a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
        ''' </summary>
        ''' <param name="EventName">NT name and path to event to open</param>
        Public Sub New(EventName As String)

            SafeWaitHandle = NativeFileIO.NtOpenEvent(EventName, 0, FileSystemRights.Synchronize Or NativeFileIO.NativeConstants.EVENT_QUERY_STATE, Nothing)

        End Sub

        Public Const PrefetchTracesReady As String = "\KernelObjects\PrefetchTracesReady"
        Public Const MemoryErrors As String = "\KernelObjects\MemoryErrors"
        Public Const LowNonPagedPoolCondition As String = "\KernelObjects\LowNonPagedPoolCondition"
        Public Const SuperfetchScenarioNotify As String = "\KernelObjects\SuperfetchScenarioNotify"
        Public Const SuperfetchParametersChanged As String = "\KernelObjects\SuperfetchParametersChanged"
        Public Const SuperfetchTracesReady As String = "\KernelObjects\SuperfetchTracesReady"
        Public Const PhysicalMemoryChange As String = "\KernelObjects\PhysicalMemoryChange"
        Public Const HighCommitCondition As String = "\KernelObjects\HighCommitCondition"
        Public Const HighMemoryCondition As String = "\KernelObjects\HighMemoryCondition"
        Public Const HighNonPagedPoolCondition As String = "\KernelObjects\HighNonPagedPoolCondition"
        Public Const SystemErrorPortReady As String = "\KernelObjects\SystemErrorPortReady"
        Public Const MaximumCommitCondition As String = "\KernelObjects\MaximumCommitCondition"
        Public Const LowCommitCondition As String = "\KernelObjects\LowCommitCondition"
        Public Const HighPagedPoolCondition As String = "\KernelObjects\HighPagedPoolCondition"
        Public Const LowMemoryCondition As String = "\KernelObjects\LowMemoryCondition"
        Public Const LowPagedPoolCondition As String = "\KernelObjects\LowPagedPoolCondition"

    End Class

    Public NotInheritable Class RegisteredEventHandler
        Implements IDisposable

        Private ReadOnly _registered_wait_handle As RegisteredWaitHandle

        Public ReadOnly Property WaitHandle As WaitHandle
        Public ReadOnly Property EventHandler As EventHandler

        Public Sub New(waitObject As WaitHandle, handler As EventHandler)

            _WaitHandle = waitObject

            _EventHandler = handler

            _registered_wait_handle = ThreadPool.RegisterWaitForSingleObject(
                    waitObject,
                    AddressOf Callback,
                    Me,
                    -1,
                    executeOnlyOnce:=True)

        End Sub

        Private Shared Sub Callback(state As Object, timedOut As Boolean)

            Dim obj = TryCast(state, RegisteredEventHandler)

            obj?._EventHandler?.Invoke(obj._WaitHandle, EventArgs.Empty)

        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose

            _registered_wait_handle?.Unregister(Nothing)

            GC.SuppressFinalize(Me)

        End Sub

    End Class

    Public Class WaitEventHandler

        Public ReadOnly Property WaitHandle As WaitHandle

        Private ReadOnly _event_handlers As New List(Of RegisteredEventHandler)

        Public Sub New(WaitHandle As WaitHandle)
            _WaitHandle = WaitHandle

        End Sub

        Public Custom Event Signalled As EventHandler
            AddHandler(value As EventHandler)
                _event_handlers.Add(New RegisteredEventHandler(_WaitHandle, value))
            End AddHandler
            RemoveHandler(value As EventHandler)
                _event_handlers.RemoveAll(
                    Function(handler)
                        If handler.EventHandler.Equals(value) Then
                            handler.Dispose()
                            Return True
                        Else
                            Return False
                        End If
                    End Function)
            End RemoveHandler
            RaiseEvent(sender As Object, e As EventArgs)
                _event_handlers.ForEach(Sub(handler) handler.EventHandler?.Invoke(sender, e))
            End RaiseEvent
        End Event

    End Class

End Namespace
