
''''' Enums.vb
''''' .NET definitions of the same enums and structures as in imdproxy.h
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Diagnostics.CodeAnalysis
Imports System.Runtime.InteropServices

#Disable Warning CA1712

Public Enum IMDPROXY_REQ As ULong
    IMDPROXY_REQ_NULL
    IMDPROXY_REQ_INFO
    IMDPROXY_REQ_READ
    IMDPROXY_REQ_WRITE
    IMDPROXY_REQ_CONNECT
    IMDPROXY_REQ_CLOSE
    IMDPROXY_REQ_UNMAP
    IMDPROXY_REQ_ZERO
    IMDPROXY_REQ_SCSI
    IMDPROXY_REQ_SHARED
End Enum

<Flags>
Public Enum IMDPROXY_FLAGS As ULong
    IMDPROXY_FLAG_NONE = 0UL
    IMDPROXY_FLAG_RO = 1UL
    IMDPROXY_FLAG_SUPPORTS_UNMAP = &H2UL '' Unmap / TRIM ranges
    IMDPROXY_FLAG_SUPPORTS_ZERO = &H4UL '' Zero - fill ranges
    IMDPROXY_FLAG_SUPPORTS_SCSI = &H8UL '' SCSI SRB operations
    IMDPROXY_FLAG_SUPPORTS_SHARED = &H10UL '' Shared image access With reservations
End Enum

''' <summary>
''' Constants used in connection with Devio proxy communication.
''' </summary>
Public MustInherit Class IMDPROXY_CONSTANTS
    Private Sub New()
    End Sub

    ''' <summary>
    ''' Header size when communicating using a shared memory object.
    ''' </summary>
    Public Const IMDPROXY_HEADER_SIZE As Integer = 4096

    ''' <summary>
    ''' Default required alignment for I/O operations.
    ''' </summary>
    Public Const REQUIRED_ALIGNMENT As Integer = 512

    Public Const RESERVATION_KEY_ANY As ULong = ULong.MaxValue

End Class

#Disable Warning IDE1006 ' Naming Styles

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_CONNECT_REQ
    Public Property request_code As IMDPROXY_REQ
    Public Property flags As ULong
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_CONNECT_RESP
    Public Property error_code As ULong
    Public Property object_ptr As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_INFO_RESP
    Public Property file_size As ULong
    Public Property req_alignment As ULong
    Public Property flags As IMDPROXY_FLAGS
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_READ_REQ
    Public Property request_code As IMDPROXY_REQ
    Public Property offset As ULong
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_READ_RESP
    Public Property errorno As ULong
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_WRITE_REQ
    Public Property request_code As IMDPROXY_REQ
    Public Property offset As ULong
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_WRITE_RESP
    Public Property errorno As ULong
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_UNMAP_REQ
    Public Property request_code As IMDPROXY_REQ
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_UNMAP_RESP
    Public Property errorno As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_ZERO_REQ
    Public Property request_code As IMDPROXY_REQ
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_ZERO_RESP
    Public Property errorno As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_SCSI_REQ
    Public Property request_code As IMDPROXY_REQ
    <MarshalAs(UnmanagedType.ByValArray, SizeConst:=16)>
    Private _cdb As Byte() 'byte[16]
    Public Property length As ULong

    Public Property Cdb As Byte()
        Get
            Return _cdb
        End Get
        Set(value As Byte())
            _cdb = value
        End Set
    End Property
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_SCSI_RESP
    Public Property errorno As ULong
    Public Property length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_SHARED_REQ
    Public Property request_code As IMDPROXY_REQ
    Public Property operation_code As IMDPROXY_SHARED_OP_CODE
    Public Property reserve_scope As ULong
    Public Property reserve_type As ULong
    Public Property existing_reservation_key As ULong
    Public Property current_channel_key As ULong
    Public Property operation_channel_key As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_SHARED_RESP
    Public Property errorno As IMDPROXY_SHARED_RESP_CODE
    Public Property unique_id As Guid
    Public Property channel_key As ULong
    Public Property generation As ULong
    Public Property reservation_key As ULong
    Public Property reservation_scope As ULong
    Public Property reservation_type As ULong
    Public Property length As ULong
End Structure

Public Enum IMDPROXY_SHARED_OP_CODE As ULong
    GetUniqueId
    ReadKeys
    Register
    ClearKeys
    Reserve
    Release
    Preempt
    RegisterIgnoreExisting
End Enum

Public Enum IMDPROXY_SHARED_RESP_CODE As ULong
    NoError
    ReservationCollision
    InvalidParameter
    IOError
End Enum

