Imports Arsenal.ImageMounter.IO.NativeFileIO
Imports Arsenal.ImageMounter.IO.NativeFileIO.NativeConstants
Imports Arsenal.ImageMounter.IO.NativeFileIO.UnsafeNativeMethods
Imports System.ComponentModel
Imports System.Diagnostics.CodeAnalysis
Imports System.Runtime.InteropServices
Imports System.Security
Imports System.Security.Permissions

#Disable Warning IDE0079 ' Remove unnecessary suppression
#Disable Warning SYSLIB0003 ' Type or member is obsolete

Namespace IO

    <SecurityCritical>
    <SecurityPermission(SecurityAction.Demand, Flags:=SecurityPermissionFlag.AllFlags)>
    Public Class FileStreamsEnumerator
        Implements IEnumerable(Of FindStreamData)

        Public Property FilePath As String

        <SecuritySafeCritical>
        Public Function GetEnumerator() As IEnumerator(Of FindStreamData) Implements IEnumerable(Of FindStreamData).GetEnumerator
            Return New Enumerator(_FilePath)
        End Function

        <SecuritySafeCritical>
        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return GetEnumerator()
        End Function

        Public Sub New(FilePath As String)
            _FilePath = FilePath
        End Sub

        Public NotInheritable Class Enumerator
            Implements IEnumerator(Of FindStreamData)

            Public ReadOnly Property FilePath As String

            Public ReadOnly Property SafeHandle As SafeFindHandle

            Private _current As FindStreamData

            Public Sub New(FilePath As String)
                _FilePath = FilePath
            End Sub

            Public ReadOnly Property Current As FindStreamData Implements IEnumerator(Of FindStreamData).Current
                <SecuritySafeCritical>
                Get
                    If disposedValue Then
                        Throw New ObjectDisposedException("FileStreamsEnumerator.Enumerator")
                    End If

                    Return _current
                End Get
            End Property

            Private ReadOnly Property IEnumerator_Current As Object Implements IEnumerator.Current
                <SecuritySafeCritical>
                Get
                    Return Current
                End Get
            End Property

            <SecuritySafeCritical>
            Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext

                If disposedValue Then
                    Throw New ObjectDisposedException("FileStreamsEnumerator.Enumerator")
                End If

                If _SafeHandle Is Nothing Then
                    _SafeHandle = FindFirstStream(_FilePath, 0, _current, 0)
                    If Not _SafeHandle.IsInvalid Then
                        Return True
                    ElseIf Marshal.GetLastWin32Error() = ERROR_HANDLE_EOF Then
                        Return False
                    Else
                        Throw New Win32Exception
                    End If
                Else
                    If FindNextStream(_SafeHandle, _current) Then
                        Return True
                    ElseIf Marshal.GetLastWin32Error() = ERROR_HANDLE_EOF Then
                        Return False
                    Else
                        Throw New Win32Exception
                    End If
                End If

            End Function

            <SecuritySafeCritical>
            Private Sub Reset() Implements IEnumerator.Reset
                Throw New NotImplementedException
            End Sub

#Region "IDisposable Support"
            Private disposedValue As Boolean ' To detect redundant calls

            ' IDisposable
            Private Sub Dispose(disposing As Boolean)
                If Not Me.disposedValue Then
                    If disposing Then
                        ' TODO: dispose managed state (managed objects).

                        _SafeHandle?.Dispose()
                    End If

                    ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                    _SafeHandle = Nothing

                    ' TODO: set large fields to null.
                    _current = Nothing
                End If
                Me.disposedValue = True
            End Sub

            '' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
            '<SecuritySafeCritical>
            'Protected Overrides Sub Finalize()
            '    ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            '    Dispose(False)
            '    MyBase.Finalize()
            'End Sub

            ' This code added by Visual Basic to correctly implement the disposable pattern.
            <SecuritySafeCritical>
            Public Sub Dispose() Implements IDisposable.Dispose
                ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
                Dispose(True)
                GC.SuppressFinalize(Me)
            End Sub
#End Region

        End Class

    End Class

End Namespace
