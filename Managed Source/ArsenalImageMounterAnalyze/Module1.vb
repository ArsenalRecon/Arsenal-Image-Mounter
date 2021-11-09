''''' Driver Version / Setup Verify application.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.IO
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32.SafeHandles

Module Module1

    Sub Main()

        Dim devices = Aggregate dev In NativeFileIO.QueryDosDevice()
                      Where dev.StartsWith("SCSI", StringComparison.OrdinalIgnoreCase) AndAlso dev.EndsWith(":", StringComparison.Ordinal)
                      Order By dev
                      Into ToList()

        Console.WriteLine("Found SCSI adapters:")
        For Each dev In devices
            Console.Write(dev)
            Console.Write(" => ")
            Console.WriteLine(String.Join(", ", NativeFileIO.QueryDosDevice(dev)))
        Next

        Console.WriteLine()
        Console.WriteLine("Attempting to open...")
        For Each dev In devices
            Console.Write(dev)
            Console.Write(" => ")
            Try
                Using NativeFileIO.OpenFileHandle($"\\?\{dev}", FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, False)
                End Using
                Console.WriteLine("Successful.")

            Catch ex As Exception
                Console.WriteLine($"Error: {ex}")

            End Try
        Next

        Console.WriteLine()
        Console.WriteLine("Attempting to query version...")
        For Each dev In devices
            Console.Write(dev)
            Console.Write(" => ")
            Try
                Using h = NativeFileIO.OpenFileHandle($"\\?\{dev}", FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, False)
                    Dim ReturnCode = CheckDriverVersion(h)
                    Console.WriteLine($"Driver version: {ReturnCode:X4}")
                End Using

            Catch ex As Exception
                Console.WriteLine($"Error: {ex.JoinMessages()}")

            End Try
        Next

        If Debugger.IsAttached Then
            Console.ReadKey()
        End If
    End Sub

    ''' <summary>
    ''' Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
    ''' library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
    ''' </summary>
    Function CheckDriverVersion(SafeFileHandle As SafeFileHandle) As Integer

        Dim ReturnCode As Integer
        Dim Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                                  NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION,
                                                                  0,
                                                                  Nothing,
                                                                  ReturnCode)

        Return ReturnCode

    End Function

End Module
