Imports Arsenal.ImageMounter.Devio.Server.Interaction

Public Class FormMountOptions

    Public Property ProxyType As DevioServiceFactory.ProxyType

    Public Property Flags As DeviceFlags

    Public Property Imagefile As String

    Public Property SectorSize As UInteger

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        Select Case ProxyType

            Case DevioServiceFactory.ProxyType.LibEwf
                rbReadOnly.Enabled = True
                rbWriteOverlay.Enabled = True
                lblReadOnly.Enabled = True
                lblFakeDiskSig.Enabled = True
                lblWriteOverlay.Enabled = True
                If (Flags And DeviceFlags.ReadOnly) <> 0 Then
                    rbReadOnly.Checked = True
                    cbFakeDiskSig.Enabled = True
                Else
                    rbWriteOverlay.Checked = True
                End If

            Case DevioServiceFactory.ProxyType.MultiPartRaw, DevioServiceFactory.ProxyType.None, DevioServiceFactory.ProxyType.DiscUtils
                rbReadOnly.Enabled = True
                rbReadWrite.Enabled = True
                lblReadOnly.Enabled = True
                lblFakeDiskSig.Enabled = True
                lblReadWrite.Enabled = True
                If (Flags And DeviceFlags.ReadOnly) <> 0 Then
                    rbReadOnly.Checked = True
                    cbFakeDiskSig.Enabled = True
                Else
                    rbReadWrite.Checked = True
                End If

            Case Else
                Throw New NotSupportedException("Unknown proxy type=" & CUInt(ProxyType))

        End Select

        cbSectorSize.Text = SectorSize.ToString()

    End Sub

    Protected Overrides Sub OnClosing(e As CancelEventArgs)
        MyBase.OnClosing(e)

        If cbFakeDiskSig.Checked Then
            Flags = Flags Or DeviceFlags.FakeDiskSignatureIfZero
        End If

        If rbReadOnly.Checked Then
            Flags = Flags Or DeviceFlags.ReadOnly
        Else
            Flags = Flags And Not DeviceFlags.ReadOnly
        End If

        SectorSize = UInteger.Parse(cbSectorSize.Text)
        
    End Sub

    Private Sub rbReadOnly_CheckedChanged(sender As Object, e As EventArgs) Handles rbReadOnly.CheckedChanged

        cbFakeDiskSig.Enabled = rbReadOnly.Checked

    End Sub

    Private Sub btnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click

        DialogResult = DialogResult.OK
        Close()

    End Sub

End Class