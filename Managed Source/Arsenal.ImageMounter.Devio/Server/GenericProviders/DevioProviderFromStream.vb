
''''' DevioProviderFromStream.vb
''''' Proxy provider that implements devio proxy service with a .NET Stream derived
''''' object as storage backend.
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

Namespace Server.GenericProviders

    ''' <summary>
    ''' Class that implements <see>IDevioProvider</see> interface with a System.IO.Stream
    ''' object as storage backend.
    ''' </summary>
    Public Class DevioProviderFromStream
        Inherits DevioProviderManagedBase

        ''' <summary>
        ''' Stream object used by this instance.
        ''' </summary>
        Public ReadOnly Property BaseStream As Stream

        ''' <summary>
        ''' Indicates whether base stream will be automatically closed when this
        ''' instance is disposed.
        ''' </summary>
        Public ReadOnly Property OwnsBaseStream As Boolean

        ''' <summary>
        ''' Creates an object implementing IDevioProvider interface with I/O redirected
        ''' to an object of a class derived from System.IO.Stream.
        ''' </summary>
        ''' <param name="Stream">Object of a class derived from System.IO.Stream.</param>
        ''' <param name="ownsStream">Indicates whether Stream object will be automatically closed when this
        ''' instance is disposed.</param>
        Public Sub New(Stream As Stream, ownsStream As Boolean)
            _BaseStream = Stream
            _OwnsBaseStream = ownsStream
        End Sub

        ''' <summary>
        ''' Returns value of BaseStream.CanWrite.
        ''' </summary>
        ''' <value>Value of BaseStream.CanWrite.</value>
        ''' <returns>Value of BaseStream.CanWrite.</returns>
        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return _BaseStream.CanWrite
            End Get
        End Property

        ''' <summary>
        ''' Returns value of BaseStream.Length.
        ''' </summary>
        ''' <value>Value of BaseStream.Length.</value>
        ''' <returns>Value of BaseStream.Length.</returns>
        Public Overrides ReadOnly Property Length As Long
            Get
                Return _BaseStream.Length
            End Get
        End Property

        ''' <summary>
        ''' Returns a fixed value of 512.
        ''' </summary>
        ''' <value>512</value>
        ''' <returns>512</returns>
        Public Property CustomSectorSize As UInteger = 512

        ''' <summary>
        ''' Returns a fixed value of 512.
        ''' </summary>
        ''' <value>512</value>
        ''' <returns>512</returns>
        Public Overrides ReadOnly Property SectorSize As UInteger
            Get
                Return _CustomSectorSize
            End Get
        End Property

        Public Overloads Overrides Function Read(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            _BaseStream.Position = fileoffset

            If _BaseStream.Position <= _BaseStream.Length AndAlso
                count > _BaseStream.Length - _BaseStream.Position Then

                count = CInt(_BaseStream.Length - _BaseStream.Position)

            End If

            Return _BaseStream.Read(buffer, bufferoffset, count)

        End Function

        Public Overloads Overrides Function Write(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer

            _BaseStream.Position = fileoffset
            _BaseStream.Write(buffer, bufferoffset, count)
            Return count

        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)

            If disposing AndAlso _OwnsBaseStream AndAlso _BaseStream IsNot Nothing Then
                _BaseStream.Dispose()
            End If

            _BaseStream = Nothing

            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
