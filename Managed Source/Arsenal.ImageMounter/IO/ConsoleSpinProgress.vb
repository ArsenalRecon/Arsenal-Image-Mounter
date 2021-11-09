Imports System.Threading

Namespace IO

    Public Class ConsoleSpinProgress
        Implements IDisposable

        Public ReadOnly Property Timer As New Timer(AddressOf Tick)

        Public ReadOnly Property CurrentChar As Char

        Public Sub New(dueTime As Integer, period As Integer)

            Timer.Change(dueTime, period)

        End Sub

        Public Sub New(dueTime As TimeSpan, period As TimeSpan)

            Timer.Change(dueTime, period)

        End Sub

        Private Sub Tick(o As Object)

            UpdateConsoleSpinProgress(_CurrentChar)

        End Sub

        Public Shared Sub UpdateConsoleSpinProgress(ByRef chr As Char)

            Select Case chr
                Case "\"c
                    chr = "|"c
                Case "|"c
                    chr = "/"c
                Case "/"c
                    chr = "-"c
                Case Else
                    chr = "\"c
            End Select

            SyncLock ConsoleSync

                Console.ForegroundColor = ConsoleProgressBar.ConsoleProgressBarColor

                Console.Write(chr)
                Console.Write(vbBack)

                Console.ResetColor()

            End SyncLock

        End Sub

        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    _Timer.Dispose()
                    Console.WriteLine(" ")

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