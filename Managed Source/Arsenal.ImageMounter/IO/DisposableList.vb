Namespace IO

    ''' <summary>
    ''' A System.Collections.Generic.List(Of T) extended with IDisposable implementation that disposes each
    ''' object in the list when the list is disposed.
    ''' </summary>
    <ComVisible(False)>
    Public Class DisposableList
        Inherits DisposableList(Of IDisposable)

        Public Sub New()
            MyBase.New()

        End Sub

        Public Sub New(capacity As Integer)
            MyBase.New(capacity)

        End Sub

        Public Sub New(collection As IEnumerable(Of IDisposable))
            MyBase.New(collection)

        End Sub

    End Class

    ''' <summary>
    ''' A System.Collections.Generic.List(Of T) extended with IDisposable implementation that disposes each
    ''' object in the list when the list is disposed.
    ''' </summary>
    ''' <typeparam name="T">Type of elements in list. Type needs to implement IDisposable interface.</typeparam>
    <ComVisible(False)>
    Public Class DisposableList(Of T As IDisposable)
        Inherits List(Of T)

        Implements IDisposable

        Private disposedValue As Boolean    ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TODO: free managed resources when explicitly called
                    For Each obj In Me
                        obj.Dispose()
                    Next
                End If
            End If
            Me.disposedValue = True

            ' TODO: free shared unmanaged resources

            Clear()
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        Protected Overrides Sub Finalize()
            Dispose(False)
            MyBase.Finalize()
        End Sub

        Public Sub New()
            MyBase.New()

        End Sub

        Public Sub New(capacity As Integer)
            MyBase.New(capacity)

        End Sub

        Public Sub New(collection As IEnumerable(Of T))
            MyBase.New(collection)

        End Sub

    End Class

End Namespace
