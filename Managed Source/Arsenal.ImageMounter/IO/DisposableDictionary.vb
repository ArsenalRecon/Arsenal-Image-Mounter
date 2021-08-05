Imports System.Runtime.Serialization

Namespace IO

    ''' <summary>
    ''' A System.Collections.Generic.Dictionary(Of TKey, TValue) extended with IDisposable implementation that disposes each
    ''' value object in the dictionary when the dictionary is disposed.
    ''' </summary>
    ''' <typeparam name="TKey"></typeparam>
    ''' <typeparam name="TValue"></typeparam>
    <Serializable>
    Public Class DisposableDictionary(Of TKey, TValue As IDisposable)
        Inherits Dictionary(Of TKey, TValue)
        Implements IDisposable

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)

            If disposing Then
                '' Dispose each object in list
                For Each value In Values
                    value?.Dispose()
                Next
            End If

            '' Clear list
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

        Public Sub New(dictionary As IDictionary(Of TKey, TValue))
            MyBase.New(dictionary)

        End Sub

        Public Sub New(comparer As IEqualityComparer(Of TKey))
            MyBase.New(comparer)

        End Sub

        Public Sub New(dictionary As IDictionary(Of TKey, TValue), comparer As IEqualityComparer(Of TKey))
            MyBase.New(dictionary, comparer)

        End Sub

        Public Sub New(capacity As Integer, comparer As IEqualityComparer(Of TKey))
            MyBase.New(capacity, comparer)

        End Sub

#If Not NET_CORE Then
        Public Overrides Sub GetObjectData(info As SerializationInfo, context As StreamingContext)
            MyBase.GetObjectData(info, context)
        End Sub

        Protected Sub New(si As SerializationInfo, context As StreamingContext)
            MyBase.New(si, context)
        End Sub
#End If

    End Class

End Namespace
