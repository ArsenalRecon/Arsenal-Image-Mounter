Imports System.ServiceProcess
Imports Arsenal.ImageMounter.IO
Imports System.Windows.Forms
Imports Arsenal.ImageMounter.Extensions
Imports Microsoft.Win32
Imports System.IO
Imports System.Threading
Imports System.Text
Imports System.ComponentModel
#If NETFRAMEWORK AndAlso Not NET45_OR_GREATER Then
Imports Ionic.Zip
#Else
Imports System.IO.Compression
#End If

''' <summary>
''' Routines for installing or uninstalling Arsenal Image Mounter kernel level
''' modules.
''' </summary>
Public NotInheritable Class DriverSetup

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property OSVersion As Version = NativeFileIO.GetOSVersion().Version

    Public Shared ReadOnly Property Kernel As String = GetKernelName()

    Public Shared ReadOnly Property HasStorPort As Boolean

    Private Shared Function GetKernelName() As String

        If _OSVersion >= New Version(10, 0) Then
            _Kernel = "Win10"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 3) Then
            _Kernel = "Win8.1"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 2) Then
            _Kernel = "Win8"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 1) Then
            _Kernel = "Win7"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 0) Then
            _Kernel = "WinLH"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(5, 2) Then
            _Kernel = "WinNET"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(5, 1) Then
            _Kernel = "WinXP"
            _HasStorPort = False
        ElseIf _OSVersion >= New Version(5, 0) Then
            _Kernel = "Win2K"
            _HasStorPort = False
        Else
            Throw New NotSupportedException($"Unsupported Windows version ('{_OSVersion}')")
        End If

        Return _Kernel

    End Function

#If NETFRAMEWORK AndAlso Not NET45_OR_GREATER Then
    ''' <summary>
    ''' Returns version of driver located inside a setup zip archive.
    ''' </summary>
    ''' <param name="zipFile">ZipFile object with setup files</param>
    Public Shared Function GetDriverVersionFromZipArchive(zipFile As ZipFile) As Version

        Dim infpath1 = $"{_Kernel}\x86\phdskmnt.sys"
        Dim infpath2 = $"{_Kernel}/x86/phdskmnt.sys"

        Dim entry =
            Aggregate e In zipFile
            Into FirstOrDefault(
                e.FileName.Equals(
                    infpath1, StringComparison.OrdinalIgnoreCase) OrElse
                e.FileName.Equals(
                    infpath2, StringComparison.OrdinalIgnoreCase))

        If entry Is Nothing Then

            Throw New KeyNotFoundException($"Driver file phdskmnt.sys for {_Kernel} missing in zip archive.")

        End If

        Using versionFile = entry.OpenReader()

            Return NativeFileIO.GetFileVersion(versionFile)

        End Using

    End Function
#Else
    ''' <summary>
    ''' Returns version of driver located inside a setup zip archive.
    ''' </summary>
    ''' <param name="zipFile">ZipFile object with setup files</param>
    Public Shared Function GetDriverVersionFromZipArchive(zipFile As ZipArchive) As Version

        Dim infpath1 = $"{_Kernel}\x86\phdskmnt.sys"
        Dim infpath2 = $"{_Kernel}/x86/phdskmnt.sys"

        Dim entry =
            Aggregate e In zipFile.Entries
            Into FirstOrDefault(
                e.FullName.Equals(
                    infpath1, StringComparison.OrdinalIgnoreCase) OrElse
                e.FullName.Equals(
                    infpath2, StringComparison.OrdinalIgnoreCase))

        If entry Is Nothing Then

            Throw New KeyNotFoundException($"Driver file phdskmnt.sys for {_Kernel} missing in zip archive.")

        End If

        Using versionFile = entry.Open()

            Return NativeFileIO.GetFileVersion(versionFile)

        End Using

    End Function
#End If

    ''' <summary>
    ''' Returns version of driver located in setup files directory.
    ''' </summary>
    ''' <param name="setupRoot">Root directory of setup files.</param>
    Public Shared Function GetSetupFileDriverVersion(setupRoot As String) As Version

        Dim versionFile = Path.Combine(setupRoot, _Kernel, "phdskmnt.inf")

        Return GetSetupFileDriverVersion(New CachedIniFile(versionFile, Encoding.ASCII))

    End Function

    ''' <summary>
    ''' Returns version of driver located in setup files directory.
    ''' </summary>
    ''' <param name="infFile">.inf file used to identify version of driver.</param>
    Public Shared Function GetSetupFileDriverVersion(infFile As CachedIniFile) As Version

        Return Version.Parse(infFile!Version!DriverVer.Split(","c)(1))

    End Function

#If NETFRAMEWORK AndAlso Not NET45_OR_GREATER Then
    ''' <summary>
    ''' Installs Arsenal Image Mounter driver components from a zip archive.
    ''' This routine automatically selects the correct driver version for
    ''' current version of Windows.
    ''' </summary>
    ''' <param name="ownerWindow">This needs to be a valid handle to a Win32
    ''' window that will be parent to dialog boxes etc shown by setup API. In
    ''' console Applications, you could call
    ''' NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    ''' console window.</param>
    ''' <param name="zipFile">An Ionic.Zip.ZipFile opened for reading that
    ''' contains setup source files. Directory layout in zip file needs to be
    ''' like in DriverSetup.zip found in DriverSetup directory in repository,
    ''' that is, one subdirectory for each kernel version followed by one
    ''' subdirectory for each architecture.</param>
    Public Shared Sub InstallFromZipArchive(ownerWindow As IWin32Window, zipFile As ZipFile)

        Dim origdir = Environment.CurrentDirectory

        Dim temppath = Path.Combine(Path.GetTempPath(), "ArsenalImageMounter-DriverSetup")

        Trace.WriteLine($"Using temp path: {temppath}")

        Directory.CreateDirectory(temppath)

        zipFile.ExtractAll(temppath, ExtractExistingFileAction.OverwriteSilently)

        Install(ownerWindow, temppath)

        Environment.CurrentDirectory = origdir

        If _HasStorPort Then

            Dim directoryRemover =
                Sub()

                    Try
                        Dim start = Stopwatch.StartNew()

                        While Directory.Exists(temppath)

                            Try
                                Directory.Delete(temppath, recursive:=True)

                            Catch ex As IOException When start.Elapsed.TotalMinutes < 15
                                Trace.WriteLine($"I/O Error removing temporary directory: {ex.JoinMessages()}")
                                Thread.Sleep(TimeSpan.FromSeconds(10))

                            End Try

                        End While

                    Catch ex As Exception
                        Trace.WriteLine($"Error removing temporary directory: {ex.JoinMessages()}")

                    End Try

                End Sub

            With New Thread(directoryRemover)
                .Start()
            End With

        End If

    End Sub
#Else
    ''' <summary>
    ''' Installs Arsenal Image Mounter driver components from a zip archive.
    ''' This routine automatically selects the correct driver version for
    ''' current version of Windows.
    ''' </summary>
    ''' <param name="ownerWindow">This needs to be a valid handle to a Win32
    ''' window that will be parent to dialog boxes etc shown by setup API. In
    ''' console Applications, you could call
    ''' NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    ''' console window.</param>
    ''' <param name="zipFile">An System.IO.Compression.ZipArchive opened for reading that
    ''' contains setup source files. Directory layout in zip file needs to be
    ''' like in DriverSetup.zip found in DriverSetup directory in repository,
    ''' that is, one subdirectory for each kernel version followed by one
    ''' subdirectory for each architecture.</param>
    Public Shared Sub InstallFromZipArchive(ownerWindow As IWin32Window, zipFile As ZipArchive)

        Dim origdir = Environment.CurrentDirectory

        Dim temppath = Path.Combine(Path.GetTempPath(), "ArsenalImageMounter-DriverSetup")

        Trace.WriteLine($"Using temp path: {temppath}")

        If Directory.Exists(temppath) Then
            Directory.Delete(temppath, recursive:=True)
        End If

        Directory.CreateDirectory(temppath)

        zipFile.ExtractToDirectory(temppath)

        Install(ownerWindow, temppath)

        Environment.CurrentDirectory = origdir

        If _HasStorPort Then

            Dim directoryRemover =
                Sub()

                    Try
                        Dim start = Stopwatch.StartNew()

                        While Directory.Exists(temppath)

                            Try
                                Directory.Delete(temppath, recursive:=True)

                            Catch ex As IOException When start.Elapsed.TotalMinutes < 15
                                Trace.WriteLine($"I/O Error removing temporary directory: {ex.JoinMessages()}")
                                Thread.Sleep(TimeSpan.FromSeconds(10))

                            End Try

                        End While

                    Catch ex As Exception
                        Trace.WriteLine($"Error removing temporary directory: {ex.JoinMessages()}")

                    End Try

                End Sub

            With New Thread(directoryRemover)
                .Start()
            End With

        End If

    End Sub
#End If

    ''' <summary>
    ''' Returns version of driver located inside a setup zip archive.
    ''' </summary>
    ''' <param name="zipStream">Stream containing a zip archive with setup files</param>
    Public Shared Function GetDriverVersionFromZipStream(zipStream As Stream) As Version

#If NETFRAMEWORK AndAlso Not NET45_OR_GREATER Then
        Return GetDriverVersionFromZipArchive(ZipFile.Read(zipStream))
#Else
        Return GetDriverVersionFromZipArchive(New ZipArchive(zipStream, ZipArchiveMode.Read))
#End If

    End Function

    ''' <summary>
    ''' Installs Arsenal Image Mounter driver components from a zip archive.
    ''' This routine automatically selects the correct driver version for
    ''' current version of Windows.
    ''' </summary>
    ''' <param name="ownerWindow">This needs to be a valid handle to a Win32
    ''' window that will be parent to dialog boxes etc shown by setup API. In
    ''' console Applications, you could call
    ''' NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    ''' console window.</param>
    ''' <param name="zipStream">A stream opened for reading a zip file
    ''' containing setup source files. Directory layout in zip file needs to be
    ''' like in DriverSetup.zip found in DriverSetup directory in repository,
    ''' that is, one subdirectory for each kernel version followed by one
    ''' subdirectory for each architecture.</param>
    Public Shared Sub InstallFromZipStream(ownerWindow As IWin32Window, zipStream As Stream)

#If NETFRAMEWORK AndAlso Not NET45_OR_GREATER Then
        InstallFromZipArchive(ownerWindow, ZipFile.Read(zipStream))
#Else
        InstallFromZipArchive(ownerWindow, New ZipArchive(zipStream, ZipArchiveMode.Read))
#End If

    End Sub

    ''' <summary>
    ''' Installs Arsenal Image Mounter driver components from specified source
    ''' path. This routine automatically selects the correct driver version for
    ''' current version of Windows.
    ''' </summary>
    ''' <param name="ownerWindow">This needs to be a valid handle to a Win32
    ''' window that will be parent to dialog boxes etc shown by setup API. In
    ''' console Applications, you could call
    ''' NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    ''' console window.</param>
    ''' <param name="setupsource">Directory with setup files. Directory layout
    ''' at this path needs to be like in DriverSetup.7z found in DriverSetup
    ''' directory in repository, that is, one subdirectory for each kernel
    ''' version followed by one subdirectory for each architecture.</param>
    Public Shared Sub Install(ownerWindow As IWin32Window, setupsource As String)

        CheckCompatibility(ownerWindow)

        If _HasStorPort Then

            InstallStorPortDriver(ownerWindow, setupsource)

        Else

            InstallScsiPortDriver(ownerWindow, setupsource)

        End If

        StartInstalledServices()

    End Sub

    Private Shared Sub StartInstalledServices()

        For Each service_name In {"vhdaccess", "awealloc", "dokan1"}

            Try
                Using service As New ServiceController(service_name)

                    Select Case service.Status
                        Case ServiceControllerStatus.Stopped, ServiceControllerStatus.Paused
                            service.Start()
                            service.WaitForStatus(ServiceControllerStatus.Running)
                            Trace.WriteLine($"Successfully loaded driver '{service_name}'")

                        Case ServiceControllerStatus.Running
                            Trace.WriteLine($"Driver '{service_name}' is already loaded.")

                        Case Else
                            Trace.WriteLine($"Driver '{service_name}' is '{service.Status}' and cannot be loaded at this time.")

                    End Select

                End Using

            Catch ex As Exception
                Trace.WriteLine($"Warning: Failed to open service controller for driver '{service_name}'. Driver not loaded.")

            End Try

        Next

    End Sub

    ''' <summary>
    ''' Removes Arsenal Image Mounter device objects and driver components.
    ''' </summary>
    Public Shared Sub Uninstall(ownerWindow As IWin32Window)

        CheckCompatibility(ownerWindow)

        RemoveDevices(ownerWindow)

        RemoveDriver()

    End Sub

    ''' <summary>
    ''' Installs Arsenal Image Mounter driver components from specified source
    ''' path. This routine installs the StorPort version of the driver, for use
    ''' on Windows Server 2003 or later.
    ''' </summary>
    ''' <param name="ownerWindow">This needs to be a valid handle to a Win32
    ''' window that will be parent to dialog boxes etc shown by setup API. In
    ''' console Applications, you could call
    ''' NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    ''' console window.</param>
    ''' <param name="setupsource">Directory with setup files. Directory layout
    ''' at this path needs to be like in DriverSetup.7z found in DriverSetup
    ''' directory in repository, that is, one subdirectory for each kernel
    ''' version followed by one subdirectory for each architecture.</param>
    Friend Shared Sub InstallStorPortDriver(ownerWindow As IWin32Window, setupsource As String)

        '' First, check if device nodes already exist.
        Try
            RemoveDevices(ownerWindow)

        Catch ex As Exception
            Trace.WriteLine($"Error removing existing device nodes. This will be ignored and driver update operation will continue anyway. Exception: {ex.JoinMessages()}")

        End Try

        Dim reboot_required = False

        '' Create device node and install driver
        Dim infPath = Path.Combine(setupsource, _Kernel, "phdskmnt.inf")

        NativeFileIO.CreateRootPnPDevice(ownerWindow.Handle, infPath, "root\phdskmnt", ForceReplaceExistingDrivers:=False, reboot_required)

        If Not reboot_required Then

            Dim pending_renames = TryCast(Registry.GetValue("HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations", Nothing), String())

            If pending_renames IsNot Nothing Then

                pending_renames = Aggregate p In pending_renames
                                  Where p IsNot Nothing AndAlso p.Length > 4
                                  Select p = p.Substring(4)
                                  Where File.Exists(p)
                                  Select If(FileVersionInfo.GetVersionInfo(p)?.OriginalFilename, Path.GetFileName(p))
                                  Into ToArray()

                Trace.WriteLine($"Pending file replace operations: '{String.Join("', '", pending_renames)}'")

                Dim installed_driver_files = CachedIniFile.EnumerateFileSectionValuePairs(infPath, "PhysicalDiskMounterDevice.Services")

                Dim pending_install_file = Aggregate item In installed_driver_files
                    Where "AddService".Equals(item.Key, StringComparison.OrdinalIgnoreCase)
                    Select installfile = $"{item.Value.Split(","c)(0)}.sys"
                    Into FirstOrDefault(pending_renames.Contains(installfile, StringComparer.OrdinalIgnoreCase))

                If pending_install_file IsNot Nothing Then
                    Trace.WriteLine($"Detected pending file replace operation for '{pending_install_file}'. Requesting reboot.")
                    reboot_required = True
                End If

            End If

        End If

        If reboot_required AndAlso
            MessageBox.Show(ownerWindow,
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
                MessageBox.Show(ownerWindow,
                                $"Reboot failed: {ex2.JoinMessages()}",
                                "Arsenal Image Mounter",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error)

            End Try

        End If

    End Sub

    ''' <summary>
    ''' Removes all plug-and-play device objects owned by Arsenal Image Mounter
    ''' driver, in preparation for uninstalling the driver at later time.
    ''' </summary>
    ''' <param name="ownerWindow">This needs to be a valid handle to a Win32
    ''' window that will be parent to dialog boxes etc shown by setup API. In
    ''' console Applications, you could call
    ''' NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    ''' console window.</param>
    Friend Shared Sub RemoveDevices(ownerWindow As IWin32Window)

        Dim hwinstances As IEnumerable(Of String) = Nothing

        If NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt", hwinstances) <> 0 Then
            Return
        End If

        For Each hwinstance In hwinstances
            NativeFileIO.RemovePnPDevice(ownerWindow.Handle, hwinstance)
        Next

    End Sub

    ''' <summary>
    ''' Installs Arsenal Image Mounter driver components from specified source
    ''' path. This routine installs the ScsiPort version of the driver, for use
    ''' on Windows XP or earlier.
    ''' </summary>
    ''' <param name="ownerWindow">This needs to be a valid handle to a Win32
    ''' window that will be parent to dialog boxes etc shown by setup API. In
    ''' console Applications, you could call
    ''' NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    ''' console window.</param>
    ''' <param name="setupsource">Directory with setup files. Directory layout
    ''' at this path needs to be like in DriverSetup.7z found in DriverSetup
    ''' directory in repository, that is, one subdirectory for each kernel
    ''' version followed by one subdirectory for each architecture.</param>
    Friend Shared Sub InstallScsiPortDriver(ownerWindow As IWin32Window, setupsource As String)

        ''
        '' Install null device .inf for control unit
        ''
        Dim CtlUnitInfPath = Path.Combine(setupsource, "CtlUnit", "ctlunit.inf")

        CtlUnitInfPath = NativeFileIO.SetupCopyOEMInf(CtlUnitInfPath, NoOverwrite:=False)
        Trace.WriteLine($"Pre-installed controller inf: '{CtlUnitInfPath}'")

        Directory.SetCurrentDirectory(setupsource)

        NativeFileIO.UnsafeNativeMethods.SetupSetNonInteractiveMode(False)

        Dim infPath = Path.Combine(".", _Kernel, "phdskmnt.inf")

        NativeFileIO.RunDLLInstallHinfSection(ownerWindow.Handle, infPath, "DefaultInstall")

        Using scm As New ServiceController("phdskmnt")
            While scm.Status <> ServiceControllerStatus.Running
                scm.Start()
                scm.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(3))
            End While
        End Using

        Dim thread As New Thread(AddressOf NativeFileIO.ScanForHardwareChanges)
        thread.Start()
        thread.Join()

    End Sub

    ''' <summary>
    ''' Removes Arsenal Image Mounter driver components.
    ''' </summary>
    Friend Shared Sub RemoveDriver()

        Using scm = NativeFileIO.UnsafeNativeMethods.OpenSCManager(Nothing, Nothing, NativeFileIO.NativeConstants.SC_MANAGER_ALL_ACCESS)

            If scm.IsInvalid Then
                Throw New Win32Exception("OpenSCManager")
            End If

            For Each svcname In {"phdskmnt", "aimwrfltr"}

                Using svc = NativeFileIO.UnsafeNativeMethods.OpenService(scm, svcname, NativeFileIO.NativeConstants.SC_MANAGER_ALL_ACCESS)

                    If svc.IsInvalid Then
                        Throw New Win32Exception("OpenService")
                    End If

                    NativeFileIO.UnsafeNativeMethods.DeleteService(svc)

                End Using

            Next

        End Using

        For Each svcname In {"phdskmnt", "aimwrfltr"}

            Dim driverSysFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System, Environment.SpecialFolderOption.DoNotVerify), $"drivers\{svcname}.sys")

            If File.Exists(driverSysFile) Then
                File.Delete(driverSysFile)
            End If

        Next

    End Sub

    Private Shared Sub CheckCompatibility(ownerWindow As IWin32Window)

        Dim native_arch = NativeFileIO.SystemArchitecture
        Dim process_arch = NativeFileIO.ProcessArchitecture

        If Not ReferenceEquals(native_arch, process_arch) Then

            Dim msg = $"WARNING: Driver setup is starting in a {process_arch} process on {native_arch} OS. Setup may fail!"

            Trace.WriteLine(msg)

            If ownerWindow IsNot Nothing Then

                MessageBox.Show(ownerWindow,
                                "This is a 32 bit process running on a 64 bit version of Windows. " &
                                "There are known problems with installing drivers in this case. If " &
                                "driver setup fails, please retry from a 64 bit application!",
                                "Compatibility warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)

            End If

        End If

    End Sub

End Class
