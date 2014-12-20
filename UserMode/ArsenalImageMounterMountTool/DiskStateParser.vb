Imports System.Threading.Tasks
Imports Arsenal.ImageMounter.ScsiAdapter
Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.PSDisk
Imports Arsenal.ImageMounter.IO

Public Class DiskStateParser
    Implements IDisposable

    Private PSPhysicalDiskParser As PSPhysicalDiskParser

    Private PSDiskParser As PSDiskParser

    Private Shared ReadOnly SizeOfScsiAddress As UInt32 = CUInt(Marshal.SizeOf(GetType(NativeFileIO.Win32API.SCSI_ADDRESS)))

    Private Shared Function GetDriveScsiIds(portnumber As Byte) As Dictionary(Of UInteger, UInteger)

        Dim GetScsiAddress =
            Function(drv As String) As NativeFileIO.Win32API.SCSI_ADDRESS?
                Try
                    Using disk As New DiskDevice(drv)
                        Dim ScsiAddress As NativeFileIO.Win32API.SCSI_ADDRESS
                        Dim rc = NativeFileIO.Win32API.
                            DeviceIoControl(disk.SafeFileHandle,
                                            NativeFileIO.Win32API.IOCTL_SCSI_GET_ADDRESS,
                                            IntPtr.Zero,
                                            0,
                                            ScsiAddress,
                                            SizeOfScsiAddress,
                                            Nothing,
                                            Nothing)

                        If rc Then
                            Return ScsiAddress
                        Else
                            Trace.WriteLine("IOCTL_SCSI_GET_ADDRESS failed for device " & drv & ": Error 0x" & Marshal.GetLastWin32Error().ToString("X"))
                            Return Nothing
                        End If
                    End Using

                Catch ex As Exception
                    Trace.WriteLine("Exception attempting to find SCSI address for device " & drv & ": " & ex.ToString())
                    Return Nothing

                End Try
            End Function

        Dim q =
            From drv In NativeFileIO.QueryDosDevice()
            Where drv.StartsWith("PhysicalDrive")
            Let address = GetScsiAddress("\\?\" & drv)
            Where address.HasValue
            Where address.Value.PortNumber = portnumber
            Let drivenumber = UInteger.Parse(drv.Substring("PhysicalDrive".Length))

        Return q.ToDictionary(Function(o) o.address.Value.DWordDeviceNumber,
                              Function(o) o.drivenumber)

    End Function

    Public Sub New()

        Try
            Me.PSPhysicalDiskParser = New PSPhysicalDiskParser("Arsenal ", "Virtual ")

        Catch ex As Exception
            Trace.WriteLine("Cannot use PowerShell Get-PhysicalDisk command: " & ex.ToString())

        End Try

        Try
            Me.PSDiskParser = New PSDiskParser("Arsenal Virtual *")

        Catch ex As Exception
            Trace.WriteLine("Cannot use PowerShell Get-Disk command: " & ex.ToString())

        End Try

    End Sub

    Public Function GetSimpleView(portnumber As Byte, deviceProperties As List(Of DeviceProperties)) As List(Of DiskStateView)

        Dim ids = GetDriveScsiIds(portnumber)

        Dim getid =
            Function(dev As DeviceProperties) As UInt32?
                Dim result As UInt32
                If ids.TryGetValue(dev.DeviceNumber, result) Then
                    Return result
                Else
                    Return Nothing
                End If
            End Function

        Dim getsig =
            Function(drv As UInt32?) As UInt32?
                If Not drv.HasValue Then
                    Return Nothing
                End If

                Try
                    Using dev As New DiskDevice("\\?\PhysicalDrive" & drv.Value.ToString(), FileAccess.Read)
                        Dim rawsig(0 To Convert.ToInt32(dev.Geometry.BytesPerSector - 1UI)) As Byte
                        dev.GetRawDiskStream().Read(rawsig, 0, rawsig.Length)
                        Return BitConverter.ToUInt32(rawsig, &H1B8)
                    End Using

                Catch ex As Exception
                    Trace.WriteLine("Error reading signature from MBR for drive " & drv.Value.ToString() & ": " & ex.ToString())

                End Try
            End Function

        Return _
            deviceProperties.
            ConvertAll(
                Function(dev) New DiskStateView With {
                    .DeviceProperties = dev,
                    .DriveNumber = getid(dev),
                    .RawDiskSignature = getsig(.DriveNumber)
                })

    End Function

    Public Function GetFullView(portnumber As Byte, deviceProperties As List(Of DeviceProperties)) As List(Of DiskStateView)

        If PSDiskParser Is Nothing OrElse PSPhysicalDiskParser Is Nothing Then
            Throw New Exception("Full disk view not supported on this platform")
        End If

        Dim diskstates = PSDiskParser.GetDiskStates().ToArray()

        Dim phdiskstates = Aggregate disk In PSPhysicalDiskParser.GetDiskStates()
                            Select New KeyValuePair(Of UInteger, PSPhysicalDiskParser.PhysicalDiskState)(DiskDevice.GetDeviceNumber("\\?\PhysicalDrive" & disk.DeviceId), disk)
                            Into ToArray()

        Return _
            deviceProperties.ConvertAll(
                Function(dev) New DiskStateView With {
                    .DeviceProperties = dev,
                    .PhysicalDiskState = (Aggregate DiskState In phdiskstates Into FirstOrDefault(DiskState.Key = dev.DeviceNumber)).Value,
                    .DiskState = If(.PhysicalDiskState IsNot Nothing,
                                    (Aggregate DiskState In diskstates
                                     Into FirstOrDefault(DiskState.Number.HasValue AndAlso DiskState.Number.Value = UInteger.Parse(.PhysicalDiskState.DeviceId))),
                                    Nothing),
                    .DriveNumber = If(.DiskState IsNot Nothing,
                                      .DiskState.Number,
                                      If(.PhysicalDiskState IsNot Nothing,
                                         UInteger.Parse(.PhysicalDiskState.DeviceId),
                                         Nothing))
                })

    End Function


#Region "IDisposable Support"
    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
                ' TODO: dispose managed state (managed objects).
            End If

            ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
            For Each obj In New IDisposable() {PSPhysicalDiskParser, PSDiskParser}
                If obj IsNot Nothing Then
                    obj.Dispose()
                End If
            Next

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

    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
