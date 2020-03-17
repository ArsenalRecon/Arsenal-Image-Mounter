Imports System.Diagnostics.CodeAnalysis
Imports System.Runtime.InteropServices.Marshal
Imports Arsenal.ImageMounter.Extensions

Namespace IO

    ''' <summary>
    ''' Pins a value object for unmanaged use.
    ''' </summary>
    <ComVisible(False)>
    Public Class PinnedBuffer
        Inherits SafeBuffer

        Protected ReadOnly Property GCHandle As GCHandle

        Protected Sub New()
            MyBase.New(ownsHandle:=True)

        End Sub

        Public ReadOnly Property Offset As Integer
            Get
                If IntPtr.Size > 4 Then
                    Return CInt(DangerousGetHandle().ToInt64() - _GCHandle.AddrOfPinnedObject().ToInt64())
                Else
                    Return DangerousGetHandle().ToInt32() - _GCHandle.AddrOfPinnedObject().ToInt32()
                End If
            End Get
        End Property

        ''' <summary>
        ''' Initializes a new instance with an existing type T object and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="instance">Existing object to marshal to unmanaged memory.</param>
        Public Shared Function Create(instance As String) As PinnedString
            Return New PinnedString(instance)
        End Function

        ''' <summary>
        ''' Initializes a new instance with an existing type T array and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="instance">Existing object to marshal to unmanaged memory.</param>
        Public Shared Function Create(Of T As Structure)(instance As T()) As PinnedBuffer(Of T)
            Return New PinnedBuffer(Of T)(instance)
        End Function

        Public Shared Function PtrToStructure(Of T)(address As IntPtr) As T
            Return DirectCast(Marshal.PtrToStructure(address, GetType(T)), T)
        End Function

        Public Shared Sub DestroyStructure(Of T)(address As IntPtr)
            Marshal.DestroyStructure(address, GetType(T))
        End Sub

        Public Shared Function Serialize(Of T As Structure)(instance As T) As PinnedBuffer(Of Byte)
            Dim pinned As New PinnedBuffer(Of Byte)(SizeOf(GetType(T)))
            pinned.Write(0, instance)
            Return pinned
        End Function

        Public Shared Function Deserialize(Of T As Structure)(buffer As Byte()) As T
            If buffer Is Nothing OrElse buffer.Length < SizeOf(GetType(T)) Then
                Throw New ArgumentException("Invalid input buffer", NameOf(buffer))
            End If

            Using pinned = Create(buffer)
                Return pinned.Read(Of T)(0)
            End Using

        End Function

        Public Sub New(existing As PinnedBuffer, offset As Integer)
            MyBase.New(ownsHandle:=True)

            Initialize(existing.NullCheck(NameOf(existing)).ByteLength)

            _GCHandle = GCHandle.Alloc(existing._GCHandle.Target, GCHandleType.Pinned)

            SetHandle(_GCHandle.AddrOfPinnedObject() + existing.Offset + offset)

        End Sub

        ''' <summary>
        ''' Initializes a new instance with an existing object and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="instance">Existing object to pin in memory.</param>
        ''' <param name="toalObjectSize">Total number of bytes used by obj in unmanaged memory</param>
        ''' <param name="byteOffset">Byte offset into unmanaged memory where this instance should start</param>
        Public Sub New(instance As Object, toalObjectSize As Integer, byteOffset As Integer)
            MyClass.New()

            If byteOffset > toalObjectSize Then
                Throw New ArgumentOutOfRangeException(NameOf(byteOffset), "Argument byteOffset must be within total object size")
            End If

            Initialize(CULng(toalObjectSize - byteOffset))

            _GCHandle = GCHandle.Alloc(instance, GCHandleType.Pinned)

            SetHandle(_GCHandle.AddrOfPinnedObject() + byteOffset)

        End Sub

        ''' <summary>
        ''' Initializes a new instance with an existing object and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="instance">Existing object to pin in memory.</param>
        ''' <param name="size">Number of bytes in unmanaged memory</param>
        Public Sub New(instance As Object, size As Integer)
            MyClass.New()

            Initialize(CULng(size))

            _GCHandle = GCHandle.Alloc(instance, GCHandleType.Pinned)

            SetHandle(_GCHandle.AddrOfPinnedObject())

        End Sub

        Protected Overrides Function ReleaseHandle() As Boolean
            _GCHandle.Free()
            Return True
        End Function

        Public ReadOnly Property Target As Object
            Get
                Return _GCHandle.Target
            End Get
        End Property

        Public Shared Operator +(existing As PinnedBuffer, offset As Integer) As PinnedBuffer

            Return New PinnedBuffer(existing, offset)

        End Operator

        Public Shared Operator -(existing As PinnedBuffer, offset As Integer) As PinnedBuffer

            Return New PinnedBuffer(existing, -offset)

        End Operator

        Public Overrides Function ToString() As String

            If _GCHandle.IsAllocated Then
                Return _GCHandle.Target.ToString()
            Else
                Return "{Unallocated}"
            End If

        End Function

    End Class

    ''' <summary>
    ''' Pins a managed string for unmanaged use.
    ''' </summary>
    <ComVisible(False)>
    Public Class PinnedString
        Inherits PinnedBuffer

        ''' <summary>
        ''' Initializes a new instance with an existing managed string and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="str">Managed string to pin in unmanaged memory.</param>
        Public Sub New(str As String)
            MyBase.New(str, str.Length << 1)

        End Sub

        ''' <summary>
        ''' Initializes a new instance with a new managed string and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="count">Size in characters of managed string to pin in unmanaged memory.</param>
        Public Sub New(count As Integer)
            MyBase.New(New String(New Char, count), count << 1)

        End Sub

        ''' <summary>
        ''' Returns managed object pinned by this instance.
        ''' </summary>
        Public Overloads ReadOnly Property Target As String
            Get
                Return DirectCast(GCHandle.Target, String)
            End Get
        End Property

        Public ReadOnly Property UnicodeString As NativeFileIO.UNICODE_STRING
            Get
                Return New NativeFileIO.UNICODE_STRING(handle, CUShort(ByteLength))
            End Get
        End Property

    End Class

    ''' <summary>
    ''' Pins an array of values for unmanaged use.
    ''' </summary>
    ''' <typeparam name="T">Type of elements in array.</typeparam>
    <ComVisible(False)>
    Public Class PinnedBuffer(Of T As Structure)
        Inherits PinnedBuffer

        ''' <summary>
        ''' Initializes a new instance with an new type T array and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="count">Number of items in new array.</param>
        Public Sub New(count As Integer)
            MyBase.New(New T(count - 1) {}, SizeOf(GetType(T)) * count)

        End Sub

        ''' <summary>
        ''' Initializes a new instance with an existing type T array and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="instance">Existing object to marshal to unmanaged memory.</param>
        Public Sub New(instance As T())
            MyBase.New(instance, Buffer.ByteLength(instance))

        End Sub

        ''' <summary>
        ''' Initializes a new instance with an existing type T array and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="instance">Existing object to marshal to unmanaged memory.</param>
        Public Sub New(instance As T(), arrayOffset As Integer, arrayItems As Integer)
            MyBase.New(instance, MakeTotalByteSize(instance, arrayOffset, arrayItems), (Buffer.ByteLength(instance) \ instance.Length) * arrayOffset)

        End Sub

        Private Shared Function MakeTotalByteSize(obj() As T, arrayOffset As Integer, arrayItems As Integer) As Integer

            If arrayOffset >= obj.Length OrElse
                arrayOffset + arrayItems > obj.Length Then

                Throw New IndexOutOfRangeException("arrayOffset and arrayItems must resolve to positions within the array")

            ElseIf arrayOffset + arrayItems < obj.Length Then

                Return (Buffer.ByteLength(obj) \ obj.Length) * (arrayOffset + arrayItems)

            Else

                Return Buffer.ByteLength(obj)

            End If

        End Function

        ''' <summary>
        ''' Returns associated object of this instance.
        ''' </summary>
        <SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification:="<Pending>")>
        Public Overloads ReadOnly Property Target As T()
            Get
                Return DirectCast(GCHandle.Target, T())
            End Get
        End Property

    End Class

End Namespace
