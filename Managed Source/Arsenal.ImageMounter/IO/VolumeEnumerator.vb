Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Runtime.Versioning
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO.NativeConstants
Imports Arsenal.ImageMounter.IO.NativeFileIO
Imports Arsenal.ImageMounter.IO.NativeFileIO.UnsafeNativeMethods

Namespace IO

    <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
    Public Class VolumeEnumerator
        Implements IEnumerable(Of String)

        Public Function GetEnumerator() As IEnumerator(Of String) Implements IEnumerable(Of String).GetEnumerator
            Return New Enumerator
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return GetEnumerator()
        End Function

        Private Class Enumerator
            Implements IEnumerator(Of String)

            Public ReadOnly Property SafeHandle As SafeFindVolumeHandle

            Private _sb(0 To 49) As Char

            Public ReadOnly Property Current As String Implements IEnumerator(Of String).Current
                Get
                    If disposedValue Then
                        Throw New ObjectDisposedException("VolumeEnumerator.Enumerator")
                    End If

                    Return _sb.AsSpan().ReadNullTerminatedUnicodeString()
                End Get
            End Property

            Private ReadOnly Property IEnumerator_Current As Object Implements IEnumerator.Current
                Get
                    Return Current
                End Get
            End Property

            Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext

                If disposedValue Then
                    Throw New ObjectDisposedException("VolumeEnumerator.Enumerator")
                End If

                If _SafeHandle Is Nothing Then
                    _SafeHandle = FindFirstVolume(_sb(0), _sb.Length)
                    If Not _SafeHandle.IsInvalid Then
                        Return True
                    ElseIf Marshal.GetLastWin32Error() = ERROR_NO_MORE_FILES Then
                        Return False
                    Else
                        Throw New Win32Exception
                    End If
                Else
                    If FindNextVolume(_SafeHandle, _sb(0), _sb.Length) Then
                        Return True
                    ElseIf Marshal.GetLastWin32Error() = ERROR_NO_MORE_FILES Then
                        Return False
                    Else
                        Throw New Win32Exception
                    End If
                End If

            End Function

            Private Sub Reset() Implements IEnumerator.Reset
                Throw New NotImplementedException
            End Sub

#Region "IDisposable Support"
            Private disposedValue As Boolean ' To detect redundant calls

            ' IDisposable
            Protected Overridable Sub Dispose(disposing As Boolean)
                If Not disposedValue Then
                    If disposing Then
                        ' TODO: dispose managed state (managed objects).
                        _SafeHandle?.Dispose()
                    End If

                    ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                    _SafeHandle = Nothing

                    ' TODO: set large fields to null.
                    _sb = Nothing
                End If
                disposedValue = True
            End Sub

            ' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
            Protected Overrides Sub Finalize()
                ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
                Dispose(False)
                MyBase.Finalize()
            End Sub

            ' This code added by Visual Basic to correctly implement the disposable pattern.
            Public Sub Dispose() Implements IDisposable.Dispose
                ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
                Dispose(True)
                GC.SuppressFinalize(Me)
            End Sub
#End Region

        End Class

    End Class

End Namespace
