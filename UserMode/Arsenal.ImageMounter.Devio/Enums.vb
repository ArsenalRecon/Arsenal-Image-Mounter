
''''' Enums.vb
''''' .NET definitions of the same enums and structures as in imdproxy.h
''''' 
''''' Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Public Enum IMDPROXY_REQ As ULong
  IMDPROXY_REQ_NULL
  IMDPROXY_REQ_INFO
  IMDPROXY_REQ_READ
  IMDPROXY_REQ_WRITE
  IMDPROXY_REQ_CONNECT
  IMDPROXY_REQ_CLOSE
End Enum

Public Enum IMDPROXY_FLAGS As ULong
  IMDPROXY_FLAG_NONE = 0UL
  IMDPROXY_FLAG_RO = 1UL
End Enum

''' <summary>
''' Constants used in connection with Devio proxy communication.
''' </summary>
Public Class IMDPROXY_CONSTANTS
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
End Class

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_CONNECT_REQ
  Public request_code As IMDPROXY_REQ
  Public flags As ULong
  Public length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_CONNECT_RESP
  Public error_code As ULong
  Public object_ptr As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_INFO_RESP
  Public file_size As ULong
  Public req_alignment As ULong
  Public flags As IMDPROXY_FLAGS
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_READ_REQ
  Public request_code As IMDPROXY_REQ
  Public offset As ULong
  Public length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_READ_RESP
  Public errorno As ULong
  Public length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_WRITE_REQ
  Public request_code As IMDPROXY_REQ
  Public offset As ULong
  Public length As ULong
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure IMDPROXY_WRITE_RESP
  Public errorno As ULong
  Public length As ULong
End Structure

