Imports System.Runtime.InteropServices.Marshal

Namespace IO

    ''' <summary>
    ''' Pins a value object for unmanaged use.
    ''' </summary>
    <ComVisible(False)>
    Public Class PinnedBuffer
        Inherits SafeBuffer

        Protected _gchandle As GCHandle

        Protected Sub New()
            MyBase.New(ownsHandle:=True)

        End Sub

        Public ReadOnly Property Offset As Integer
            Get
                If IntPtr.Size > 4 Then
                    Return CInt(DangerousGetHandle().ToInt64() - _gchandle.AddrOfPinnedObject().ToInt64())
                Else
                    Return DangerousGetHandle().ToInt32() - _gchandle.AddrOfPinnedObject().ToInt32()
                End If
            End Get
        End Property

        ''' <summary>
        ''' Initializes a new instance with an existing type T object and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="obj">Existing object to marshal to unmanaged memory.</param>
        Public Shared Function Create(obj As String) As PinnedString
            Return New PinnedString(obj)
        End Function

        ''' <summary>
        ''' Initializes a new instance with an existing type T array and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="obj">Existing object to marshal to unmanaged memory.</param>
        Public Shared Function Create(Of T As Structure)(obj As T()) As PinnedBuffer(Of T)
            Return New PinnedBuffer(Of T)(obj)
        End Function

        Public Shared Function PtrToStructure(Of T)(ptr As IntPtr) As T
            Return DirectCast(Marshal.PtrToStructure(ptr, GetType(T)), T)
        End Function

        Public Shared Sub DestroyStructure(Of T)(ptr As IntPtr)
            Marshal.DestroyStructure(ptr, GetType(T))
        End Sub

        Public Shared Function Serialize(Of T As Structure)(obj As T) As Byte()
            Using pinned As New PinnedBuffer(Of Byte)(SizeOf(GetType(T)))
                pinned.Write(0, obj)
                Return pinned.Target
            End Using
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

            Initialize(existing.ByteLength)

            _gchandle = GCHandle.Alloc(existing._gchandle.Target, GCHandleType.Pinned)

            SetHandle(_gchandle.AddrOfPinnedObject() + existing.Offset + offset)

        End Sub

        ''' <summary>
        ''' Initializes a new instance with an existing object and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="obj">Existing object to pin in memory.</param>
        ''' <param name="toalObjectSize">Total number of bytes used by obj in unmanaged memory</param>
        ''' <param name="byteOffset">Byte offset into unmanaged memory where this instance should start</param>
        Public Sub New(obj As Object, toalObjectSize As Integer, byteOffset As Integer)
            MyClass.New()

            If byteOffset > toalObjectSize Then
                Throw New ArgumentOutOfRangeException("Argument offset must be within total object size", "offset")
            End If

            Initialize(CULng(toalObjectSize - byteOffset))

            _gchandle = GCHandle.Alloc(obj, GCHandleType.Pinned)

            SetHandle(_gchandle.AddrOfPinnedObject() + byteOffset)

        End Sub

        ''' <summary>
        ''' Initializes a new instance with an existing object and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="obj">Existing object to pin in memory.</param>
        ''' <param name="size">Number of bytes in unmanaged memory</param>
        Public Sub New(obj As Object, size As Integer)
            MyClass.New()

            Initialize(CULng(size))

            _gchandle = GCHandle.Alloc(obj, GCHandleType.Pinned)

            SetHandle(_gchandle.AddrOfPinnedObject())

        End Sub

        Protected Overrides Function ReleaseHandle() As Boolean
            _gchandle.Free()
            Return True
        End Function

        Public ReadOnly Property Target As Object
            Get
                Return _gchandle.Target
            End Get
        End Property

        Public Shared Operator +(existing As PinnedBuffer, offset As Integer) As PinnedBuffer

            Return New PinnedBuffer(existing, offset)

        End Operator

        Public Shared Operator -(existing As PinnedBuffer, offset As Integer) As PinnedBuffer

            Return New PinnedBuffer(existing, -offset)

        End Operator

        Public Overrides Function ToString() As String

            If _gchandle.IsAllocated Then
                Return _gchandle.Target.ToString()
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
                Return DirectCast(_gchandle.Target, String)
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
        ''' <param name="obj">Existing object to marshal to unmanaged memory.</param>
        Public Sub New(obj As T())
            MyBase.New(obj, Buffer.ByteLength(obj))

        End Sub

        ''' <summary>
        ''' Initializes a new instance with an existing type T array and pins memory
        ''' position.
        ''' </summary>
        ''' <param name="obj">Existing object to marshal to unmanaged memory.</param>
        Public Sub New(obj As T(), arrayOffset As Integer, arrayItems As Integer)
            MyBase.New(obj, MakeTotalByteSize(obj, arrayOffset, arrayItems), (Buffer.ByteLength(obj) \ obj.Length) * arrayOffset)

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
        Public Overloads ReadOnly Property Target As T()
            Get
                Return DirectCast(_gchandle.Target, T())
            End Get
        End Property

    End Class

End Namespace
