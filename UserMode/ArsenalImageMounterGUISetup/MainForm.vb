
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

    Private Shared ReadOnly setupsource As String = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    Private Shared ReadOnly UsingDebugConsole As Boolean

    Shared Sub New()

        If ConfigurationManager.AppSettings!DebugConsole = Boolean.TrueString Then
            NativeFileIO.Win32API.AllocConsole()
            Trace.Listeners.Add(New ConsoleTraceListener)
            UsingDebugConsole = True
        End If

    End Sub

    Private Shared Function GetEULA() As String

        Using Stream =
            GetType(MainForm).
            Assembly.
            GetManifestResourceStream(GetType(MainForm), "EULA.txt")

            Using reader As New StreamReader(Stream)

                Return reader.ReadToEnd()

            End Using

        End Using

    End Function

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        My.Settings.Reload()

        If Not My.Settings.EULAConfirmed Then

            If MessageBox.Show(Me,
                               GetEULA(),
                               "Arsenal Image Mounter",
                               MessageBoxButtons.OKCancel,
                               MessageBoxIcon.Information) <> DialogResult.OK Then
                Application.Exit()
                Return
            End If

        End If

        My.Settings.EULAConfirmed = True

        My.Settings.Save()

        Try
            tbOSType.Text = DriverSetup.kernel & " (" & If(DriverSetup.hasStorPort, "storport", "scsiport") & ")"

        Catch ex As Exception
            tbOSType.Text = "Exception: " & ex.GetBaseException().Message

        End Try

        RefreshStatus()

    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        MyBase.OnClosed(e)

        My.Settings.Save()

    End Sub

    Private Sub RefreshStatus()

        SuspendLayout()

        btnInstall.Enabled = False
        btnUninstall.Enabled = False

        Try
            If API.AdapterDevicePresent Then
                Try
                    Using New ScsiAdapter
                    End Using

                    btnInstall.Enabled = False
                    btnUninstall.Enabled = True
                    tbStatus.Text = "Installed."

                Catch ex As Exception
                    btnInstall.Enabled = True
                    btnUninstall.Enabled = True
                    tbStatus.Text = "Needs upgrade."

                End Try
            Else
                btnInstall.Enabled = True
                btnUninstall.Enabled = False
                tbStatus.Text = "Not installed."
            End If

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            tbStatus.Text = "Exception: " & ex.GetBaseException().Message

        Finally
            ResumeLayout()

        End Try

    End Sub

    Private Sub btnInstall_Click(sender As Object, e As EventArgs) Handles btnInstall.Click, btnUninstall.Click

        Try
            If sender Is btnInstall Then
                DriverSetup.Install(Handle, setupsource)
            ElseIf sender Is btnUninstall Then
                DriverSetup.Uninstall(Handle)
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

        Try
            Using New ScsiAdapter
            End Using

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            "A reboot may be required to complete driver setup.",
                            "Arsenal Image Mounter",
                            MessageBoxButtons.OK)

        End Try

    End Sub
End Class