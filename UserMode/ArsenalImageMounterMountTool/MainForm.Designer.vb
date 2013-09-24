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
        Me.lbDevices = New System.Windows.Forms.ListBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.btnRemoveSelected = New System.Windows.Forms.Button()
        Me.btnRemoveAll = New System.Windows.Forms.Button()
        Me.btnMountRaw = New System.Windows.Forms.Button()
        Me.btnMountDiscUtils = New System.Windows.Forms.Button()
        Me.btnMountLibEwf = New System.Windows.Forms.Button()
        Me.btnRescanBus = New System.Windows.Forms.Button()
        Me.cbNotifyLibEwf = New System.Windows.Forms.CheckBox()
        Me.btnMountMultiPartRaw = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lbDevices
        '
        Me.lbDevices.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lbDevices.FormattingEnabled = True
        Me.lbDevices.Location = New System.Drawing.Point(12, 25)
        Me.lbDevices.Name = "lbDevices"
        Me.lbDevices.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple
        Me.lbDevices.Size = New System.Drawing.Size(226, 82)
        Me.lbDevices.TabIndex = 0
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(12, 9)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(56, 13)
        Me.Label1.TabIndex = 1
        Me.Label1.Text = "Device list"
        '
        'btnRefresh
        '
        Me.btnRefresh.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRefresh.Location = New System.Drawing.Point(12, 113)
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Size = New System.Drawing.Size(226, 24)
        Me.btnRefresh.TabIndex = 2
        Me.btnRefresh.Text = "Refresh list"
        Me.btnRefresh.UseVisualStyleBackColor = True
        '
        'btnRemoveSelected
        '
        Me.btnRemoveSelected.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRemoveSelected.Enabled = False
        Me.btnRemoveSelected.Location = New System.Drawing.Point(12, 173)
        Me.btnRemoveSelected.Name = "btnRemoveSelected"
        Me.btnRemoveSelected.Size = New System.Drawing.Size(226, 24)
        Me.btnRemoveSelected.TabIndex = 3
        Me.btnRemoveSelected.Text = "Remove selected"
        Me.btnRemoveSelected.UseVisualStyleBackColor = True
        '
        'btnRemoveAll
        '
        Me.btnRemoveAll.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRemoveAll.Enabled = False
        Me.btnRemoveAll.Location = New System.Drawing.Point(12, 203)
        Me.btnRemoveAll.Name = "btnRemoveAll"
        Me.btnRemoveAll.Size = New System.Drawing.Size(226, 24)
        Me.btnRemoveAll.TabIndex = 3
        Me.btnRemoveAll.Text = "Remove all"
        Me.btnRemoveAll.UseVisualStyleBackColor = True
        '
        'btnMountRaw
        '
        Me.btnMountRaw.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountRaw.Location = New System.Drawing.Point(12, 233)
        Me.btnMountRaw.Name = "btnMountRaw"
        Me.btnMountRaw.Size = New System.Drawing.Size(226, 24)
        Me.btnMountRaw.TabIndex = 3
        Me.btnMountRaw.Text = "Mount raw image"
        Me.btnMountRaw.UseVisualStyleBackColor = True
        '
        'btnMountDiscUtils
        '
        Me.btnMountDiscUtils.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountDiscUtils.Location = New System.Drawing.Point(12, 292)
        Me.btnMountDiscUtils.Name = "btnMountDiscUtils"
        Me.btnMountDiscUtils.Size = New System.Drawing.Size(226, 24)
        Me.btnMountDiscUtils.TabIndex = 3
        Me.btnMountDiscUtils.Text = "Mount through DiscUtils"
        Me.btnMountDiscUtils.UseVisualStyleBackColor = True
        '
        'btnMountLibEwf
        '
        Me.btnMountLibEwf.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountLibEwf.Location = New System.Drawing.Point(12, 322)
        Me.btnMountLibEwf.Name = "btnMountLibEwf"
        Me.btnMountLibEwf.Size = New System.Drawing.Size(226, 24)
        Me.btnMountLibEwf.TabIndex = 3
        Me.btnMountLibEwf.Text = "Mount through libewf"
        Me.btnMountLibEwf.UseVisualStyleBackColor = True
        '
        'btnRescanBus
        '
        Me.btnRescanBus.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRescanBus.Location = New System.Drawing.Point(12, 143)
        Me.btnRescanBus.Name = "btnRescanBus"
        Me.btnRescanBus.Size = New System.Drawing.Size(226, 24)
        Me.btnRescanBus.TabIndex = 2
        Me.btnRescanBus.Text = "Rescan SCSI bus"
        Me.btnRescanBus.UseVisualStyleBackColor = True
        '
        'cbNotifyLibEwf
        '
        Me.cbNotifyLibEwf.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.cbNotifyLibEwf.AutoSize = True
        Me.cbNotifyLibEwf.Location = New System.Drawing.Point(15, 352)
        Me.cbNotifyLibEwf.Name = "cbNotifyLibEwf"
        Me.cbNotifyLibEwf.Size = New System.Drawing.Size(147, 17)
        Me.cbNotifyLibEwf.TabIndex = 5
        Me.cbNotifyLibEwf.Text = "libewf notification console"
        Me.cbNotifyLibEwf.UseVisualStyleBackColor = True
        '
        'btnMountMultiPartRaw
        '
        Me.btnMountMultiPartRaw.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnMountMultiPartRaw.Location = New System.Drawing.Point(12, 262)
        Me.btnMountMultiPartRaw.Name = "btnMountMultiPartRaw"
        Me.btnMountMultiPartRaw.Size = New System.Drawing.Size(226, 24)
        Me.btnMountMultiPartRaw.TabIndex = 3
        Me.btnMountMultiPartRaw.Text = "Mount multi-part raw"
        Me.btnMountMultiPartRaw.UseVisualStyleBackColor = True
        '
        'MainForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(250, 377)
        Me.Controls.Add(Me.cbNotifyLibEwf)
        Me.Controls.Add(Me.btnMountLibEwf)
        Me.Controls.Add(Me.btnMountDiscUtils)
        Me.Controls.Add(Me.btnMountMultiPartRaw)
        Me.Controls.Add(Me.btnMountRaw)
        Me.Controls.Add(Me.btnRemoveAll)
        Me.Controls.Add(Me.btnRemoveSelected)
        Me.Controls.Add(Me.btnRescanBus)
        Me.Controls.Add(Me.btnRefresh)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.lbDevices)
        Me.Name = "MainForm"
        Me.Text = "Arsenal Image Mounter"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents lbDevices As System.Windows.Forms.ListBox
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents btnRefresh As System.Windows.Forms.Button
    Friend WithEvents btnRemoveSelected As System.Windows.Forms.Button
    Friend WithEvents btnRemoveAll As System.Windows.Forms.Button
    Friend WithEvents btnMountRaw As System.Windows.Forms.Button
    Friend WithEvents btnMountDiscUtils As System.Windows.Forms.Button
    Friend WithEvents btnMountLibEwf As System.Windows.Forms.Button
    Friend WithEvents btnRescanBus As System.Windows.Forms.Button
    Friend WithEvents cbNotifyLibEwf As System.Windows.Forms.CheckBox
    Friend WithEvents btnMountMultiPartRaw As System.Windows.Forms.Button

End Class
