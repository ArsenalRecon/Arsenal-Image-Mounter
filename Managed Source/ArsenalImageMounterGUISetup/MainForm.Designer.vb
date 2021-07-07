<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated> _
Partial Class MainForm
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
        Me.Label1 = New System.Windows.Forms.Label()
        Me.tbStatus = New System.Windows.Forms.TextBox()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.tbOSType = New System.Windows.Forms.TextBox()
        Me.btnInstall = New System.Windows.Forms.Button()
        Me.btnUninstall = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(12, 9)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(69, 13)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "Setup status:"
        '
        'tbStatus
        '
        Me.tbStatus.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tbStatus.Location = New System.Drawing.Point(12, 25)
        Me.tbStatus.Name = "tbStatus"
        Me.tbStatus.ReadOnly = True
        Me.tbStatus.Size = New System.Drawing.Size(268, 20)
        Me.tbStatus.TabIndex = 1
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(12, 51)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(113, 13)
        Me.Label2.TabIndex = 0
        Me.Label2.Text = "Driver type for this OS:"
        '
        'tbOSType
        '
        Me.tbOSType.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tbOSType.Location = New System.Drawing.Point(12, 67)
        Me.tbOSType.Name = "tbOSType"
        Me.tbOSType.ReadOnly = True
        Me.tbOSType.Size = New System.Drawing.Size(268, 20)
        Me.tbOSType.TabIndex = 1
        '
        'btnInstall
        '
        Me.btnInstall.Enabled = False
        Me.btnInstall.Location = New System.Drawing.Point(12, 93)
        Me.btnInstall.Name = "btnInstall"
        Me.btnInstall.Size = New System.Drawing.Size(92, 33)
        Me.btnInstall.TabIndex = 2
        Me.btnInstall.Text = "Install"
        Me.btnInstall.UseVisualStyleBackColor = True
        '
        'btnUninstall
        '
        Me.btnUninstall.Enabled = False
        Me.btnUninstall.Location = New System.Drawing.Point(110, 93)
        Me.btnUninstall.Name = "btnUninstall"
        Me.btnUninstall.Size = New System.Drawing.Size(92, 33)
        Me.btnUninstall.TabIndex = 2
        Me.btnUninstall.Text = "Uninstall"
        Me.btnUninstall.UseVisualStyleBackColor = True
        '
        'MainForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(288, 134)
        Me.Controls.Add(Me.btnUninstall)
        Me.Controls.Add(Me.btnInstall)
        Me.Controls.Add(Me.tbOSType)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.tbStatus)
        Me.Controls.Add(Me.Label1)
        Me.MinimumSize = New System.Drawing.Size(304, 173)
        Me.Name = "MainForm"
        Me.Text = "Arsenal Image Mounter Setup"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents tbStatus As System.Windows.Forms.TextBox
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents tbOSType As System.Windows.Forms.TextBox
    Friend WithEvents btnInstall As System.Windows.Forms.Button
    Friend WithEvents btnUninstall As System.Windows.Forms.Button
End Class
