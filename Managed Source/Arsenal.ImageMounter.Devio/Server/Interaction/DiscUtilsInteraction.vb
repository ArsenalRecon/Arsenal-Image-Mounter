Imports System.Windows.Forms
Imports Arsenal.ImageMounter.Extensions

Namespace Server.Interaction

    Public MustInherit Class DiscUtilsInteraction

        Public Shared Property RAMDiskPartitionOffset As Integer = 65536

        Public Shared Property RAMDiskEndOfDiskFreeSpace As Integer = 1 << 20

        Private Sub New()
        End Sub

        Shared Sub New()
            DevioServiceFactory.Initialize()
        End Sub

        Public Shared Function InteractiveCreateRAMDisk(adapter As ScsiAdapter) As RAMDiskService

            Dim DeviceNumber As UInteger = ScsiAdapter.AutoDeviceNumber

            Dim strsize = Microsoft.VisualBasic.InputBox("Enter size in MB", "RAM disk", "0")

            Dim size_mb As Long
            If Not Long.TryParse(strsize, size_mb) Then
                Return Nothing
            End If

            Dim ramdisk As New RAMDiskService(adapter, size_mb << 20, RAMDiskFileSystem.NTFS)

            If ramdisk.MountPoint IsNot Nothing Then
                Try
                    Process.Start(ramdisk.MountPoint)

                Catch ex As Exception
                    MessageBox.Show($"Failed to open Explorer window for created RAM disk: {ex.JoinMessages()}")

                End Try
            End If

            Return ramdisk

        End Function

    End Class

End Namespace
