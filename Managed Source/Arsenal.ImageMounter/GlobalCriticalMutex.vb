''''' API.vb
''''' API for manipulating flag values, issuing SCSI bus rescans and similar
''''' tasks.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Threading
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32

Public NotInheritable Class GlobalCriticalMutex
    Implements IDisposable

    Private Const GlobalCriticalSectionMutexName = "Global\AIMCriticalOperation"

    Private mutex As Mutex

    Public ReadOnly Property WasAbandoned As Boolean

    Public Sub New()

        Dim createdNew As Boolean = Nothing

        mutex = New Mutex(initiallyOwned:=True, name:=GlobalCriticalSectionMutexName, createdNew:=createdNew)

        Try
            If Not createdNew Then
                mutex.WaitOne()
            End If

        Catch ex As AbandonedMutexException
            _WasAbandoned = True

        Catch ex As Exception
            mutex.Dispose()

            Throw New Exception("Error entering global critical section for Arsenal Image Mounter driver", ex)

        End Try

    End Sub

    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Private Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                mutex.ReleaseMutex()
                mutex.Dispose()
            End If

            ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

            ' TODO: set large fields to null.
            mutex = Nothing
        End If
        disposedValue = True
    End Sub

    ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    'Protected Overrides Sub Finalize()
    '    ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
    '    Dispose(False)
    '    MyBase.Finalize()
    'End Sub

    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(True)
        ' TODO: uncomment the following line if Finalize() is overridden above.
        'GC.SuppressFinalize(Me)
    End Sub

End Class
