Imports System.Management.Automation

Namespace PSDisk

    Public Class PSDiskAPI

        Public Shared Sub SetDisk(drivenumber As UInteger, params As IDictionary)

            Using ps = PowerShell.Create(RunspaceMode.NewRunspace)

                ps.AddCommand("Set-Disk")

                ps.AddParameter("Number", drivenumber)

                ps.AddParameters(params)

                ps.Invoke()

            End Using

        End Sub

        Public Shared Sub SetDiskOffline(drivenumber As UInteger, offline As Boolean)

            Using ps = PowerShell.Create(RunspaceMode.NewRunspace)

                ps.AddCommand("Set-Disk")

                ps.AddParameter("Number", drivenumber)

                ps.AddParameter("IsOffline", offline)

                ps.Invoke()

            End Using

        End Sub

        Public Shared Sub SetPartition(drivenumber As UInteger, partitionnumber As UInteger, params As IDictionary)

            Using ps = PowerShell.Create(RunspaceMode.NewRunspace)

                ps.AddCommand("Set-Partition")

                ps.AddParameter("DiskNumber", drivenumber)

                ps.AddParameter("PartitionNumber", partitionnumber)

                ps.AddParameters(params)

                ps.Invoke()

            End Using

        End Sub

        'Refactored from: PSDiskAPI.SetDisk(obj.DriveNumber.Value, New Dictionary(Of String, Object) From {{"IsOffline", False}})

        Public Shared Sub SetPartitionOffline(drivenumber As UInteger, partitionnumber As UInteger, offline As Boolean)

            Using ps = PowerShell.Create(RunspaceMode.NewRunspace)

                ps.AddCommand("Set-Partition")

                ps.AddParameter("DiskNumber", drivenumber)

                ps.AddParameter("PartitionNumber", partitionnumber)

                ps.AddParameter("IsOffline", offline)

                ps.Invoke()

            End Using

        End Sub

        Public Shared Sub AddPartitionAccessPath(drivenumber As UInteger, partitionnumber As UInteger, params As IDictionary)

            Using ps = PowerShell.Create(RunspaceMode.NewRunspace)

                ps.AddCommand("Add-PartitionAccessPath")

                ps.AddParameter("DiskNumber", drivenumber)

                ps.AddParameter("PartitionNumber", partitionnumber)

                ps.AddParameters(params)

                ps.Invoke()

            End Using

        End Sub

        Public Shared Function GetPartitions(drivenumber As UInteger, params As IDictionary) As Collection(Of PSObject)

            Using ps = PowerShell.Create(RunspaceMode.NewRunspace)

                ps.AddCommand("Get-Partition")

                ps.AddParameter("DiskNumber", drivenumber)

                ps.AddParameters(params)

                Return ps.Invoke()

            End Using

        End Function

        Public Shared Sub AddPartitionDriveLetter(drivenumber As UInteger, partitionnumber As UInteger)

            Using ps = PowerShell.Create(RunspaceMode.NewRunspace)

                ps.AddCommand("Add-PartitionAccessPath")

                ps.AddParameter("DiskNumber", drivenumber)

                ps.AddParameter("PartitionNumber", partitionnumber)

                ps.AddParameter("AssignDriveLetter")

                ps.Invoke()

            End Using

        End Sub

    End Class

End Namespace
