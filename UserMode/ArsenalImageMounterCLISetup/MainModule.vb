
''''' MainModule.vb
''''' Console driver setup application, for scripting and similar.
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Module MainModule

    Private ReadOnly kernel As String
    Private ReadOnly ver As Version = Environment.OSVersion.Version
    Private ReadOnly hasStorPort As Boolean
    Private ReadOnly setupsource As String = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

    Sub New()

        Trace.Listeners.Add(New ConsoleTraceListener)

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

    End Sub

    Sub Main(args As String())

        Dim opMode As OpMode = opMode.Status

        If args IsNot Nothing AndAlso args.Length > 0 Then
            If args(0).Equals("/install", StringComparison.InvariantCultureIgnoreCase) Then
                opMode = opMode.Install
            ElseIf args(0).Equals("/uninstall", StringComparison.InvariantCultureIgnoreCase) Then
                opMode = opMode.Uninstall
            ElseIf args(0).Equals("/status", StringComparison.InvariantCultureIgnoreCase) Then
                opMode = opMode.Status
            Else
                Trace.WriteLine("Syntax: PDMSetup /install|/uninstall|/status")
            End If
        End If

        Trace.WriteLine("Kernel type: " & kernel)
        Trace.WriteLine("Kernel supports StorPort: " & hasStorPort)
        Trace.WriteLine("Setup source path: " & setupsource)

        Try
            Dim rc = SetupOperation(opMode)

            Environment.Exit(rc)

        Catch ex As Exception
            If TypeOf ex Is Win32Exception Then
                Trace.WriteLine("Win32 error: " & DirectCast(ex, Win32Exception).NativeErrorCode)
            End If
            Trace.WriteLine(ex.ToString())
            Environment.Exit(-1)

        End Try

    End Sub

    Function SetupOperation(opMode As OpMode) As Integer

        Select Case opMode

            Case opMode.Install
                If AdapterDevicePresent Then
                    Trace.WriteLine("Already installed.")
                    Return 1
                Else
                    Install()
                    Trace.WriteLine("Driver successfully installed.")
                    Return 0
                End If

            Case opMode.Uninstall
                If AdapterDevicePresent Then
                    Uninstall()
                    Trace.WriteLine("Driver successfully uninstalled.")
                    Return 0
                Else
                    Trace.WriteLine("Virtual SCSI adapter not installed.")
                    Return 1
                End If

            Case opMode.Status
                If AdapterDevicePresent Then
                    Trace.WriteLine("Virtual SCSI adapter installed.")
                    Return 0
                Else
                    Trace.WriteLine("Virtual SCSI adapter not installed.")
                    Return 1
                End If

            Case Else
                Return -1

        End Select

    End Function

    Public ReadOnly Property AdapterDevicePresent As Boolean
        Get
            Dim devInsts = API.GetAdapterDeviceInstances()
            If devInsts Is Nothing OrElse devInsts.Count = 0 Then
                Return False
            Else
                Return True
            End If
        End Get
    End Property

    Public Sub Install()

        If hasStorPort Then

            InstallStorPortDriver()

        Else

            InstallScsiPortDriver()

        End If

    End Sub

    Public Sub InstallStorPortDriver()

        Dim infPath = Path.Combine(setupsource, kernel, "phdskmnt.inf")

        NativeFileIO.CreateRootPnPDevice(NativeFileIO.Win32API.GetConsoleWindow(), infPath, "root\phdskmnt")

    End Sub

    Public Sub RemoveDevices()

        Dim hwinstances As String() = Nothing

        NativeFileIO.GetDeviceInstancesForService("phdskmnt", hwinstances)

        For Each hwinstance In From hwinst In hwinstances Where Not String.IsNullOrEmpty(hwinst)
            NativeFileIO.RemovePnPDevice(NativeFileIO.Win32API.GetConsoleWindow(), hwinstance, removeDriverPackage:=False)
        Next

    End Sub

    Public Sub InstallScsiPortDriver()

        ''
        '' Install null device .inf for control unit
        ''
        Dim CtlUnitInfPath = Path.Combine(setupsource, "CtlUnit", "ctlunit.inf")

        NativeFileIO.SetupCopyOEMInf(CtlUnitInfPath, NoOverwrite:=False)

        Directory.SetCurrentDirectory(setupsource)

        NativeFileIO.Win32API.SetupSetNonInteractiveMode(False)

        Dim infPath = Path.Combine(".", kernel, "phdskmnt.inf")

        NativeFileIO.RunDLLInstallHinfSection(NativeFileIO.Win32API.GetConsoleWindow(), infPath, "DefaultInstall")

        Using scm As New ServiceController("phdskmnt")
            While scm.Status <> ServiceControllerStatus.Running
                scm.Start()
                scm.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(3))
            End While
        End Using

        NativeFileIO.ScanForHardwareChanges()

    End Sub

    Public Sub RemoveDriver()

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

    Public Sub Uninstall()

        If hasStorPort Then
            RemoveDevices()
        End If

        RemoveDriver()

    End Sub

End Module

Enum OpMode
    Install
    Uninstall
    Status
End Enum
