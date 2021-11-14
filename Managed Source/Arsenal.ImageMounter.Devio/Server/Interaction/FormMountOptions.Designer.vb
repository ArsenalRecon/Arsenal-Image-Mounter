<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated> _
Partial Class FormMountOptions
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode> _
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
    <System.Diagnostics.DebuggerStepThrough> _
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
        Me.Label1 = New System.Windows.Forms.Label()
        Me.cbSectorSize = New System.Windows.Forms.ComboBox()
        Me.cbRemovable = New System.Windows.Forms.CheckBox()
        Me.SuspendLayout()
        '
        'rbReadOnly
        '
        Me.rbReadOnly.AutoSize = True
        Me.rbReadOnly.Enabled = False
        Me.rbReadOnly.Location = New System.Drawing.Point(15, 15)
        Me.rbReadOnly.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.rbReadOnly.Name = "rbReadOnly"
        Me.rbReadOnly.Size = New System.Drawing.Size(77, 19)
        Me.rbReadOnly.TabIndex = 0
        Me.rbReadOnly.Text = "Read only"
        Me.rbReadOnly.UseVisualStyleBackColor = True
        '
        'rbWriteOverlay
        '
        Me.rbWriteOverlay.AutoSize = True
        Me.rbWriteOverlay.Enabled = False
        Me.rbWriteOverlay.Location = New System.Drawing.Point(14, 65)
        Me.rbWriteOverlay.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.rbWriteOverlay.Name = "rbWriteOverlay"
        Me.rbWriteOverlay.Size = New System.Drawing.Size(111, 19)
        Me.rbWriteOverlay.TabIndex = 2
        Me.rbWriteOverlay.Text = "Write temporary"
        Me.rbWriteOverlay.UseVisualStyleBackColor = True
        '
        'rbReadWrite
        '
        Me.rbReadWrite.AutoSize = True
        Me.rbReadWrite.Enabled = False
        Me.rbReadWrite.Location = New System.Drawing.Point(14, 145)
        Me.rbReadWrite.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.rbReadWrite.Name = "rbReadWrite"
        Me.rbReadWrite.Size = New System.Drawing.Size(96, 19)
        Me.rbReadWrite.TabIndex = 4
        Me.rbReadWrite.Text = "Write original"
        Me.rbReadWrite.UseVisualStyleBackColor = True
        '
        'lblReadOnly
        '
        Me.lblReadOnly.Enabled = False
        Me.lblReadOnly.Location = New System.Drawing.Point(19, 38)
        Me.lblReadOnly.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.lblReadOnly.Name = "lblReadOnly"
        Me.lblReadOnly.Size = New System.Drawing.Size(552, 23)
        Me.lblReadOnly.TabIndex = 1
        Me.lblReadOnly.Text = "Mount the disk image as a read-only disk device.  No write operations will be all" &
    "owed."
        '
        'lblWriteOverlay
        '
        Me.lblWriteOverlay.Enabled = False
        Me.lblWriteOverlay.Location = New System.Drawing.Point(18, 88)
        Me.lblWriteOverlay.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.lblWriteOverlay.Name = "lblWriteOverlay"
        Me.lblWriteOverlay.Size = New System.Drawing.Size(552, 54)
        Me.lblWriteOverlay.TabIndex = 3
        Me.lblWriteOverlay.Text = resources.GetString("lblWriteOverlay.Text")
        '
        'lblReadWrite
        '
        Me.lblReadWrite.Enabled = False
        Me.lblReadWrite.Location = New System.Drawing.Point(18, 168)
        Me.lblReadWrite.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.lblReadWrite.Name = "lblReadWrite"
        Me.lblReadWrite.Size = New System.Drawing.Size(552, 40)
        Me.lblReadWrite.TabIndex = 5
        Me.lblReadWrite.Text = "Mount the disk image as a writeable disk device. Modifications will be written to" &
    " the disk image. (Caution - this option modifies the original disk image.)"
        '
        'cbFakeDiskSig
        '
        Me.cbFakeDiskSig.AutoSize = True
        Me.cbFakeDiskSig.Location = New System.Drawing.Point(14, 243)
        Me.cbFakeDiskSig.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.cbFakeDiskSig.Name = "cbFakeDiskSig"
        Me.cbFakeDiskSig.Size = New System.Drawing.Size(126, 19)
        Me.cbFakeDiskSig.TabIndex = 8
        Me.cbFakeDiskSig.Text = "Fake disk signature"
        Me.cbFakeDiskSig.UseVisualStyleBackColor = True
        '
        'lblFakeDiskSig
        '
        Me.lblFakeDiskSig.Location = New System.Drawing.Point(18, 264)
        Me.lblFakeDiskSig.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.lblFakeDiskSig.Name = "lblFakeDiskSig"
        Me.lblFakeDiskSig.Size = New System.Drawing.Size(552, 50)
        Me.lblFakeDiskSig.TabIndex = 9
        Me.lblFakeDiskSig.Text = resources.GetString("lblFakeDiskSig.Text")
        '
        'btnOK
        '
        Me.btnOK.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnOK.AutoSize = True
        Me.btnOK.Location = New System.Drawing.Point(312, 348)
        Me.btnOK.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.btnOK.Name = "btnOK"
        Me.btnOK.Size = New System.Drawing.Size(126, 38)
        Me.btnOK.TabIndex = 11
        Me.btnOK.Text = "OK"
        Me.btnOK.UseVisualStyleBackColor = True
        '
        'btnCancel
        '
        Me.btnCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnCancel.AutoSize = True
        Me.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.btnCancel.Location = New System.Drawing.Point(444, 348)
        Me.btnCancel.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(126, 38)
        Me.btnCancel.TabIndex = 12
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(21, 216)
        Me.Label1.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(65, 15)
        Me.Label1.TabIndex = 6
        Me.Label1.Text = "Sector size:"
        '
        'cbSectorSize
        '
        Me.cbSectorSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cbSectorSize.Items.AddRange(New Object() {"512", "1024", "2048", "4096", "8192", "16384", "32768", "65536"})
        Me.cbSectorSize.Location = New System.Drawing.Point(108, 212)
        Me.cbSectorSize.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.cbSectorSize.Name = "cbSectorSize"
        Me.cbSectorSize.Size = New System.Drawing.Size(140, 23)
        Me.cbSectorSize.TabIndex = 7
        '
        'cbRemovable
        '
        Me.cbRemovable.AutoSize = True
        Me.cbRemovable.Location = New System.Drawing.Point(14, 329)
        Me.cbRemovable.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.cbRemovable.Name = "cbRemovable"
        Me.cbRemovable.Size = New System.Drawing.Size(190, 19)
        Me.cbRemovable.TabIndex = 10
        Me.cbRemovable.Text = "Create ""removable"" disk device"
        Me.cbRemovable.UseVisualStyleBackColor = True
        '
        'FormMountOptions
        '
        Me.AcceptButton = Me.btnOK
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoScroll = True
        Me.AutoSize = True
        Me.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(597, 400)
        Me.ControlBox = False
        Me.Controls.Add(Me.cbRemovable)
        Me.Controls.Add(Me.cbSectorSize)
        Me.Controls.Add(Me.Label1)
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
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
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
    Private WithEvents cbSectorSize As System.Windows.Forms.ComboBox
    Private WithEvents Label1 As System.Windows.Forms.Label
    Private WithEvents cbRemovable As System.Windows.Forms.CheckBox
End Class
