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

    End Class

End Namespace
