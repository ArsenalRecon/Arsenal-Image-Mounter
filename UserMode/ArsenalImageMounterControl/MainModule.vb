
''''' MainModule.vb
''''' Main module for control application.
''''' 
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Net
Imports System.IO

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
            Console.WriteLine(ex.GetBaseException().Message)
            Trace.WriteLine(ex.ToString())

        End Try

    End Sub

    Sub SafeMain(args As String())

        Dim FileName As String = Nothing
        Dim DiskPath As String = Nothing
        Dim DeviceNumber As UInt32 = ScsiAdapter.AutoDeviceNumber
        Dim ShowHelp As Boolean = False
        Dim Mode As OpMode
        Dim DiskSize As Long
        Dim Offset As Long
        Dim SectorSize As UInt32
        Dim Flags As DeviceFlags

        For Each arg In args
            If arg.Equals("/trace", StringComparison.InvariantCultureIgnoreCase) Then
                Trace.Listeners.Add(New ConsoleTraceListener(True))
            ElseIf arg.StartsWith("/device=", StringComparison.InvariantCultureIgnoreCase) Then
                DeviceNumber = UInteger.Parse(arg.Substring("/device=".Length), Globalization.NumberStyles.HexNumber)
            ElseIf arg.StartsWith("/filename=", StringComparison.InvariantCultureIgnoreCase) Then
                FileName = arg.Substring("/filename=".Length)
            ElseIf arg.StartsWith("/disksize=", StringComparison.InvariantCultureIgnoreCase) Then
                DiskSize = Long.Parse(arg.Substring("/disksize=".Length)) << 20
            ElseIf arg.StartsWith("/offset=", StringComparison.InvariantCultureIgnoreCase) Then
                Offset = Long.Parse(arg.Substring("/offset=".Length)) << 9
            ElseIf arg.StartsWith("/sectorsize=", StringComparison.InvariantCultureIgnoreCase) Then
                SectorSize = UInteger.Parse(arg.Substring("/sectorsize=".Length))
            ElseIf arg.StartsWith("/getdevicenumber=", StringComparison.InvariantCultureIgnoreCase) Then
                Mode = OpMode.GetDeviceNumber
                DiskPath = arg.Substring("/getdevicenumber=".Length)
            ElseIf arg.Equals("/proxy", StringComparison.InvariantCultureIgnoreCase) Then
                Flags = Flags Or DeviceFlags.TypeProxy
            ElseIf arg.StartsWith("/proxy=", StringComparison.InvariantCultureIgnoreCase) Then
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
                        Console.WriteLine("Unsupported proxy type: " & ProxyType)
                        ShowHelp = True
                        Exit For
                End Select
            ElseIf arg.Equals("/vm", StringComparison.InvariantCultureIgnoreCase) Then
                Flags = Flags Or DeviceFlags.TypeVM
            ElseIf arg.Equals("/sparse", StringComparison.InvariantCultureIgnoreCase) Then
                Flags = Flags Or DeviceFlags.SparseFile
            ElseIf arg.Equals("/readonly", StringComparison.InvariantCultureIgnoreCase) Then
                Flags = Flags Or DeviceFlags.ReadOnly
            ElseIf arg.Equals("/add", StringComparison.InvariantCultureIgnoreCase) Then
                Mode = OpMode.Add
            ElseIf arg.Equals("/rescan", StringComparison.InvariantCultureIgnoreCase) Then
                Mode = OpMode.Rescan
            ElseIf arg.Equals("/remove", StringComparison.InvariantCultureIgnoreCase) Then
                Mode = OpMode.Remove
            ElseIf arg.Equals("/query", StringComparison.InvariantCultureIgnoreCase) Then
                Mode = OpMode.QueryDevice
            ElseIf arg.Equals("/list", StringComparison.InvariantCultureIgnoreCase) Then
                Mode = OpMode.ListDevices
            ElseIf arg = "/?" OrElse arg.Equals("/help", StringComparison.InvariantCultureIgnoreCase) Then
                ShowHelp = True
                Exit For
            Else
                Console.WriteLine("Unsupported switch: " & arg)
                ShowHelp = True
                Exit For
            End If
        Next

        If ShowHelp Then
            Console.WriteLine("Invalid command line syntax.")
            Return
        End If

        Trace.WriteLine("Selected device number: " & DeviceNumber.ToString("X6"))

        Select Case Mode

            Case OpMode.Add
                Using Adapter As New ScsiAdapter
                    Adapter.CreateDevice(DiskSize:=DiskSize,
                                         BytesPerSector:=SectorSize,
                                         ImageOffset:=Offset,
                                         Flags:=Flags,
                                         Filename:=FileName,
                                         NativePath:=False,
                                         DeviceNumber:=DeviceNumber)
                End Using
                Console.WriteLine("Created device (format: LLTTPP hex): " & DeviceNumber.ToString("X6"))

            Case OpMode.Remove
                Using adapter As New ScsiAdapter
                    adapter.RemoveDevice(DeviceNumber)
                End Using

            Case OpMode.GetDeviceNumber
                Using disk As New DiskDevice(DiskPath, FileAccess.ReadWrite)
                    DeviceNumber = disk.GetDeviceNumber()
                    Console.WriteLine("DeviceNumber (format: LLTTPP hex): " & DeviceNumber.ToString("X6"))
                End Using

            Case OpMode.QueryDevice
                Using adapter As New ScsiAdapter
                    Dim prop = adapter.QueryDevice(DeviceNumber)
                    For Each field In prop.GetType().GetFields()
                        Console.WriteLine(field.Name & " = " & field.GetValue(prop).ToString())
                    Next
                End Using

            Case OpMode.ListDevices
                Using adapter As New ScsiAdapter
                    Dim DeviceList = adapter.GetDeviceList()
                    If DeviceList.Count = 0 Then
                        Console.WriteLine("No virtual disks defined.")
                        Return
                    End If
                    Console.WriteLine("List of active device numbers: (format: LLTTPP hex)")
                    For Each i In DeviceList
                        Console.WriteLine(i.ToString("X6"))
                    Next
                End Using

            Case OpMode.Rescan
                Dim result = API.RescanScsiAdapter()
                Console.WriteLine("Result: " & result)

            Case Else
                Console.WriteLine("Nothing to do.")

        End Select

    End Sub

End Module
