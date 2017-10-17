
''''' ServerModule.vb
''''' Main module for PhysicalDiskMounterService application.
''''' 
''''' Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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
Imports Arsenal.ImageMounter

Module ServerModule

    Private Event RunToEnd As EventHandler

    <MTAThread()>
    Sub Main(args As String())

        Try
            SafeMain(args)

        Catch ex As AbandonedMutexException
            Console.WriteLine("Unexpected client exit.")
            Trace.WriteLine(ex.ToString())

        Catch ex As Exception
            Console.WriteLine(ex.JoinMessages())
            Trace.WriteLine(ex.ToString())

        End Try

        RaiseEvent RunToEnd(Nothing, EventArgs.Empty)

        If Debugger.IsAttached Then
            Console.ReadKey()
        End If

    End Sub

    Sub ShowVersionInfo()

        Dim asm_file = Assembly.GetExecutingAssembly().Location
        Dim file_ver = FileVersionInfo.GetVersionInfo(asm_file)

        Console.WriteLine(
            "Low-level command line interface to Arsenal Image Mounter virtual" & Environment.NewLine &
            "SCSI miniport driver." & Environment.NewLine &
            Environment.NewLine &
            "Version " & file_ver.FileVersion.ToString() & Environment.NewLine &
            Environment.NewLine &
            "Copyright (C) 2012-2015 Arsenal Recon." & Environment.NewLine &
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

    Sub SafeMain(args As String())

        Dim DeviceName As String = Nothing
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
            ElseIf arg.Equals("/mount", StringComparison.OrdinalIgnoreCase) Then
                Mount = True
            ElseIf arg.Equals("/mount:removable", StringComparison.OrdinalIgnoreCase) Then
                Mount = True
                DeviceFlags = DeviceFlags Or DeviceFlags.Removable
            ElseIf arg.StartsWith("/libewfoutput=", StringComparison.OrdinalIgnoreCase) Then
                libewfDebugOutput = arg.Substring("/libewfoutput=".Length)
            ElseIf arg.StartsWith("/debugcompare=", StringComparison.OrdinalIgnoreCase) Then
                DebugCompare = arg.Substring("/debugcompare=".Length)
            ElseIf arg = "/?" OrElse arg.Equals("/help", StringComparison.OrdinalIgnoreCase) Then
                ShowHelp = True
                Exit For
            ElseIf arg.Equals("/version", StringComparison.OrdinalIgnoreCase) Then
                ShowVersionInfo()
                Return
            Else
                Console.WriteLine("Unsupported switch: " & arg)
                ShowHelp = True
                Exit For
            End If
        Next

        If _
          ShowHelp OrElse
          String.IsNullOrWhiteSpace(DeviceName) Then

            Dim asmname = Assembly.GetExecutingAssembly().GetName().Name

            Console.WriteLine(asmname & "." & Environment.NewLine &
                              Environment.NewLine &
                              "Integrated command line interface to Arsenal Image Mounter virtual SCSI" & Environment.NewLine &
                              "miniport driver." & Environment.NewLine &
                              Environment.NewLine &
                              "For version information, license, copyrights and credits, type aim_cli /version" & Environment.NewLine &
                              Environment.NewLine &
                              "Syntax, automatically select object name and mount:" & Environment.NewLine &
                              asmname & " /mount[:removable] [/readonly] [/buffersize=bytes]" & Environment.NewLine &
                              "    /filename=imagefilename [/provider=DiscUtils|LibEwf|MultiPartRaw]" & Environment.NewLine &
                              Environment.NewLine &
                              "Syntax, start shared memory service mode, for mounting from other applications:" & Environment.NewLine &
                              asmname & " /name=objectname [/mount[:removable]] [/readonly] [/buffersize=bytes]" & Environment.NewLine &
                              "    /filename=imagefilename [/provider=DiscUtils|LibEwf|MultiPartRaw]" & Environment.NewLine &
                              Environment.NewLine &
                              "Syntax, start TCP/IP service mode, for mounting from other computers:" & Environment.NewLine &
                              asmname & " [/ipaddress=address] /port=tcpport [/mount[:removable]] [/readonly]" & Environment.NewLine &
                              "    /filename=imagefilename [/provider=DiscUtils|LibEwf|MultiPartRaw]" & Environment.NewLine &
                              Environment.NewLine &
                              "DiscUtils and MultiPartRaw support libraries are included embedded in this" & Environment.NewLine &
                              "application. Libewf support needs libewf.dll, zlib.dll and msvcr100.dll as" & Environment.NewLine &
                              "external dll files.")

            Return

        End If

        Dim Provider As IDevioProvider

        Select Case ProviderName.ToLowerInvariant()

            Case "discutils"
                Provider = DevioServiceFactory.GetProviderDiscUtils(DeviceName, DiskAccess)

            Case "libewf"
                DevioProviderLibEwf.NotificationFile = libewfDebugOutput
                AddHandler RunToEnd,
                    Sub() DevioProviderLibEwf.NotificationFile = Nothing

                If Verbose Then
                    DevioProviderLibEwf.NotificationVerbose = True
                End If
                Provider = DevioServiceFactory.GetProviderLibEwf(DeviceName, DiskAccess)

            Case "multipartraw"
                Provider = DevioServiceFactory.GetProviderMultiPartRaw(DeviceName, DiskAccess)

            Case Else
                Console.WriteLine("Provider names can be DiscUtils, LibEwf Or MultiPartRaw.")
                Return

        End Select

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

        Else

            Provider.Dispose()
            Console.WriteLine("Shared memory object name, TCP/IP port or /mount switch must be specified.")
            Return

        End If

        If Mount Then
            Console.WriteLine("Opening image file And mounting as virtual disk...")
            Service.StartServiceThreadAndMount(New ScsiAdapter, DeviceFlags)
            Console.WriteLine("Virtual disk created. Press Ctrl+C to remove virtual disk and exit.")
        Else
            Console.WriteLine("Opening image file...")
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
                    Console.WriteLine(ex.JoinMessages())

                End Try
            End Sub

        Service.WaitForServiceThreadExit()

        Console.WriteLine("Service stopped.")

    End Sub

End Module
