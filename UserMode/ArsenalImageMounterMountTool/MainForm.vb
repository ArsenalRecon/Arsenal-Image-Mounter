
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
Imports System.Threading.Tasks

Public Class MainForm

    Private Adapter As ScsiAdapter
    Private ReadOnly ServiceList As New List(Of DevioServiceBase)

    Private IsClosing As Boolean
    Private LastCreatedDevice As UInteger?

    Public ReadOnly DeviceListRefreshEvent As New EventWaitHandle(initialState:=False, mode:=EventResetMode.AutoReset)

    Protected Overrides Sub OnLoad(e As EventArgs)
        Do
            Try
                Adapter = New ScsiAdapter
                Exit Do

            Catch ex As FileNotFoundException

                Dim rc =
                    MessageBox.Show(Me,
                                    "This application requires a virtual SCSI miniport driver to create virtual disks. The " &
                                    "necessary driver is not currently installed. Do you wish to install the driver now?",
                                    "Arsenal Image Mounter",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Information,
                                    MessageBoxDefaultButton.Button2)

                If rc = DialogResult.No Then
                    Application.Exit()
                    Return
                End If

                If InstallDriver() Then
                    Continue Do
                Else
                    Application.Exit()
                    Return
                End If

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Dim rc =
                    MessageBox.Show(Me,
                                   ex.GetBaseException().Message,
                                   ex.GetBaseException().GetType().ToString(),
                                   MessageBoxButtons.RetryCancel,
                                   MessageBoxIcon.Exclamation)

                If rc <> DialogResult.Retry Then
                    Application.Exit()
                    Return
                End If

            End Try
        Loop

        MyBase.OnLoad(e)

        With New Thread(AddressOf DeviceListRefreshTask)
            .Start()
        End With

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

    Protected Overrides Sub OnClosed(e As EventArgs)
        IsClosing = True

        DeviceListRefreshEvent.Set()

        MyBase.OnClosed(e)
    End Sub

    Private Sub RefreshDeviceList() Handles btnRefresh.Click

        If IsClosing OrElse Disposing OrElse IsDisposed Then
            Return
        End If

        If InvokeRequired Then
            Invoke(New Action(AddressOf RefreshDeviceList))
            Return
        End If

        SetLabelBusy()

        Thread.Sleep(400)

        btnRemoveSelected.Enabled = False

        DeviceListRefreshEvent.Set()

        'With lbDevices.Items
        '    .Clear()
        '    For Each DeviceInfo In
        '      From DeviceNumber In DeviceList
        '      Select Adapter.QueryDevice(DeviceNumber)

        '        .Add(DeviceInfo.DeviceNumber.ToString("X6") & " - " & DeviceInfo.Filename)

        '    Next
        '    btnRemoveAll.Enabled = .Count > 0
        'End With

    End Sub

    Private Sub SetLabelBusy()

        With lblDeviceList
            .Text = "Loading device list..."
            .ForeColor = Color.White
            .BackColor = Color.DarkRed
        End With

        lblDeviceList.Update()

    End Sub

    Private Sub SetDiskView(list As ICollection(Of DiskStateView), finished As Boolean)

        If finished Then
            With lblDeviceList
                .Text = "Device list"
                .ForeColor = SystemColors.ControlText
                .BackColor = SystemColors.Control
            End With
        End If

        DiskStateViewBindingSource.DataSource = list

        If list Is Nothing OrElse list.Count = 0 Then
            btnRemoveSelected.Enabled = False
            btnRemoveAll.Enabled = False
            Return
        End If

        btnRemoveAll.Enabled = True

        If LastCreatedDevice.HasValue Then
            Dim obj =
                Aggregate diskview In list
                Into FirstOrDefault(diskview.DeviceProperties.DeviceNumber = LastCreatedDevice.Value)

            LastCreatedDevice = Nothing

            '' If a refresh started before device was added and has not yet finished,
            '' the newly created device will not be found here. This routine will be
            '' called again when next refresh has finished in which case an object
            '' will be found.
            If obj Is Nothing Then
                Return
            End If

            If obj.IsOffline.GetValueOrDefault() Then
                If obj.DiskState Is Nothing OrElse obj.DiskState.OfflineReason <> PSDiskParser.OfflineReason.SignatureConflict Then
                    MessageBox.Show(Me,
                                    "The new virtual disk was mounted in offline mode. Please use Disk Management to analyze why disk is offline and for bringing it online.",
                                    "Disk offline",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Exclamation)
                ElseIf obj.IsReadOnly OrElse Not obj.DriveNumber.HasValue Then
                    MessageBox.Show(Me,
                                    "The new virtual disk was mounted in offline mode due to a signature conflict with another disk.",
                                    "Disk offline",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Exclamation)
                ElseIf _
                    MessageBox.Show(Me,
                                    "The new virtual disk was mounted in offline mode due to a signature conflict with another disk. Do you wish to let Windows create a new disk signature and bring the virtual disk online?",
                                    "Disk offline",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Exclamation) = DialogResult.Yes Then

                    Try
                        Update()

                        Using New AsyncMessageBox("Please wait...")
                            PSDiskAPI.SetDisk(obj.DriveNumber.Value, New Dictionary(Of String, Object) From {{"IsOffline", False}})
                        End Using

                        MessageBox.Show(Me,
                                        "The virtual disk was successfully brought online.",
                                        "Disk online",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Information)

                    Catch ex As Exception
                        MessageBox.Show(Me,
                                        "An error occured: " & ex.GetBaseException().Message,
                                        ex.GetBaseException().GetType().ToString(),
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Hand)

                    End Try

                    SetLabelBusy()

                    ThreadPool.QueueUserWorkItem(Sub() RefreshDeviceList())

                End If
            End If
        End If
    End Sub

    Private Sub DeviceListRefreshTask()
        Try

            Using parser As New DiskStateParser()

                Dim devicelist = Task.Factory.StartNew(AddressOf Adapter.GetDeviceProperties)

                Dim simpleviewtask = Task.Factory.StartNew(Function() parser.GetSimpleView(Adapter.ScsiPortNumber, devicelist.Result))

                Dim fullviewtask = Task.Factory.StartNew(Function() parser.GetFullView(Adapter.ScsiPortNumber, devicelist.Result))

                While Not IsHandleCreated
                    If IsClosing OrElse Disposing OrElse IsDisposed Then
                        Return
                    End If
                    Thread.Sleep(300)
                End While

                Invoke(New Action(AddressOf SetLabelBusy))

                Dim simpleview = simpleviewtask.Result

                If IsClosing OrElse Disposing OrElse IsDisposed Then
                    Return
                End If

                Invoke(Sub() SetDiskView(simpleview, finished:=False))

                Dim listFunction As Func(Of Byte, List(Of ScsiAdapter.DeviceProperties), List(Of DiskStateView))

                Try
                    Dim fullview = fullviewtask.Result

                    If IsClosing OrElse Disposing OrElse IsDisposed Then
                        Return
                    End If

                    Invoke(Sub() SetDiskView(fullview, finished:=True))

                    listFunction = AddressOf parser.GetFullView

                Catch ex As Exception
                    Trace.WriteLine("Full disk state view not supported on this platform: " & ex.ToString())

                    listFunction = AddressOf parser.GetSimpleView

                    Invoke(Sub() SetDiskView(simpleview, finished:=True))

                End Try

                Do

                    DeviceListRefreshEvent.WaitOne()

                    If IsClosing OrElse Disposing OrElse IsDisposed Then
                        Exit Do
                    End If

                    Invoke(New Action(AddressOf SetLabelBusy))

                    Dim view = listFunction(Adapter.ScsiPortNumber, Adapter.GetDeviceProperties())

                    If IsClosing OrElse Disposing OrElse IsDisposed Then
                        Return
                    End If

                    Invoke(Sub() SetDiskView(view, finished:=True))

                Loop

            End Using

        Catch ex As Exception
            Trace.WriteLine("Device list view thread caught exception: " & ex.ToString())

        Finally
            DeviceListRefreshEvent.Dispose()

        End Try

    End Sub

    Private Sub lbDevices_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lbDevices.SelectionChanged

        btnRemoveSelected.Enabled = lbDevices.SelectedRows.Count > 0

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
            For Each DeviceItem In
              lbDevices.
              SelectedRows().
              OfType(Of DataGridViewRow)().
              Select(Function(row) row.DataBoundItem).
              OfType(Of DiskStateView)()

                Adapter.RemoveDevice(DeviceItem.DeviceProperties.DeviceNumber)
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

        Update()

        Using New AsyncMessageBox("Please wait...")

            For Each Imagefile In Imagefiles
                Try
                    Dim Service = DevioServiceFactory.AutoMount(Imagefile,
                                                                Adapter,
                                                                ProxyType,
                                                                Flags)

                    AddServiceToShutdownHandler(Service)

                    LastCreatedDevice = Service.DiskDeviceNumber

                Catch ex As Exception
                    Trace.WriteLine(ex.ToString())
                    MessageBox.Show(ex.GetBaseException().Message,
                                    ex.GetBaseException().GetType().ToString(),
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Exclamation)

                End Try
            Next

            RefreshDeviceList()

        End Using

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

    Private Sub cbNotifyLibEwf_CheckedChanged(sender As Object, e As EventArgs) Handles cbNotifyLibEwf.CheckedChanged

        Try

            'Dim pipename As String
            'Using CurrentProcess = Process.GetCurrentProcess
            '    pipename = "\\?\pipe\libewf-devio-" & CurrentProcess.Id
            'End Using
            'Dim pipe As New Pipes.NamedPipeServerStream(pipename, Pipes.PipeDirection.In, 0, Pipes.PipeTransmissionMode.Byte, Pipes.PipeOptions.None)
            'Using pipe

            'End Using

            If cbNotifyLibEwf.Checked Then
                NativeFileIO.Win32API.AllocConsole()
                If Not UsingDebugConsole Then
                    Trace.Listeners.Add(New ConsoleTraceListener With {.Name = "AIMConsoleTraceListener"})
                End If
            Else
                If Not UsingDebugConsole Then
                    Trace.Listeners.Remove("AIMConsoleTraceListener")
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

    Private Function InstallDriver() As Boolean

        Try
            Using msgbox As New AsyncMessageBox("Driver setup in progress")

                Using zipStream = GetType(MainForm).Assembly.GetManifestResourceStream(GetType(MainForm), "DriverFiles.zip")

                    DriverSetup.InstallFromZipFile(msgbox.Handle, zipStream)

                End Using

            End Using

        Catch ex As Exception
            Dim msg = ex.ToString()
            Trace.WriteLine("Exception on driver install: " & msg)
            LogMessage(msg)

            MessageBox.Show(Me,
                            "An error occurred while installing driver: " & ex.GetBaseException().Message,
                            "Driver Setup",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

            Return False

        End Try

        MessageBox.Show(Me,
                        "Driver was successfully installed.",
                        "Driver Setup",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information)

        Return True

    End Function

End Class
