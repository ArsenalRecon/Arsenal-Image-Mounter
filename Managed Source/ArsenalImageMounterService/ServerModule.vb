
''''' ServerModule.vb
''''' Main module for PhysicalDiskMounterService application.
''''' 
''''' Copyright (c) 2012-2020, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Diagnostics.CodeAnalysis
Imports System.Globalization
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.Devio.Server.Interaction
Imports Arsenal.ImageMounter.Devio.Server.Services
Imports Arsenal.ImageMounter.Devio.Server.SpecializedProviders
Imports Arsenal.ImageMounter.IO

Public Module ServerModule

    Private Event RunToEnd As EventHandler

    Private ReadOnly architectureLibPath As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"i386", "x86"},
        {"AMD64", "x64"}
    }

    Private Function GetArchitectureLibPath() As String

        Dim architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")

        If String.IsNullOrWhiteSpace(architecture) Then
            Return String.Empty
        End If

        Dim path As String = Nothing

        If architectureLibPath.TryGetValue(architecture, path) Then
            Return path
        End If

        Return architecture

    End Function

    Private ReadOnly assemblyPaths As String() = {
        Path.Combine("lib", GetArchitectureLibPath()),
        "Lib",
        "DiskDriver"
    }

    Sub New()

        Dim appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)

        For i = 0 To assemblyPaths.Length - 1
            assemblyPaths(i) = Path.Combine(appPath, assemblyPaths(i))
        Next

        Dim native_dll_paths = assemblyPaths.Concat(Environment.GetEnvironmentVariable("PATH").Split(";"c))

        Environment.SetEnvironmentVariable("PATH", String.Join(";", native_dll_paths))

    End Sub

    <MTAThread>
    Public Function Main(ParamArray args As String()) As Integer

        Try
            Dim cmdline = $"{Environment.CommandLine} "

            Dim pos = cmdline.IndexOf(" /background ", StringComparison.OrdinalIgnoreCase)

            If pos >= 0 Then

                cmdline = $"{cmdline.Substring(0, pos)} /detach {cmdline.Substring(pos + " /background ".Length)}"

                Dim pstart As New ProcessStartInfo With {
                    .UseShellExecute = False
                }

                Dim arguments_pos = 0

                While arguments_pos < cmdline.Length AndAlso cmdline(arguments_pos) = " "c
                    arguments_pos += 1
                End While

                If arguments_pos < cmdline.Length AndAlso cmdline(arguments_pos) = """"c Then

                    Do
                        arguments_pos += 1
                    Loop While arguments_pos < cmdline.Length AndAlso cmdline(arguments_pos) <> """"c

                    arguments_pos += 1

                Else

                    While arguments_pos < cmdline.Length AndAlso cmdline(arguments_pos) <> " "c
                        arguments_pos += 1
                    End While

                End If

                If arguments_pos < cmdline.Length Then

                    pstart.FileName = cmdline.Substring(0, arguments_pos)

                    While arguments_pos < cmdline.Length AndAlso cmdline(arguments_pos) = " "c
                        arguments_pos += 1
                    End While

                    pstart.Arguments = cmdline.Substring(arguments_pos)

                End If

                Using process As New Process

                    process.StartInfo = pstart

                    process.Start()

                    Return process.Id

                End Using

            End If

            Return SafeMain(args)

        Catch ex As AbandonedMutexException
            Trace.WriteLine(ex.ToString())
            Console.ForegroundColor = ConsoleColor.Red
            Console.Error.WriteLine("Unexpected client exit.")
            Console.ResetColor()
            Return Marshal.GetHRForException(ex)

        Catch ex As Exception
            Trace.WriteLine(ex.ToString())
            Console.ForegroundColor = ConsoleColor.Red
            Console.Error.WriteLine(ex.JoinMessages(Environment.NewLine))
            Console.ResetColor()
            Return Marshal.GetHRForException(ex)

        Finally
            RaiseEvent RunToEnd(Nothing, EventArgs.Empty)

            If Debugger.IsAttached Then
                Console.ReadKey()
            End If

        End Try

    End Function

    Public Sub ShowVersionInfo()

        Dim asm_file = Assembly.GetExecutingAssembly().Location
        Dim file_ver = FileVersionInfo.GetVersionInfo(asm_file).FileVersion

        Dim driver_ver As String

        Try
            Using adapter As New ScsiAdapter
                driver_ver = $"Driver version: {adapter.GetDriverSubVersion()}"
            End Using

        Catch ex As Exception
            driver_ver = $"Error checking driver version: {ex.JoinMessages()}"

        End Try

        Console.WriteLine(
            $"Integrated command line interface to Arsenal Image Mounter virtual
SCSI miniport driver.

Application version {file_ver}

{driver_ver}
            
Copyright (C) 2012-2020 Arsenal Recon.

http://www.ArsenalRecon.com

Please see EULA.txt for license information.")

    End Sub

    <SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification:="<Pending>")>
    <SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification:="<Pending>")>
    Private Function SafeMain(args As IEnumerable(Of String)) As Integer

        Dim ImagePath As String = Nothing
        Dim WriteOverlayImageFile As String = Nothing
        Dim ObjectName As String = Nothing
        Dim ListenAddress As IPAddress = IPAddress.Any
        Dim ListenPort As Integer
        Dim BufferSize As Long = DevioShmService.DefaultBufferSize
        Dim DiskAccess As FileAccess = FileAccess.ReadWrite
        Dim Mount As Boolean = False
        Dim ProviderName As String = "DiscUtils"
        Dim ShowHelp As Boolean = False
        Dim Verbose As Boolean
        Dim DeviceFlags As DeviceFlags
        Dim DebugCompare As String = Nothing
        Dim libewfDebugOutput As String = "CONOUT$"
        Dim OutputImage As String = Nothing
        Dim OutputImageVariant As String = "dynamic"
        Dim Dismount As String = Nothing
        Dim ForceDismount As Boolean
        Dim BackgroundMode As Boolean

        For Each arg In args
            If arg.Equals("/trace", StringComparison.OrdinalIgnoreCase) Then
                Trace.Listeners.Add(New ConsoleTraceListener(True))
                Verbose = True
            ElseIf arg.StartsWith("/trace=", StringComparison.OrdinalIgnoreCase) Then
                Dim tracefile = New StreamWriter(arg.Substring("/trace=".Length), append:=True) With {.AutoFlush = True}
                Trace.Listeners.Add(New TextWriterTraceListener(tracefile))
                Verbose = True
            ElseIf arg.StartsWith("/name=", StringComparison.OrdinalIgnoreCase) Then
                ObjectName = arg.Substring("/name=".Length)
            ElseIf arg.StartsWith("/ipaddress=", StringComparison.OrdinalIgnoreCase) Then
                ListenAddress = IPAddress.Parse(arg.Substring("/ipaddress=".Length))
            ElseIf arg.StartsWith("/port=", StringComparison.OrdinalIgnoreCase) Then
                ListenPort = Integer.Parse(arg.Substring("/port=".Length))
            ElseIf arg.StartsWith("/buffersize=", StringComparison.OrdinalIgnoreCase) Then
                BufferSize = Long.Parse(arg.Substring("/buffersize=".Length))
            ElseIf arg.StartsWith("/device=", StringComparison.OrdinalIgnoreCase) Then
                ImagePath = arg.Substring("/device=".Length)
            ElseIf arg.StartsWith("/filename=", StringComparison.OrdinalIgnoreCase) Then
                ImagePath = arg.Substring("/filename=".Length)
            ElseIf arg.StartsWith("/provider=", StringComparison.OrdinalIgnoreCase) Then
                ProviderName = arg.Substring("/provider=".Length)
            ElseIf arg.Equals("/readonly", StringComparison.OrdinalIgnoreCase) Then
                DiskAccess = FileAccess.Read
            ElseIf arg.StartsWith("/writeoverlay=", StringComparison.OrdinalIgnoreCase) Then
                WriteOverlayImageFile = arg.Substring("/writeoverlay=".Length)
                DiskAccess = FileAccess.Read
                DeviceFlags = DeviceFlags Or DeviceFlags.ReadOnly Or DeviceFlags.WriteOverlay
            ElseIf arg.Equals("/mount", StringComparison.OrdinalIgnoreCase) Then
                Mount = True
            ElseIf arg.Equals("/mount:removable", StringComparison.OrdinalIgnoreCase) Then
                Mount = True
                DeviceFlags = DeviceFlags Or DeviceFlags.Removable
            ElseIf arg.Equals("/mount:cdrom", StringComparison.OrdinalIgnoreCase) Then
                Mount = True
                DeviceFlags = DeviceFlags Or DeviceFlags.DeviceTypeCD
            ElseIf arg.StartsWith("/convert=", StringComparison.OrdinalIgnoreCase) Then
                OutputImage = arg.Substring("/convert=".Length)
                DiskAccess = FileAccess.Read
            ElseIf arg.StartsWith("/saveas=", StringComparison.OrdinalIgnoreCase) Then
                OutputImage = arg.Substring("/saveas=".Length)
                DiskAccess = FileAccess.Read
            ElseIf arg.StartsWith("/variant=", StringComparison.OrdinalIgnoreCase) Then
                OutputImageVariant = arg.Substring("/variant=".Length)
            ElseIf arg.StartsWith("/libewfoutput=", StringComparison.OrdinalIgnoreCase) Then
                libewfDebugOutput = arg.Substring("/libewfoutput=".Length)
            ElseIf arg.StartsWith("/debugcompare=", StringComparison.OrdinalIgnoreCase) Then
                DebugCompare = arg.Substring("/debugcompare=".Length)
            ElseIf arg.Equals("/dismount", StringComparison.OrdinalIgnoreCase) Then
                Dismount = "*"
            ElseIf arg.StartsWith("/dismount=", StringComparison.OrdinalIgnoreCase) Then
                Dismount = arg.Substring("/dismount=".Length)
            ElseIf arg.Equals("/force", StringComparison.OrdinalIgnoreCase) Then
                ForceDismount = True
            ElseIf arg.Equals("/detach", StringComparison.OrdinalIgnoreCase) Then
                BackgroundMode = True
            ElseIf arg = "/?" OrElse arg.Equals("/help", StringComparison.OrdinalIgnoreCase) Then
                ShowHelp = True
                Exit For
            ElseIf arg.Equals("/version", StringComparison.OrdinalIgnoreCase) Then
                ShowVersionInfo()
                Return 0
            ElseIf arg.Equals("/list", StringComparison.OrdinalIgnoreCase) Then
                ListDevices()
                Return 0
            Else
                Console.WriteLine($"Unsupported switch: {arg}")
                ShowHelp = True
                Exit For
            End If
        Next

        If _
            ShowHelp OrElse
            (String.IsNullOrWhiteSpace(ImagePath) AndAlso String.IsNullOrWhiteSpace(Dismount)) Then

            Dim asmname = Assembly.GetExecutingAssembly().GetName().Name

            Dim providers = String.Join("|", DevioServiceFactory.InstalledProvidersByNameAndFileAccess.Keys)

            Dim msg = "aim_cli.

Arsenal Image Mounter CLI (AIM CLI) - an integrated command line interface to the Arsenal 
Image Mounter virtual SCSI miniport driver.

Before using AIM CLI, please see readme_cli.txt and ""Arsenal Recon - End User License Agreement.txt"" for detailed usage and license information.

Please note: AIM CLI should be run with administrative privileges. If you would like to use AIM CLI to interact with EnCase (E01 and Ex01) or AFF4 forensic disk images, you must make the Libewf (libewf.dll) and LibAFF4 (libaff4.dll) libraries available in the expected (/lib/x64) or same folder as aim_cli.exe. AIM CLI mounts disk images in write-temporary mode by default, to maintain compatibility with a large number of scripts in which users have replaced other solutions with AIM CLI.

Syntax to mount a raw/forensic/virtual machine disk image as a ""real"" disk:
aim_cli.exe /mount[:removable|:cdrom] [/buffersize=bytes] [/readonly] /filename=imagefilename /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None [/writeoverlay=differencingimagefile] [/background]

Syntax to start shared memory service mode, for mounting from other applications:
aim_cli.exe /name=objectname [/buffersize=bytes] [/readonly] /filename=imagefilename /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None [/background]

Syntax to start TCP/IP service mode, for mounting from other computers:
aim_cli.exe [/ipaddress=listenaddress] /port=tcpport [/readonly] /filename=imagefilename /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None [/background]

Syntax to convert a disk image without mounting:
aim_cli.exe /filename=imagefilename /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None /convert=outputimagefilename [/variant=fixed|dynamic] [/background]

Syntax to save a new disk image after mounting:
aim_cli.exe /device=devicenumber /saveas=outputimagefilename [/variant=fixed|dynamic] [/background]

Syntax to dismount a mounted device:
aim_cli.exe /dismount[=devicenumber] [/force]

"
            msg = LineFormat(msg, 4)

            Console.WriteLine(msg)

            Return 1

        End If

        If Not String.IsNullOrWhiteSpace(Dismount) Then

            Dim devicenumber As UInteger

            If Dismount.Equals("*", StringComparison.Ordinal) Then

                devicenumber = ScsiAdapter.AutoDeviceNumber

            ElseIf Not UInteger.TryParse(Dismount, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, devicenumber) Then

                Console.WriteLine($"Invalid device number: {Dismount}

Expected hexadecimal SCSI address in the form PPTTLL, for example: 000100")

                Return 1
            End If

            Try
                Using adapter As New ScsiAdapter

                    If devicenumber = ScsiAdapter.AutoDeviceNumber Then

                        If adapter.GetDeviceList().Length = 0 Then

                            Console.ForegroundColor = ConsoleColor.Red
                            Console.Error.WriteLine("No mounted devices.")
                            Console.ResetColor()
                            Return 2

                        End If

                        If ForceDismount Then

                            adapter.RemoveAllDevices()

                        Else

                            adapter.RemoveAllDevicesSafe()

                        End If

                        Console.WriteLine("All devices dismounted.")

                    Else

                        If Not adapter.GetDeviceList().Contains(devicenumber) Then

                            Console.ForegroundColor = ConsoleColor.Red
                            Console.Error.WriteLine($"No device mounted with device number {devicenumber:X6}.")
                            Console.ResetColor()
                            Return 2

                        End If

                        If ForceDismount Then

                            adapter.RemoveDevice(devicenumber)

                        Else

                            adapter.RemoveDeviceSafe(devicenumber)

                        End If

                        Console.WriteLine("Devices dismounted.")

                    End If

                End Using

            Catch ex As Exception
                Trace.WriteLine(ex.ToString())
                Console.ForegroundColor = ConsoleColor.Red
                Console.Error.WriteLine(ex.JoinMessages())
                Console.ResetColor()

                Return 2

            End Try

        End If

        If String.IsNullOrWhiteSpace(ImagePath) Then

            Return 0

        End If

        Console.WriteLine($"Opening image file '{ImagePath}'...")

        If StringComparer.OrdinalIgnoreCase.Equals(ProviderName, "libewf") Then

            DevioProviderLibEwf.SetNotificationFile(libewfDebugOutput)

            AddHandler RunToEnd, Sub() DevioProviderLibEwf.SetNotificationFile(Nothing)

            If Verbose Then
                DevioProviderLibEwf.NotificationVerbose = True
            End If

        End If

        Dim Provider = DevioServiceFactory.GetProvider(ImagePath, DiskAccess, ProviderName)

        If Not String.IsNullOrWhiteSpace(DebugCompare) Then

            Dim DebugCompareStream = File.OpenRead(DebugCompare)

            Provider = New DebugProvider(Provider, DebugCompareStream)

        End If

        Dim Service As DevioServiceBase

        If Not String.IsNullOrWhiteSpace(ObjectName) Then

            Service = New DevioShmService(ObjectName, Provider, OwnsProvider:=True, BufferSize:=BufferSize)

        ElseIf ListenPort <> 0 Then

            Service = New DevioTcpService(ListenAddress, ListenPort, Provider, OwnsProvider:=True)

        ElseIf Mount Then

            Service = New DevioShmService(Provider, OwnsProvider:=True, BufferSize:=BufferSize)

        ElseIf OutputImage IsNot Nothing Then

            Provider.ConvertToImage(OutputImage, OutputImageVariant, BackgroundMode)

            Return 0

        Else

            Provider.Dispose()

            Console.WriteLine("None of /name, /port, /mount or /convert switches specified, nothing to do.")

            Return 1

        End If

        If Mount Then
            Console.WriteLine("Mounting as virtual disk...")

            Service.WriteOverlayImageName = WriteOverlayImageFile

            Dim adapter As ScsiAdapter

            Try
                adapter = New ScsiAdapter

            Catch ex As Exception
                Throw New IOException("Cannot access Arsenal Image Mounter driver. Check that the driver is installed and that you are running this application with administrative privileges.")

            End Try

            Service.StartServiceThreadAndMount(adapter, DeviceFlags)

            Using device = Service.OpenDiskDevice(0)
                Console.WriteLine($"Virtual disk is {device.DevicePath} with SCSI address {device.ScsiAddress}")
            End Using

        Else

            Service.StartServiceThread()

        End If

        If BackgroundMode Then

            If Mount Then

                Console.WriteLine($"Virtual disk created. To dismount, type aim_cli /dismount={Service.DiskDeviceNumber:X6}")

            Else

                Console.WriteLine("Image file opened, ready for incoming connections.")

            End If

            NativeFileIO.SafeNativeMethods.FreeConsole()

        Else

            If Mount Then

                Console.WriteLine("Virtual disk created. Press Ctrl+C to remove virtual disk and exit.")

            Else

                Console.WriteLine("Image file opened, waiting for incoming connections. Press Ctrl+C to exit.")

            End If

            AddHandler Console.CancelKeyPress,
                Sub(sender, e)
                    Try
                        Console.WriteLine("Stopping service...")
                        Service.Dispose()

                        e.Cancel = True

                    Catch ex As Exception
                        Trace.WriteLine(ex.ToString())
                        Console.ForegroundColor = ConsoleColor.Red
                        Console.Error.WriteLine(ex.JoinMessages())
                        Console.ResetColor()

                    End Try
                End Sub

        End If

        Service.WaitForServiceThreadExit()

        Console.WriteLine("Service stopped.")

        If Service.Exception IsNot Nothing Then
            Throw New Exception("Service failed.", Service.Exception)
        End If

        Return 0

    End Function

    <Extension>
    Private Sub ConvertToImage(provider As IDevioProvider, outputImage As String, OutputImageVariant As String, background As Boolean)

        Using provider

            Using cancel As New CancellationTokenSource

                Console.WriteLine($"Converting to new image file '{outputImage}'...")

                If background Then

                    NativeFileIO.SafeNativeMethods.FreeConsole()

                Else

                    AddHandler Console.CancelKeyPress,
                        Sub(sender, e)
                            Try
                                Console.WriteLine("Stopping...")
                                cancel.Cancel()

                                e.Cancel = True

                            Catch ex As Exception
                                Trace.WriteLine(ex.ToString())
                                Console.WriteLine(ex.JoinMessages())

                            End Try
                        End Sub

                End If

                Dim image_type = Path.GetExtension(outputImage).TrimStart("."c).ToUpperInvariant()

                Select Case image_type

                    Case "DD", "RAW", "IMG", "IMA", "ISO", "BIN", "001"
                        provider.ConvertToRawImage(outputImage, OutputImageVariant, cancel.Token)

                    Case Else
                        provider.ConvertToDiscUtilsImage(outputImage, image_type, OutputImageVariant, cancel.Token)

                End Select

                Console.WriteLine($"Image converted successfully.")

            End Using

        End Using

    End Sub

    Public Sub ListDevices()

        Dim adapters = API.GetAdapterDeviceInstanceNames()

        If adapters Is Nothing Then

            Console.WriteLine("Driver not installed.")

            Return

        End If

        For Each devinstNameAdapter In adapters

            Dim devinstAdapter = NativeFileIO.GetDevInst(devinstNameAdapter)

            If Not devinstAdapter.HasValue Then
                Continue For
            End If

            Console.WriteLine($"Adapter {devinstNameAdapter}")

            For Each dev In
                From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter.Value)
                Let path = NativeFileIO.GetPhysicalDeviceObjectName(devinstChild)
                Where Not String.IsNullOrWhiteSpace(path)
                Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
                Let physical_drive_path = NativeFileIO.GetPhysicalDrivePathForNtDevice(path)

                Console.WriteLine($"SCSI address {dev.address} found at {dev.path} devinst {dev.devinstChild} ({dev.physical_drive_path}).")

            Next

        Next

    End Sub

End Module
