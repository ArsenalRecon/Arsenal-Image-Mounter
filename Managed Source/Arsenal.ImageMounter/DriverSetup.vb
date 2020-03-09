Imports System.ServiceProcess
Imports Ionic.Zip
Imports Arsenal.ImageMounter.IO
Imports System.Windows.Forms
Imports Ionic.Crc

''' <summary>
''' Routines for installing or uninstalling Arsenal Image Mounter kernel level
''' modules.
''' </summary>
Public Class DriverSetup

    Public Shared ReadOnly kernel As String
    Public Shared ReadOnly hasStorPort As Boolean

    Shared Sub New()

        Dim os_version = NativeFileIO.GetOSVersion().Version

        If os_version >= New Version(10, 0) Then
            kernel = "Win10"
            hasStorPort = True
        ElseIf os_version >= New Version(6, 3) Then
            kernel = "Win8.1"
            hasStorPort = True
        ElseIf os_version >= New Version(6, 2) Then
            kernel = "Win8"
            hasStorPort = True
        ElseIf os_version >= New Version(6, 1) Then
            kernel = "Win7"
            hasStorPort = True
        ElseIf os_version >= New Version(6, 0) Then
            kernel = "WinLH"
            hasStorPort = True
        ElseIf os_version >= New Version(5, 2) Then
            kernel = "WinNET"
            hasStorPort = True
        ElseIf os_version >= New Version(5, 1) Then
            kernel = "WinXP"
            hasStorPort = False
        ElseIf os_version >= New Version(5, 0) Then
            kernel = "Win2K"
            hasStorPort = False
        Else
            Throw New NotSupportedException(
                "Unsupported Windows version ('" &
                os_version.ToString() & "')")
        End If

    End Sub

    ''' <summary>
    ''' Returns version of driver located inside a setup zip archive.
    ''' </summary>
    ''' <param name="zipFile">ZipFile object with setup files</param>
    Public Shared Function GetArchiveDriverVersion(zipFile As ZipFile) As Version

        Dim infpath1 = $"{kernel}\x86\phdskmnt.sys"
        Dim infpath2 = $"{kernel}/x86/phdskmnt.sys"

        Dim entry =
            Aggregate e In zipFile
            Into FirstOrDefault(
                e.FileName.Equals(
                    infpath1, StringComparison.OrdinalIgnoreCase) OrElse
                e.FileName.Equals(
                    infpath2, StringComparison.OrdinalIgnoreCase))

        If entry Is Nothing Then

            Throw New KeyNotFoundException($"Driver file phdskmnt.sys for {kernel} missing in zip archive.")

        End If

        Using versionFile = entry.OpenReader()

            Return NativeFileIO.GetFileVersionInfo(versionFile)

        End Using

    End Function

    ''' <summary>
    ''' Returns version of driver located in setup files directory.
    ''' </summary>
    ''' <param name="setupRoot">Root directory of setup files.</param>
    Public Shared Function GetSetupFileDriverVersion(setupRoot As String) As Version

        Dim versionFile = Path.Combine(setupRoot, kernel, "phdskmnt.inf")

        Return GetSetupFileDriverVersion(New CachedIniFile(versionFile, Encoding.ASCII))

    End Function

    ''' <summary>
    ''' Returns version of driver located in setup files directory.
    ''' </summary>
    ''' <param name="infFile">.inf file used to identify version of driver.</param>
    Public Shared Function GetSetupFileDriverVersion(infFile As CachedIniFile) As Version

        Return Version.Parse(infFile!Version!DriverVer.Split(","c)(1))

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
    ''' <param name="zipFile">An Ionic.Zip.ZipFile opened for reading that
    ''' contains setup source files. Directory layout in zip file needs to be
    ''' like in DriverSetup.zip found in DriverSetup directory in repository,
    ''' that is, one subdirectory for each kernel version followed by one
    ''' subdirectory for each architecture.</param>
    Public Shared Sub InstallFromZipFile(ownerWindow As IntPtr, zipFile As ZipFile)

        Dim origdir = Environment.CurrentDirectory

        Dim temppath = Path.Combine(Path.GetTempPath(), "ArsenalImageMounter-DriverSetup")

        Trace.WriteLine("Using temp path: " & temppath)

        Directory.CreateDirectory(temppath)

        zipFile.ExtractAll(temppath, ExtractExistingFileAction.OverwriteSilently)

        Install(ownerWindow, temppath)

        Environment.CurrentDirectory = origdir

        If hasStorPort Then

            Dim directoryRemover =
                Sub()

                    Dim start = Stopwatch.StartNew()

                    While Directory.Exists(temppath)

                        Try
                            Directory.Delete(temppath, recursive:=True)

                        Catch ex As IOException When start.Elapsed.TotalMinutes < 15
                            Trace.WriteLine($"I/O Error removing temporary directory: {ex.ToString()}")
                            Thread.Sleep(TimeSpan.FromSeconds(10))
                            Continue While

                        Catch ex As Exception
                            Trace.WriteLine($"Error removing temporary directory: {ex.ToString()}")

                        End Try

                    End While

                End Sub

            With New Thread(directoryRemover)
                .Start()
            End With

        End If

    End Sub

    ''' <summary>
    ''' Returns version of driver located inside a setup zip archive.
    ''' </summary>
    ''' <param name="zipStream">Stream containing a zip archive with setup files</param>
    Public Shared Function GetArchiveDriverVersion(zipStream As Stream) As Version

        Return GetArchiveDriverVersion(ZipFile.Read(zipStream))

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
    Public Shared Sub InstallFromZipFile(ownerWindow As IntPtr, zipStream As Stream)

        InstallFromZipFile(ownerWindow, ZipFile.Read(zipStream))

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
    Public Shared Sub Install(ownerWindow As IntPtr, setupsource As String)

        CheckCompatibility(ownerWindow)

        If hasStorPort Then

            InstallStorPortDriver(ownerWindow, setupsource)

        Else

            InstallScsiPortDriver(ownerWindow, setupsource)

        End If

    End Sub

    ''' <summary>
    ''' Removes Arsenal Image Mounter device objects and driver components.
    ''' </summary>
    Public Shared Sub Uninstall(ownerWindow As IntPtr)

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
    Protected Shared Sub InstallStorPortDriver(ownerWindow As IntPtr, setupsource As String)

        '' First, check if device nodes already exist.
        Try
            RemoveDevices(ownerWindow)

        Catch ex As Exception
            Trace.WriteLine($"Error removing existing device nodes. This will be ignored and driver update operation will continue anyway. Exception: {ex.ToString()}")

        End Try

        '' Create device node and install driver
        Dim infPath = Path.Combine(setupsource, kernel, "phdskmnt.inf")
        NativeFileIO.CreateRootPnPDevice(ownerWindow, infPath, "root\phdskmnt")

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
    Protected Shared Sub RemoveDevices(ownerWindow As IntPtr)

        Dim hwinstances As String() = Nothing

        NativeFileIO.GetDeviceInstancesForService("phdskmnt", hwinstances)

        For Each hwinstance In hwinstances
            NativeFileIO.RemovePnPDevice(ownerWindow, hwinstance)
        Next

    End Sub

    ''' <summary>
    ''' Installs Arsenal Image Mounter driver components from specified source
    ''' path. This routine installs the ScsiPort version of the driver, for use
    ''' on Windwos XP or earlier.
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
    Protected Shared Sub InstallScsiPortDriver(ownerWindow As IntPtr, setupsource As String)

        ''
        '' Install null device .inf for control unit
        ''
        Dim CtlUnitInfPath = Path.Combine(setupsource, "CtlUnit", "ctlunit.inf")

        CtlUnitInfPath = NativeFileIO.SetupCopyOEMInf(CtlUnitInfPath, NoOverwrite:=False)
        Trace.WriteLine("Pre-installed controller inf: '" & CtlUnitInfPath & "'")

        Directory.SetCurrentDirectory(setupsource)

        NativeFileIO.Win32API.SetupSetNonInteractiveMode(False)

        Dim infPath = Path.Combine(".", kernel, "phdskmnt.inf")

        NativeFileIO.RunDLLInstallHinfSection(ownerWindow, infPath, "DefaultInstall")

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
    Protected Shared Sub RemoveDriver()

        Using scm = NativeFileIO.Win32API.OpenSCManager(Nothing, Nothing, NativeFileIO.Win32API.SC_MANAGER_ALL_ACCESS)

            If scm.IsInvalid Then
                Throw New Win32Exception("OpenSCManager")
            End If

            Using svc = NativeFileIO.Win32API.OpenService(scm, "phdskmnt", NativeFileIO.Win32API.SC_MANAGER_ALL_ACCESS)

                If svc.IsInvalid Then
                    Throw New Win32Exception("OpenService")
                End If

                NativeFileIO.Win32API.DeleteService(svc)

            End Using

        End Using

        Dim driverSysFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System, Environment.SpecialFolderOption.DoNotVerify), "drivers\phdskmnt.sys")

        If File.Exists(driverSysFile) Then
            File.Delete(driverSysFile)
        End If

    End Sub

    Private Shared Sub CheckCompatibility(ownerWindow As IntPtr)

        If Environment.Is64BitOperatingSystem AndAlso
            Not Environment.Is64BitProcess Then

            Trace.WriteLine("WARNING: Driver setup is starting in a 32 bit process on 64 bit OS. Setup may fail!")

            If ownerWindow <> IntPtr.Zero Then

                MessageBox.Show(NativeWindow.FromHandle(ownerWindow),
                                "This is a 32 bit process running on a 64 bit version of Windows. " &
                                "There are known problems with installing drivers in this case. If " &
                                "driver setup fails, please retry from a 64 bit application!",
                                "Compatibility warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)

            End If

        End If

    End Sub

End Class
