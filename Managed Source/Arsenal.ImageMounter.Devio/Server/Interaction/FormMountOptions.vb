Imports Arsenal.ImageMounter.Devio.Server.Interaction

Public Class FormMountOptions

    Public Property SelectedFakeSignature As Boolean
        Get
            Return cbFakeDiskSig.Checked
        End Get
        Set
            cbFakeDiskSig.Checked = Value
        End Set
    End Property

    Public Property SelectedRemovable As Boolean
        Get
            Return cbRemovable.Checked
        End Get
        Set
            cbRemovable.Checked = Value
        End Set
    End Property

    Public Property SelectedReadOnly As Boolean
        Get
            Return rbReadOnly.Checked
        End Get
        Set(value As Boolean)
            If value Then
                rbReadOnly.Checked = True
            ElseIf rbWriteOverlay.Enabled Then
                rbWriteOverlay.Checked = True
            ElseIf rbReadWrite.Enabled Then
                rbReadWrite.Checked = True
            End If
        End Set
    End Property

    Public Property SelectedAccessMode As DevioServiceFactory.VirtualDiskAccess
        Get
            If rbReadOnly.Checked Then
                Return DevioServiceFactory.VirtualDiskAccess.ReadOnly
            ElseIf rbReadWrite.Checked Then
                Return DevioServiceFactory.VirtualDiskAccess.ReadWriteOriginal
            ElseIf rbWriteOverlay.Checked Then
                Return DevioServiceFactory.VirtualDiskAccess.ReadWriteOverlay
            Else
                Throw New NotSupportedException("No supported combination of VirtualDiskAccess modes selected.")
            End If
        End Get
        Set(value As DevioServiceFactory.VirtualDiskAccess)
            Select Case value
                Case DevioServiceFactory.VirtualDiskAccess.ReadOnly
                    rbReadOnly.Checked = True
                Case DevioServiceFactory.VirtualDiskAccess.ReadWriteOriginal
                    rbReadWrite.Checked = True
                Case DevioServiceFactory.VirtualDiskAccess.ReadWriteOverlay
                    rbWriteOverlay.Checked = True
                Case Else
                    Throw New NotSupportedException("Not a supported combination of VirtualDiskAccess modes: " & value.ToString())
            End Select
        End Set
    End Property

    Public Property SelectedSectorSize As UInteger
        Get
            Return UInteger.Parse(cbSectorSize.Text)
        End Get
        Set(value As UInteger)
            cbSectorSize.Text = value.ToString()
        End Set
    End Property

    Public WriteOnly Property SupportedAccessModes As IEnumerable(Of DevioServiceFactory.VirtualDiskAccess)
        Set(values As IEnumerable(Of DevioServiceFactory.VirtualDiskAccess))
            For Each value In values
                Select Case value
                    Case DevioServiceFactory.VirtualDiskAccess.ReadOnly
                        rbReadOnly.Enabled = True
                    Case DevioServiceFactory.VirtualDiskAccess.ReadWriteOriginal
                        rbReadWrite.Enabled = True
                    Case DevioServiceFactory.VirtualDiskAccess.ReadWriteOverlay
                        rbWriteOverlay.Enabled = True
                End Select
            Next
        End Set
    End Property

    Private Sub rbReadOnly_CheckedChanged() Handles rbReadOnly.CheckedChanged

        cbFakeDiskSig.Enabled = rbReadOnly.Checked
        lblFakeDiskSig.Enabled = rbReadOnly.Checked

    End Sub

    Private Sub btnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click

        DialogResult = Windows.Forms.DialogResult.OK
        Close()

    End Sub

End Class