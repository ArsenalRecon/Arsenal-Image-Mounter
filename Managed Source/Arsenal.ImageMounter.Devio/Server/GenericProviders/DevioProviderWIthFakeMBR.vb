
''''' DevioProviderUnmanagedBase.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
'''''

Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports DiscUtils
Imports DiscUtils.Partitions
Imports Buffer = System.Buffer

Namespace Server.GenericProviders

    Public Class DevioProviderWithFakeMBR
        Implements IDevioProvider

        ''' <summary>
        ''' Event when object is about to be disposed
        ''' </summary>
        Public Event Disposing As EventHandler Implements IDevioProvider.Disposing

        ''' <summary>
        ''' Event when object has been disposed
        ''' </summary>
        Public Event Disposed As EventHandler Implements IDevioProvider.Disposed

        Public Const PrefixLength As Integer = 64 << 10

        Public ReadOnly Property BaseProvider As IDevioProvider

        Friend ReadOnly Property PrefixBuffer As Byte() = New Byte(0 To PrefixLength - 1) {}

        Friend ReadOnly Property SuffixBuffer As Byte()

        Private Shared ReadOnly _default_boot_code As Byte() = {&HF4, &HEB, &HFD}   ' HLT ; JMP -3

        Public Shared Function GetVBRPartitionLength(baseProvider As IDevioProvider) As Long

            Dim vbr(0 To CInt(baseProvider.NullCheck(NameOf(baseProvider)).SectorSize - 1UI)) As Byte

            If baseProvider.Read(vbr, 0, vbr.Length, 0) < vbr.Length Then
                Return 0
            End If

            Dim vbr_sector_size = BitConverter.ToInt16(vbr, &HB)

            If vbr_sector_size <= 0 Then
                Return 0
            End If

            Dim total_sectors As Long

            total_sectors = BitConverter.ToUInt16(vbr, &H13)

            If total_sectors = 0 Then

                total_sectors = BitConverter.ToUInt32(vbr, &H20)

            End If

            If total_sectors = 0 Then

                total_sectors = BitConverter.ToInt64(vbr, &H28)

            End If

            If total_sectors < 0 Then

                Return 0

            End If

            Return total_sectors * vbr_sector_size

        End Function

        Public Sub New(BaseProvider As IDevioProvider)
            Me.New(BaseProvider, GetVBRPartitionLength(BaseProvider))

        End Sub

        Public Sub New(BaseProvider As IDevioProvider, PartitionLength As Long)

            _BaseProvider = BaseProvider.NullCheck(NameOf(BaseProvider))

            PartitionLength = Math.Max(BaseProvider.Length, PartitionLength)

            _SuffixBuffer = New Byte(0 To CInt(PartitionLength - BaseProvider.Length - 1)) {}

            Dim virtual_length = PrefixLength + PartitionLength

            Dim sectorSize = BaseProvider.SectorSize

            Dim builder As New BiosPartitionedDiskBuilder(virtual_length, Geometry.FromCapacity(virtual_length, CInt(sectorSize)))

            Dim prefix_sector_length = PrefixLength \ sectorSize

            Dim partition_sector_length = PartitionLength \ sectorSize

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
                Return PrefixLength + _BaseProvider.Length + _SuffixBuffer.Length
            End Get
        End Property

        Public ReadOnly Property SectorSize As UInteger Implements IDevioProvider.SectorSize
            Get
                Return _BaseProvider.SectorSize
            End Get
        End Property

        Public ReadOnly Property CanWrite As Boolean Implements IDevioProvider.CanWrite
            Get
                Return _BaseProvider.CanWrite
            End Get
        End Property

        Public ReadOnly Property SupportsShared As Boolean = False Implements IDevioProvider.SupportsShared

        Public Sub SharedKeys(Request As IMDPROXY_SHARED_REQ, <Out> ByRef Response As IMDPROXY_SHARED_RESP, <Out> ByRef Keys() As ULong) Implements IDevioProvider.SharedKeys
            Throw New NotImplementedException()
        End Sub

        Public Function Read(data As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

            Dim prefix_count = 0

            If count > 0 AndAlso fileoffset < PrefixLength Then

                prefix_count = Math.Min(CInt(PrefixLength - fileoffset), count)

                Marshal.Copy(_PrefixBuffer, CInt(fileoffset), data + bufferoffset, prefix_count)

                fileoffset += prefix_count
                bufferoffset += prefix_count
                count -= prefix_count

            End If

            Dim base_count = 0

            If count > 0 AndAlso fileoffset < (PrefixLength + _BaseProvider.Length) Then

                base_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length - fileoffset, count))

                base_count = _BaseProvider.Read(data, bufferoffset, base_count, fileoffset - PrefixLength)

                If base_count < 0 Then

                    Return base_count

                End If

                fileoffset += base_count
                bufferoffset += base_count
                count -= base_count

            End If

            Dim suffix_count = 0

            If count > 0 AndAlso fileoffset < Length Then

                suffix_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length + _SuffixBuffer.Length - fileoffset, count))

                Marshal.Copy(_SuffixBuffer, CInt(fileoffset - _BaseProvider.Length - PrefixLength), data + bufferoffset, suffix_count)

            End If

            Return prefix_count + base_count + suffix_count

        End Function

        Public Function Read(data As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Read

            Dim prefix_count = 0

            If count > 0 AndAlso fileoffset < PrefixLength Then

                prefix_count = Math.Min(CInt(PrefixLength - fileoffset), count)

                Buffer.BlockCopy(_PrefixBuffer, CInt(fileoffset), data, bufferoffset, prefix_count)

                fileoffset += prefix_count
                bufferoffset += prefix_count
                count -= prefix_count

            End If

            Dim base_count = 0

            If count > 0 AndAlso fileoffset < (PrefixLength + _BaseProvider.Length) Then

                base_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length - fileoffset, count))

                base_count = _BaseProvider.Read(data, bufferoffset, base_count, fileoffset - PrefixLength)

                If base_count < 0 Then

                    Return base_count

                End If

                fileoffset += base_count
                bufferoffset += base_count
                count -= base_count

            End If

            Dim suffix_count = 0

            If count > 0 AndAlso fileoffset < Length Then

                suffix_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length + _SuffixBuffer.Length - fileoffset, count))

                Buffer.BlockCopy(_SuffixBuffer, CInt(fileoffset - _BaseProvider.Length - PrefixLength), data, bufferoffset, suffix_count)

            End If

            Return prefix_count + base_count + suffix_count

        End Function

        Public Function Write(data As IntPtr, bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

            Dim prefix_count = 0

            If count > 0 AndAlso fileoffset < PrefixLength Then

                prefix_count = Math.Min(CInt(PrefixLength - fileoffset), count)

                Marshal.Copy(data + bufferoffset, _PrefixBuffer, CInt(fileoffset), prefix_count)

                fileoffset += prefix_count
                bufferoffset += prefix_count
                count -= prefix_count

            End If

            Dim base_count = 0

            If count > 0 AndAlso fileoffset < (PrefixLength + _BaseProvider.Length) Then

                base_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length - fileoffset, count))

                base_count = _BaseProvider.Write(data, bufferoffset, base_count, fileoffset - PrefixLength)

                If base_count < 0 Then

                    Return base_count

                End If

                fileoffset += base_count
                bufferoffset += base_count
                count -= base_count

            End If

            Dim suffix_count = 0

            If count > 0 AndAlso fileoffset < Length Then

                suffix_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length + _SuffixBuffer.Length - fileoffset, count))

                Marshal.Copy(data + bufferoffset, _SuffixBuffer, CInt(fileoffset - _BaseProvider.Length - PrefixLength), suffix_count)

            End If

            Return prefix_count + base_count + suffix_count

        End Function

        Public Function Write(data As Byte(), bufferoffset As Integer, count As Integer, fileoffset As Long) As Integer Implements IDevioProvider.Write

            Dim prefix_count = 0

            If count > 0 AndAlso fileoffset < PrefixLength Then

                prefix_count = Math.Min(CInt(PrefixLength - fileoffset), count)

                Buffer.BlockCopy(data, bufferoffset, _PrefixBuffer, CInt(fileoffset), prefix_count)

                fileoffset += prefix_count
                bufferoffset += prefix_count
                count -= prefix_count

            End If

            Dim base_count = 0

            If count > 0 AndAlso fileoffset < (PrefixLength + _BaseProvider.Length) Then

                base_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length - fileoffset, count))

                base_count = _BaseProvider.Write(data, bufferoffset, base_count, fileoffset - PrefixLength)

                If base_count < 0 Then

                    Return base_count

                End If

                fileoffset += base_count
                bufferoffset += base_count
                count -= base_count

            End If

            Dim suffix_count = 0

            If count > 0 AndAlso fileoffset < Length Then

                suffix_count = CInt(Math.Min(PrefixLength + _BaseProvider.Length + _SuffixBuffer.Length - fileoffset, count))

                Buffer.BlockCopy(data, bufferoffset, _SuffixBuffer, CInt(fileoffset - _BaseProvider.Length - PrefixLength), suffix_count)

            End If

            Return prefix_count + base_count + suffix_count

        End Function

        Public ReadOnly Property IsDisposed As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            OnDisposing(EventArgs.Empty)

            If Not _IsDisposed Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TODO: set large fields to null.
            End If
            _IsDisposed = True

            OnDisposed(EventArgs.Empty)
        End Sub

        ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        Protected Overrides Sub Finalize()
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(False)
            MyBase.Finalize()
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            ' TODO: uncomment the following line if Finalize() is overridden above.
            GC.SuppressFinalize(Me)
        End Sub

        ''' <summary>
        ''' Raises Disposing event.
        ''' </summary>
        ''' <param name="e">Event arguments</param>
        Protected Overridable Sub OnDisposing(e As EventArgs)
            RaiseEvent Disposing(Me, e)
        End Sub

        ''' <summary>
        ''' Raises Disposed event.
        ''' </summary>
        ''' <param name="e">Event arguments</param>
        Protected Overridable Sub OnDisposed(e As EventArgs)
            RaiseEvent Disposed(Me, e)
        End Sub

        Private Const DiskSignatureOffset As Integer = &H1B8

        Private Const PartitionTableOffset As Integer = 512 - 2 - 4 * 16

        Private Shared Function GenerateDiskSignature() As Integer

            Dim value = NativeFileIO.GenRandomInt32()

            Return value Or &H80808081 And &HFEFEFEFF

        End Function

    End Class

End Namespace
