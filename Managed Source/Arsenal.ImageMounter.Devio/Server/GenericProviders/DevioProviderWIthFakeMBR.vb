
''''' DevioProviderUnmanagedBase.vb
''''' 
''''' Copyright (c) 2012-2019, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.IO
Imports DiscUtils.Partitions
Imports Buffer = System.Buffer

Namespace Server.GenericProviders

    Public Class DevioProviderWithFakeMBR
        Implements IDevioProvider

        Public Const PrefixLength As Integer = 64 << 10

        Public ReadOnly Property BaseProvider As IDevioProvider

        Public ReadOnly Property PrefixBuffer As Byte() = New Byte(0 To PrefixLength - 1) {}

        Private Shared ReadOnly _default_boot_code As Byte() = {&HF4, &HEB, &HFD}   ' HLT ; JMP -3

        Public Sub New(BaseProvider As IDevioProvider)

            _BaseProvider = BaseProvider

            Dim virtual_length = BaseProvider.Length + PrefixLength

            Dim sectorSize = BaseProvider.SectorSize

            Dim builder As New BiosPartitionedDiskBuilder(virtual_length, Geometry.FromCapacity(virtual_length, CInt(sectorSize)))

            Dim prefix_sector_length = PrefixLength \ sectorSize

            Dim partition_sector_length = BaseProvider.Length \ sectorSize

            builder.PartitionTable.CreatePrimaryBySector(prefix_sector_length, prefix_sector_length + partition_sector_length - 1, BiosPartitionTypes.Ntfs, markActive:=True)

            Dim stream = builder.Build()

            Buffer.BlockCopy(_default_boot_code, 0, _PrefixBuffer, 0, _default_boot_code.Length)

            Dim signature = BitConverter.GetBytes(GenerateDiskSignature())

            Buffer.BlockCopy(signature, 0, _PrefixBuffer, DiskSignatureOffset, signature.Length)

            stream.Position = PartitionTableOffset
            stream.Read(_PrefixBuffer, PartitionTableOffset, 16)

            _PrefixBuffer(510) = &H55

            _PrefixBuffer(511) = &HAA

        End Sub

        Public ReadOnly Property Length As Long Implements IDevioProvider.Length
            Get
                Return BaseProvider.Length + PrefixLength
            End Get
        End Property

        Public ReadOnly Property SectorSize As UInteger Implements IDevioProvider.SectorSize
            Get
                Return BaseProvider.SectorSize
            End Get
        End Property

        Public ReadOnly Property CanWrite As Boolean Implements IDevioProvider.CanWrite
            Get
                Return BaseProvider.CanWrite
            End Get
        End Property

        Public ReadOnly Property SupportsShared As Boolean = False Implements IDevioProvider.SupportsShared

        Public Sub SharedKeys(Request As IMDPROXY_SHARED_REQ, <Out> ByRef Response As IMDPROXY_SHARED_RESP, <Out> ByRef Keys() As ULong) Implements IDevioProvider.SharedKeys
            Throw New NotImplementedException()
        End Sub

        Public Function Read(data As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

            Dim prefix_count = 0

            If fileoffset < PrefixLength Then

                prefix_count = Math.Min(CInt(PrefixLength - fileoffset), count)

                Marshal.Copy(_PrefixBuffer, CInt(fileoffset), data + bufferoffset, prefix_count)

                fileoffset += prefix_count
                bufferoffset += prefix_count
                count -= prefix_count

            End If

            Dim rc As Integer

            If count > 0 Then

                rc = _BaseProvider.Read(data, bufferoffset, count, fileoffset - PrefixLength)

                If rc < 0 Then

                    Return rc

                End If

            End If

            Return rc + prefix_count

        End Function

        Public Function Read(data As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

            Dim prefix_count = 0

            If fileoffset < PrefixLength Then

                prefix_count = Math.Min(CInt(PrefixLength - fileoffset), count)

                Buffer.BlockCopy(_PrefixBuffer, CInt(fileoffset), data, bufferoffset, prefix_count)

                fileoffset += prefix_count
                bufferoffset += prefix_count
                count -= prefix_count

            End If

            Dim rc As Integer

            If count > 0 Then

                rc = _BaseProvider.Read(data, bufferoffset, count, fileoffset - PrefixLength)

                If rc < 0 Then

                    Return rc

                End If

            End If

            Return rc + prefix_count

        End Function

        Public Function Write(buffer As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

            If fileoffset < PrefixLength Then

                Return -1

            End If

            Return _BaseProvider.Write(buffer, bufferoffset, count, fileoffset - PrefixLength)

        End Function

        Public Function Write(buffer As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

            If fileoffset < PrefixLength Then

                Return -1

            End If

            Return _BaseProvider.Write(buffer, bufferoffset, count, fileoffset - PrefixLength)

        End Function

        Public ReadOnly Property IsDisposed As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)

            If Not _IsDisposed Then

                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    _BaseProvider?.Dispose()
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                ' TODO: set large fields to null.
                _BaseProvider = Nothing
            End If

            _IsDisposed = True

        End Sub

        ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        'Protected Overrides Sub Finalize()
        '    ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        '    Dispose(False)
        '    MyBase.Finalize()
        'End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            ' TODO: uncomment the following line if Finalize() is overridden above.
            ' GC.SuppressFinalize(Me)
        End Sub

        Private Const DiskSignatureOffset As Integer = &H1B8

        Private Const PartitionTableOffset As Integer = 512 - 2 - 4 * 16

        Private Shared Function GenerateDiskSignature() As Integer

            Dim value As Integer = 0

            NativeFileIO.Win32API.RtlGenRandom(value, 4)

            Return value Or &H80808081 And &HFEFEFEFF

        End Function

    End Class

End Namespace