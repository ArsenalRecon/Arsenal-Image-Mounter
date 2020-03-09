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
        Set
            If Value Then
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
                Throw New InvalidOperationException("No supported combination of VirtualDiskAccess modes selected.")
            End If
        End Get
        Set
            Select Case Value
                Case DevioServiceFactory.VirtualDiskAccess.ReadOnly
                    rbReadOnly.Checked = True
                Case DevioServiceFactory.VirtualDiskAccess.ReadWriteOriginal
                    rbReadWrite.Checked = True
                Case DevioServiceFactory.VirtualDiskAccess.ReadWriteOverlay
                    rbWriteOverlay.Checked = True
                Case Else
                    Throw New InvalidOperationException($"Not a supported combination of VirtualDiskAccess modes: {Value}")
            End Select
        End Set
    End Property

    Public Property SelectedSectorSize As UInteger
        Get
            Return UInteger.Parse(cbSectorSize.Text)
        End Get
        Set
            cbSectorSize.Text = Value.ToString()
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

    Private Sub btnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click

        DialogResult = Windows.Forms.DialogResult.OK
        Close()

    End Sub

End Class