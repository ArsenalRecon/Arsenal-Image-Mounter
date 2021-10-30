
''''' MainModule.vb
''''' Main module for control application.
''''' 
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Net
Imports System.IO
Imports System.Reflection
Imports Arsenal.ImageMounter.IO
Imports System.Diagnostics.CodeAnalysis
Imports Arsenal.ImageMounter
Imports Arsenal.ImageMounter.Extensions

Enum OpMode
    None
    Add
    Remove
    ListDevices
    GetDeviceNumber
    QueryDevice

    Rescan

End Enum

Module MainModule

    Sub Main(args As String())

        Try
            SafeMain(args)

        Catch ex As Exception
            Console.WriteLine(ex.JoinMessages())
            Trace.WriteLine(ex.ToString())

        End Try

    End Sub

    Sub SafeMain(args As String())

        Dim FileName As String = Nothing
        Dim DiskPath As String = Nothing
        Dim DeviceNumber As UInt32?
        Dim ShowHelp As Boolean = False
        Dim Mode As OpMode
        Dim DiskSize As Long
        Dim Offset As Long
        Dim SectorSize As UInt32
        Dim Flags As DeviceFlags

        For Each arg In args
            If arg.Equals("/trace", StringComparison.OrdinalIgnoreCase) Then
                Trace.Listeners.Add(New ConsoleTraceListener(True))
            ElseIf arg.StartsWith("/device=", StringComparison.OrdinalIgnoreCase) Then
                DeviceNumber = UInteger.Parse(arg.Substring("/device=".Length), Globalization.NumberStyles.HexNumber)
            ElseIf arg.StartsWith("/filename=", StringComparison.OrdinalIgnoreCase) Then
                FileName = arg.Substring("/filename=".Length)
            ElseIf arg.StartsWith("/disksize=", StringComparison.OrdinalIgnoreCase) Then
                DiskSize = Long.Parse(arg.Substring("/disksize=".Length)) << 20
            ElseIf arg.StartsWith("/offset=", StringComparison.OrdinalIgnoreCase) Then
                Offset = Long.Parse(arg.Substring("/offset=".Length)) << 9
            ElseIf arg.StartsWith("/sectorsize=", StringComparison.OrdinalIgnoreCase) Then
                SectorSize = UInteger.Parse(arg.Substring("/sectorsize=".Length))
            ElseIf arg.StartsWith("/getdevicenumber=", StringComparison.OrdinalIgnoreCase) Then
                Mode = OpMode.GetDeviceNumber
                DiskPath = arg.Substring("/getdevicenumber=".Length)
            ElseIf arg.Equals("/cd", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.DeviceTypeCD
            ElseIf arg.Equals("/proxy", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.TypeProxy
            ElseIf arg.StartsWith("/proxy=", StringComparison.OrdinalIgnoreCase) Then
                Dim ProxyType = arg.Substring("/proxy=".Length)
                Flags = Flags Or DeviceFlags.TypeProxy
                Select Case ProxyType.ToLowerInvariant()
                    Case "shm"
                        Flags = Flags Or DeviceFlags.ProxyTypeSharedMemory
                    Case "ip"
                        Flags = Flags Or DeviceFlags.ProxyTypeTCP
                    Case "comm"
                        Flags = Flags Or DeviceFlags.ProxyTypeComm
                    Case Else
                        Console.WriteLine($"Unsupported proxy type: {ProxyType}")
                        ShowHelp = True
                        Exit For
                End Select
            ElseIf arg.Equals("/vm", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.TypeVM
            ElseIf arg.Equals("/sparse", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.SparseFile
            ElseIf arg.Equals("/readonly", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.ReadOnly
            ElseIf arg.Equals("/removable", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.Removable
            ElseIf arg.Equals("/awe", StringComparison.OrdinalIgnoreCase) Then
                Flags = Flags Or DeviceFlags.TypeFile Or DeviceFlags.FileTypeAwe
            ElseIf arg.Equals("/add", StringComparison.OrdinalIgnoreCase) Then
                Mode = OpMode.Add
            ElseIf arg.Equals("/rescan", StringComparison.OrdinalIgnoreCase) Then
                Mode = OpMode.Rescan
            ElseIf arg.Equals("/remove", StringComparison.OrdinalIgnoreCase) Then
                Mode = OpMode.Remove
            ElseIf arg.Equals("/query", StringComparison.OrdinalIgnoreCase) Then
                Mode = OpMode.QueryDevice
            ElseIf arg.Equals("/list", StringComparison.OrdinalIgnoreCase) Then
                Mode = OpMode.ListDevices
            ElseIf arg = "/?" OrElse arg.Equals("/help", StringComparison.OrdinalIgnoreCase) Then
                ShowHelp = True
                Exit For
            Else
                Console.WriteLine($"Unsupported switch: {arg}")
                ShowHelp = True
                Exit For
            End If
        Next

        If ShowHelp Or Mode = Nothing Then
            Console.WriteLine("Invalid command line syntax.")
            Console.WriteLine()
            Dim asmname = Assembly.GetExecutingAssembly().GetName().Name

            Console.WriteLine(asmname & "." & Environment.NewLine &
                              Environment.NewLine &
                              "Syntax:" & Environment.NewLine &
                              asmname & " /add|/rescan|/remove|/query|/list [/trace] " & Environment.NewLine &
                              "    [/device=llttpp] [/filename=path] [/disksize=nnn] [/offset=nnn] " & Environment.NewLine &
                              "    [/sectorsize=nnn] [/getdevicenumber=path] [/proxy=shm] [/vm|/awe] [/sparse]" & Environment.NewLine &
                              "    [/readonly] [/removable]")
            Return
        End If

        Trace.WriteLine($"Selected device number: {If(DeviceNumber Is Nothing, "Auto", DeviceNumber.Value.ToString("X6"))}")

        Select Case Mode

            Case OpMode.Add
                Dim CreateDeviceNumber = If(DeviceNumber, ScsiAdapter.AutoDeviceNumber)
                Using Adapter As New ScsiAdapter
                    Adapter.CreateDevice(DiskSize:=DiskSize,
                                         BytesPerSector:=SectorSize,
                                         ImageOffset:=Offset,
                                         Flags:=Flags,
                                         Filename:=FileName,
                                         NativePath:=False,
                                         DeviceNumber:=CreateDeviceNumber)
                End Using
                Console.WriteLine($"Created device (format: LLTTPP hex): {CreateDeviceNumber:X6}")

            Case OpMode.Remove
                Using adapter As New ScsiAdapter
                    If DeviceNumber.HasValue Then
                        adapter.RemoveDevice(DeviceNumber.Value)
                    Else
                        adapter.RemoveAllDevices()
                    End If
                End Using

            Case OpMode.GetDeviceNumber
                Using disk As New DiskDevice(DiskPath, FileAccess.ReadWrite)
                    Console.WriteLine($"DeviceNumber (format: LLTTPP hex): {disk.DeviceNumber:X6}")
                End Using

            Case OpMode.QueryDevice
                Using adapter As New ScsiAdapter
                    Dim DeviceList As IEnumerable(Of UInt32)
                    If DeviceNumber.HasValue Then
                        DeviceList = {DeviceNumber.Value}
                    Else
                        DeviceList = adapter.GetDeviceList()
                    End If

                    For Each Device In DeviceList.Select(AddressOf adapter.QueryDevice)
                        Console.WriteLine()
                        For Each field In Device.GetType().GetFields()
                            If field.FieldType Is GetType(UInt32) Then
                                Console.WriteLine($"{field.Name} = {DirectCast(field.GetValue(Device), UInt32):X8}")
                            Else
                                Console.WriteLine($"{field.Name} = {If(field.GetValue(Device), "(null)")}")
                            End If
                        Next
                        Try
                            Using disk = adapter.OpenDevice(Device.DeviceNumber, 0)
                                Console.WriteLine(disk.DevicePath)
                                For Each volume In disk.EnumerateDiskVolumes()
                                    Console.WriteLine($"Contains volume {volume}")
                                    For Each mount_point In NativeFileIO.EnumerateVolumeMountPoints(volume)
                                        Console.WriteLine($"  Mounted at {mount_point}")
                                    Next
                                Next
                            End Using

                        Catch ex As Exception
                            Console.Error.WriteLine($"Error opening device: {ex.JoinMessages()}")

                        End Try
                    Next
                End Using

            Case OpMode.ListDevices
                Using adapter As New ScsiAdapter
                    Dim DeviceList = adapter.GetDeviceList()
                    If DeviceList.Length = 0 Then
                        Console.WriteLine("No virtual disks defined.")
                        Return
                    End If
                    Console.WriteLine("List of active device numbers: (format: LLTTPP hex)")
                    For Each i In DeviceList
                        Console.WriteLine(i.ToString("X6"))
                    Next
                End Using

            Case OpMode.Rescan
                Using adapter As New ScsiAdapter
                    Dim result = adapter.RescanScsiAdapter()
                    adapter.RescanBus()

                    Console.WriteLine($"Result: {result}")
                End Using

            Case Else
                Console.WriteLine("Nothing to do.")

        End Select

    End Sub

End Module
