Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.IO.NativeFileIO

Namespace Reflection

    Public MustInherit Class NativeLib

        Private Sub New()
        End Sub

#If NETSTANDARD OrElse NETCOREAPP Then

        Public Shared ReadOnly Property IsWindows As Boolean = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

        Public Shared Function GetProcAddress(hModule As IntPtr, procedureName As String, delegateType As Type) As [Delegate]

            Return Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(hModule, procedureName), delegateType)

        End Function

        Public Shared Function GetProcAddressNoThrow(hModule As IntPtr, procedureName As String, delegateType As Type) As [Delegate]

            Dim fptr As IntPtr

            If Not NativeLibrary.TryGetExport(hModule, procedureName, fptr) Then
                Return Nothing
            End If

            Return Marshal.GetDelegateForFunctionPointer(fptr, delegateType)

        End Function

        Public Shared Function GetProcAddress(moduleName As String, procedureName As String, delegateType As Type) As [Delegate]

            Dim hModule = NativeLibrary.Load(moduleName)

            Return Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(hModule, procedureName), delegateType)

        End Function

        Public Shared Function GetProcAddressNoThrow(moduleName As String, procedureName As String) As IntPtr

            Dim hModule As IntPtr
            If Not NativeLibrary.TryLoad(moduleName, hModule) Then
                Return Nothing
            End If

            Dim address As IntPtr

            If Not NativeLibrary.TryGetExport(hModule, procedureName, address) Then
                Return Nothing
            End If

            Return address

        End Function

#Else
        Public Shared ReadOnly Property IsWindows As Boolean = True

        Public Shared Function GetProcAddress(hModule As IntPtr, procedureName As String, delegateType As Type) As [Delegate]

            Return Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType)

        End Function

        Public Shared Function GetProcAddressNoThrow(hModule As IntPtr, procedureName As String, delegateType As Type) As [Delegate]

            Dim fptr = UnsafeNativeMethods.GetProcAddress(hModule, procedureName)

            If fptr = Nothing Then
                Return Nothing
            End If

            Return Marshal.GetDelegateForFunctionPointer(fptr, delegateType)

        End Function

        Public Shared Function GetProcAddress(moduleName As String, procedureName As String, delegateType As Type) As [Delegate]

            Dim hModule = Win32Try(UnsafeNativeMethods.LoadLibrary(moduleName))

            Return Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType)

        End Function

        Public Shared Function GetProcAddressNoThrow(moduleName As String, procedureName As String) As IntPtr

            Dim hModule = UnsafeNativeMethods.LoadLibrary(moduleName)

            If hModule = Nothing Then
                Return Nothing
            End If

            Return UnsafeNativeMethods.GetProcAddress(hModule, procedureName)

        End Function

#End If

        Public Shared Function GetProcAddressNoThrow(moduleName As String, procedureName As String, delegateType As Type) As [Delegate]

            Dim fptr = GetProcAddressNoThrow(moduleName, procedureName)

            If fptr = Nothing Then
                Return Nothing
            End If

            Return Marshal.GetDelegateForFunctionPointer(fptr, delegateType)

        End Function


    End Class


End Namespace
