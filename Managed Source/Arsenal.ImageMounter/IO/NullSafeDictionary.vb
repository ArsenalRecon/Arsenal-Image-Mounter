#Disable Warning IDE0079 ' Remove unnecessary suppression

Imports System.Runtime.InteropServices

Namespace IO

    ''' <summary>
    ''' An extension to Dictionary(Of TKey, TValue) that returns a
    ''' default item for non-existing keys
    ''' </summary>
    <ComVisible(False)>
    Public MustInherit Class NullSafeDictionary(Of TKey, TValue)
        Implements IDictionary(Of TKey, TValue)

        Private ReadOnly m_Dictionary As Dictionary(Of TKey, TValue)

        ''' <summary>Gets a value that is returned as item for non-existing
        ''' keys in dictionary</summary>
        Protected MustOverride Function GetDefaultValue(Key As TKey) As TValue

        Public ReadOnly Property SyncRoot As Object
            Get
                Return DirectCast(m_Dictionary, ICollection).SyncRoot
            End Get
        End Property

        ''' <summary>
        ''' Creates a new NullSafeDictionary object
        ''' </summary>
        Public Sub New()
            m_Dictionary = New Dictionary(Of TKey, TValue)
        End Sub

        ''' <summary>
        ''' Creates a new NullSafeDictionary object
        ''' </summary>
        Public Sub New(Comparer As IEqualityComparer(Of TKey))
            m_Dictionary = New Dictionary(Of TKey, TValue)(Comparer)
        End Sub

        ''' <summary>
        ''' Gets or sets the item for a key in dictionary. If no item exists for key, the default
        ''' value for this SafeDictionary is returned
        ''' </summary>
        ''' <param name="key"></param>
        Default Public Property Item(key As TKey) As TValue Implements IDictionary(Of TKey, TValue).Item
            Get
                SyncLock SyncRoot
                    If m_Dictionary.TryGetValue(key, Item) Then
                        Return Item
                    Else
                        Return GetDefaultValue(key)
                    End If
                End SyncLock
            End Get
            Set
                SyncLock SyncRoot
                    If m_Dictionary.ContainsKey(key) Then
                        m_Dictionary(key) = Value
                    Else
                        m_Dictionary.Add(key, Value)
                    End If
                End SyncLock
            End Set
        End Property

        Private Sub ICollection_Add(item As KeyValuePair(Of TKey, TValue)) Implements ICollection(Of KeyValuePair(Of TKey, TValue)).Add
            DirectCast(m_Dictionary, ICollection(Of KeyValuePair(Of TKey, TValue))).Add(item)
        End Sub

        Public Sub Clear() Implements ICollection(Of KeyValuePair(Of TKey, TValue)).Clear
            m_Dictionary.Clear()
        End Sub

#Disable Warning CA1033 ' Interface methods should be callable by child types
        Private Function ICollection_Contains(item As KeyValuePair(Of TKey, TValue)) As Boolean Implements ICollection(Of KeyValuePair(Of TKey, TValue)).Contains
            Return DirectCast(m_Dictionary, ICollection(Of KeyValuePair(Of TKey, TValue))).Contains(item)
        End Function

        Private Sub ICollection_CopyTo(array() As KeyValuePair(Of TKey, TValue), arrayIndex As Integer) Implements ICollection(Of KeyValuePair(Of TKey, TValue)).CopyTo
            DirectCast(m_Dictionary, ICollection(Of KeyValuePair(Of TKey, TValue))).CopyTo(array, arrayIndex)
        End Sub
#Enable Warning CA1033 ' Interface methods should be callable by child types

        Public ReadOnly Property Count As Integer Implements ICollection(Of KeyValuePair(Of TKey, TValue)).Count
            Get
                Return m_Dictionary.Count
            End Get
        End Property

        Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of KeyValuePair(Of TKey, TValue)).IsReadOnly
            Get
                Return False
            End Get
        End Property

        Private Function ICollection_Remove(item As KeyValuePair(Of TKey, TValue)) As Boolean Implements ICollection(Of KeyValuePair(Of TKey, TValue)).Remove
            Return DirectCast(m_Dictionary, ICollection(Of KeyValuePair(Of TKey, TValue))).Remove(item)
        End Function

        Public Sub Add(key As TKey, value As TValue) Implements IDictionary(Of TKey, TValue).Add
            m_Dictionary.Add(key, value)
        End Sub

        Public Function ContainsKey(key As TKey) As Boolean Implements IDictionary(Of TKey, TValue).ContainsKey
            Return m_Dictionary.ContainsKey(key)
        End Function

        Public ReadOnly Property Keys As ICollection(Of TKey) Implements IDictionary(Of TKey, TValue).Keys
            Get
                Return m_Dictionary.Keys
            End Get
        End Property

        Public Function Remove(key As TKey) As Boolean Implements IDictionary(Of TKey, TValue).Remove
            Return m_Dictionary.Remove(key)
        End Function

        Public Function TryGetValue(key As TKey, ByRef value As TValue) As Boolean Implements IDictionary(Of TKey, TValue).TryGetValue
            Return m_Dictionary.TryGetValue(key, value)
        End Function

        Public ReadOnly Property Values As ICollection(Of TValue) Implements IDictionary(Of TKey, TValue).Values
            Get
                Return m_Dictionary.Values
            End Get
        End Property

        Private Function ICollection_GetEnumerator() As IEnumerator(Of KeyValuePair(Of TKey, TValue)) Implements IEnumerable(Of KeyValuePair(Of TKey, TValue)).GetEnumerator
            Return DirectCast(m_Dictionary, ICollection(Of KeyValuePair(Of TKey, TValue))).GetEnumerator()
        End Function

        Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return m_Dictionary.GetEnumerator()
        End Function
    End Class

    Public Class NullSafeStringDictionary
        Inherits NullSafeDictionary(Of String, String)

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(Comparer As IEqualityComparer(Of String))
            MyBase.New(Comparer)
        End Sub

        Protected Overrides Function GetDefaultValue(Key As String) As String
            Return String.Empty
        End Function
    End Class

End Namespace
