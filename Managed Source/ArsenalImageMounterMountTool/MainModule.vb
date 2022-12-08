Imports System.Configuration
Imports System.IO
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO.Native
Imports Microsoft.Win32

Public Module MainModule

    Friend UsingDebugConsole As Boolean

    Private ReadOnly logfile As String = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".log")

    Sub New()

        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf AppDomain_UnhandledException

    End Sub

    Public Sub Main()

        If ConfigurationManager.AppSettings!DebugConsole = Boolean.TrueString Then
            NativeFileIO.SafeNativeMethods.AllocConsole()
            Trace.Listeners.Add(New ConsoleTraceListener With {.Name = "AIMConsoleTraceListener"})
            UsingDebugConsole = True
        End If

        Dim privileges_enabled = NativeFileIO.EnablePrivileges(
            NativeConstants.SE_BACKUP_NAME,
            NativeConstants.SE_RESTORE_NAME,
            NativeConstants.SE_DEBUG_NAME,
            NativeConstants.SE_MANAGE_VOLUME_NAME,
            NativeConstants.SE_SECURITY_NAME,
            NativeConstants.SE_TCB_NAME)

        If privileges_enabled IsNot Nothing Then
            Trace.WriteLine($"Enabled privileges: {String.Join(", ", privileges_enabled)}")
        Else
            Trace.WriteLine($"Error enabling privileges: {Marshal.GetLastWin32Error()}")
        End If

        Dim eulaconfirmed = Registry.GetValue("HKEY_CURRENT_USER\Software\Arsenal Recon\Image Mounter", "EULAConfirmed", 0)

        If TypeOf eulaconfirmed IsNot Integer OrElse CType(eulaconfirmed, Integer) < 1 Then
            If MessageBox.Show(GetEULA(),
                               "Arsenal Image Mounter",
                               MessageBoxButtons.OKCancel,
                               MessageBoxIcon.Information) <> DialogResult.OK Then
                Application.Exit()
                Return
            End If

        End If

        Registry.SetValue("HKEY_CURRENT_USER\Software\Arsenal Recon\Image Mounter", "EULAConfirmed", 1)

        AddHandler Application.ThreadException, AddressOf Application_ThreadException

        Application.Run(New MainForm)

    End Sub

    Private Function GetEULA() As String

        Using reader As New StreamReader(GetType(MainModule).Assembly.GetManifestResourceStream(GetType(MainModule), "EULA.txt"))

            Return reader.ReadToEnd()

        End Using

    End Function

    Public Sub LogMessage(msg As String)

        Try
            File.AppendAllText(logfile,
                               $"---------------
{Date.Now:yyyy-MM-dd HH:mm:ss}
{msg}
")

        Catch ex As Exception
            Trace.WriteLine($"Exception while logging message: {ex}")

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
                MessageBox.Show($"Unhandled error: {msg}

Ignore error and continue?",
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
            MessageBox.Show($"Error: {e.Exception.JoinMessages()}

Ignore error and continue?",
                            e.Exception.GetBaseException().GetType().Name,
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Stop)

        If rc = DialogResult.No Then
            Application.Exit()
        End If

    End Sub

End Module
