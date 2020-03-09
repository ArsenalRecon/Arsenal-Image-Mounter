Imports System.Runtime.InteropServices
Imports System.Threading
Imports Arsenal.ImageMounter.IO

Public Module MainModule

    Friend UsingDebugConsole As Boolean

    Private ReadOnly logfile As String = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".log")

    Sub New()

        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf AppDomain_UnhandledException

    End Sub

    Public Sub Main(args As String())

        If ConfigurationManager.AppSettings!DebugConsole = Boolean.TrueString Then
            NativeFileIO.Win32API.AllocConsole()
            Trace.Listeners.Add(New ConsoleTraceListener With {.Name = "AIMConsoleTraceListener"})
            UsingDebugConsole = True
        End If

        My.Settings.Reload()

        Dim privileges_enabled = NativeFileIO.EnablePrivileges(
            NativeFileIO.Win32API.SE_BACKUP_NAME,
            NativeFileIO.Win32API.SE_RESTORE_NAME,
            NativeFileIO.Win32API.SE_DEBUG_NAME,
            NativeFileIO.Win32API.SE_MANAGE_VOLUME_NAME,
            NativeFileIO.Win32API.SE_SECURITY_NAME,
            NativeFileIO.Win32API.SE_TCB_NAME)

        If privileges_enabled IsNot Nothing Then
            Trace.WriteLine($"Enabled privileges: {String.Join(", ", privileges_enabled)}")
        Else
            Trace.WriteLine($"Error enabling privileges: {Marshal.GetLastWin32Error()}")
        End If

        If Not My.Settings.EULAConfirmed Then

            If MessageBox.Show(GetEULA(),
                               "Arsenal Image Mounter",
                               MessageBoxButtons.OKCancel,
                               MessageBoxIcon.Information) <> DialogResult.OK Then
                Return
            End If

            My.Settings.EULAConfirmed = True

            My.Settings.Save()

        End If

        AddHandler Application.ThreadException, AddressOf Application_ThreadException

        Application.Run(New MainForm)

        My.Settings.Save()

    End Sub

    Private Function GetEULA() As String

        Using reader As New StreamReader(GetType(MainModule).Assembly.GetManifestResourceStream(GetType(MainModule), "EULA.txt"))

            Return reader.ReadToEnd()

        End Using

    End Function

    Public Sub LogMessage(msg As String)

        Try
            File.AppendAllText(logfile,
                                 $"---------------{Environment.NewLine}{Date.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{msg}{Environment.NewLine}")

        Catch ex As Exception
            Trace.WriteLine($"Exception while logging message: {ex.ToString()}")

        End Try

    End Sub

    Private Sub AppDomain_UnhandledException(sender As Object, e As UnhandledExceptionEventArgs)

        Dim msg = e.ExceptionObject.ToString()

        Trace.WriteLine($"AppDomain.UnhandledException: {msg}")

        LogMessage(msg)

        If e.IsTerminating Then
            Dim rc =
                MessageBox.Show($"Fatal error: {msg}",
                                e.ExceptionObject.GetType().ToString(),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Stop)
        Else
            Dim rc =
                MessageBox.Show($"Unhandled error: {msg}{Environment.NewLine}{Environment.NewLine}Ignore error and continue?",
                            e.ExceptionObject.GetType().ToString(),
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Stop)

            If rc = DialogResult.No Then
                Environment.Exit(1)
            End If
        End If

    End Sub

    Private Sub Application_ThreadException(sender As Object, e As ThreadExceptionEventArgs)

        Dim msg = e.Exception.ToString()

        Trace.WriteLine("Application.ThreadException: " & msg)

        LogMessage(msg)

        Dim rc =
            MessageBox.Show("Error: " & e.Exception.JoinMessages() &
                            Environment.NewLine &
                            Environment.NewLine &
                            "Ignore error and continue?",
                            e.Exception.GetBaseException().GetType().Name,
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Stop)

        If rc = DialogResult.No Then
            Application.Exit()
        End If

    End Sub

End Module
