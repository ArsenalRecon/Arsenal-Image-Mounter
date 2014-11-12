
/// common.h
/// Definitions for global constants for use both in kernel mode and user mode
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

#ifndef _COMMON_H_
#define _COMMON_H_

///
/// Bit constants for the Flags field in IMSCSI_CREATE_DATA
///

/// Read-only device
#define IMSCSI_OPTION_RO                0x00000001

/// Check if flags specifies read-only
#define IMSCSI_READONLY(x)              ((ULONG)(x) & 0x00000001)

/// Removable, hot-plug, device
#define IMSCSI_OPTION_REMOVABLE         0x00000002

/// Check if flags specifies removable
#define IMSCSI_REMOVABLE(x)             ((ULONG)(x) & 0x00000002)

/// Specifies that image files are created with sparse attribute.
#define IMSCSI_OPTION_SPARSE_FILE       0x00000004

/// Check if flags specifies sparse
#define IMSCSI_SPARSE_FILE(x)           ((ULONG)(x) & 0x00000004)

/// Swaps each byte pair in image file (not currently supported).
//#define IMSCSI_OPTION_BYTE_SWAP         0x00000008

/// Check if flags specifies byte swapping (not currently supported).
//#define IMSCSI_BYTE_SWAP(x)             ((ULONG)(x) & 0x00000008)

/// Device type is virtual harddisk partition
#define IMSCSI_DEVICE_TYPE_HD           0x00000010
/// Device type is virtual floppy drive
#define IMSCSI_DEVICE_TYPE_FD           0x00000020
/// Device type is virtual CD/DVD-ROM drive
#define IMSCSI_DEVICE_TYPE_CD           0x00000030

/// Extracts the IMSCSI_DEVICE_TYPE_xxx from flags
#define IMSCSI_DEVICE_TYPE(x)           ((ULONG)(x) & 0x000000F0)

/// Virtual disk is backed by image file
#define IMSCSI_TYPE_FILE                0x00000100
/// Virtual disk is backed by virtual memory
#define IMSCSI_TYPE_VM                  0x00000200
/// Virtual disk is backed by proxy connection
#define IMSCSI_TYPE_PROXY               0x00000300

/// Extracts the IMSCSI_TYPE_xxx from flags
#define IMSCSI_TYPE(x)                  ((ULONG)(x) & 0x00000F00)

/// Proxy connection is direct-type
#define IMSCSI_PROXY_TYPE_DIRECT        0x00000000
/// Proxy connection is over serial line
#define IMSCSI_PROXY_TYPE_COMM          0x00001000
/// Proxy connection is over TCP/IP
#define IMSCSI_PROXY_TYPE_TCP           0x00002000
/// Proxy connection uses shared memory
#define IMSCSI_PROXY_TYPE_SHM           0x00003000

/// Extracts the IMSCSI_PROXY_TYPE_xxx from flags
#define IMSCSI_PROXY_TYPE(x)            ((ULONG)(x) & 0x0000F000)

// Types with file mode

/// Proxy connection is direct-type
#define IMSCSI_FILE_TYPE_DIRECT         0x00000000
/// Proxy connection is over serial line
#define IMSCSI_FILE_TYPE_AWEALLOC       0x00001000

/// Extracts the IMSCSI_PROXY_TYPE_xxx from flags
#define IMSCSI_FILE_TYPE(x)             ((ULONG)(x) & 0x0000F000)

/// Extracts the IMSCSI_PROXY_TYPE_xxx from flags
#define IMSCSI_IMAGE_MODIFIED           0x00010000

/// Report a fake disk signature if zero
#define IMSCSI_FAKE_DISK_SIG_IF_ZERO    0x00020000

/// Specify as device number to remove all devices.
#define IMSCSI_ALL_DEVICES              (0x00FFFFFFUL)

#pragma warning(push)
#pragma warning(disable : 4200)                       /* Prevent C4200 messages. */
#pragma warning(disable : 4201)                       /* Prevent C4201 messages. */

typedef union _DEVICE_NUMBER
{
    struct
    {
        UCHAR   PathId;
        UCHAR   TargetId;
        UCHAR   Lun;
    };

    /// On add/remove this can be set to IMSCSI_AUTO_DEVICE_NUMBER
    ULONG       LongNumber;

} DEVICE_NUMBER, *PDEVICE_NUMBER;

///
/// Structure used with SMP_IMSCSI_CREATE_DEVICE and
/// SMP_IMSCSI_QUERY_DEVICE calls.
///
typedef struct _SRB_IMSCSI_CREATE_DATA
{
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL  SrbIoControl;

    /// On create this can be set to IMSCSI_AUTO_DEVICE_NUMBER
    DEVICE_NUMBER   DeviceNumber;

    /// Total size in bytes.
    LARGE_INTEGER   DiskSize;

    /// Bytes per sector
    ULONG           BytesPerSector;

    /// The byte offset in image file where the virtual disk data begins.
    LARGE_INTEGER   ImageOffset;
    
    /// Creation flags. Type of device and type of connection.
    ULONG           Flags;
    
    /// Length in bytes of the FileName member.
    USHORT          FileNameLength;
    
    /// Dynamically-sized member that specifies the image file name.
    WCHAR           FileName[];

} SRB_IMSCSI_CREATE_DATA, *PSRB_IMSCSI_CREATE_DATA;

typedef struct _SRB_IMSCSI_QUERY_ADAPTER
{
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL  SrbIoControl;

    ULONG           NumberOfDevices;

    DEVICE_NUMBER   DeviceList[];

} SRB_IMSCSI_QUERY_ADAPTER, *PSRB_IMSCSI_QUERY_ADAPTER;

typedef struct _SRB_IMSCSI_SET_DEVICE_FLAGS
{
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL  SrbIoControl;

    DEVICE_NUMBER   DeviceNumber;

    ULONG           FlagsToChange;

    ULONG           FlagValues;

} SRB_IMSCSI_SET_DEVICE_FLAGS, *PSRB_IMSCSI_SET_DEVICE_FLAGS;

typedef struct {
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL  SrbIoControl;

    DEVICE_NUMBER   DeviceNumber;

} SRB_IMSCSI_REMOVE_DEVICE, *PSRB_IMSCSI_REMOVE_DEVICE;

typedef struct {
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL  SrbIoControl;

    // API compatibility version is returned in SrbIoControl.ReturnCode.
    // SubVersion contains, in newer versions, a revision version that
    // applications can check to see if latest version is loaded.
    ULONG           SubVersion;

} SRB_IMSCSI_QUERY_VERSION, *PSRB_IMSCSI_QUERY_VERSION;

typedef struct {
  SRB_IO_CONTROL        sic;
} SRB_IMSCSI_CHECK, *PSRB_IMSCSI_CHECK;

#define FUNCTION_SIGNATURE          "PhDskMnt"

///
/// Control codes for IOCTL_SCSI_MINIPORT requests.
///
#define SMP_IMSCSI                   0x83730000
#define SMP_IMSCSI_QUERY_VERSION     ((ULONG) (SMP_IMSCSI | 0x800))
#define SMP_IMSCSI_CREATE_DEVICE     ((ULONG) (SMP_IMSCSI | 0x801))
#define SMP_IMSCSI_QUERY_DEVICE      ((ULONG) (SMP_IMSCSI | 0x802))
#define SMP_IMSCSI_QUERY_ADAPTER     ((ULONG) (SMP_IMSCSI | 0x803))
#define SMP_IMSCSI_CHECK             ((ULONG) (SMP_IMSCSI | 0x804))
#define SMP_IMSCSI_SET_DEVICE_FLAGS  ((ULONG) (SMP_IMSCSI | 0x805))
#define SMP_IMSCSI_REMOVE_DEVICE     ((ULONG) (SMP_IMSCSI | 0x806))

#pragma warning(pop)

///
/// Macro to check if size of supplied SrbIoControl buffer looks ok
///
#define SRB_IO_CONTROL_SIZE_OK(o) ((o)->SrbIoControl.Length >= sizeof(*(o))-sizeof(SRB_IO_CONTROL))

#endif    // _COMMON_H_
