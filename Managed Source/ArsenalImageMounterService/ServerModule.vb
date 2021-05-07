
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
Imports Microsoft.Win32.SafeHandles

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

                Using ready_wait As New ManualResetEvent(initialState:=False)

                    NativeFileIO.SetInheritable(ready_wait.SafeWaitHandle, inheritable:=True)

                    cmdline = $"{cmdline.Substring(0, pos)} /detach={ready_wait.SafeWaitHandle.DangerousGetHandle()} {cmdline.Substring(pos + " /background ".Length)}"

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

                        Using process_wait = NativeFileIO.CreateWaitHandle(process.Handle, inheritable:=False)

                            WaitHandle.WaitAny({process_wait, ready_wait})

                        End Using

                        If process.HasExited Then
                            Return 0
                        Else
                            Return process.Id
                        End If

                    End Using

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

    Private Function SafeMain(args As IEnumerable(Of String)) As Integer

        Dim image_path As String = Nothing
        Dim write_overlay_image_file As String = Nothing
        Dim ObjectName As String = Nothing
        Dim listen_address As IPAddress = IPAddress.Any
        Dim listen_port As Integer
        Dim buffer_size As Long = DevioShmService.DefaultBufferSize
        Dim disk_access As FileAccess = FileAccess.ReadWrite
        Dim mount As Boolean = False
        Dim provider_name As String = "DiscUtils"
        Dim show_help As Boolean = False
        Dim verbose As Boolean
        Dim device_flags As DeviceFlags
        Dim debug_compare As String = Nothing
        Dim libewf_debug_output As String = "CONOUT$"
        Dim output_image As String = Nothing
        Dim output_image_variant As String = "dynamic"
        Dim dismount As String = Nothing
        Dim force_dismount As Boolean
        Dim detach_event As SafeWaitHandle = Nothing
        Dim fake_mbr As Boolean

        For Each arg In args
            If arg.Equals("/trace", StringComparison.OrdinalIgnoreCase) Then
                Trace.Listeners.Add(New ConsoleTraceListener(True))
                verbose = True
                'ElseIf arg.StartsWith("/filever=", StringComparison.OrdinalIgnoreCase) Then
                '    Dim fileverpath = arg.Substring("/filever=".Length)
                '    Console.WriteLine(NativeFileIO.GetFileVersion(fileverpath).ToString())
            ElseIf arg.StartsWith("/trace=", StringComparison.OrdinalIgnoreCase) Then
                Dim tracefile = New StreamWriter(arg.Substring("/trace=".Length), append:=True) With {.AutoFlush = True}
                Trace.Listeners.Add(New TextWriterTraceListener(tracefile))
                verbose = True
            ElseIf arg.StartsWith("/name=", StringComparison.OrdinalIgnoreCase) Then
                ObjectName = arg.Substring("/name=".Length)
            ElseIf arg.StartsWith("/ipaddress=", StringComparison.OrdinalIgnoreCase) Then
                listen_address = IPAddress.Parse(arg.Substring("/ipaddress=".Length))
            ElseIf arg.StartsWith("/port=", StringComparison.OrdinalIgnoreCase) Then
                listen_port = Integer.Parse(arg.Substring("/port=".Length))
            ElseIf arg.StartsWith("/buffersize=", StringComparison.OrdinalIgnoreCase) Then
                buffer_size = Long.Parse(arg.Substring("/buffersize=".Length))
            ElseIf arg.StartsWith("/device=", StringComparison.OrdinalIgnoreCase) Then
                image_path = arg.Substring("/device=".Length)
            ElseIf arg.StartsWith("/filename=", StringComparison.OrdinalIgnoreCase) Then
                image_path = arg.Substring("/filename=".Length)
            ElseIf arg.StartsWith("/provider=", StringComparison.OrdinalIgnoreCase) Then
                provider_name = arg.Substring("/provider=".Length)
            ElseIf arg.Equals("/readonly", StringComparison.OrdinalIgnoreCase) Then
                disk_access = FileAccess.Read
            ElseIf arg.Equals("/fakesig", StringComparison.OrdinalIgnoreCase) Then
                device_flags = device_flags Or DeviceFlags.FakeDiskSignatureIfZero
            ElseIf arg.Equals("/fakembr", StringComparison.OrdinalIgnoreCase) Then
                fake_mbr = True
            ElseIf arg.StartsWith("/writeoverlay=", StringComparison.OrdinalIgnoreCase) Then
                write_overlay_image_file = arg.Substring("/writeoverlay=".Length)
                disk_access = FileAccess.Read
                device_flags = device_flags Or DeviceFlags.ReadOnly Or DeviceFlags.WriteOverlay
            ElseIf arg.Equals("/mount", StringComparison.OrdinalIgnoreCase) Then
                mount = True
            ElseIf arg.Equals("/mount:removable", StringComparison.OrdinalIgnoreCase) Then
                mount = True
                device_flags = device_flags Or DeviceFlags.Removable
            ElseIf arg.Equals("/mount:cdrom", StringComparison.OrdinalIgnoreCase) Then
                mount = True
                device_flags = device_flags Or DeviceFlags.DeviceTypeCD
            ElseIf arg.StartsWith("/convert=", StringComparison.OrdinalIgnoreCase) Then
                output_image = arg.Substring("/convert=".Length)
                disk_access = FileAccess.Read
            ElseIf arg.StartsWith("/saveas=", StringComparison.OrdinalIgnoreCase) Then
                output_image = arg.Substring("/saveas=".Length)
                disk_access = FileAccess.Read
            ElseIf arg.StartsWith("/variant=", StringComparison.OrdinalIgnoreCase) Then
                output_image_variant = arg.Substring("/variant=".Length)
            ElseIf arg.StartsWith("/libewfoutput=", StringComparison.OrdinalIgnoreCase) Then
                libewf_debug_output = arg.Substring("/libewfoutput=".Length)
            ElseIf arg.StartsWith("/debugcompare=", StringComparison.OrdinalIgnoreCase) Then
                debug_compare = arg.Substring("/debugcompare=".Length)
            ElseIf arg.Equals("/dismount", StringComparison.OrdinalIgnoreCase) Then
                dismount = "*"
            ElseIf arg.StartsWith("/dismount=", StringComparison.OrdinalIgnoreCase) Then
                dismount = arg.Substring("/dismount=".Length)
            ElseIf arg.Equals("/force", StringComparison.OrdinalIgnoreCase) Then
                force_dismount = True
            ElseIf arg.StartsWith("/detach=", StringComparison.OrdinalIgnoreCase) Then
                detach_event = New SafeWaitHandle(New IntPtr(Integer.Parse(arg.Substring("/detach=".Length), NumberFormatInfo.InvariantInfo)), ownsHandle:=True)
            ElseIf arg = "/?" OrElse arg.Equals("/help", StringComparison.OrdinalIgnoreCase) Then
                show_help = True
                Exit For
            ElseIf arg.Equals("/version", StringComparison.OrdinalIgnoreCase) Then
                ShowVersionInfo()
                Return 0
            ElseIf arg.Equals("/list", StringComparison.OrdinalIgnoreCase) Then
                ListDevices()
                Return 0
            Else
                Console.WriteLine($"Unsupported switch: {arg}")
                show_help = True
                Exit For
            End If
        Next

        If _
            show_help OrElse
            (String.IsNullOrWhiteSpace(image_path) AndAlso String.IsNullOrWhiteSpace(dismount)) Then

            Dim asmname = Assembly.GetExecutingAssembly().GetName().Name

            Dim providers = String.Join("|", DevioServiceFactory.InstalledProvidersByNameAndFileAccess.Keys)

            Dim msg = "aim_cli.

Arsenal Image Mounter CLI (AIM CLI) - an integrated command line interface to the Arsenal 
Image Mounter virtual SCSI miniport driver.

Before using AIM CLI, please see readme_cli.txt and ""Arsenal Recon - End User License Agreement.txt"" for detailed usage and license information.

Please note: AIM CLI should be run with administrative privileges. If you would like to use AIM CLI to interact with EnCase (E01 and Ex01) or AFF4 forensic disk images, you must make the Libewf (libewf.dll) and LibAFF4 (libaff4.dll) libraries available in the expected (/lib/x64) or same folder as aim_cli.exe. AIM CLI mounts disk images in write-original mode by default, to maintain compatibility with a large number of scripts in which users have replaced other solutions with AIM CLI.

Syntax to mount a raw/forensic/virtual machine disk image as a ""real"" disk:
aim_cli.exe /mount[:removable|:cdrom] [/buffersize=bytes] [/readonly] [/fakesig] [/fakembr] /filename=imagefilename /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None [/writeoverlay=differencingimagefile] [/background]

Syntax to start shared memory service mode, for mounting from other applications:
aim_cli.exe /name=objectname [/buffersize=bytes] [/readonly] [/fakembr] /filename=imagefilename /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None [/background]

Syntax to start TCP/IP service mode, for mounting from other computers:
aim_cli.exe [/ipaddress=listenaddress] /port=tcpport [/readonly] [/fakembr] /filename=imagefilename /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None [/background]

Syntax to convert a disk image without mounting:
aim_cli.exe /filename=imagefilename [/fakembr] /provider=DiscUtils|LibEWF|LibAFF4|MultipartRaw|None /convert=outputimagefilename [/variant=fixed|dynamic] [/background]

Syntax to save a new disk image after mounting:
aim_cli.exe /device=devicenumber /saveas=outputimagefilename [/variant=fixed|dynamic] [/background]

Syntax to dismount a mounted device:
aim_cli.exe /dismount[=devicenumber] [/force]

"

            msg = LineFormat(msg, 4)

            Console.WriteLine(msg)

            Return 1

        End If

        If Not String.IsNullOrWhiteSpace(dismount) Then

            Dim devicenumber As UInteger

            If dismount.Equals("*", StringComparison.Ordinal) Then

                devicenumber = ScsiAdapter.AutoDeviceNumber

            ElseIf Not UInteger.TryParse(dismount, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, devicenumber) Then

                Console.WriteLine($"Invalid device number: {dismount}

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

                        If force_dismount Then

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

                        If force_dismount Then

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

        If String.IsNullOrWhiteSpace(image_path) Then

            Return 0

        End If

        Console.WriteLine($"Opening image file '{image_path}'...")

        If StringComparer.OrdinalIgnoreCase.Equals(provider_name, "libewf") Then

            DevioProviderLibEwf.SetNotificationFile(libewf_debug_output)

            AddHandler RunToEnd, Sub() DevioProviderLibEwf.SetNotificationFile(Nothing)

            If verbose Then
                DevioProviderLibEwf.NotificationVerbose = True
            End If

        End If

        Dim provider = DevioServiceFactory.GetProvider(image_path, disk_access, provider_name)

        If Not String.IsNullOrWhiteSpace(debug_compare) Then

            Dim DebugCompareStream = File.OpenRead(debug_compare)

            provider = New DebugProvider(provider, DebugCompareStream)

        End If

        If fake_mbr Then

            provider = New DevioProviderWithFakeMBR(provider)

        End If

        Dim service As DevioServiceBase

        If Not String.IsNullOrWhiteSpace(ObjectName) Then

            service = New DevioShmService(ObjectName, provider, OwnsProvider:=True, BufferSize:=buffer_size)

        ElseIf listen_port <> 0 Then

            service = New DevioTcpService(listen_address, listen_port, provider, OwnsProvider:=True)

        ElseIf mount Then

            service = New DevioShmService(provider, OwnsProvider:=True, BufferSize:=buffer_size)

        ElseIf output_image IsNot Nothing Then

            provider.ConvertToImage(output_image, output_image_variant, detach_event)

            Return 0

        Else

            provider.Dispose()

            Console.WriteLine("None of /name, /port, /mount or /convert switches specified, nothing to do.")

            Return 1

        End If

        If mount Then
            Console.WriteLine("Mounting as virtual disk...")

            service.WriteOverlayImageName = write_overlay_image_file

            Dim adapter As ScsiAdapter

            Try
                adapter = New ScsiAdapter

            Catch ex As Exception
                Throw New IOException("Cannot access Arsenal Image Mounter driver. Check that the driver is installed and that you are running this application with administrative privileges.")

            End Try

            service.StartServiceThreadAndMount(adapter, device_flags)

            Try
                Dim device_name = $"\\?\{service.GetDiskDeviceName()}"

                Console.WriteLine($"Device number {service.DiskDeviceNumber:X6}")
                Console.WriteLine($"Device is {device_name}")
                Console.WriteLine()

                For Each vol In NativeFileIO.EnumerateDiskVolumes(device_name)

                    Console.WriteLine($"Contains volume {vol}")

                    For Each mnt In NativeFileIO.EnumerateVolumeMountPoints(vol)

                        Console.WriteLine($"  Mounted at {mnt}")

                    Next

                Next

            Catch ex As Exception
                Console.WriteLine($"Error displaying volume mount points: {ex.JoinMessages()}")

            End Try

        Else

            service.StartServiceThread()

        End If

        If detach_event IsNot Nothing Then

            If mount Then

                Console.WriteLine($"Virtual disk created. To dismount, type aim_cli /dismount={service.DiskDeviceNumber:X6}")

            Else

                Console.WriteLine("Image file opened, ready for incoming connections.")

            End If

            CloseConsole(detach_event)

        Else

            If mount Then

                Console.WriteLine("Virtual disk created. Press Ctrl+C to remove virtual disk and exit.")

            Else

                Console.WriteLine("Image file opened, waiting for incoming connections. Press Ctrl+C to exit.")

            End If

            AddHandler Console.CancelKeyPress,
                Sub(sender, e)
                    Try
                        Console.WriteLine("Stopping service...")
                        service.Dispose()

                        e.Cancel = True

                    Catch ex As Exception
                        Trace.WriteLine(ex.ToString())
                        Console.ForegroundColor = ConsoleColor.Red
                        Console.Error.WriteLine(ex.JoinMessages())
                        Console.ResetColor()

                    End Try
                End Sub

        End If

        service.WaitForServiceThreadExit()

        Console.WriteLine("Service stopped.")

        If service.Exception IsNot Nothing Then
            Throw New Exception("Service failed.", service.Exception)
        End If

        Return 0

    End Function

    <Extension>
    Private Sub ConvertToImage(provider As IDevioProvider, outputImage As String, OutputImageVariant As String, detachEvent As SafeWaitHandle)

        Using provider

            Using cancel As New CancellationTokenSource

                Console.WriteLine($"Converting to new image file '{outputImage}'...")

                If detachEvent IsNot Nothing Then

                    CloseConsole(detachEvent)

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

        Dim adapters = API.EnumerateAdapterDeviceInstanceNames()

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

    Private Sub CloseConsole(DetachEvent As SafeWaitHandle)

        Using DetachEvent
            NativeFileIO.SetEvent(DetachEvent)
        End Using

        Console.SetIn(TextReader.Null)
        Console.SetOut(TextWriter.Null)
        Console.SetError(TextWriter.Null)

        NativeFileIO.SafeNativeMethods.FreeConsole()

    End Sub

End Module
