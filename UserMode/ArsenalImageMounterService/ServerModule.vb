
''''' ServerModule.vb
''''' Main module for PhysicalDiskMounterService application.
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
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

    <MTAThread()>
    Sub Main(args As String())

        Try
            SafeMain(args)

        Catch ex As AbandonedMutexException
            Console.WriteLine("Unexpected client exit.")
            Trace.WriteLine(ex.ToString())

        Catch ex As Exception
            Console.WriteLine(ex.GetBaseException().Message)
            Trace.WriteLine(ex.ToString())

        End Try

        If Debugger.IsAttached Then
            Console.ReadKey()
        End If

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

        For Each arg In args
            If arg.Equals("/trace", StringComparison.InvariantCultureIgnoreCase) Then
                Trace.Listeners.Add(New ConsoleTraceListener(True))
                Verbose = True
            ElseIf arg.StartsWith("/name=", StringComparison.InvariantCultureIgnoreCase) Then
                ObjectName = arg.Substring("/name=".Length)
            ElseIf arg.StartsWith("/ipaddress=", StringComparison.InvariantCultureIgnoreCase) Then
                ListenAddress = IPAddress.Parse(arg.Substring("/ipaddress=".Length))
            ElseIf arg.StartsWith("/port=", StringComparison.InvariantCultureIgnoreCase) Then
                ListenPort = Integer.Parse(arg.Substring("/port=".Length))
            ElseIf arg.StartsWith("/buffersize=", StringComparison.InvariantCultureIgnoreCase) Then
                BufferSize = Long.Parse(arg.Substring("/buffersize=".Length))
            ElseIf arg.StartsWith("/filename=", StringComparison.InvariantCultureIgnoreCase) Then
                DeviceName = arg.Substring("/filename=".Length)
            ElseIf arg.StartsWith("/provider=", StringComparison.InvariantCultureIgnoreCase) Then
                ProviderName = arg.Substring("/provider=".Length)
            ElseIf arg.Equals("/readonly", StringComparison.InvariantCultureIgnoreCase) Then
                DiskAccess = FileAccess.Read
            ElseIf arg.Equals("/mount", StringComparison.InvariantCultureIgnoreCase) Then
                Mount = True
            ElseIf arg = "/?" OrElse arg.Equals("/help", StringComparison.InvariantCultureIgnoreCase) Then
                ShowHelp = True
                Exit For
            Else
                Console.WriteLine("Unsupported switch: " & arg)
                ShowHelp = True
                Exit For
            End If
        Next

        If _
          ShowHelp OrElse
          String.IsNullOrEmpty(DeviceName) Then

            Dim asmname = Assembly.GetExecutingAssembly().GetName().Name

            Console.WriteLine(asmname & "." & Environment.NewLine &
                              Environment.NewLine &
                              "Syntax, automatically select object name and mount:" & Environment.NewLine &
                              asmname & " /mount [/readonly] [/buffersize=bytes] /filename=imagefilename" & Environment.NewLine &
                              "    [/provider=DiscUtils|LibEwf]" & Environment.NewLine &
                              Environment.NewLine &
                              "Syntax, start shared memory service mode, for mounting from other applications:" & Environment.NewLine &
                              asmname & " /name=objectname [/mount] [/readonly] [/buffersize=bytes]" & Environment.NewLine &
                              "    /filename=imagefilename [/provider=DiscUtils|LibEwf]" & Environment.NewLine &
                              Environment.NewLine &
                              "Syntax, start TCP/IP service mode, for mounting from other computers:" & Environment.NewLine &
                              asmname & " [/ipaddress=address] /port=tcpport [/mount] [/readonly]" & Environment.NewLine &
                              "    /filename=imagefilename [/provider=DiscUtils|LibEwf|MultiPartRaw]")

            Return

        End If

        Dim Provider As IDevioProvider

        Select Case ProviderName.ToLowerInvariant()

            Case "discutils"
                Provider = DevioServiceFactory.GetProviderDiscUtils(DeviceName, DiskAccess)

            Case "libewf"
                Provider = DevioServiceFactory.GetProviderLibEwf(DeviceName, DiskAccess)

            Case "multipartraw"
                Provider = DevioServiceFactory.GetProviderMultiPartRaw(DeviceName, DiskAccess)

            Case Else
                Console.WriteLine("Provider names can be DiscUtils or LibEwf.")
                Return

        End Select

        Dim Service As DevioServiceBase

        If Not String.IsNullOrEmpty(ObjectName) Then

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
            Console.WriteLine("Opening image file and mounting as virtual disk...")
            Service.StartServiceThreadAndMount(New ScsiAdapter, 0)
            Console.WriteLine("Virtual disk created. Press Ctrl+C to remove virtual disk and exit.")
        Else
            Console.WriteLine("Opening image file...")
            Service.StartServiceThread()
            Console.WriteLine("Image file opened, waiting for incoming connections. Press Ctrl+C to exit.")
        End If

        AddHandler Console.CancelKeyPress,
            Sub(sender, e)
                Console.WriteLine("Stopping service...")
                Service.Dispose()

                Try
                    e.Cancel = True
                Catch
                End Try
            End Sub

        Service.WaitForServiceThreadExit()

        Console.WriteLine("Service stopped.")

    End Sub

End Module
