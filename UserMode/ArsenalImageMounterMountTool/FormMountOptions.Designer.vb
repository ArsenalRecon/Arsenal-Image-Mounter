<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FormMountOptions
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(FormMountOptions))
        Me.rbReadOnly = New System.Windows.Forms.RadioButton()
        Me.rbWriteOverlay = New System.Windows.Forms.RadioButton()
        Me.rbReadWrite = New System.Windows.Forms.RadioButton()
        Me.lblReadOnly = New System.Windows.Forms.Label()
        Me.lblWriteOverlay = New System.Windows.Forms.Label()
        Me.lblReadWrite = New System.Windows.Forms.Label()
        Me.cbFakeDiskSig = New System.Windows.Forms.CheckBox()
        Me.lblFakeDiskSig = New System.Windows.Forms.Label()
        Me.btnOK = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'rbReadOnly
        '
        Me.rbReadOnly.AutoSize = True
        Me.rbReadOnly.Enabled = False
        Me.rbReadOnly.Location = New System.Drawing.Point(13, 13)
        Me.rbReadOnly.Name = "rbReadOnly"
        Me.rbReadOnly.Size = New System.Drawing.Size(73, 17)
        Me.rbReadOnly.TabIndex = 0
        Me.rbReadOnly.Text = "Read only"
        Me.rbReadOnly.UseVisualStyleBackColor = True
        '
        'rbWriteOverlay
        '
        Me.rbWriteOverlay.AutoSize = True
        Me.rbWriteOverlay.Enabled = False
        Me.rbWriteOverlay.Location = New System.Drawing.Point(13, 117)
        Me.rbWriteOverlay.Name = "rbWriteOverlay"
        Me.rbWriteOverlay.Size = New System.Drawing.Size(99, 17)
        Me.rbWriteOverlay.TabIndex = 0
        Me.rbWriteOverlay.Text = "Write temporary"
        Me.rbWriteOverlay.UseVisualStyleBackColor = True
        '
        'rbReadWrite
        '
        Me.rbReadWrite.AutoSize = True
        Me.rbReadWrite.Enabled = False
        Me.rbReadWrite.Location = New System.Drawing.Point(13, 187)
        Me.rbReadWrite.Name = "rbReadWrite"
        Me.rbReadWrite.Size = New System.Drawing.Size(86, 17)
        Me.rbReadWrite.TabIndex = 0
        Me.rbReadWrite.Text = "Write original"
        Me.rbReadWrite.UseVisualStyleBackColor = True
        '
        'lblReadOnly
        '
        Me.lblReadOnly.Enabled = False
        Me.lblReadOnly.Location = New System.Drawing.Point(16, 33)
        Me.lblReadOnly.Name = "lblReadOnly"
        Me.lblReadOnly.Size = New System.Drawing.Size(473, 20)
        Me.lblReadOnly.TabIndex = 1
        Me.lblReadOnly.Text = "This option mounts the image file as a read-only disk device. No write operations" & _
    " are allowed."
        '
        'lblWriteOverlay
        '
        Me.lblWriteOverlay.Enabled = False
        Me.lblWriteOverlay.Location = New System.Drawing.Point(16, 137)
        Me.lblWriteOverlay.Name = "lblWriteOverlay"
        Me.lblWriteOverlay.Size = New System.Drawing.Size(473, 47)
        Me.lblWriteOverlay.TabIndex = 1
        Me.lblWriteOverlay.Text = resources.GetString("lblWriteOverlay.Text")
        '
        'lblReadWrite
        '
        Me.lblReadWrite.Enabled = False
        Me.lblReadWrite.Location = New System.Drawing.Point(16, 207)
        Me.lblReadWrite.Name = "lblReadWrite"
        Me.lblReadWrite.Size = New System.Drawing.Size(473, 35)
        Me.lblReadWrite.TabIndex = 1
        Me.lblReadWrite.Text = "This option mounts the image file as a readable and writable disk device. All rea" & _
    "d and write operations are directed to the original image file. Use this option " & _
    "to modify an image file."
        '
        'cbFakeDiskSig
        '
        Me.cbFakeDiskSig.Enabled = False
        Me.cbFakeDiskSig.Location = New System.Drawing.Point(19, 56)
        Me.cbFakeDiskSig.Name = "cbFakeDiskSig"
        Me.cbFakeDiskSig.Size = New System.Drawing.Size(456, 15)
        Me.cbFakeDiskSig.TabIndex = 2
        Me.cbFakeDiskSig.Text = "Fake disk signature"
        Me.cbFakeDiskSig.UseVisualStyleBackColor = True
        '
        'lblFakeDiskSig
        '
        Me.lblFakeDiskSig.Enabled = False
        Me.lblFakeDiskSig.Location = New System.Drawing.Point(16, 74)
        Me.lblFakeDiskSig.Name = "lblFakeDiskSig"
        Me.lblFakeDiskSig.Size = New System.Drawing.Size(473, 40)
        Me.lblFakeDiskSig.TabIndex = 1
        Me.lblFakeDiskSig.Text = resources.GetString("lblFakeDiskSig.Text")
        '
        'btnOK
        '
        Me.btnOK.Location = New System.Drawing.Point(267, 248)
        Me.btnOK.Name = "btnOK"
        Me.btnOK.Size = New System.Drawing.Size(108, 33)
        Me.btnOK.TabIndex = 3
        Me.btnOK.Text = "OK"
        Me.btnOK.UseVisualStyleBackColor = True
        '
        'btnCancel
        '
        Me.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.btnCancel.Location = New System.Drawing.Point(381, 248)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(108, 33)
        Me.btnCancel.TabIndex = 3
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'FormMountOptions
        '
        Me.AcceptButton = Me.btnOK
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(501, 293)
        Me.ControlBox = False
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnOK)
        Me.Controls.Add(Me.cbFakeDiskSig)
        Me.Controls.Add(Me.lblReadWrite)
        Me.Controls.Add(Me.lblWriteOverlay)
        Me.Controls.Add(Me.lblFakeDiskSig)
        Me.Controls.Add(Me.lblReadOnly)
        Me.Controls.Add(Me.rbReadWrite)
        Me.Controls.Add(Me.rbWriteOverlay)
        Me.Controls.Add(Me.rbReadOnly)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "FormMountOptions"
        Me.ShowIcon = False
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Mount options"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Private WithEvents rbReadOnly As System.Windows.Forms.RadioButton
    Private WithEvents rbWriteOverlay As System.Windows.Forms.RadioButton
    Private WithEvents rbReadWrite As System.Windows.Forms.RadioButton
    Private WithEvents lblReadOnly As System.Windows.Forms.Label
    Private WithEvents lblWriteOverlay As System.Windows.Forms.Label
    Private WithEvents lblReadWrite As System.Windows.Forms.Label
    Private WithEvents cbFakeDiskSig As System.Windows.Forms.CheckBox
    Private WithEvents lblFakeDiskSig As System.Windows.Forms.Label
    Private WithEvents btnOK As System.Windows.Forms.Button
    Private WithEvents btnCancel As System.Windows.Forms.Button
End Class
