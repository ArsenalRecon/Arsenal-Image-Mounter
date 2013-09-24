
/// imdproxy.h
/// Client components for ImDisk/devio proxy services, for use with ImScsi kernel
/// components.
/// 
/// Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#ifndef _INC_IMDPROXY_
#define _INC_IMDPROXY_

#if !defined(_WIN32) && !defined(_NTDDK_)
typedef long LONG;
typedef unsigned long ULONG;
typedef long long LONGLONG;
typedef unsigned long long ULONGLONG;
typedef unsigned short WCHAR;
#endif

#pragma warning(push)
#pragma warning(disable : 4201)

#define IMDPROXY_SVC                    L"ImDskSvc"
#define IMDPROXY_SVC_PIPE_DOSDEV_NAME   L"\\\\.\\PIPE\\" IMDPROXY_SVC
#define IMDPROXY_SVC_PIPE_NATIVE_NAME   L"\\Device\\NamedPipe\\" IMDPROXY_SVC

#define IMDPROXY_FLAG_RO                0x1

typedef enum _IMDPROXY_REQ
  {
    IMDPROXY_REQ_NULL,
    IMDPROXY_REQ_INFO,
    IMDPROXY_REQ_READ,
    IMDPROXY_REQ_WRITE,
    IMDPROXY_REQ_CONNECT,
    IMDPROXY_REQ_CLOSE,
  } IMDPROXY_REQ;

typedef struct _IMDPROXY_CONNECT_REQ
{
  ULONGLONG request_code;
  ULONGLONG flags;
  ULONGLONG length;
} IMDPROXY_CONNECT_REQ, *PIMDPROXY_CONNECT_REQ;

typedef struct _IMDPROXY_CONNECT_RESP
{
  ULONGLONG error_code;
  ULONGLONG object_ptr;
} IMDPROXY_CONNECT_RESP, *PIMDPROXY_CONNECT_RESP;

typedef struct _IMDPROXY_INFO_RESP
{
  ULONGLONG file_size;
  ULONGLONG req_alignment;
  ULONGLONG flags;
} IMDPROXY_INFO_RESP, *PIMDPROXY_INFO_RESP;

typedef struct _IMDPROXY_READ_REQ
{
  ULONGLONG request_code;
  ULONGLONG offset;
  ULONGLONG length;
} IMDPROXY_READ_REQ, *PIMDPROXY_READ_REQ;

typedef struct _IMDPROXY_READ_RESP
{
  ULONGLONG errorno;
  ULONGLONG length;
} IMDPROXY_READ_RESP, *PIMDPROXY_READ_RESP;

typedef struct _IMDPROXY_WRITE_REQ
{
  ULONGLONG request_code;
  ULONGLONG offset;
  ULONGLONG length;
} IMDPROXY_WRITE_REQ, *PIMDPROXY_WRITE_REQ;

typedef struct _IMDPROXY_WRITE_RESP
{
  ULONGLONG errorno;
  ULONGLONG length;
} IMDPROXY_WRITE_RESP, *PIMDPROXY_WRITE_RESP;

// For shared memory proxy communication only. Offset to data area in
// shared memory.
#define IMDPROXY_HEADER_SIZE 4096

typedef struct _PROXY_CONNECTION
{
  enum
    {
      PROXY_CONNECTION_DEVICE,
      PROXY_CONNECTION_SHM
    } connection_type;       // Connection type

  union
  {
    // Valid if connection_type is PROXY_CONNECTION_DEVICE
    PFILE_OBJECT device;     // Pointer to proxy communication object

    // Valid if connection_type is PROXY_CONNECTION_SHM
    struct
    {
      HANDLE request_event_handle;
      PKEVENT request_event;
      HANDLE response_event_handle;
      PKEVENT response_event;
      PUCHAR shared_memory;
      ULONG_PTR shared_memory_size;
    };
  };
} PROXY_CONNECTION, *PPROXY_CONNECTION;

NTSTATUS
ImScsiConnectProxy(IN OUT PPROXY_CONNECTION Proxy,
		   IN OUT PIO_STATUS_BLOCK IoStatusBlock,
		   IN PKEVENT CancelEvent OPTIONAL,
		   IN ULONG Flags,
		   IN PWSTR ConnectionString,
		   IN USHORT ConnectionStringLength);

VOID
ImScsiCloseProxy(IN PPROXY_CONNECTION Proxy);

NTSTATUS
ImScsiQueryInformationProxy(IN PPROXY_CONNECTION Proxy,
			    IN OUT PIO_STATUS_BLOCK IoStatusBlock,
			    IN PKEVENT CancelEvent OPTIONAL,
			    OUT PIMDPROXY_INFO_RESP ProxyInfoResponse,
			    IN ULONG ProxyInfoResponseLength);

NTSTATUS
ImScsiReadProxy(IN PPROXY_CONNECTION Proxy,
		IN OUT PIO_STATUS_BLOCK IoStatusBlock,
		IN PKEVENT CancelEvent OPTIONAL,
		OUT PVOID Buffer,
		IN ULONG Length,
		IN PLARGE_INTEGER ByteOffset);

NTSTATUS
ImScsiWriteProxy(IN PPROXY_CONNECTION Proxy,
		 IN OUT PIO_STATUS_BLOCK IoStatusBlock,
		 IN PKEVENT CancelEvent OPTIONAL,
		 OUT PVOID Buffer,
		 IN ULONG Length,
		 IN PLARGE_INTEGER ByteOffset);

#pragma warning(pop)

#endif // _INC_IMDPROXY_
