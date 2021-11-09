
''''' MainForm.vb
''''' GUI mount tool.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
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
Imports System.IO
Imports System.Text
Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.PSDisk
Imports Arsenal.ImageMounter.IO
Imports Arsenal.ImageMounter.Devio
Imports System.Diagnostics.CodeAnalysis
Imports Arsenal.ImageMounter
Imports System.ComponentModel
Imports System.Windows.Forms
Imports System.Drawing

#Disable Warning IDE1006 ' Naming Styles

Public Class MainForm

    Private Class ServiceListItem

        Public Property ImageFile As String

        Public Property Service As DevioServiceBase

    End Class

    Private Adapter As ScsiAdapter
    Private ReadOnly ServiceList As New List(Of ServiceListItem)

    Private IsClosing As Boolean
    Private LastCreatedDevice As UInteger?

    Private ReadOnly DeviceListRefreshEvent As New AutoResetEvent(initialState:=False)

    Protected Overrides Sub OnLoad(e As EventArgs)

        Dim SetupRun As Boolean

        Do

            Try
                Adapter = New ScsiAdapter

                Dim loadedVersion As Version = Nothing
                Try
                    loadedVersion = Adapter.GetDriverSubVersion()

                Catch ex As Exception
                    Trace.WriteLine($"Error checking driver version: {ex}")

                End Try

                If loadedVersion Is Nothing OrElse
                    loadedVersion < GetEmbeddedDriverVersion() Then

                    Dim rc =
                        MessageBox.Show(Me,
                                        "There is an update available to the Arsenal Image Mounter driver. Do you want to install the updated driver now?",
                                        "Arsenal Image Mounter",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Information,
                                        MessageBoxDefaultButton.Button2)

                    If rc = DialogResult.Yes Then

                        Adapter.Dispose()
                        Adapter = Nothing

                        If InstallDriver() Then
                            Continue Do
                        Else
                            Exit Do
                        End If
                    End If

                End If

                Exit Do

            Catch ex As FileNotFoundException

                If SetupRun Then

                    If MessageBox.Show(Me,
                                       "You need to restart your computer to finish driver setup. Do you want to restart now?",
                                       "Arsenal Image Mounter",
                                       MessageBoxButtons.OKCancel,
                                       MessageBoxIcon.Information,
                                       MessageBoxDefaultButton.Button2) = DialogResult.OK Then

                        Try
                            NativeFileIO.ShutdownSystem(NativeFileIO.ShutdownFlags.Reboot, NativeFileIO.ShutdownReasons.ReasonFlagPlanned)
                            Environment.Exit(0)

                        Catch ex2 As Exception
                            Trace.WriteLine(ex2.ToString())
                            MessageBox.Show(Me,
                                            "Reboot failed: " & ex2.JoinMessages(),
                                            "Arsenal Image Mounter",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Exclamation)

                        End Try

                    End If

                    Exit Do

                End If

                Dim rc =
                    MessageBox.Show(Me,
                                    "This application requires a virtual SCSI miniport driver to create virtual disks. The " &
                                    "necessary driver is either not currently installed or the currently installed driver is " &
                                    "incompatible with the current version of this application. Do you want to install the driver now?",
                                    "Arsenal Image Mounter",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Information,
                                    MessageBoxDefaultButton.Button2)

                If rc = DialogResult.No Then
                    Exit Do
                End If

                SetupRun = True

                If InstallDriver() Then
                    Continue Do
                Else
                    Exit Do
                End If

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Dim rc =
                    MessageBox.Show(Me,
                                   ex.JoinMessages(),
                                   ex.GetBaseException().GetType().Name,
                                   MessageBoxButtons.RetryCancel,
                                   MessageBoxIcon.Exclamation)

                If rc <> DialogResult.Retry Then
                    Exit Do
                End If

            End Try

        Loop

        If Adapter Is Nothing Then
            Application.Exit()
            Return
        End If

        MyBase.OnLoad(e)

        With New Thread(AddressOf DeviceListRefreshThread)
            .Start()
        End With

        'With New Thread(AddressOf LibEwfNotifyStreamReader)
        '    .Start()
        'End With

    End Sub

    Protected Overrides Sub OnClosing(e As CancelEventArgs)

        IsClosing = True

        Try
            Dim ServiceItems As ICollection(Of ServiceListItem)
            SyncLock ServiceList
                ServiceItems = ServiceList.ToArray()
            End SyncLock
            For Each Item In ServiceItems
                If Item?.Service?.HasDiskDevice Then
                    Trace.WriteLine($"Requesting service for device {Item.Service.DiskDeviceNumber:X6} to shut down...")
                    Item.Service.DismountAndStopServiceThread(TimeSpan.FromSeconds(10))
                Else
                    ServiceList.Remove(Item)
                End If
            Next

        Catch ex As Exception
            e.Cancel = True
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetBaseException().GetType().Name,
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

    Private Sub SetDiskView(list As List(Of DiskStateView), finished As Boolean)

        If finished Then
            With lblDeviceList
                .Text = "Device list"
                .ForeColor = SystemColors.ControlText
                .BackColor = SystemColors.Control
            End With
        End If

        For Each item In
            From view In list
            Join serviceItem In ServiceList
            On view.ScsiId Equals serviceItem.Service.DiskDeviceNumber.ToString("X6")

            item.view.DeviceProperties.Filename = item.serviceItem.ImageFile
        Next

        For Each prop In From item In list
                         Where item.DeviceProperties.Filename Is Nothing
                         Select item.DeviceProperties

            prop.Filename = "RAM disk"
        Next

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
                If _
                    MessageBox.Show(Me,
                                    "The new virtual disk was mounted in offline mode. Do you wish to bring the virtual disk online?",
                                    "Disk offline",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Exclamation) = DialogResult.Yes Then

                    Try
                        Update()

                        If obj.DevicePath.StartsWith("\\?\PhysicalDrive", StringComparison.Ordinal) Then
                            Using New AsyncMessageBox("Please wait...")
                                Using device As New DiskDevice(obj.DevicePath, FileAccess.ReadWrite)
                                    device.DiskPolicyOffline = False
                                End Using
                            End Using
                        End If

                        MessageBox.Show(Me,
                                        "The virtual disk was successfully brought online.",
                                        "Disk online",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Information)

                    Catch ex As Exception
                        Trace.WriteLine(ex.ToString())
                        MessageBox.Show(Me,
                                        $"An error occurred: {ex.JoinMessages()}",
                                        ex.GetBaseException().GetType().Name,
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Hand)

                    End Try

                    SetLabelBusy()

                    ThreadPool.QueueUserWorkItem(Sub() RefreshDeviceList())

                End If
            End If
        End If
    End Sub

    Private Sub DeviceListRefreshThread()
        Try

            Dim simpleviewtask = Task.Factory.StartNew(Function() DiskStateParser.GetSimpleView(Adapter, Adapter.EnumerateDevicesProperties()).ToList(), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default)

            'Dim fullviewtask = Task.Factory.StartNew(Function() parser.GetFullView(Adapter.ScsiPortNumber, devicelist.Result))

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

            Dim listFunction As Func(Of ScsiAdapter, IEnumerable(Of ScsiAdapter.DeviceProperties), IEnumerable(Of DiskStateView))

            'Try
            '    Dim fullview = fullviewtask.Result

            '    If IsClosing OrElse Disposing OrElse IsDisposed Then
            '        Return
            '    End If

            '    Invoke(Sub() SetDiskView(fullview, finished:=True))

            '    listFunction = AddressOf parser.GetFullView

            'Catch ex As Exception
            '    Trace.WriteLine("Full disk state view not supported on this platform: " & ex.ToString())

            listFunction = AddressOf DiskStateParser.GetSimpleView

            Invoke(Sub() SetDiskView(simpleview, finished:=True))

            'End Try

            Do

                DeviceListRefreshEvent.WaitOne()

                If IsClosing OrElse Disposing OrElse IsDisposed Then
                    Exit Do
                End If

                Invoke(New Action(AddressOf SetLabelBusy))

                Dim view = listFunction(Adapter, Adapter.EnumerateDevicesProperties()).ToList()

                If IsClosing OrElse Disposing OrElse IsDisposed Then
                    Return
                End If

                Invoke(Sub() SetDiskView(view, finished:=True))

            Loop

        Catch ex As Exception
            Trace.WriteLine($"Device list view thread caught exception: {ex}")
            LogMessage($"Device list view thread caught exception: {ex}")

            Dim action =
                Sub()
                    MessageBox.Show(Me,
                                    $"Exception while enumerating disk drives: {ex.JoinMessages()}",
                                    ex.GetBaseException().GetType().Name,
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error)

                    Application.Exit()
                End Sub

            Invoke(action)

        End Try

    End Sub

    Private Sub lbDevices_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lbDevices.SelectionChanged

        btnRemoveSelected.Enabled = lbDevices.SelectedRows.Count > 0
        btnShowOpened.Enabled = lbDevices.SelectedRows.Count > 0

    End Sub

    Private Sub btnRemoveAll_Click(sender As Object, e As EventArgs) Handles btnRemoveAll.Click

        Try
            Adapter.RemoveAllDevices()
            RefreshDeviceList()

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

    End Sub

    Private Sub btnShowOpened_Click(sender As Object, e As EventArgs) Handles btnShowOpened.Click

        Try
            For Each DeviceItem In lbDevices.
                SelectedRows().
                OfType(Of DataGridViewRow)().
                Select(Function(row) row.DataBoundItem).
                OfType(Of DiskStateView)()

                Dim item = Task.Factory.StartNew(
                    Function()

                        Dim pdo_path = API.EnumeratePhysicalDeviceObjectPaths(Adapter.DeviceInstance, DeviceItem.DeviceProperties.DeviceNumber).FirstOrDefault()
                        Dim dev_path = NativeFileIO.QueryDosDevice(NativeFileIO.GetPhysicalDriveNameForNtDevice(pdo_path)).FirstOrDefault()

                        Dim processes = NativeFileIO.EnumerateProcessesHoldingFileHandle(pdo_path, dev_path).Select(AddressOf NativeFileIO.FormatProcessName)

                        Dim processlist = String.Join(Environment.NewLine, processes)

                        Return processlist

                    End Function, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).
                    ContinueWith(
                    Sub(t)

                        MessageBox.Show(Me,
                            t.Result,
                            "Process list",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)

                    End Sub, TaskScheduler.FromCurrentSynchronizationContext())

                ThreadPool.QueueUserWorkItem(
                    Sub()
                        While Not item.Wait(TimeSpan.FromSeconds(2))

                            Dim time = NativeFileIO.LastObjectNameQuueryTime

                            If time = 0 OrElse NativeFileIO.SafeNativeMethods.GetTickCount64() - time < 4000 Then
                                Continue While
                            End If

                            Invoke(Sub()

                                       MessageBox.Show(Me,
                                                       $"Handle enumeration hung. Last checked object access was 0x{NativeFileIO.LastObjectNameQueryGrantedAccess:X}",
                                                       "Process list failed",
                                                       MessageBoxButtons.OK,
                                                       MessageBoxIcon.Error)

                                   End Sub)

                            Exit While

                        End While
                    End Sub)

            Next

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

    End Sub

    Private Sub btnRemoveSelected_Click(sender As Object, e As EventArgs) Handles btnRemoveSelected.Click

        Try
            For Each DeviceItem In lbDevices.
                SelectedRows().
                OfType(Of DataGridViewRow)().
                Select(Function(row) row.DataBoundItem).
                OfType(Of DiskStateView)()

                Adapter.RemoveDevice(DeviceItem.DeviceProperties.DeviceNumber)
            Next

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

        RefreshDeviceList()

    End Sub

    Private Sub AddServiceToShutdownHandler(ServiceItem As ServiceListItem)

        AddHandler ServiceItem.Service.ServiceShutdown,
          Sub()
              SyncLock ServiceList
                  ServiceList.RemoveAll(AddressOf ServiceItem.Equals)
              End SyncLock
              RefreshDeviceList()
          End Sub

        SyncLock ServiceList
            ServiceList.Add(ServiceItem)
        End SyncLock

    End Sub

    Private Sub btnMount_Click(sender As Object, e As EventArgs) Handles btnMountRaw.Click,
        btnMountLibEwf.Click,
        btnMountDiscUtils.Click,
        btnMountMultiPartRaw.Click,
        btnMountLibAFF4.Click

        Dim ProxyType As DevioServiceFactory.ProxyType

        If sender Is btnMountRaw Then
            ProxyType = DevioServiceFactory.ProxyType.None
        ElseIf sender Is btnMountMultiPartRaw Then
            ProxyType = DevioServiceFactory.ProxyType.MultiPartRaw
        ElseIf sender Is btnMountDiscUtils Then
            ProxyType = DevioServiceFactory.ProxyType.DiscUtils
        ElseIf sender Is btnMountLibEwf Then
            If Not LibewfVerify.VerifyLibewf(Me) Then
                Return
            End If
            ProxyType = DevioServiceFactory.ProxyType.LibEwf
        ElseIf sender Is btnMountLibAFF4 Then
            ProxyType = DevioServiceFactory.ProxyType.LibAFF4
        Else
            Return
        End If

        Dim Imagefile As String
        Dim Flags As DeviceFlags
        Using OpenFileDialog As New OpenFileDialog With {
          .CheckFileExists = True,
          .DereferenceLinks = True,
          .Multiselect = False,
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

            Imagefile = OpenFileDialog.FileName
        End Using

        Update()

        Try
            Dim SectorSize As UInteger
            Dim DiskAccess As DevioServiceFactory.VirtualDiskAccess

            Using FormMountOptions As New FormMountOptions

                With FormMountOptions

                    .SetSupportedAccessModes(DevioServiceFactory.GetSupportedVirtualDiskAccess(ProxyType, Imagefile)
)
                    If Flags.HasFlag(DeviceFlags.ReadOnly) Then
                        .SelectedReadOnly = True
                    Else
                        .SelectedReadOnly = False
                    End If

                    Using service = DevioServiceFactory.GetService(Imagefile, FileAccess.Read, ProxyType)
                        .SelectedSectorSize = service.SectorSize
                    End Using

                    If .ShowDialog(Me) <> DialogResult.OK Then
                        Return
                    End If

                    If .SelectedFakeSignature Then
                        Flags = Flags Or DeviceFlags.FakeDiskSignatureIfZero
                    End If

                    If .SelectedReadOnly Then
                        Flags = Flags Or DeviceFlags.ReadOnly
                    Else
                        Flags = Flags And Not DeviceFlags.ReadOnly
                    End If

                    If .SelectedRemovable Then
                        Flags = Flags Or DeviceFlags.Removable
                    End If

                    DiskAccess = .SelectedAccessMode

                    SectorSize = .SelectedSectorSize

                End With

            End Using

            Update()

            Using New AsyncMessageBox("Please wait...")

                Dim Service = DevioServiceFactory.GetService(Imagefile, DiskAccess, ProxyType)

                Service.SectorSize = SectorSize

                Service.StartServiceThreadAndMount(Adapter, Flags)

                Dim ServiceItem As New ServiceListItem With {
                    .ImageFile = Imagefile,
                    .Service = Service
                }

                AddServiceToShutdownHandler(ServiceItem)

                LastCreatedDevice = Service.DiskDeviceNumber

            End Using

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

        RefreshDeviceList()

    End Sub

    Private Sub btnRescanBus_Click(sender As Object, e As EventArgs) Handles btnRescanBus.Click

        Try
            Adapter.RescanScsiAdapter()
            Adapter.RescanBus()

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation)

        End Try

        Adapter.UpdateDiskProperties()

    End Sub

    Private Sub cbNotifyLibEwf_CheckedChanged(sender As Object, e As EventArgs) Handles cbNotifyLibEwf.CheckedChanged

        Try

            If cbNotifyLibEwf.Checked Then
                NativeFileIO.SafeNativeMethods.AllocConsole()
                If Not UsingDebugConsole Then
                    Trace.Listeners.Add(New ConsoleTraceListener With {.Name = "AIMConsoleTraceListener"})
                End If
            Else
                If Not UsingDebugConsole Then
                    Trace.Listeners.Remove("AIMConsoleTraceListener")
                    NativeFileIO.SafeNativeMethods.FreeConsole()
                End If
            End If

        Catch ex As Exception
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

        End Try

        Try

            If cbNotifyLibEwf.Checked Then
                Server.SpecializedProviders.DevioProviderLibEwf.SetNotificationFile("CONOUT$")
                Server.SpecializedProviders.DevioProviderLibEwf.NotificationVerbose = True
            Else
                Server.SpecializedProviders.DevioProviderLibEwf.NotificationVerbose = False
                Server.SpecializedProviders.DevioProviderLibEwf.SetNotificationFile(Nothing)
            End If

        Catch ex As Exception
            MessageBox.Show(Me,
                            ex.JoinMessages(),
                            ex.GetType().ToString(),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

        End Try

    End Sub

    Private Shared Function GetEmbeddedDriverVersion() As Version

        Using zipStream = GetType(MainForm).Assembly.GetManifestResourceStream(GetType(MainForm), "DriverFiles.zip")

            Return DriverSetup.GetDriverVersionFromZipStream(zipStream)

        End Using

    End Function

    Private Function InstallDriver() As Boolean

        Try
            Using msgbox As New AsyncMessageBox("Driver setup in progress")

                Using zipStream = GetType(MainForm).Assembly.GetManifestResourceStream(GetType(MainForm), "DriverFiles.zip")

                    DriverSetup.InstallFromZipStream(msgbox, zipStream)

                End Using

            End Using

        Catch ex As Exception
            Dim msg = ex.ToString()
            Trace.WriteLine($"Exception on driver install: {msg}")
            LogMessage(msg)

            MessageBox.Show(Me,
                            $"An error occurred while installing driver: {ex.JoinMessages()}",
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

    'Private Sub LibEwfNotifyStreamReader()

    '    Try

    '        Using reader As New StreamReader(Devio.Server.SpecializedProviders.DevioProviderLibEwf.OpenNotificationStream(), Encoding.ASCII)

    '            Do

    '                If IsClosing OrElse Disposing OrElse IsDisposed Then
    '                    Return
    '                End If

    '                Dim b = reader.ReadLine()
    '                If b Is Nothing Then
    '                    Return
    '                End If

    '                Trace.WriteLine(b)

    '            Loop

    '        End Using

    '    Catch ex As Exception
    '        Trace.WriteLine(ex.ToString())

    '        If IsClosing OrElse Disposing OrElse IsDisposed Then
    '            Return
    '        End If

    '        Invoke(Sub()
    '                   MessageBox.Show(Me,
    '                                   ex.GetBaseException().GetType().Name,
    '                                   "Error setting up notification stream for libewf.dll: " & ex.JoinMessages(),
    '                                   MessageBoxButtons.OK,
    '                                   MessageBoxIcon.Error)
    '               End Sub)

    '    End Try

    'End Sub

    Private Sub btnRAMDisk_Click(sender As Object, e As EventArgs) Handles btnRAMDisk.Click

        Try
            Dim ramdisk = DiscUtilsInteraction.InteractiveCreateRAMDisk(Adapter)

            If ramdisk Is Nothing Then
                Return
            End If

            AddServiceToShutdownHandler(New ServiceListItem With {.ImageFile = "RAM disk", .Service = ramdisk})

            RefreshDeviceList()

        Catch ex As Exception
            MessageBox.Show(Me, ex.JoinMessages(), "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation)

        End Try

    End Sub

End Class
