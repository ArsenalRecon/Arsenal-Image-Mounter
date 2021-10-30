
''''' DummyProvider.vb
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

    Public NotInheritable Class DummyProvider
        Implements IDevioProvider

        ''' <summary>
        ''' Event when object is about to be disposed
        ''' </summary>
        Public Event Disposing As EventHandler Implements IDevioProvider.Disposing

        ''' <summary>
        ''' Event when object has been disposed
        ''' </summary>
        Public Event Disposed As EventHandler Implements IDevioProvider.Disposed

        Public Sub New(Length As Long)

            _Length = Length

        End Sub

        Public ReadOnly Property Length As Long Implements IDevioProvider.Length

        Public ReadOnly Property SectorSize As UInteger Implements IDevioProvider.SectorSize
            Get
                Return 512
            End Get
        End Property

        Public ReadOnly Property CanWrite As Boolean Implements IDevioProvider.CanWrite
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property SupportsShared As Boolean Implements IDevioProvider.SupportsShared
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Public Sub SharedKeys(Request As IMDPROXY_SHARED_REQ, <Out> ByRef Response As IMDPROXY_SHARED_RESP, <Out> ByRef Keys() As ULong) Implements IDevioProvider.SharedKeys
            Throw New NotImplementedException()
        End Sub

        Public Function Read(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read
            Throw New NotImplementedException()
        End Function

        Public Function Read(buffer() As Byte, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read
            Throw New NotImplementedException()
        End Function

        Public Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write
            Throw New NotImplementedException()
        End Function

        Public Function Write(buffer() As Byte, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write
            Throw New NotImplementedException()
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
        End Sub

    End Class

End Namespace
