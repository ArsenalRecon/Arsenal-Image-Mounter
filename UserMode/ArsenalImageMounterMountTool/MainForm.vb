
''''' MainForm.vb
''''' GUI mount tool.
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.Devio.Server.Services
Imports Arsenal.ImageMounter.Devio.Server.Interaction
Imports System.Threading

Public Class MainForm

    Private Shared WithEvents CurrentAppDomain As AppDomain = AppDomain.CurrentDomain

    Private Adapter As ScsiAdapter
    Private ReadOnly ServiceList As New List(Of DevioServiceBase)

    Private IsClosing As Boolean

    Private Shared ReadOnly LogFilename As String = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "log.txt")

    Public Shared ReadOnly UsingDebugConsole As Boolean

    Shared Sub New()

        If ConfigurationManager.AppSettings!DebugConsole = Boolean.TrueString Then
            NativeFileIO.Win32API.AllocConsole()
            Trace.Listeners.Add(New ConsoleTraceListener)
            UsingDebugConsole = True
        End If

    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        Do
            Try
                Adapter = New ScsiAdapter
                Exit Do

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                If MessageBox.Show(ex.GetBaseException().Message,
                                   ex.GetBaseException().GetType().ToString(),
                                   MessageBoxButtons.RetryCancel,
                                   MessageBoxIcon.Exclamation) <> Windows.Forms.DialogResult.Retry Then
                    Application.Exit()
                    Return
                End If

            End Try
        Loop

        MyBase.OnLoad(e)

        RefreshDeviceList()
    End Sub

    Protected Overrides Sub OnClosing(e As CancelEventArgs)

        IsClosing = True

        Try
            Dim Services As IEnumerable(Of DevioServiceBase)
            SyncLock ServiceList
                Services = ServiceList.ToArray()
            End SyncLock
            For Each Service In Services
                If Service.HasDiskDevice Then
                    Trace.WriteLine("Requesting service for device " & Service.DiskDeviceNumber.ToString("X6") & " to shut down...")
                    Service.DismountAndStopServiceThread(TimeSpan.FromSeconds(10))
                Else
                    ServiceList.Remove(Service)
                End If
            Next

        Catch ex As Exception
            e.Cancel = True
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(ex.GetBaseException().Message,
                            ex.GetBaseException().GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

        If e.Cancel Then
            IsClosing = False
            RefreshDeviceList()
            Return
        End If

        MyBase.OnClosing(e)

    End Sub

    Private Sub RefreshDeviceList() Handles btnRefresh.Click

        If IsClosing Then
            Return
        End If

        If InvokeRequired Then
            Invoke(New Action(AddressOf RefreshDeviceList))
            Return
        End If

        Thread.Sleep(400)

        Dim DeviceList = Adapter.GetDeviceList()

        Thread.Sleep(400)

        btnRemoveSelected.Enabled = False

        With lbDevices.Items
            .Clear()
            For Each DeviceInfo In
              From DeviceNumber In DeviceList
              Select Adapter.QueryDevice(DeviceNumber)

                .Add(DeviceInfo.DeviceNumber.ToString("X6") & " - " & DeviceInfo.Filename)

            Next
            btnRemoveAll.Enabled = .Count > 0
        End With

    End Sub

    Private Sub lbDevices_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lbDevices.SelectedIndexChanged

        btnRemoveSelected.Enabled = lbDevices.SelectedIndices.Count > 0

    End Sub

    Private Sub btnRemoveAll_Click(sender As Object, e As EventArgs) Handles btnRemoveAll.Click

        Try
            Adapter.RemoveAllDevices()
            RefreshDeviceList()

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(ex.GetBaseException().Message,
                            ex.GetBaseException().GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

    End Sub

    Private Sub btnRemoveSelected_Click(sender As Object, e As EventArgs) Handles btnRemoveSelected.Click

        Try
            For Each DeviceNumber In
              From DeviceItem In lbDevices.SelectedItems().OfType(Of String)()
              Select UInteger.Parse(DeviceItem.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(0), NumberStyles.HexNumber)

                Adapter.RemoveDevice(DeviceNumber)
            Next

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(ex.GetBaseException().Message,
                            ex.GetBaseException().GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

        RefreshDeviceList()

    End Sub

    Private Sub AddServiceToShutdownHandler(Service As DevioServiceBase)

        AddHandler Service.ServiceShutdown,
          Sub()
              SyncLock ServiceList
                  ServiceList.RemoveAll(AddressOf Service.Equals)
              End SyncLock
              RefreshDeviceList()
          End Sub

        SyncLock ServiceList
            ServiceList.Add(Service)
        End SyncLock

    End Sub

    Private Sub btnMount_Click(sender As Object, e As EventArgs) Handles btnMountRaw.Click, btnMountLibEwf.Click, btnMountDiscUtils.Click, btnMountMultiPartRaw.Click

        Dim ProxyType As DevioServiceFactory.ProxyType

        If sender Is btnMountRaw Then
            ProxyType = DevioServiceFactory.ProxyType.None
        ElseIf sender Is btnMountMultiPartRaw Then
            ProxyType = DevioServiceFactory.ProxyType.MultiPartRaw
        ElseIf sender Is btnMountDiscUtils Then
            ProxyType = DevioServiceFactory.ProxyType.DiscUtils
        ElseIf sender Is btnMountLibEwf Then
            ProxyType = DevioServiceFactory.ProxyType.LibEwf
        Else
            Return
        End If

        Dim Imagefiles As String()
        Dim Flags As DeviceFlags
        Using OpenFileDialog As New OpenFileDialog With {
          .CheckFileExists = True,
          .DereferenceLinks = True,
          .Multiselect = True,
          .ReadOnlyChecked = True,
          .ShowReadOnly = True,
          .SupportMultiDottedExtensions = True,
          .ValidateNames = True,
          .AutoUpgradeEnabled = True,
          .Title = "Open image file"
        }
            If OpenFileDialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            If OpenFileDialog.ReadOnlyChecked Then
                Flags = Flags Or DeviceFlags.ReadOnly
            End If

            Imagefiles = OpenFileDialog.FileNames
        End Using

        For Each Imagefile In Imagefiles
            Try
                Dim Service = DevioServiceFactory.AutoMount(Imagefile,
                                                            Adapter,
                                                            ProxyType,
                                                            Flags)

                AddServiceToShutdownHandler(Service)

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                MessageBox.Show(ex.GetBaseException().Message,
                                ex.GetBaseException().GetType().ToString(),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Exclamation)

            End Try
        Next

        RefreshDeviceList()

    End Sub

    Private Sub btnRescanBus_Click(sender As Object, e As EventArgs) Handles btnRescanBus.Click

        Try
            API.RescanScsiAdapter()

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(ex.GetBaseException().Message,
                            ex.GetBaseException().GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

        Adapter.UpdateDiskProperties()

    End Sub

    Private Shared Sub CurrentAppDomain_UnhandledException(sender As Object, e As UnhandledExceptionEventArgs) Handles CurrentAppDomain.UnhandledException

        Dim logfile = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".log")
        File.AppendAllText(logfile,
                             "---------------" & Environment.NewLine &
                             Date.Now.ToString() & Environment.NewLine &
                             e.ExceptionObject.ToString() & Environment.NewLine)

    End Sub

    Private Sub cbNotifyLibEwf_CheckedChanged(sender As Object, e As EventArgs) Handles cbNotifyLibEwf.CheckedChanged

        Try

            'Dim pipename As String
            'Using CurrentProcess = Process.GetCurrentProcess
            '    pipename = "\\.\pipe\libewf-devio-" & CurrentProcess.Id
            'End Using
            'Dim pipe As New Pipes.NamedPipeServerStream(pipename, Pipes.PipeDirection.In, 0, Pipes.PipeTransmissionMode.Byte, Pipes.PipeOptions.None)
            'Using pipe

            'End Using

            If cbNotifyLibEwf.Checked Then
                NativeFileIO.Win32API.AllocConsole()
            Else
                If Not UsingDebugConsole Then
                    NativeFileIO.Win32API.FreeConsole()
                End If
            End If

        Catch ex As Exception
            MessageBox.Show(Me,
                            ex.GetBaseException().Message,
                            ex.GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

        End Try

    End Sub
End Class
