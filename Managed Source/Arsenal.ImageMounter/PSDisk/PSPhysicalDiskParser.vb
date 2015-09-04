Imports System.Management.Automation
Imports System.Reflection
Imports System.Linq.Expressions

Namespace PSDisk

    Public Class PSPhysicalDiskParser
        Implements IDisposable

        Public Enum CannotPoolReason As UInt16
            Unknown = 0
            Other = 1
            In_a_Pool = 2
            Not_Healthy = 3
            Removable_Media = 4
            In_Use_by_Cluster = 5
            Offline = 6
            Insufficient_Capacity = 7
            Spare_Disk = 8
            Reserved_by_subsystem = 9
        End Enum

        Public Class PhysicalDiskState

            Public Property ObjectId As String ' = "{1}\\VPCWIN8ENT\root/Microsoft/Windows/Storage/Providers_v2\SPACES_PhysicalDisk.ObjectId=""{26be8332-3f79-11e3-824f-806e6f6e6963}:PD:{d2598005-8323-11e3-beff-00155db3d602}"""
            Public Property PassThroughClass As String ' = Nothing
            Public Property PassThroughIds As String ' = Nothing
            Public Property PassThroughNamespace As String ' = Nothing
            Public Property PassThroughServer As String ' = Nothing
            Public Property UniqueId As String ' = "SCSI\Disk&Ven_Arsenal&Prod_Virtual_\1&2afd7d61&1&000100:VPCWIN8ENT"
            Public Property AllocatedSize As UInt64? ' = 0
            Public Property BusType As UInt16? ' = 6
            Public Property CannotPoolReason As UInt16() = {6} 'CannotPoolReason
            Public Property CanPool As Boolean? ' = False
            Public Property Description As String ' = ""
            Public Property DeviceId As String ' = "3"
            Public Property EnclosureNumber As UInt16? ' = Nothing
            Public Property FirmwareVersion As String ' = "0001"
            Public Property FriendlyName As String ' = "PhysicalDisk3"
            Public Property HealthStatus As UInt16? ' = 0
            Public Property IsIndicationEnabled As Boolean? ' = Nothing
            Public Property IsPartial As Boolean? ' = False
            Public Property LogicalSectorSize As UInt64? ' = 512
            Public Property Manufacturer As String ' = "Arsenal "
            Public Property MediaType As UInt16? ' = 0
            Public Property Model As String ' = "Virtual "
            Public Property OperationalStatus As UInt16() ' = {2}
            Public Property OtherCannotPoolReasonDescription As String ' = Nothing
            Public Property PartNumber As String ' = Nothing
            Public Property PhysicalLocation As String ' = Nothing
            Public Property PhysicalSectorSize As UInt64? ' = 512
            Public Property SerialNumber As String ' = Nothing
            Public Property Size As UInt64? ' = 1468006400
            Public Property SlotNumber As UInt16? ' = Nothing
            Public Property SoftwareVersion As String ' = Nothing
            Public Property SpindleSpeed As UInt32? ' = 4294967295
            Public Property SupportedUsages As UInt16() ' = {1, 2, 3, 4, 5}
            Public Property Usage As UInt16? ' = 1
            Public Property PSComputerName As String ' = Nothing

            Public Sub New()

            End Sub

            Private Sub New(obj As PSObject)
                FieldAssigner(Of PhysicalDiskState).AssignFieldsFromPSObject(Me, obj)

            End Sub

            Friend Shared Function FromPSObject(obj As PSObject) As PhysicalDiskState
                Return New PhysicalDiskState(obj)
            End Function

        End Class

        Private PS As PowerShell

        Public Sub New()

            PS = PowerShell.Create(RunspaceMode.NewRunspace)

            PS.AddCommand("Get-PhysicalDisk")

        End Sub

        Public Sub New(manufacturer As String, model As String)
            Me.New()

            PS.AddParameter("Manufacturer", manufacturer)
            PS.AddParameter("Model", model)

        End Sub

        Public Function GetDiskStates() As IEnumerable(Of PhysicalDiskState)

            Return PS.Invoke().Select(AddressOf PhysicalDiskState.FromPSObject)

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

End Namespace
