Imports System.Management.Automation
Imports System.Reflection
Imports System.Linq.Expressions

Namespace PSDisk

    Public Class PSPartitionParser
        Implements IDisposable

        Public Class PartitionState

            Public Property PartitionNumber As UInteger

            Public Property DriveLetter As Char

            Public Property IsOffline As Boolean

            Public Property Offset As ULong

            Public Property Size As ULong

            Public Sub New()

            End Sub

            Private Sub New(obj As PSObject)
                FieldAssigner(Of PartitionState).AssignFieldsFromPSObject(Me, obj)

            End Sub

            Friend Shared Function FromPSObject(obj As PSObject) As PartitionState
                Return New PartitionState(obj)
            End Function

        End Class

        Private PS As PowerShell

        Public Sub New()

            PS = PowerShell.Create(RunspaceMode.NewRunspace)

            PS.AddCommand("Get-Partition")

        End Sub

        Public Sub New(disknumber As UInteger)
            Me.New()

            PS.AddParameter("DiskNumber", disknumber)

        End Sub

        Public Function GetPartitionState() As IEnumerable(Of PartitionState)

            Return PS.Invoke().Select(AddressOf PartitionState.FromPSObject)

        End Function

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ''' <remarks>
        ''' This method may fail to load if PowerShell 3.0 is not installed. Therefore,
        ''' that code cannot be placed in Dispose() method and calling this method needs
        ''' to be protected in a Try/Catch.
        ''' </remarks>
        Private Sub DisposePS()

            If PS IsNot Nothing Then
                PS.Dispose()
            End If

        End Sub

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                Try
                    DisposePS()

                Catch

                End Try

                ' TODO: set large fields to null.
            End If
            Me.disposedValue = True
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

End Namespace
