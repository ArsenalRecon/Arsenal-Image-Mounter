//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Buffers;
using System;
using System.Runtime.InteropServices;

namespace Arsenal.ImageMounter.Devio;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CA1712 // Do not prefix enum values with type name

public enum IMDPROXY_REQ : ulong
{
    IMDPROXY_REQ_NULL,
    IMDPROXY_REQ_INFO,
    IMDPROXY_REQ_READ,
    IMDPROXY_REQ_WRITE,
    IMDPROXY_REQ_CONNECT,
    IMDPROXY_REQ_CLOSE,
    IMDPROXY_REQ_UNMAP,
    IMDPROXY_REQ_ZERO,
    IMDPROXY_REQ_SCSI,
    IMDPROXY_REQ_SHARED
}

[Flags]
public enum IMDPROXY_FLAGS : ulong
{
    IMDPROXY_FLAG_NONE = 0UL,
    IMDPROXY_FLAG_RO = 1UL,
    IMDPROXY_FLAG_SUPPORTS_UNMAP = 0x2UL, // Unmap / TRIM ranges
    IMDPROXY_FLAG_SUPPORTS_ZERO = 0x4UL, // Zero - fill ranges
    IMDPROXY_FLAG_SUPPORTS_SCSI = 0x8UL, // SCSI SRB operations
    IMDPROXY_FLAG_SUPPORTS_SHARED = 0x10UL, // Shared image access With reservations
    IMDPROXY_FLAG_KEEP_OPEN = 0x20UL, // DevIoDrv mode with persistent virtual file
}

/// <summary>
/// Constants used in connection with Devio proxy communication.
/// </summary>
public static class IMDPROXY_CONSTANTS
{
    /// <summary>
    /// Header size when communicating using a shared memory object.
    /// </summary>
    public const int IMDPROXY_HEADER_SIZE = 4096;

    /// <summary>
    /// Default required alignment for I/O operations.
    /// </summary>
    public const int REQUIRED_ALIGNMENT = 512;
    public const ulong RESERVATION_KEY_ANY = ulong.MaxValue;
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_CONNECT_REQ
{
    public IMDPROXY_REQ request_code { get; set; }
    public ulong flags { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_CONNECT_RESP
{
    public ulong error_code { get; set; }
    public ulong object_ptr { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_INFO_RESP
{
    public ulong file_size { get; set; }
    public ulong req_alignment { get; set; }
    private ulong ulflags;
    public IMDPROXY_FLAGS flags
    {
        readonly get => (IMDPROXY_FLAGS)ulflags;
        set => ulflags = (ulong)value;
    }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_READ_REQ
{
    public IMDPROXY_REQ request_code { get; set; }
    public ulong offset { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_READ_RESP
{
    public ulong errorno { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_WRITE_REQ
{
    public IMDPROXY_REQ request_code { get; set; }
    public ulong offset { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_WRITE_RESP
{
    public ulong errorno { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_UNMAP_REQ
{
    public IMDPROXY_REQ request_code { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_UNMAP_RESP
{
    public ulong errorno { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_ZERO_REQ
{
    public IMDPROXY_REQ request_code { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_ZERO_RESP
{
    public ulong errorno { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_SCSI_REQ
{
    public IMDPROXY_REQ request_code { get; set; }

    private unsafe fixed byte cdb[16];

    public unsafe Span<byte> Cdb => BufferExtensions.CreateSpan(ref cdb[0], 16);

    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_SCSI_RESP
{
    public ulong errorno { get; set; }
    public ulong length { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_SHARED_REQ
{
    public IMDPROXY_REQ request_code { get; set; }
    public IMDPROXY_SHARED_OP_CODE operation_code { get; set; }
    public ulong reserve_scope { get; set; }
    public ulong reserve_type { get; set; }
    public ulong existing_reservation_key { get; set; }
    public ulong current_channel_key { get; set; }
    public ulong operation_channel_key { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public partial struct IMDPROXY_SHARED_RESP
{
    public IMDPROXY_SHARED_RESP_CODE errorno { get; set; }
    public Guid unique_id { get; set; }
    public ulong channel_key { get; set; }
    public ulong generation { get; set; }
    public ulong reservation_key { get; set; }
    public ulong reservation_scope { get; set; }
    public ulong reservation_type { get; set; }
    public ulong length { get; set; }
}

public enum IMDPROXY_SHARED_OP_CODE : ulong
{
    GetUniqueId,
    ReadKeys,
    Register,
    ClearKeys,
    Reserve,
    Release,
    Preempt,
    RegisterIgnoreExisting
}

public enum IMDPROXY_SHARED_RESP_CODE : ulong
{
    NoError,
    ReservationCollision,
    InvalidParameter,
    IOError
}

public struct IMDPROXY_DEVIODRV_BUFFER_HEADER
{
    public ulong request_code { get; set; }     // Request code to forward to response header.
    public ulong io_tag { get; set; }           // Tag to forward to response header.
    public ulong flags { get; set; }            // Reserved. Currently not used.

    public static unsafe int SizeOf { get; } = sizeof(IMDPROXY_DEVIODRV_BUFFER_HEADER);
}

