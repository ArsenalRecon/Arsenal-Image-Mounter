Imports System.Security
Imports System.Security.Permissions
Imports System.Text
Imports System.Threading

#Disable Warning IDE0079 ' Remove unnecessary suppression
#Disable Warning SYSLIB0003 ' Type or member is obsolete

Namespace IO

    <SecuritySafeCritical>
    Public Class ConsoleProgressBar
        Implements IDisposable

        Public ReadOnly Property Timer As New Timer(AddressOf Tick)

        Public ReadOnly Property CurrentValue As Integer

        Private ReadOnly updateFunc As Func(Of Double)

        <SecuritySafeCritical>
        Private Sub New(update As Func(Of Double))
            updateFunc = update
            CreateConsoleProgressBar()
        End Sub

        Public Sub New(dueTime As Integer, period As Integer, update As Func(Of Double))
            Me.New(update)

            Timer.Change(dueTime, period)

        End Sub

        Public Sub New(dueTime As TimeSpan, period As TimeSpan, update As Func(Of Double))
            Me.New(update)

            Timer.Change(dueTime, period)

        End Sub

        <SecuritySafeCritical>
        Private Sub Tick(o As Object)

            Dim newvalue = updateFunc()

            If newvalue <> 1D AndAlso CInt(100 * newvalue) = _CurrentValue Then
                Return
            End If

            UpdateConsoleProgressBar(newvalue)

        End Sub

        <SecuritySafeCritical>
        <SecurityPermission(SecurityAction.Demand, Flags:=SecurityPermissionFlag.AllFlags)>
        Public Shared Sub CreateConsoleProgressBar()

            If NativeFileIO.UnsafeNativeMethods.GetFileType(NativeFileIO.UnsafeNativeMethods.GetStdHandle(NativeFileIO.StdHandle.Output)) <> NativeFileIO.Win32FileType.Character Then
                Return
            End If

            Dim row As New StringBuilder(Console.WindowWidth)

            row.Append("["c)

            row.Append("."c, Math.Max(Console.WindowWidth - 3, 0))

            row.Append("]"c)

            row.Append(vbCr(0))

            SyncLock ConsoleSync

                Console.ForegroundColor = ConsoleProgressBarColor

                Console.Write(row.ToString())

                Console.ResetColor()

            End SyncLock

        End Sub

        <SecuritySafeCritical>
        <SecurityPermission(SecurityAction.Demand, Flags:=SecurityPermissionFlag.AllFlags)>
        Public Shared Sub UpdateConsoleProgressBar(value As Double)

            If NativeFileIO.UnsafeNativeMethods.GetFileType(NativeFileIO.UnsafeNativeMethods.GetStdHandle(NativeFileIO.StdHandle.Output)) <> NativeFileIO.Win32FileType.Character Then
                Return
            End If

            If value > 1D Then
                value = 1D
            ElseIf value < 0 Then
                value = 0D
            End If

            Dim currentPos = CInt((Console.WindowWidth - 3) * value)

            Dim row As New StringBuilder(Console.WindowWidth)

            row.Append("["c)

            row.Append("="c, Math.Max(currentPos, 0))

            row.Append("."c, Math.Max(Console.WindowWidth - 3 - currentPos, 0))

            Dim percent = $" {(100 * value):0} % "

            Dim midpos = (Console.WindowWidth - 3 - percent.Length) >> 1

            If midpos > 0 AndAlso row.Length >= percent.Length Then

                row.Remove(midpos, percent.Length)

                row.Insert(midpos, percent)

            End If

            row.Append("]"c)

            row.Append(vbCr(0))

            SyncLock ConsoleSync

                Console.ForegroundColor = ConsoleProgressBarColor

                Console.Write(row.ToString())

                Console.ResetColor()

            End SyncLock

        End Sub

        Public Shared Sub FinishConsoleProgressBar()

            UpdateConsoleProgressBar(1D)

            Console.WriteLine()

        End Sub

        Public Shared Property ConsoleProgressBarColor As ConsoleColor = ConsoleColor.Cyan

        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    _Timer.Dispose()
                    FinishConsoleProgressBar()

                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                ' TODO: set large fields to null.
                _Timer = Nothing
            End If
            disposedValue = True
        End Sub

        ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        Protected Overrides Sub Finalize()
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(False)
            MyBase.Finalize()
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            ' TODO: uncomment the following line if Finalize() is overridden above.
            GC.SuppressFinalize(Me)
        End Sub

    End Class

End Namespace