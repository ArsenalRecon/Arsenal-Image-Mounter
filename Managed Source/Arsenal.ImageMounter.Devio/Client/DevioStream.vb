
''''' DevioStream.vb
''''' Client side component for use with devio proxy services from other clients
''''' than actual Arsenal Image Mounter driver. This could be useful for example
''''' for directly examining virtual disk contents directly in an application,
''''' even if that disk contents is accessed through a proxy.
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

Namespace Client

    ''' <summary>
    ''' Base class for classes that implement Stream for client side of Devio protocol.
    ''' </summary>
    Public MustInherit Class DevioStream
        Inherits Stream

        Public Event Disposing As EventHandler

        Public Event Disposed As EventHandler

        ''' <summary>
        ''' Object name used by proxy implementation.
        ''' </summary>
        Public ReadOnly Property ObjectName As String

        ''' <summary>
        ''' Virtual disk size of server object.
        ''' </summary>
        Protected Property Size As Long

        ''' <summary>
        ''' Alignment requirement for I/O at server.
        ''' </summary>
        Protected Property Alignment As Long

        ''' <summary>
        ''' Proxy flags specified for proxy connection.
        ''' </summary>
        Protected Property Flags As IMDPROXY_FLAGS

        ''' <summary>
        ''' Initiates a new instance with supplied object name and read-only flag.
        ''' </summary>
        ''' <param name="name">Object name used by proxy implementation.</param>
        ''' <param name="read_only">Flag set to true to indicate read-only proxy
        ''' operation.</param>
        Protected Sub New(name As String, read_only As Boolean)
            ObjectName = name
            If read_only Then
                Flags = IMDPROXY_FLAGS.IMDPROXY_FLAG_RO
            End If
        End Sub

        ''' <summary>
        ''' Indicates whether Stream is readable. This implementation returns a
        ''' constant value of True, because Devio proxy implementations are
        ''' always readable.
        ''' </summary>
        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Indicates whether Stream is seekable. This implementation returns a
        ''' constant value of True.
        ''' </summary>
        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Indicates whether Stream is writable. This implementation returns True
        ''' unless ProxyFlags property contains IMDPROXY_FLAGS.IMDPROXY_FLAG_RO value.
        ''' </summary>
        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return Not Flags.HasFlag(IMDPROXY_FLAGS.IMDPROXY_FLAG_RO)
            End Get
        End Property

        ''' <summary>
        ''' This implementation does not do anything.
        ''' </summary>
        Public Overrides Sub Flush()

        End Sub

        ''' <summary>
        ''' When overridden in a derived class, closes communication and causes server side to exit.
        ''' </summary>
        Protected Overrides Sub Dispose(disposing As Boolean)

            RaiseEvent disposing(Me, EventArgs.Empty)

            MyBase.Dispose(disposing)

            RaiseEvent Disposed(Me, EventArgs.Empty)

        End Sub

        ''' <summary>
        ''' Returns current virtual disk size.
        ''' </summary>
        Public Overrides ReadOnly Property Length As Long
            Get
                Return Size
            End Get
        End Property

        ''' <summary>
        ''' Current byte position in Stream.
        ''' </summary>
        Public Overrides Property Position As Long

        ''' <summary>
        ''' Moves current position in Stream.
        ''' </summary>
        ''' <param name="offset">Byte offset to move. Can be negative to move backwards.</param>
        ''' <param name="origin">Origin from where number of bytes to move counts.</param>
        ''' <returns>Returns new absolute position in Stream.</returns>
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long

            Select Case origin

                Case SeekOrigin.Begin
                    Position = offset

                Case SeekOrigin.Current
                    Position += offset

                Case SeekOrigin.End
                    Position = Size + offset

                Case Else
                    Throw New ArgumentException("Invalid origin", NameOf(origin))

            End Select

            Return Position

        End Function

        ''' <summary>
        ''' This method is not supported in this implementation and throws a NotImplementedException.
        ''' A derived class can override this method to implement a resize feature.
        ''' </summary>
        ''' <param name="value">New total size of Stream</param>
        Public Overrides Sub SetLength(value As Long)
            Throw New NotImplementedException("SetLength() not implemented for DevioStream objects.")
        End Sub

        ''' <summary>
        ''' Alignment requirement for I/O at server.
        ''' </summary>
        Public ReadOnly Property RequiredAlignment As Long
            Get
                Return Alignment
            End Get
        End Property

        ''' <summary>
        ''' Proxy flags specified for proxy connection.
        ''' </summary>
        Public ReadOnly Property ProxyFlags As IMDPROXY_FLAGS
            Get
                Return Flags
            End Get
        End Property

    End Class

End Namespace
