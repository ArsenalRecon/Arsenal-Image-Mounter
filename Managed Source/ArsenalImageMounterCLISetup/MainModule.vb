
''''' MainModule.vb
''''' Console driver setup application, for scripting and similar.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.DriverSetup
Imports Arsenal.ImageMounter.API
Imports Arsenal.ImageMounter.IO
Imports System.Windows.Forms
Imports System.ComponentModel
Imports System.Reflection
Imports Arsenal.ImageMounter

Public Module MainModule

    Private ReadOnly ownerWindow As New NativeFileIO.NativeWindowHandle(NativeFileIO.SafeNativeMethods.GetConsoleWindow())

    Public Function Main(ParamArray args As String()) As Integer

        Trace.Listeners.Add(New ConsoleTraceListener)

        Dim opMode As OpMode = OpMode.Status

        If args?.Length > 0 Then
            If args(0).Equals("/install", StringComparison.OrdinalIgnoreCase) Then
                opMode = OpMode.Install
            ElseIf args(0).Equals("/uninstall", StringComparison.OrdinalIgnoreCase) Then
                opMode = OpMode.Uninstall
            ElseIf args(0).Equals("/status", StringComparison.OrdinalIgnoreCase) Then
                opMode = OpMode.Status
            Else
                Trace.WriteLine($"Syntax: {Assembly.GetExecutingAssembly().GetName().Name} /install|/uninstall|/status")
            End If
        End If

        Try
            Return SetupOperation(opMode)

        Catch ex As Exception
            If TypeOf ex Is Win32Exception Then
                Trace.WriteLine($"Win32 error: {DirectCast(ex, Win32Exception).NativeErrorCode}")
            End If
            Trace.WriteLine(ex.ToString())
            Return -1

        End Try

    End Function

    Public Function SetupOperation(opMode As OpMode) As Integer

        Trace.WriteLine($"Kernel type: {Kernel}")
        Trace.WriteLine($"Kernel supports StorPort: {HasStorPort}")

        Select Case opMode

            Case OpMode.Install
                Using zipStream = GetType(MainModule).Assembly.GetManifestResourceStream(
                        GetType(MainModule), "DriverFiles.zip")

                    InstallFromZipStream(ownerWindow, zipStream)

                End Using

                Try
                    Using New ScsiAdapter
                    End Using

                    Trace.WriteLine("Driver successfully installed.")
                    Return 0

                Catch ex As Exception
                    Trace.WriteLine("A reboot may be required to complete driver setup.")
                    Return 1

                End Try

            Case OpMode.Uninstall
                If AdapterDevicePresent Then
                    Uninstall(ownerWindow)
                    Trace.WriteLine("Driver successfully uninstalled.")

                    Try
                        Using New ScsiAdapter
                        End Using

                        Trace.WriteLine("A reboot may be required to complete driver setup.")
                        Return 2

                    Catch
                        Return 0

                    End Try
                Else
                    Trace.WriteLine("Virtual SCSI adapter not installed.")
                    Return 1
                End If

            Case OpMode.Status
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

End Module

Public Enum OpMode
    Install
    Uninstall
    Status
End Enum
