<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class MainForm
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
        Me.components = New System.ComponentModel.Container()
        Me.lblDeviceList = New System.Windows.Forms.Label()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.btnRemoveSelected = New System.Windows.Forms.Button()
        Me.btnRemoveAll = New System.Windows.Forms.Button()
        Me.btnMountRaw = New System.Windows.Forms.Button()
        Me.btnMountDiscUtils = New System.Windows.Forms.Button()
        Me.btnMountLibEwf = New System.Windows.Forms.Button()
        Me.btnRescanBus = New System.Windows.Forms.Button()
        Me.cbNotifyLibEwf = New System.Windows.Forms.CheckBox()
        Me.btnMountMultiPartRaw = New System.Windows.Forms.Button()
        Me.lbDevices = New System.Windows.Forms.DataGridView()
        Me.ScsiIdDataGridViewTextBoxColumn = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.ImagePathDataGridViewTextBoxColumn = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.DriveNumberString = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.IsOfflineDataGridViewTextBoxColumn = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.PartitionLayoutDataGridViewTextBoxColumn = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.SignatureDataGridViewTextBoxColumn = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.DiskSizeDataGridViewTextBoxColumn = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.IsReadOnlyDataGridViewTextBoxColumn = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.DiskStateViewBindingSource = New System.Windows.Forms.BindingSource(Me.components)
        CType(Me.lbDevices, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.DiskStateViewBindingSource, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'lblDeviceList
        '
        Me.lblDeviceList.AutoSize = True
        Me.lblDeviceList.Font = New System.Drawing.Font("Microsoft Sans Serif", 8.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblDeviceList.Location = New System.Drawing.Point(12, 9)
        Me.lblDeviceList.Name = "lblDeviceList"
        Me.lblDeviceList.Size = New System.Drawing.Size(67, 13)
        Me.lblDeviceList.TabIndex = 1
        Me.lblDeviceList.Text = "Device list"
        '
        'btnRefresh
        '
        Me.btnRefresh.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRefresh.Location = New System.Drawing.Point(12, 258)
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Size = New System.Drawing.Size(629, 24)
        Me.btnRefresh.TabIndex = 2
        Me.btnRefresh.Text = "Refresh list"
        Me.btnRefresh.UseVisualStyleBackColor = True
        '
        'btnRemoveSelected
        '
        Me.btnRemoveSelected.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRemoveSelected.Enabled = False
        Me.btnRemoveSelected.Location = New System.Drawing.Point(12, 318)
        Me.btnRemoveSelected.Name = "btnRemoveSelected"
        Me.btnRemoveSelected.Size = New System.Drawing.Size(629, 24)
        Me.btnRemoveSelected.TabIndex = 3
        Me.btnRemoveSelected.Text = "Remove selected"
        Me.btnRemoveSelected.UseVisualStyleBackColor = True
        '
        'btnRemoveAll
        '
        Me.btnRemoveAll.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRemoveAll.Enabled = False
        Me.btnRemoveAll.Location = New System.Drawing.Point(12, 348)
        Me.btnRemoveAll.Name = "btnRemoveAll"
        Me.btnRemoveAll.Size = New System.Drawing.Size(629, 24)
        Me.btnRemoveAll.TabIndex = 3
        Me.btnRemoveAll.Text = "Remove all"
        Me.btnRemoveAll.UseVisualStyleBackColor = True
        '
        'btnMountRaw
        '
        Me.btnMountRaw.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountRaw.Location = New System.Drawing.Point(12, 378)
        Me.btnMountRaw.Name = "btnMountRaw"
        Me.btnMountRaw.Size = New System.Drawing.Size(629, 24)
        Me.btnMountRaw.TabIndex = 3
        Me.btnMountRaw.Text = "Mount raw image"
        Me.btnMountRaw.UseVisualStyleBackColor = True
        '
        'btnMountDiscUtils
        '
        Me.btnMountDiscUtils.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountDiscUtils.Location = New System.Drawing.Point(12, 437)
        Me.btnMountDiscUtils.Name = "btnMountDiscUtils"
        Me.btnMountDiscUtils.Size = New System.Drawing.Size(629, 24)
        Me.btnMountDiscUtils.TabIndex = 3
        Me.btnMountDiscUtils.Text = "Mount through DiscUtils"
        Me.btnMountDiscUtils.UseVisualStyleBackColor = True
        '
        'btnMountLibEwf
        '
        Me.btnMountLibEwf.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountLibEwf.Location = New System.Drawing.Point(12, 467)
        Me.btnMountLibEwf.Name = "btnMountLibEwf"
        Me.btnMountLibEwf.Size = New System.Drawing.Size(629, 24)
        Me.btnMountLibEwf.TabIndex = 3
        Me.btnMountLibEwf.Text = "Mount through libewf"
        Me.btnMountLibEwf.UseVisualStyleBackColor = True
        '
        'btnRescanBus
        '
        Me.btnRescanBus.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRescanBus.Location = New System.Drawing.Point(12, 288)
        Me.btnRescanBus.Name = "btnRescanBus"
        Me.btnRescanBus.Size = New System.Drawing.Size(629, 24)
        Me.btnRescanBus.TabIndex = 2
        Me.btnRescanBus.Text = "Rescan SCSI bus"
        Me.btnRescanBus.UseVisualStyleBackColor = True
        '
        'cbNotifyLibEwf
        '
        Me.cbNotifyLibEwf.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.cbNotifyLibEwf.AutoSize = True
        Me.cbNotifyLibEwf.Location = New System.Drawing.Point(15, 497)
        Me.cbNotifyLibEwf.Name = "cbNotifyLibEwf"
        Me.cbNotifyLibEwf.Size = New System.Drawing.Size(98, 17)
        Me.cbNotifyLibEwf.TabIndex = 5
        Me.cbNotifyLibEwf.Text = "Debug console"
        Me.cbNotifyLibEwf.UseVisualStyleBackColor = True
        '
        'btnMountMultiPartRaw
        '
        Me.btnMountMultiPartRaw.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountMultiPartRaw.Location = New System.Drawing.Point(12, 407)
        Me.btnMountMultiPartRaw.Name = "btnMountMultiPartRaw"
        Me.btnMountMultiPartRaw.Size = New System.Drawing.Size(629, 24)
        Me.btnMountMultiPartRaw.TabIndex = 3
        Me.btnMountMultiPartRaw.Text = "Mount multi-part raw"
        Me.btnMountMultiPartRaw.UseVisualStyleBackColor = True
        '
        'lbDevices
        '
        Me.lbDevices.AllowUserToAddRows = False
        Me.lbDevices.AllowUserToDeleteRows = False
        Me.lbDevices.AllowUserToResizeRows = False
        Me.lbDevices.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lbDevices.AutoGenerateColumns = False
        Me.lbDevices.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.lbDevices.Columns.AddRange(New System.Windows.Forms.DataGridViewColumn() {Me.ScsiIdDataGridViewTextBoxColumn, Me.ImagePathDataGridViewTextBoxColumn, Me.DriveNumberString, Me.IsOfflineDataGridViewTextBoxColumn, Me.PartitionLayoutDataGridViewTextBoxColumn, Me.SignatureDataGridViewTextBoxColumn, Me.DiskSizeDataGridViewTextBoxColumn, Me.IsReadOnlyDataGridViewTextBoxColumn})
        Me.lbDevices.DataSource = Me.DiskStateViewBindingSource
        Me.lbDevices.Location = New System.Drawing.Point(12, 25)
        Me.lbDevices.Name = "lbDevices"
        Me.lbDevices.ReadOnly = True
        Me.lbDevices.RowHeadersVisible = False
        Me.lbDevices.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.lbDevices.Size = New System.Drawing.Size(629, 227)
        Me.lbDevices.TabIndex = 6
        '
        'ScsiIdDataGridViewTextBoxColumn
        '
        Me.ScsiIdDataGridViewTextBoxColumn.DataPropertyName = "ScsiId"
        Me.ScsiIdDataGridViewTextBoxColumn.HeaderText = "SCSI Id"
        Me.ScsiIdDataGridViewTextBoxColumn.Name = "ScsiIdDataGridViewTextBoxColumn"
        Me.ScsiIdDataGridViewTextBoxColumn.ReadOnly = True
        Me.ScsiIdDataGridViewTextBoxColumn.Width = 60
        '
        'ImagePathDataGridViewTextBoxColumn
        '
        Me.ImagePathDataGridViewTextBoxColumn.DataPropertyName = "ImagePath"
        Me.ImagePathDataGridViewTextBoxColumn.HeaderText = "Image object/path"
        Me.ImagePathDataGridViewTextBoxColumn.Name = "ImagePathDataGridViewTextBoxColumn"
        Me.ImagePathDataGridViewTextBoxColumn.ReadOnly = True
        Me.ImagePathDataGridViewTextBoxColumn.Width = 200
        '
        'DriveNumberString
        '
        Me.DriveNumberString.DataPropertyName = "DriveNumberString"
        Me.DriveNumberString.HeaderText = "Drive number"
        Me.DriveNumberString.Name = "DriveNumberString"
        Me.DriveNumberString.ReadOnly = True
        Me.DriveNumberString.Width = 50
        '
        'IsOfflineDataGridViewTextBoxColumn
        '
        Me.IsOfflineDataGridViewTextBoxColumn.DataPropertyName = "OfflineString"
        Me.IsOfflineDataGridViewTextBoxColumn.HeaderText = "Online Offline"
        Me.IsOfflineDataGridViewTextBoxColumn.Name = "IsOfflineDataGridViewTextBoxColumn"
        Me.IsOfflineDataGridViewTextBoxColumn.ReadOnly = True
        Me.IsOfflineDataGridViewTextBoxColumn.Width = 40
        '
        'PartitionLayoutDataGridViewTextBoxColumn
        '
        Me.PartitionLayoutDataGridViewTextBoxColumn.DataPropertyName = "PartitionLayout"
        Me.PartitionLayoutDataGridViewTextBoxColumn.HeaderText = "Partition layout"
        Me.PartitionLayoutDataGridViewTextBoxColumn.Name = "PartitionLayoutDataGridViewTextBoxColumn"
        Me.PartitionLayoutDataGridViewTextBoxColumn.ReadOnly = True
        Me.PartitionLayoutDataGridViewTextBoxColumn.Width = 50
        '
        'SignatureDataGridViewTextBoxColumn
        '
        Me.SignatureDataGridViewTextBoxColumn.DataPropertyName = "Signature"
        Me.SignatureDataGridViewTextBoxColumn.HeaderText = "Disk signature"
        Me.SignatureDataGridViewTextBoxColumn.Name = "SignatureDataGridViewTextBoxColumn"
        Me.SignatureDataGridViewTextBoxColumn.ReadOnly = True
        Me.SignatureDataGridViewTextBoxColumn.Width = 80
        '
        'DiskSizeDataGridViewTextBoxColumn
        '
        Me.DiskSizeDataGridViewTextBoxColumn.DataPropertyName = "DiskSize"
        Me.DiskSizeDataGridViewTextBoxColumn.HeaderText = "Disk size"
        Me.DiskSizeDataGridViewTextBoxColumn.Name = "DiskSizeDataGridViewTextBoxColumn"
        Me.DiskSizeDataGridViewTextBoxColumn.ReadOnly = True
        Me.DiskSizeDataGridViewTextBoxColumn.Width = 80
        '
        'IsReadOnlyDataGridViewTextBoxColumn
        '
        Me.IsReadOnlyDataGridViewTextBoxColumn.DataPropertyName = "ReadOnlyString"
        Me.IsReadOnlyDataGridViewTextBoxColumn.HeaderText = "Read Write"
        Me.IsReadOnlyDataGridViewTextBoxColumn.Name = "IsReadOnlyDataGridViewTextBoxColumn"
        Me.IsReadOnlyDataGridViewTextBoxColumn.ReadOnly = True
        Me.IsReadOnlyDataGridViewTextBoxColumn.Width = 40
        '
        'DiskStateViewBindingSource
        '
        Me.DiskStateViewBindingSource.DataSource = GetType(Arsenal.ImageMounter.DiskStateView)
        '
        'MainForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(653, 522)
        Me.Controls.Add(Me.lbDevices)
        Me.Controls.Add(Me.cbNotifyLibEwf)
        Me.Controls.Add(Me.btnMountLibEwf)
        Me.Controls.Add(Me.btnMountDiscUtils)
        Me.Controls.Add(Me.btnMountMultiPartRaw)
        Me.Controls.Add(Me.btnMountRaw)
        Me.Controls.Add(Me.btnRemoveAll)
        Me.Controls.Add(Me.btnRemoveSelected)
        Me.Controls.Add(Me.btnRescanBus)
        Me.Controls.Add(Me.btnRefresh)
        Me.Controls.Add(Me.lblDeviceList)
        Me.Name = "MainForm"
        Me.Text = "Arsenal Image Mounter"
        CType(Me.lbDevices, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.DiskStateViewBindingSource, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Private WithEvents lblDeviceList As System.Windows.Forms.Label
    Private WithEvents btnRefresh As System.Windows.Forms.Button
    Private WithEvents btnRemoveSelected As System.Windows.Forms.Button
    Private WithEvents btnRemoveAll As System.Windows.Forms.Button
    Private WithEvents btnMountRaw As System.Windows.Forms.Button
    Private WithEvents btnMountDiscUtils As System.Windows.Forms.Button
    Private WithEvents btnMountLibEwf As System.Windows.Forms.Button
    Private WithEvents btnRescanBus As System.Windows.Forms.Button
    Private WithEvents cbNotifyLibEwf As System.Windows.Forms.CheckBox
    Private WithEvents btnMountMultiPartRaw As System.Windows.Forms.Button
    Private WithEvents lbDevices As System.Windows.Forms.DataGridView
    Private WithEvents DiskStateViewBindingSource As System.Windows.Forms.BindingSource
    Private WithEvents DriveNumberDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents ScsiIdDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents ImagePathDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents DriveNumberString As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents IsOfflineDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents PartitionLayoutDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents SignatureDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents DiskSizeDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn
    Private WithEvents IsReadOnlyDataGridViewTextBoxColumn As System.Windows.Forms.DataGridViewTextBoxColumn

End Class
