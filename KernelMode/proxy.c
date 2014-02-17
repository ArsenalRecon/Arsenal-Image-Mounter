
/// proxy.c
/// Client components for ImDisk/devio proxy services, for use with ImScsi kernel
/// components.
/// 
/// Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#include "phdskmnt.h"

#include "imdproxy.h"

#pragma warning(disable : 4204)
#pragma warning(disable : 4221)

VOID
ImScsiCloseProxy(IN PPROXY_CONNECTION Proxy)
{
  //PAGED_CODE();

  ASSERT(Proxy != NULL);

  switch (Proxy->connection_type)
    {
    case PROXY_CONNECTION_DEVICE:
      if (Proxy->device != NULL)
	ObDereferenceObject(Proxy->device);

      Proxy->device = NULL;
      break;

    case PROXY_CONNECTION_SHM:
      if ((Proxy->request_event != NULL) &
	  (Proxy->response_event != NULL) &
	  (Proxy->shared_memory != NULL))
	{
	  *(ULONGLONG*)Proxy->shared_memory = IMDPROXY_REQ_CLOSE;
	  KeSetEvent(Proxy->request_event, (KPRIORITY) 0, FALSE);
	}

      if (Proxy->request_event_handle != NULL)
	{
	  ZwClose(Proxy->request_event_handle);
	  Proxy->request_event_handle = NULL;
	}

      if (Proxy->response_event_handle != NULL)
	{
	  ZwClose(Proxy->response_event_handle);
	  Proxy->response_event_handle = NULL;
	}

      if (Proxy->request_event != NULL)
	{
	  ObDereferenceObject(Proxy->request_event);
	  Proxy->request_event = NULL;
	}

      if (Proxy->response_event != NULL)
	{
	  ObDereferenceObject(Proxy->response_event);
	  Proxy->response_event = NULL;
	}

      if (Proxy->shared_memory != NULL)
	{
	  ZwUnmapViewOfSection(NtCurrentProcess(), Proxy->shared_memory);
	  Proxy->shared_memory = NULL;
	}

      break;
    }
}

NTSTATUS
ImScsiCallProxy(IN PPROXY_CONNECTION Proxy,
		OUT PIO_STATUS_BLOCK IoStatusBlock,
		IN PKEVENT CancelEvent OPTIONAL,
		IN PVOID RequestHeader,
		IN ULONG RequestHeaderSize,
		IN PVOID RequestData,
		IN ULONG RequestDataSize,
		IN OUT PVOID ResponseHeader,
		IN ULONG ResponseHeaderSize,
		IN OUT PVOID ResponseData,
		IN ULONG ResponseDataBufferSize,
		IN ULONG *ResponseDataSize)
{
  NTSTATUS status;

  //PAGED_CODE();

  ASSERT(Proxy != NULL);

  switch (Proxy->connection_type)
    {
    case PROXY_CONNECTION_DEVICE:
      {
	if (RequestHeaderSize > 0)
	  {
	    if (CancelEvent != NULL ?
		KeReadStateEvent(CancelEvent) != 0 :
		FALSE)
	      {
		KdPrint(("ImScsi Proxy Client: Request cancelled.\n."));

		IoStatusBlock->Status = STATUS_CANCELLED;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }

	    status = ImScsiSafeIOStream(Proxy->device,
					IRP_MJ_WRITE,
					IoStatusBlock,
					CancelEvent,
					RequestHeader,
					RequestHeaderSize);

	    if (!NT_SUCCESS(status))
	      {
		KdPrint(("ImScsi Proxy Client: Request header error %#x\n.",
			 status));

		IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }
	  }

	if (RequestDataSize > 0)
	  {
	    if (CancelEvent != NULL ?
		KeReadStateEvent(CancelEvent) != 0 :
		FALSE)
	      {
		KdPrint(("ImScsi Proxy Client: Request cancelled.\n."));

		IoStatusBlock->Status = STATUS_CANCELLED;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }

	    KdPrint2
	      (("ImScsi Proxy Client: Sent req. Sending data stream.\n"));

	    status = ImScsiSafeIOStream(Proxy->device,
					IRP_MJ_WRITE,
					IoStatusBlock,
					CancelEvent,
					RequestData,
					RequestDataSize);

	    if (!NT_SUCCESS(status))
	      {
		KdPrint(("ImScsi Proxy Client: Data stream send failed. "
			 "Sent %u bytes with I/O status %#x.\n",
			 IoStatusBlock->Information, IoStatusBlock->Status));

		IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }

	    KdPrint2
	      (("ImScsi Proxy Client: Data stream of %u bytes sent with I/O "
		"status %#x. Status returned by stream writer is %#x. "
		"Waiting for IMDPROXY_RESP_WRITE.\n",
		IoStatusBlock->Information, IoStatusBlock->Status, status));
	  }

	if (ResponseHeaderSize > 0)
	  {
	    if (CancelEvent != NULL ?
		KeReadStateEvent(CancelEvent) != 0 :
		FALSE)
	      {
		KdPrint(("ImScsi Proxy Client: Request cancelled.\n."));

		IoStatusBlock->Status = STATUS_CANCELLED;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }

	    status = ImScsiSafeIOStream(Proxy->device,
					IRP_MJ_READ,
					IoStatusBlock,
					CancelEvent,
					ResponseHeader,
					ResponseHeaderSize);

	    if (!NT_SUCCESS(status))
	      {
		KdPrint(("ImScsi Proxy Client: Response header error %#x\n.",
			 status));

		IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }
	  }

	if (ResponseDataSize != NULL ? *ResponseDataSize > 0 : FALSE)
	  {
	    if (*ResponseDataSize > ResponseDataBufferSize)
	      {
		KdPrint(("ImScsi Proxy Client: Fatal: Request %u bytes, "
			 "receiving %u bytes.\n",
			 ResponseDataBufferSize, *ResponseDataSize));

		IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }

	    if (CancelEvent != NULL ?
		KeReadStateEvent(CancelEvent) != 0 :
		FALSE)
	      {
		KdPrint(("ImScsi Proxy Client: Request cancelled.\n."));

		IoStatusBlock->Status = STATUS_CANCELLED;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }

	    KdPrint2
	      (("ImScsi Proxy Client: Got ok resp. Waiting for data.\n"));

	    status = ImScsiSafeIOStream(Proxy->device,
					IRP_MJ_READ,
					IoStatusBlock,
					CancelEvent,
					ResponseData,
					*ResponseDataSize);

	    if (!NT_SUCCESS(status))
	      {
		KdPrint(("ImScsi Proxy Client: Response data error %#x\n.",
			 status));

		KdPrint(("ImScsi Proxy Client: Response data %u bytes, "
			 "got %u bytes.\n",
			 *ResponseDataSize,
			 (ULONG) IoStatusBlock->Information));

		IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
		IoStatusBlock->Information = 0;
		return IoStatusBlock->Status;
	      }

	    KdPrint2
	      (("ImScsi Proxy Client: Received %u byte data stream.\n",
		IoStatusBlock->Information));
	  }

	IoStatusBlock->Status = STATUS_SUCCESS;
	if ((RequestDataSize > 0) & (IoStatusBlock->Information == 0))
	  IoStatusBlock->Information = RequestDataSize;
	return IoStatusBlock->Status;
      }

    case PROXY_CONNECTION_SHM:
      {
	PVOID wait_objects[] = {
	  Proxy->response_event,
	  CancelEvent
	};

	ULONG number_of_wait_objects = CancelEvent != NULL ? 2 : 1;

	// Some parameter sanity checks
	if ((RequestHeaderSize > IMDPROXY_HEADER_SIZE) |
	    (ResponseHeaderSize > IMDPROXY_HEADER_SIZE) |
	    ((RequestDataSize + IMDPROXY_HEADER_SIZE) >
	     Proxy->shared_memory_size))
	  {
	    KdPrint(("ImScsi Proxy Client: "
		     "Parameter values not supported.\n."));

	    IoStatusBlock->Status = STATUS_INVALID_BUFFER_SIZE;
	    IoStatusBlock->Information = 0;
	    return IoStatusBlock->Status;
	  }

	IoStatusBlock->Information = 0;

	if (RequestHeaderSize > 0)
	  RtlCopyMemory(Proxy->shared_memory,
			RequestHeader,
			RequestHeaderSize);

	if (RequestDataSize > 0)
	  RtlCopyMemory(Proxy->shared_memory + IMDPROXY_HEADER_SIZE,
			RequestData,
			RequestDataSize);

	KeSetEvent(Proxy->request_event, (KPRIORITY) 0, TRUE);

	status = KeWaitForMultipleObjects(number_of_wait_objects,
					  wait_objects,
					  WaitAny,
					  Executive,
					  KernelMode,
					  FALSE,
					  NULL,
					  NULL);

	if (status == STATUS_WAIT_1)
	  {
	    KdPrint(("ImScsi Proxy Client: Incomplete wait %#x.\n.", status));

	    IoStatusBlock->Status = STATUS_CANCELLED;
	    IoStatusBlock->Information = 0;
	    return IoStatusBlock->Status;
	  }

	if (ResponseHeaderSize > 0)
	  RtlCopyMemory(ResponseHeader,
			Proxy->shared_memory,
			ResponseHeaderSize);

	// If server end requests to send more data than we requested, we
	// treat that as an unrecoverable device error and exit.
	if (ResponseDataSize != NULL ? *ResponseDataSize > 0 : FALSE)
	  if ((*ResponseDataSize > ResponseDataBufferSize) |
	      ((*ResponseDataSize + IMDPROXY_HEADER_SIZE) >
	       Proxy->shared_memory_size))
	    {
	      KdPrint(("ImScsi Proxy Client: Invalid response size %u.\n.",
		       *ResponseDataSize));

	      IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
	      IoStatusBlock->Information = 0;
	      return IoStatusBlock->Status;
	    }
	  else
	    {
	      RtlCopyMemory(ResponseData,
			    Proxy->shared_memory + IMDPROXY_HEADER_SIZE,
			    *ResponseDataSize);

	      IoStatusBlock->Information = *ResponseDataSize;
	    }

	IoStatusBlock->Status = STATUS_SUCCESS;
	if ((RequestDataSize > 0) & (IoStatusBlock->Information == 0))
	  IoStatusBlock->Information = RequestDataSize;
	return IoStatusBlock->Status;
      }

    default:
      return STATUS_DRIVER_INTERNAL_ERROR;
    }
}

///
/// Note that this function when successful replaces the Proxy->device pointer
/// to point to the connected device object instead of the proxy service pipe.
/// This means that the only reference to the proxy service pipe after calling
/// this function is the original handle to the pipe.
///
NTSTATUS
ImScsiConnectProxy(IN OUT PPROXY_CONNECTION Proxy,
		   OUT PIO_STATUS_BLOCK IoStatusBlock,
		   IN PKEVENT CancelEvent OPTIONAL,
		   IN ULONG Flags,
		   IN PWSTR ConnectionString,
		   IN USHORT ConnectionStringLength)
{
  IMDPROXY_CONNECT_REQ connect_req;
  IMDPROXY_CONNECT_RESP connect_resp;
  NTSTATUS status;

  //PAGED_CODE();

  ASSERT(Proxy != NULL);
  ASSERT(IoStatusBlock != NULL);
  ASSERT(ConnectionString != NULL);

  if (IMSCSI_PROXY_TYPE(Flags) == IMSCSI_PROXY_TYPE_SHM)
    {
      OBJECT_ATTRIBUTES object_attributes;
      UNICODE_STRING base_name = { 0 };
      UNICODE_STRING event_name = { 0 };
      base_name.Buffer = ConnectionString;
      base_name.Length = ConnectionStringLength;
      base_name.MaximumLength = ConnectionStringLength;
      event_name.MaximumLength = ConnectionStringLength + 20;
      event_name.Buffer = (PWCHAR) ExAllocatePoolWithTag(
          PagedPool,
          event_name.MaximumLength,
          MP_TAG_GENERAL);
      if (event_name.Buffer == NULL)
	{
	  status = STATUS_INSUFFICIENT_RESOURCES;

	  IoStatusBlock->Status = status;
	  IoStatusBlock->Information = 0;
	  return IoStatusBlock->Status;
	}

      InitializeObjectAttributes(&object_attributes,
				 &event_name,
				 OBJ_CASE_INSENSITIVE,
				 NULL,
				 NULL);

      RtlCopyUnicodeString(&event_name, &base_name);
      RtlAppendUnicodeToString(&event_name, L"_Request");

      status = ZwOpenEvent(&Proxy->request_event_handle,
			   EVENT_ALL_ACCESS,
			   &object_attributes);

      if (!NT_SUCCESS(status))
	{
	  Proxy->request_event_handle = NULL;
	  ExFreePoolWithTag(event_name.Buffer, MP_TAG_GENERAL);
	      
	  IoStatusBlock->Status = status;
	  IoStatusBlock->Information = 0;
	  return IoStatusBlock->Status;
	}

      status = ObReferenceObjectByHandle(Proxy->request_event_handle,
					 EVENT_ALL_ACCESS,
					 *ExEventObjectType,
					 KernelMode,
					 &Proxy->request_event,
					 NULL);

      if (!NT_SUCCESS(status))
	{
	  Proxy->request_event = NULL;
	  ExFreePoolWithTag(event_name.Buffer, MP_TAG_GENERAL);
	  
	  IoStatusBlock->Status = status;
	  IoStatusBlock->Information = 0;
	  return IoStatusBlock->Status;
	}

      RtlCopyUnicodeString(&event_name, &base_name);
      RtlAppendUnicodeToString(&event_name, L"_Response");

      status = ZwOpenEvent(&Proxy->response_event_handle,
			   EVENT_ALL_ACCESS,
			   &object_attributes);

      if (!NT_SUCCESS(status))
	{
	  Proxy->response_event_handle = NULL;
	  ExFreePoolWithTag(event_name.Buffer, MP_TAG_GENERAL);

	  IoStatusBlock->Status = status;
	  IoStatusBlock->Information = 0;
	  return IoStatusBlock->Status;
	}

      status = ObReferenceObjectByHandle(Proxy->response_event_handle,
					 EVENT_ALL_ACCESS,
					 *ExEventObjectType,
					 KernelMode,
					 &Proxy->response_event,
					 NULL);

      if (!NT_SUCCESS(status))
	{
	  Proxy->response_event = NULL;
	  ExFreePoolWithTag(event_name.Buffer, MP_TAG_GENERAL);

	  IoStatusBlock->Status = status;
	  IoStatusBlock->Information = 0;
	  return IoStatusBlock->Status;
	}

      IoStatusBlock->Status = STATUS_SUCCESS;
      IoStatusBlock->Information = 0;
      return IoStatusBlock->Status;
    }

  connect_req.request_code = IMDPROXY_REQ_CONNECT;
  connect_req.flags = Flags;
  connect_req.length = ConnectionStringLength;

  KdPrint(("ImScsi Proxy Client: Sending IMDPROXY_CONNECT_REQ.\n"));

  status = ImScsiCallProxy(Proxy,
			   IoStatusBlock,
			   CancelEvent,
			   &connect_req,
			   sizeof(connect_req),
			   ConnectionString,
			   ConnectionStringLength,
			   &connect_resp,
			   sizeof(IMDPROXY_CONNECT_RESP),
			   NULL,
			   0,
			   NULL);

  if (!NT_SUCCESS(status))
  {
      KdPrint(("ImScsi Proxy Client: ImScsiCallProxy failed: %#X.\n", status));
      IoStatusBlock->Status = status;
      IoStatusBlock->Information = 0;
      return IoStatusBlock->Status;
  }

  if (connect_resp.error_code != 0)
  {
      KdPrint(("ImScsi Proxy Client: Proxy returned error code: %#I64X.\n", connect_resp.error_code));
      IoStatusBlock->Status = STATUS_CONNECTION_REFUSED;
      IoStatusBlock->Information = 0;
      return IoStatusBlock->Status;
  }

  // If the proxy gave us a reference to an object to use for direct connection
  // to the server we have to change the active reference to use here.
  if (connect_resp.object_ptr != 0)
  {
      ObDereferenceObject(Proxy->device);
      Proxy->device = (PFILE_OBJECT)(ULONG_PTR) connect_resp.object_ptr;
  }

  KdPrint(("ImScsi Proxy Client: Got ok response IMDPROXY_CONNECT_RESP.\n"));

  IoStatusBlock->Status = STATUS_SUCCESS;
  IoStatusBlock->Information = 0;
  return IoStatusBlock->Status;
}

NTSTATUS
ImScsiQueryInformationProxy(IN PPROXY_CONNECTION Proxy,
			    OUT PIO_STATUS_BLOCK IoStatusBlock,
			    IN PKEVENT CancelEvent,
			    OUT PIMDPROXY_INFO_RESP ProxyInfoResponse,
			    IN ULONG ProxyInfoResponseLength)
{
  ULONGLONG proxy_req = IMDPROXY_REQ_INFO;
  NTSTATUS status;

  //PAGED_CODE();

  ASSERT(Proxy != NULL);
  ASSERT(IoStatusBlock != NULL);

  if ((ProxyInfoResponse == NULL) |
      (ProxyInfoResponseLength < sizeof(IMDPROXY_INFO_RESP)))
    {
      IoStatusBlock->Status = STATUS_BUFFER_OVERFLOW;
      IoStatusBlock->Information = 0;
      return IoStatusBlock->Status;
    }

  KdPrint(("ImScsi Proxy Client: Sending IMDPROXY_REQ_INFO.\n"));

  status = ImScsiCallProxy(Proxy,
			   IoStatusBlock,
			   CancelEvent,
			   &proxy_req,
			   sizeof(proxy_req),
			   NULL,
			   0,
			   ProxyInfoResponse,
			   sizeof(IMDPROXY_INFO_RESP),
			   NULL,
			   0,
			   NULL);

  if (!NT_SUCCESS(status))
    {
      IoStatusBlock->Status = status;
      IoStatusBlock->Information = 0;
      return IoStatusBlock->Status;
    }

  KdPrint(("ImScsi Proxy Client: Got ok response IMDPROXY_INFO_RESP.\n"));

  if (ProxyInfoResponse->req_alignment - 1 > FILE_512_BYTE_ALIGNMENT)
    {
      KdPrint(("ImScsi IMDPROXY_INFO_RESP: Unsupported sizes. "
	       "Got %p-%p size and %p-%p alignment.\n",
	       ProxyInfoResponse->file_size,
	       ProxyInfoResponse->req_alignment));

      IoStatusBlock->Status = STATUS_INVALID_PARAMETER;
      IoStatusBlock->Information = 0;
      return IoStatusBlock->Status;
    }

  IoStatusBlock->Status = STATUS_SUCCESS;
  IoStatusBlock->Information = 0;
  return IoStatusBlock->Status;
}

NTSTATUS
ImScsiReadProxy(IN PPROXY_CONNECTION Proxy,
		OUT PIO_STATUS_BLOCK IoStatusBlock,
		IN PKEVENT CancelEvent,
		OUT PVOID Buffer,
		IN ULONG Length,
		IN PLARGE_INTEGER ByteOffset)
{
	IMDPROXY_READ_REQ read_req;
	IMDPROXY_READ_RESP read_resp;
	NTSTATUS status;
	ULONG_PTR max_transfer_size;
	ULONG length_done;

	//PAGED_CODE();

	ASSERT(Proxy != NULL);
	ASSERT(IoStatusBlock != NULL);
	ASSERT(Buffer != NULL);
	ASSERT(ByteOffset != NULL);

	if (Proxy->connection_type == PROXY_CONNECTION_SHM)
		max_transfer_size = Proxy->shared_memory_size - IMDPROXY_HEADER_SIZE;
	else
		max_transfer_size = Length;

	length_done = 0;
	status = STATUS_SUCCESS;

	while (length_done < Length)
	{
		ULONG length_to_do = Length - length_done;

		KdPrint2(("ImScsi Proxy Client: "
			"IMDPROXY_REQ_READ 0x%.8x done 0x%.8x left to do.\n",
			length_done, length_to_do));

		read_req.request_code = IMDPROXY_REQ_READ;
		read_req.offset = ByteOffset->QuadPart + length_done;
		read_req.length = min(length_to_do, max_transfer_size);

		KdPrint2(("ImScsi Proxy Client: "
			"IMDPROXY_REQ_READ 0x%.8x%.8x bytes at 0x%.8x%.8x.\n",
			((PLARGE_INTEGER)&read_req.length)->HighPart,
			((PLARGE_INTEGER)&read_req.length)->LowPart,
			((PLARGE_INTEGER)&read_req.offset)->HighPart,
			((PLARGE_INTEGER)&read_req.offset)->LowPart));

		status = ImScsiCallProxy(Proxy,
			IoStatusBlock,
			CancelEvent,
			&read_req,
			sizeof(read_req),
			NULL,
			0,
			&read_resp,
			sizeof(read_resp),
			(PUCHAR)Buffer + length_done,
			(ULONG)read_req.length,
			(PULONG)&read_resp.length);

		if (!NT_SUCCESS(status))
		{
			IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
			IoStatusBlock->Information = length_done;
			return IoStatusBlock->Status;
		}

		length_done += (ULONG)read_resp.length;

		if (read_resp.errorno != 0)
		{
			KdPrint(("ImScsi Proxy Client: Server returned error %p-%p.\n",
				read_resp.errorno));
			IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
			IoStatusBlock->Information = length_done;
			return IoStatusBlock->Status;
		}

		KdPrint2(("ImScsi Proxy Client: Server sent 0x%.8x%.8x bytes.\n",
			((PLARGE_INTEGER)&read_resp.length)->HighPart,
			((PLARGE_INTEGER)&read_resp.length)->LowPart));

		if (read_resp.length == 0)
			break;
	}

	IoStatusBlock->Status = status;
	IoStatusBlock->Information = length_done;

	return status;
}

NTSTATUS
ImScsiWriteProxy(IN PPROXY_CONNECTION Proxy,
		 OUT PIO_STATUS_BLOCK IoStatusBlock,
		 IN PKEVENT CancelEvent,
		 IN PVOID Buffer,
		 IN ULONG Length,
		 IN PLARGE_INTEGER ByteOffset)
{
	IMDPROXY_WRITE_REQ write_req;
	IMDPROXY_WRITE_RESP write_resp;
	NTSTATUS status;
	ULONG_PTR max_transfer_size;
	ULONG length_done;

	//PAGED_CODE();

	ASSERT(Proxy != NULL);
	ASSERT(IoStatusBlock != NULL);
	ASSERT(Buffer != NULL);
	ASSERT(ByteOffset != NULL);

	if (Proxy->connection_type == PROXY_CONNECTION_SHM)
		max_transfer_size = Proxy->shared_memory_size - IMDPROXY_HEADER_SIZE;
	else
		max_transfer_size = Length;

	length_done = 0;
	status = STATUS_SUCCESS;

	while (length_done < Length)
	{
		ULONG length_to_do = Length - length_done;

		KdPrint2(("ImScsi Proxy Client: "
			"IMDPROXY_REQ_WRITE 0x%.8x done 0x%.8x left to do.\n",
			length_done, length_to_do));

		write_req.request_code = IMDPROXY_REQ_WRITE;
		write_req.offset = ByteOffset->QuadPart + length_done;
		write_req.length = min(length_to_do, max_transfer_size);

		KdPrint2(("ImScsi Proxy Client: "
			"IMDPROXY_REQ_WRITE 0x%.8x%.8x bytes at 0x%.8x%.8x.\n",
			((PLARGE_INTEGER)&write_req.length)->HighPart,
			((PLARGE_INTEGER)&write_req.length)->LowPart,
			((PLARGE_INTEGER)&write_req.offset)->HighPart,
			((PLARGE_INTEGER)&write_req.offset)->LowPart));

		status = ImScsiCallProxy(
			Proxy,
			IoStatusBlock,
			CancelEvent,
			&write_req,
			sizeof(write_req),
			(PUCHAR)Buffer + length_done,
			(ULONG)write_req.length,
			&write_resp,
			sizeof(write_resp),
			NULL,
			0,
			NULL);

		if (!NT_SUCCESS(status))
		{
			IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
			IoStatusBlock->Information = length_done;
			return IoStatusBlock->Status;
		}

		if (write_resp.errorno != 0)
		{
			KdPrint(("ImScsi Proxy Client: Server returned error 0x%.8x%.8x.\n",
				write_resp.errorno));
			IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
			IoStatusBlock->Information = length_done;
			return IoStatusBlock->Status;
		}

		if (write_resp.length != write_req.length)
		{
			KdPrint(("ImScsi Proxy Client: IMDPROXY_REQ_WRITE %u bytes, "
				"IMDPROXY_RESP_WRITE %u bytes.\n",
				Length,
				(ULONG)write_resp.length));
			IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
			IoStatusBlock->Information = length_done;
			return IoStatusBlock->Status;
		}

		KdPrint2(("ImScsi Proxy Client: Server replied OK.\n"));

		length_done += (ULONG)write_req.length;
	}

	IoStatusBlock->Status = STATUS_SUCCESS;
	IoStatusBlock->Information = length_done;
	return IoStatusBlock->Status;
	
	//if (write_resp.length != Length)
	//  {
	//    KdPrint(("ImScsi Proxy Client: IMDPROXY_REQ_WRITE %u bytes, "
	//      "IMDPROXY_RESP_WRITE %u bytes.\n",
	//      Length, (ULONG) write_resp.length));
	//    IoStatusBlock->Status = STATUS_IO_DEVICE_ERROR;
	//    IoStatusBlock->Information = 0;
	//    return IoStatusBlock->Status;
	//  }

	//KdPrint2(("ImScsi Proxy Client: Got ok response. "
	//   "Resetting IoStatusBlock fields.\n"));

	//IoStatusBlock->Status = STATUS_SUCCESS;
	//IoStatusBlock->Information = Length;
	//return IoStatusBlock->Status;
}

