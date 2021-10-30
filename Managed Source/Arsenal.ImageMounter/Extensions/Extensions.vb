Imports System.ComponentModel
Imports System.Globalization
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32

Namespace Extensions

    Public Module ExtensionMethods

        <Extension>
        Public Iterator Function EnumerateMessages(ex As Exception) As IEnumerable(Of String)

            While ex IsNot Nothing
                If TypeOf ex Is TargetInvocationException Then

                ElseIf TypeOf ex Is AggregateException Then

                    Dim agex = DirectCast(ex, AggregateException)
                    For Each msg In agex.InnerExceptions.SelectMany(AddressOf EnumerateMessages)
                        Yield msg
                    Next
                    Return

                ElseIf TypeOf ex Is ReflectionTypeLoadException Then

                    Dim ldex = DirectCast(ex, ReflectionTypeLoadException)
                    Yield ex.Message
                    For Each msg In ldex.LoaderExceptions.SelectMany(AddressOf EnumerateMessages)
                        Yield msg
                    Next

                Else

                    Yield ex.Message

                End If

                ex = ex.InnerException
            End While

        End Function

        <Extension>
        Public Function JoinMessages(ex As Exception) As String

            Return ex?.JoinMessages(Environment.NewLine & Environment.NewLine)

        End Function

        <Extension>
        Public Function JoinMessages(ex As Exception, separator As String) As String

            Return String.Join(separator, ex?.EnumerateMessages())

        End Function

        <Extension>
        Public Iterator Function Enumerate(ex As Exception) As IEnumerable(Of Exception)

            While ex IsNot Nothing
                Yield ex

                If TypeOf ex Is AggregateException Then
                    Dim agex = DirectCast(ex, AggregateException)
                    For Each inner In agex.InnerExceptions.SelectMany(AddressOf Enumerate)
                        Yield inner
                    Next

                    Return
                End If

                ex = ex.InnerException
            End While

        End Function

        <Extension>
        Public Function Join(strings As IEnumerable(Of String), separator As String) As String

            Return String.Join(separator, strings)

        End Function

        <Extension>
        Public Function Join(strings As String(), separator As String) As String

            Return String.Join(separator, strings)

        End Function

        <Extension>
        Public Function Concat(strings As IEnumerable(Of String)) As String

            Return String.Concat(strings)

        End Function

        <Extension>
        Public Function Concat(strings As String()) As String

            Return String.Concat(strings)

        End Function

        <Extension>
        Public Sub QueueDispose(instance As IDisposable)

            ThreadPool.QueueUserWorkItem(Sub() instance.Dispose())

        End Sub

        <Extension>
        Public Sub SetValueSafe(Of T As Class)(RegKey As RegistryKey, name As String, value As T)

            If value Is Nothing Then

                RegKey?.DeleteValue(name, throwOnMissingValue:=False)

            Else

                RegKey?.SetValue(name, value)

            End If

        End Sub

        <Extension>
        Public Sub SetValueSafe(Of T As Structure)(RegKey As RegistryKey, name As String, value As T?)

            If value Is Nothing Then

                RegKey?.DeleteValue(name, throwOnMissingValue:=False)

            Else

                RegKey?.SetValue(name, value)

            End If

        End Sub

        <Extension>
        Public Sub SetValueSafe(Of T As Class)(RegKey As RegistryKey, name As String, value As T, valueKind As RegistryValueKind)

            If value Is Nothing Then

                RegKey?.DeleteValue(name, throwOnMissingValue:=False)

            Else

                RegKey?.SetValue(name, value, valueKind)

            End If

        End Sub

        <Extension>
        Public Sub SetValueSafe(Of T As Structure)(RegKey As RegistryKey, name As String, value As T?, valueKind As RegistryValueKind)

            If value Is Nothing Then

                RegKey?.DeleteValue(name, throwOnMissingValue:=False)

            Else

                RegKey?.SetValue(name, value, valueKind)

            End If

        End Sub

        <Extension>
        Public Function GetSynchronizationContext(syncobj As ISynchronizeInvoke) As SynchronizationContext
            Return DirectCast(syncobj.NullCheck(NameOf(syncobj)).Invoke(New Func(Of SynchronizationContext)(Function() SynchronizationContext.Current), Nothing), SynchronizationContext)
        End Function

        <Extension>
        Public Function NullCheck(Of T As Class)(instance As T, param As String) As T

            If instance Is Nothing Then
                Throw New ArgumentNullException(param)
            End If

            Return instance

        End Function

        <Extension>
        Public Function ToMembersString(o As Object) As String

            If o Is Nothing Then
                Return "{null}"
            Else
                Return TryCast(GetType(Reflection.MembersStringParser(Of )).
                    MakeGenericType(o.GetType()).
                    GetMethod("ToString", BindingFlags.Public Or BindingFlags.Static).
                    Invoke(Nothing, {o}), String)
            End If

        End Function

        <Extension>
        Public Function ToMembersString(Of T As Structure)(o As T) As String

            Return Reflection.MembersStringParser(Of T).ToString(o)

        End Function

        <Extension>
        Public Function ToHexString(bytes As ICollection(Of Byte)) As String

            If bytes Is Nothing Then
                Return Nothing
            End If

            Dim valuestr As New StringBuilder(bytes.Count << 1)
            For Each b In bytes
                valuestr.Append(b.ToString("x2"))
            Next

            Return valuestr.ToString()

        End Function

        <Extension>
        Public Function ToHexString(bytes As ICollection(Of Byte), offset As Integer, count As Integer) As String

            If bytes Is Nothing OrElse offset > bytes.Count OrElse offset + count > bytes.Count Then
                Return Nothing
            End If

            Dim valuestr As New StringBuilder(count << 1)
            For i = offset To offset + count - 1
                valuestr.Append(bytes(i).ToString("x2"))
            Next

            Return valuestr.ToString()

        End Function

        <Extension>
        Public Function ToHexString(bytes As IEnumerable(Of Byte)) As String

            If bytes Is Nothing Then
                Return Nothing
            End If

            Dim valuestr As New StringBuilder
            For Each b In bytes
                valuestr.Append(b.ToString("x2"))
            Next

            Return valuestr.ToString()

        End Function

        Public Function ParseHexString(str As String) As Byte()

            Dim bytes = New Byte(0 To (str.Length >> 1) - 1) {}

            For i = 0 To bytes.Length - 1

                bytes(i) = Byte.Parse(str.Substring(i << 1, 2), NumberStyles.HexNumber)

            Next

            Return bytes

        End Function

        Public Iterator Function ParseHexString(str As IEnumerable(Of Char)) As IEnumerable(Of Byte)

            Dim buffer(0 To 1) As Char

            For Each c In str

                If buffer(0) = Nothing Then
                    buffer(0) = c
                Else
                    buffer(1) = c
                    Yield Byte.Parse(New String(buffer), NumberStyles.HexNumber)
                    Array.Clear(buffer, 0, 2)
                End If

            Next

        End Function

        Public Function ParseHexString(str As String, offset As Integer, count As Integer) As Byte()

            Dim bytes = New Byte(0 To (count >> 1) - 1) {}

            For i = 0 To count - 1

                bytes(i) = Byte.Parse(str.Substring((i + offset) << 1, 2), NumberStyles.HexNumber)

            Next

            Return bytes

        End Function

        <Extension>
        Public Function GetTopMostOwner(form As Form) As Form

            While form?.Owner IsNot Nothing
                form = form.Owner
            End While

            Return form

        End Function

        <Extension>
        Public Function IsBufferZero(buffer As Byte()) As Boolean
            Return NativeFileIO.UnsafeNativeMethods.RtlCompareMemoryUlong(buffer, New IntPtr(buffer.LongLength), 0).ToInt64() = buffer.LongLength
        End Function

    End Module

End Namespace
