
''''' ServerModule.vb
''''' Main module for PhysicalDiskMounterService application.
''''' 
''''' Copyright (c) 2012-2019, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Reflection
Imports Arsenal.ImageMounter.Devio.Server.Interaction
Imports Arsenal.ImageMounter.Devio.Server.SpecializedProviders
Imports Arsenal.ImageMounter.Devio.Server.Services
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders
Imports Arsenal.ImageMounter.IO
Imports DiscUtils

Module ServerModule

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

    Private ReadOnly assemblyPaths As New List(Of String)(3) From {
        Path.Combine("lib", GetArchitectureLibPath()),
        "Lib",
        "DiskDriver"
    }

    Sub New()

        Dim appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)

        For i = 0 To assemblyPaths.Count - 1
            assemblyPaths(i) = Path.Combine(appPath, assemblyPaths(i))
        Next

        Dim native_dll_paths = assemblyPaths.Concat(Environment.GetEnvironmentVariable("PATH").Split(";"c))

        Environment.SetEnvironmentVariable("PATH", String.Join(";", native_dll_paths))

    End Sub

    <MTAThread()>
    Public Function Main(ParamArray args As String()) As Integer

        Try
            Return SafeMain(args)

        Catch ex As AbandonedMutexException
            Console.ForegroundColor = ConsoleColor.Red
            Console.Error.WriteLine("Unexpected client exit.")
            Console.ResetColor()
            Trace.WriteLine(ex.ToString())
            Return -1

        Catch ex As Exception
            Console.ForegroundColor = ConsoleColor.Red
            Console.Error.WriteLine(ex.JoinMessages(Environment.NewLine))
            Console.ResetColor()
            Trace.WriteLine(ex.ToString())
            Return -1

        Finally
            RaiseEvent RunToEnd(Nothing, EventArgs.Empty)

            If Debugger.IsAttached Then
                Console.ReadKey()
            End If

        End Try

    End Function

    Public Sub ShowVersionInfo()

        Dim asm_file = Assembly.GetExecutingAssembly().Location
        Dim file_ver = FileVersionInfo.GetVersionInfo(asm_file)

        Console.WriteLine(
            "Integrated command line interface to Arsenal Image Mounter virtual" & Environment.NewLine &
            "SCSI miniport driver." & Environment.NewLine &
            Environment.NewLine &
            "Version " & file_ver.FileVersion.ToString() & Environment.NewLine &
            Environment.NewLine &
            "Copyright (C) 2012-2019 Arsenal Recon." & Environment.NewLine &
            Environment.NewLine &
            Environment.NewLine &
            "http://www.ArsenalRecon.com" & Environment.NewLine &
            Environment.NewLine &
            "Arsenal Image Mounter including its kernel driver, API library," & Environment.NewLine &
            "command line and graphical user applications (""the Software"")" & Environment.NewLine &
            "are provided ""AS Is"" and ""WITH ALL FAULTS,"" without warranty" & Environment.NewLine &
            "of any kind, including without limitation the warranties of" & Environment.NewLine &
            "merchantability, fitness for a particular purpose and" & Environment.NewLine &
            "non - infringement.Arsenal makes no warranty that the Software" & Environment.NewLine &
            "is free of defects or is suitable for any particular purpose." & Environment.NewLine &
            "In no event shall Arsenal be responsible for loss or damages" & Environment.NewLine &
            "arising from the installation or use of the Software, including" & Environment.NewLine &
            "but not limited to any indirect, punitive, special, incidental" & Environment.NewLine &
            "or consequential damages of any character including, without" & Environment.NewLine &
            "limitation, damages for loss of goodwill, work stoppage," & Environment.NewLine &
            "computer failure or malfunction, or any and all other" & Environment.NewLine &
            "commercial damages or losses.The entire risk as to the" & Environment.NewLine &
            "quality and performance of the Software is borne by you.Should" & Environment.NewLine &
            "the Software prove defective, you and not Arsenal assume the" & Environment.NewLine &
            "entire cost of any service and repair." & Environment.NewLine &
            Environment.NewLine &
            "Arsenal Consulting, Inc. (d/b/a Arsenal Recon) retains the copyright to the" & Environment.NewLine &
            "Arsenal Image Mounter source code being made available under terms of the" & Environment.NewLine &
            "Affero General Public License v3." & Environment.NewLine &
            "(http://www.fsf.org/licensing/licenses/agpl-3.0.html). This source code may" & Environment.NewLine &
            "be used in projects that are licensed so as to be compatible with AGPL v3." & Environment.NewLine &
            Environment.NewLine &
            "Contributors to Arsenal Image Mounter must sign the Arsenal Contributor" & Environment.NewLine &
            "Agreement(""ACA"").The ACA gives Arsenal and the contributor joint" & Environment.NewLine &
            "copyright interests in the code." & Environment.NewLine &
            Environment.NewLine &
            "If your project is not licensed under an AGPL v3 compatible license," & Environment.NewLine &
            "contact us directly regarding alternative licensing.")


    End Sub

    Private Function SafeMain(ParamArray args As String()) As Integer

        Dim DeviceName As String = Nothing
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
            ElseIf arg.StartsWith("/filename=", StringComparison.OrdinalIgnoreCase) Then
                DeviceName = arg.Substring("/filename=".Length)
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
            ElseIf arg.StartsWith("/variant=", StringComparison.OrdinalIgnoreCase) Then
                OutputImageVariant = arg.Substring("/variant=".Length)
            ElseIf arg.StartsWith("/libewfoutput=", StringComparison.OrdinalIgnoreCase) Then
                libewfDebugOutput = arg.Substring("/libewfoutput=".Length)
            ElseIf arg.StartsWith("/debugcompare=", StringComparison.OrdinalIgnoreCase) Then
                DebugCompare = arg.Substring("/debugcompare=".Length)
            ElseIf arg = "/?" OrElse arg.Equals("/help", StringComparison.OrdinalIgnoreCase) Then
                ShowHelp = True
                Exit For
            ElseIf arg.Equals("/version", StringComparison.OrdinalIgnoreCase) Then
                ShowVersionInfo()
                Return 0
            Else
                Console.WriteLine($"Unsupported switch: {arg}")
                ShowHelp = True
                Exit For
            End If
        Next

        If _
          ShowHelp OrElse
          String.IsNullOrWhiteSpace(DeviceName) Then

            Dim asmname = Assembly.GetExecutingAssembly().GetName().Name

            Dim providers = String.Join(", ", DevioServiceFactory.InstalledProvidersByNameAndFileAccess.Keys)

            Console.WriteLine($"{asmname}.

Integrated command line interface to Arsenal Image Mounter virtual SCSI
miniport driver.

For version information, license, copyrights and credits, type aim_cli /version

Syntax to mount an image file as a virtual disk:
{asmname} /mount[:removable|:cdrom] [/buffersize=bytes] [/readonly]
    /filename=imagefilename /provider={providers}
    [/writeoverlay=differencingimagefile]

Syntax, start shared memory service mode, for mounting from other applications:
{asmname} /name=objectname [/buffersize=bytes] [/readonly]
    /filename=imagefilename /provider={providers}

Syntax, start TCP/IP service mode, for mounting from other computers:
{asmname} [/ipaddress=listenaddress] /port=tcpport [/readonly]
    /filename=imagefilename /provider={providers}

Syntax, convert image file without mounting as virtual disk:
{asmname}  /filename=imagefilename /provider={providers}
    /convert=outimagefilename [/variant=fixed|dynamic]

DiscUtils and MultiPartRaw provider libraries are included embedded in this
application. Libewf provider needs libewf.dll and LibAFF4 provider needs
libaff4.dll.

When converting, output image type can be DD, IMG or RAW for raw format or VHD,
VHDX, VDI or VMDK virtual machine disk formats. For virtual machine disk
formats, the optional /variant switch can be used to specify either fixed or
dynamically expanding formats. Default is dynamic.")

            Return 1

        End If

        Console.WriteLine($"Opening image file '{DeviceName}'...")

        If StringComparer.OrdinalIgnoreCase.Equals(ProviderName, "libewf") Then

            DevioProviderLibEwf.NotificationFile = libewfDebugOutput

            AddHandler RunToEnd, Sub() DevioProviderLibEwf.NotificationFile = Nothing

            If Verbose Then
                DevioProviderLibEwf.NotificationVerbose = True
            End If

        End If

        Dim Provider = DevioServiceFactory.GetProvider(DeviceName, DiskAccess, ProviderName)

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

            ConvertToImage(Provider, OutputImage, OutputImageVariant)
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
            Console.WriteLine("Virtual disk created. Press Ctrl+C to remove virtual disk and exit.")
        Else
            Service.StartServiceThread()
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

        Service.WaitForServiceThreadExit()

        Console.WriteLine("Service stopped.")

        If Service.Exception IsNot Nothing Then
            Throw New Exception("Service failed.", Service.Exception)
        End If

        Return 0

    End Function

    Public Property ImageIoBufferSize As Integer = 2 << 20

    Public Sub ConvertToImage(provider As IDevioProvider, outputImage As String, OutputImageVariant As String)

        Using provider

            Using cancel As New CancellationTokenSource

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

                Console.WriteLine($"Converting to new image file '{outputImage}'...")

                Dim image_type = Path.GetExtension(outputImage).TrimStart("."c).ToUpperInvariant()

                Select Case image_type

                    Case "VHD", "VHDX", "VDI", "VMDK"
                        ConvertToDiscUtilsImage(provider, outputImage, image_type, OutputImageVariant, cancel.Token)

                    Case "DD", "RAW", "IMG", "IMA", "ISO", "BIN"
                        ConvertToRawImage(provider, outputImage, OutputImageVariant, cancel.Token)

                End Select

                Console.WriteLine($"Image converted successfully.")

            End Using

        End Using

    End Sub

    Public Sub ConvertToDiscUtilsImage(provider As IDevioProvider, outputImage As String, type As String, OutputImageVariant As String, cancel As CancellationToken)

        Using builder = VirtualDisk.CreateDisk(type, OutputImageVariant, outputImage, provider.Length, Geometry.FromCapacity(provider.Length, CInt(provider.SectorSize)), Nothing)

            Dim target = builder.Content

            provider.WriteToSkipEmptyBlocks(target, ImageIoBufferSize, cancel)

        End Using

    End Sub

    Public Sub ConvertToRawImage(provider As IDevioProvider, outputImage As String, OutputImageVariant As String, cancel As CancellationToken)

        Using target As New FileStream(outputImage, FileMode.Create, FileAccess.Write, FileShare.Delete, ImageIoBufferSize)

            If "fixed".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase) Then

            ElseIf "dynamic".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase) Then

                NativeFileIO.SetFileSparseFlag(target.SafeFileHandle, True)

            Else

                Throw New ArgumentException($"Value {OutputImageVariant} not supported as output image variant. Valid values are fixed or dynamic.")

            End If

            provider.WriteToSkipEmptyBlocks(target, ImageIoBufferSize, cancel)

        End Using

    End Sub

End Module
