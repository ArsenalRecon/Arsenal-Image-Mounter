Imports System.Management.Automation
Imports System.Reflection

Public Class PSDiskParser
    Implements IDisposable

    Public Enum OfflineReason As UInt16
        IsOnline = 0
        AdministrativePolicy = 1
        SignatureConflict = 4
    End Enum

    Public Enum OperationalStatus As UInt16
        OK = 1
        IsOffline = 4
    End Enum

    Public Enum PartitionStyle As UInt16
        RAW = 0
        MBR = 1
        GPT = 2
    End Enum

    Public Class DiskState

        Private Shared ReadOnly fields As New Dictionary(Of String, FieldInfo)

        Shared Sub New()
            fields = Aggregate fld In GetType(DiskState).GetFields(BindingFlags.NonPublic Or BindingFlags.Instance)
                      Where fld.Name.StartsWith("_", StringComparison.Ordinal)
                      Into ToDictionary(String.Intern(fld.Name.Substring(1)))
        End Sub

        Public Property AllocatedSize As UInt64 ' = 16777216
        Public Property BootFromDisk As Boolean ' = False
        Public Property BusType As UInt16 ' = 6
        Public Property FirmwareVersion As String ' = "0001"
        Public Property FriendlyName As String ' = "Arsenal Virtual  SCSI Disk Device"
        Public Property Guid As String ' = Nothing
        Public Property HealthStatus As UInt16 ' = 1
        Public Property IsBoot As Boolean ' = False
        Public Property IsClustered As Boolean ' = False
        Public Property IsOffline As Boolean ' = True
        Public Property IsReadOnly As Boolean ' = True
        Public Property IsSystem As Boolean ' = False
        Public Property LargestFreeExtent As UInt64 ' = 0
        Public Property Location As String ' = Nothing
        Public Property LogicalSectorSize As UInt32 ' = 0
        Public Property Manufacturer As String ' = "Arsenal "
        Public Property Model As String ' = "Virtual "
        Public Property Number As UInt32 ' = 2
        Public Property NumberOfPartitions As UInt32 ' = 1
        Public Property ObjectId As String ' = "\\?\scsi#disk&ven_arsenal&prod_virtual_#1&2afd7d61&1&000000#{53f56307-b6bf-11d0-94f2-00a0c91efb8b}"
        Public Property OfflineReason As OfflineReason ' UInt16 = 0 = Online, 1 = Policy, 4 = Sign conflict
        Public Property OperationalStatus As OperationalStatus ' UInt16 = 1 = OK, 4 = Offline
        Public Property PartitionStyle As PartitionStyle ' UInt16 = 1 = MBR
        Public Property Path As String ' = "\\?\scsi#disk&ven_arsenal&prod_virtual_#1&2afd7d61&1&000000#{53f56307-b6bf-11d0-94f2-00a0c91efb8b}"
        Public Property PhysicalSectorSize As UInt32 ' = 512
        Public Property ProvisioningType As UInt16 ' = 2
        Public Property SerialNumber As String ' = Nothing
        Public Property Signature As UInt32 ' = 2499952371
        Public Property Size As UInt64 ' = 16777216
        Public Property UniqueId As String ' = "SCSI\DISK&VEN_ARSENAL&PROD_VIRTUAL_\1&2AFD7D61&1&000000:VPCWIN8ENT"
        Public Property UniqueIdFormat As UInt16 ' = 0
        Public Property PSComputerName As String ' = Nothing

        Public Sub New()

        End Sub

        Private Sub New(obj As PSObject)

            For Each commonprop In
                From prop In obj.Properties
                Join fld In fields
                On prop.Name Equals fld.Key

                commonprop.fld.Value.SetValue(Me, commonprop.prop.Value)

            Next

        End Sub

        Friend Shared Function FromPSObject(obj As PSObject) As DiskState
            Return New DiskState(obj)
        End Function

    End Class

    Private PS As PowerShell

    Public Sub New()

        PS = PowerShell.Create(RunspaceMode.NewRunspace)

        PS.AddCommand("Get-Disk")

    End Sub

    Public Sub New(namefilter As String)
        Me.New()

        PS.AddParameter("FriendlyName", namefilter)

    End Sub

    Public Function GetDiskStates() As IEnumerable(Of DiskState)

        Return PS.Invoke().Select(AddressOf DiskState.FromPSObject)

    End Function

#Region "IDisposable Support"
    Private disposedValue As Boolean ' To detect redundant calls

    ''' <remarks>
    ''' This method may fail to load if PowerShell 3.0 is not installed. Therefore,
    ''' that code cannot be placed in Dispose() method and calling this method needs
    ''' to be protected in a Try/Catch.
    ''' </remarks>
    Private Sub DisposePS()

        If PS IsNot Nothing Then
            PS.Dispose()
        End If

    End Sub

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
                ' TODO: dispose managed state (managed objects).
            End If

            ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
            Try
                DisposePS()

            Catch

            End Try

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
