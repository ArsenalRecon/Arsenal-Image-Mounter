
''''' DevioProviderFromStream.vb
''''' Proxy provider that implements devio proxy service with a .NET Stream derived
''''' object as storage backend.
''''' 
''''' Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Namespace Server.GenericProviders

    ''' <summary>
    ''' Class that implements <see>IDevioProvider</see> interface with a System.IO.Stream
    ''' object as storage backend.
    ''' </summary>
    Public Class DevioProviderFromStream
        Inherits DevioProviderManagedBase

        Private _BaseStream As Stream

        ''' <summary>
        ''' Stream object used by this instance.
        ''' </summary>
        Public ReadOnly Property BaseStream As Stream
            Get
                Return _BaseStream
            End Get
        End Property

        ''' <summary>
        ''' Indicates whether base stream will be automacially closed when this
        ''' instance is disposed.
        ''' </summary>
        Public ReadOnly OwnsBaseStream As Boolean

        ''' <summary>
        ''' Creates an object implementing IDevioProvider interface with I/O redirected
        ''' to an object of a class derived from System.IO.Stream.
        ''' </summary>
        ''' <param name="Stream">Object of a class derived from System.IO.Stream.</param>
        ''' <param name="ownsStream">Indicates whether Stream object will be automacially closed when this
        ''' instance is disposed.</param>
        Public Sub New(Stream As Stream, ownsStream As Boolean)
            _BaseStream = Stream
            OwnsBaseStream = ownsStream
        End Sub

        ''' <summary>
        ''' Returns value of BaseStream.CanWrite.
        ''' </summary>
        ''' <value>Value of BaseStream.CanWrite.</value>
        ''' <returns>Value of BaseStream.CanWrite.</returns>
        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return BaseStream.CanWrite
            End Get
        End Property

        ''' <summary>
        ''' Returns value of BaseStream.Length.
        ''' </summary>
        ''' <value>Value of BaseStream.Length.</value>
        ''' <returns>Value of BaseStream.Length.</returns>
        Public Overrides ReadOnly Property Length As Long
            Get
                Return BaseStream.Length
            End Get
        End Property

        ''' <summary>
        ''' Returns a fixed value of 512.
        ''' </summary>
        ''' <value>512</value>
        ''' <returns>512</returns>
        Public Overrides ReadOnly Property SectorSize As UInteger
            Get
                Return 512
            End Get
        End Property

        Public Overloads Overrides Function Read(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            BaseStream.Position = fileoffset
            Return BaseStream.Read(buffer, bufferoffset, count)

        End Function

        Public Overloads Overrides Function Write(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            BaseStream.Position = fileoffset
            BaseStream.Write(buffer, bufferoffset, count)
            Return count

        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If _BaseStream IsNot Nothing Then
                If OwnsBaseStream Then
                    _BaseStream.Dispose()
                End If
                _BaseStream = Nothing
            End If

            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
