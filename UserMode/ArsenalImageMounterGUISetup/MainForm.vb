''''' MainForm.vb
''''' GUI driver setup application.
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Public Class MainForm

    Private Shared ReadOnly kernel As String
    Private Shared ReadOnly ver As Version = Environment.OSVersion.Version
    Private Shared ReadOnly hasStorPort As Boolean
    Private Shared ReadOnly setupsource As String = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    Private Shared ReadOnly UsingDebugConsole As Boolean

    Shared Sub New()

        If ConfigurationManager.AppSettings!DebugConsole = Boolean.TrueString Then
            NativeFileIO.Win32API.AllocConsole()
            Trace.Listeners.Add(New ConsoleTraceListener)
            UsingDebugConsole = True
        End If

        If ver.Major >= 6 AndAlso ver.Minor >= 1 Then
            kernel = "Win7"
            hasStorPort = True
        ElseIf ver.Major >= 6 AndAlso ver.Minor >= 0 Then
            kernel = "WinLH"
            hasStorPort = True
        ElseIf ver.Major >= 5 AndAlso ver.Minor >= 2 Then
            kernel = "WinNET"
            hasStorPort = True
        ElseIf ver.Major >= 5 AndAlso ver.Minor >= 1 Then
            kernel = "WinXP"
            hasStorPort = False
        ElseIf ver.Major >= 5 AndAlso ver.Minor >= 0 Then
            kernel = "Win2K"
            hasStorPort = False
        Else
            kernel = "Not supported"
            hasStorPort = False
        End If

        Trace.WriteLine("Kernel type: " & kernel)
        Trace.WriteLine("Kernel supports StorPort: " & hasStorPort)
        Trace.WriteLine("Setup source path: " & setupsource)

    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        Try
            tbOSType.Text = kernel & " (" & If(hasStorPort, "storport", "scsiport") & ")"

        Catch ex As Exception
            tbOSType.Text = "Exception: " & ex.GetBaseException().Message

        End Try

        RefreshStatus()

    End Sub

    Private Sub RefreshStatus()

        Try
            Dim devInsts = API.GetAdapterDeviceInstances()
            If devInsts Is Nothing OrElse devInsts.Count = 0 Then
                btnInstall.Enabled = True
                tbStatus.Text = "Not installed."
            Else
                btnInstall.Enabled = False
                tbStatus.Text = "Installed."
            End If

        Catch ex As Exception
            tbStatus.Text = "Exception: " & ex.GetBaseException().Message

        End Try

    End Sub

    Private Sub btnInstall_Click(sender As Object, e As EventArgs) Handles btnInstall.Click

        Try

            If hasStorPort Then

                Dim infPath = Path.Combine(setupsource, kernel, "phdskmnt.inf")

                NativeFileIO.CreateRootPnPDevice(Handle, infPath, "root\phdskmnt")

            Else

                ''
                '' Install null device .inf for control unit
                ''
                Dim CtlUnitInfPath = Path.Combine(setupsource, "CtlUnit", "ctlunit.inf")

                NativeFileIO.SetupCopyOEMInf(CtlUnitInfPath, NoOverwrite:=True)

                Directory.SetCurrentDirectory(setupsource)

                NativeFileIO.Win32API.SetupSetNonInteractiveMode(False)

                Dim infPath = Path.Combine(".", kernel, "phdskmnt.inf")

                NativeFileIO.RunDLLInstallHinfSection(Handle, infPath, "DefaultInstall")

                Using scm As New ServiceController("phdskmnt")
                    While scm.Status <> ServiceControllerStatus.Running
                        scm.Start()
                        scm.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(3))
                    End While
                End Using

                NativeFileIO.ScanForHardwareChanges()

            End If

        Catch ex As Exception
            If TypeOf ex Is Win32Exception Then
                Trace.WriteLine("Win32 error: " & DirectCast(ex, Win32Exception).NativeErrorCode)
            End If
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.GetBaseException().Message,
                            ex.GetBaseException().GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

        Finally
            RefreshStatus()

        End Try

    End Sub
End Class