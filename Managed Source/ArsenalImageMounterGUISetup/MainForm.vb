''''' MainForm.vb
''''' GUI driver setup application.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.ComponentModel
Imports System.Configuration
Imports System.IO
Imports System.Windows.Forms
Imports Arsenal.ImageMounter
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32

#Disable Warning IDE1006 ' Naming Styles

Public Class MainForm

    Private Shared ReadOnly UsingDebugConsole As Boolean

    Shared Sub New()

        If ConfigurationManager.AppSettings!DebugConsole = Boolean.TrueString Then
            NativeFileIO.SafeNativeMethods.AllocConsole()
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

        Dim eulaconfirmed = Registry.GetValue("HKEY_CURRENT_USER\Software\Arsenal Recon\Image Mounter", "EULAConfirmed", 0)

        If TypeOf eulaconfirmed IsNot Integer OrElse CType(eulaconfirmed, Integer) < 1 Then
            If MessageBox.Show(Me,
                               GetEULA(),
                               "Arsenal Image Mounter",
                               MessageBoxButtons.OKCancel,
                               MessageBoxIcon.Information) <> DialogResult.OK Then
                Application.Exit()
                Return
            End If

        End If

        Registry.SetValue("HKEY_CURRENT_USER\Software\Arsenal Recon\Image Mounter", "EULAConfirmed", 1)

        Try
            tbOSType.Text = $"{DriverSetup.Kernel} ({If(DriverSetup.HasStorPort, "storport", "scsiport")})"

        Catch ex As Exception
            tbOSType.Text = $"Exception: {ex.JoinMessages()}"

        End Try

        RefreshStatus()

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
            tbStatus.Text = $"Exception: {ex.JoinMessages()}"

        Finally
            ResumeLayout()

        End Try

    End Sub

    Private Sub btnInstall_Click(sender As Object, e As EventArgs) Handles btnInstall.Click, btnUninstall.Click

        Try
            If sender Is btnInstall Then
                Using zipStream = GetType(MainForm).Assembly.GetManifestResourceStream(
                    GetType(MainForm), "DriverFiles.zip")

                    DriverSetup.InstallFromZipStream(Me, zipStream)

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
                End Using
            ElseIf sender Is btnUninstall Then
                DriverSetup.Uninstall(Me)

                Try
                    Using New ScsiAdapter
                    End Using

                    MessageBox.Show(Me,
                                    "A reboot may be required to complete driver setup.",
                                    "Arsenal Image Mounter",
                                    MessageBoxButtons.OK)

                Catch ex As Exception
                    Trace.WriteLine(ex.ToString())

                End Try
            End If

        Catch ex As Exception
            If TypeOf ex Is Win32Exception Then
                Trace.WriteLine($"Win32 error: {DirectCast(ex, Win32Exception).NativeErrorCode}")
            End If
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

        Finally
            RefreshStatus()

        End Try

    End Sub
End Class