
/// common.h
/// Definitions for global constants for use both in kernel mode and user mode
/// components.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#ifndef _COMMON_H_
#define _COMMON_H_

#ifndef _T
#if defined(_NTDDK_) && !defined(UNICODE)
#define UNICODE
#endif
#if defined(_NTDDK_) && !defined(_UNICODE)
#define _UNICODE
#endif
#define _T(x) L##x
#ifndef _WINNT_
#include <ntdef.h>
#endif
#endif

#define IMSCSI_DRIVER_VERSION           ((ULONG) 0x0101)

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
#define IMSCSI_OPTION_BYTE_SWAP         0x00000008

/// Check if flags specifies byte swapping (not currently supported).
#define IMSCSI_BYTE_SWAP(x)             ((ULONG)(x) & 0x00000008)

/// Device type is virtual harddisk partition
#define IMSCSI_DEVICE_TYPE_HD           0x00000010
/// Device type is virtual floppy drive
#define IMSCSI_DEVICE_TYPE_FD           0x00000020
/// Device type is virtual CD/DVD-ROM drive
#define IMSCSI_DEVICE_TYPE_CD           0x00000030
/// Device type is unknown "raw" (for use with third-party client drivers)
#define IMSCSI_DEVICE_TYPE_RAW          0x00000040

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

/// Serialized I/O to an image file, done in a worker thread
#define IMSCSI_FILE_TYPE_QUEUED_IO      0x00000000
/// Direct parallel I/O to AWEAlloc driver (physical RAM), done in request thread
#define IMSCSI_FILE_TYPE_AWEALLOC       0x00001000
/// Direct parallel I/O to an image file, done in request thread
/// Requires lower level driver to be callable at DISPATCH_LEVEL, otherwise
/// IRQL_NOT_LESS_THAN_OR_EQUAL
#define IMSCSI_FILE_TYPE_PARALLEL_IO    0x00002000
/// Buffered I/O to an image file. Disables FILE_NO_INTERMEDIATE_BUFFERING when
/// opening image file.
#define IMSCSI_FILE_TYPE_BUFFERED_IO    0x00003000

/// Extracts the IMSCSI_PROXY_TYPE_xxx from flags
#define IMSCSI_FILE_TYPE(x)             ((ULONG)(x) & 0x0000F000)

/// Extracts the IMSCSI_PROXY_TYPE_xxx from flags
#define IMSCSI_IMAGE_MODIFIED           0x00010000

/// Report a fake disk signature instead of existing one
#define IMSCSI_FAKE_DISK_SIG            0x00020000
/// Obsolete name
#define IMSCSI_FAKE_DISK_SIG_IF_ZERO    IMSCSI_FAKE_DISK_SIG

/// This flag causes the driver to open image files in shared write mode even
/// if the image is opened for writing. This could be useful in some cases,
/// but could easily corrupt filesystems on image files if used incorrectly.
#define IMSCSI_OPTION_SHARED_IMAGE      0x00040000
/// Check if flags indicate shared write mode
#define IMSCSI_SHARED_IMAGE(x)          ((ULONG)(x) & 0x00040000)

/// Redirect write operations to a differencing image. Valid for file or proxy
/// modes.
#define IMSCSI_OPTION_WRITE_OVERLAY     0x00080000
/// Check if flags indicate write overlay mode
#define IMSCSI_WRITE_OVERLAY(x)         ((ULONG)(x) & 0x00080000)

/// Specify as device number to remove all devices.
#define IMSCSI_ALL_DEVICES              (0x00FFFFFFUL)

/// Specify as device number to auto-select device number for new device.
#define IMSCSI_AUTO_DEVICE_NUMBER       (0x00FFFFFFUL)

/// Specify as SCSI port number to look for devices with any number.
#define IMSCSI_ANY_PORT_NUMBER          (0xFF)

#pragma warning(push)
#pragma warning(disable : 4200)                       /* Prevent C4200 messages. */
#pragma warning(disable : 4201)                       /* Prevent C4201 messages. */

#ifndef AWEALLOC_DRIVER_NAME
#define AWEALLOC_DRIVER_NAME           _T("AWEAlloc")
#endif
#ifndef AWEALLOC_DEVICE_NAME
#define AWEALLOC_DEVICE_NAME           _T("\\Device\\") AWEALLOC_DRIVER_NAME
#endif

///
/// Registry settings. It is possible to specify devices to be mounted
/// automatically when the driver loads.
///
#define IMSCSI_CFG_PARAMETER_KEY                  _T("\\Parameters")
#define IMSCSI_CFG_MAX_DEVICES_VALUE              _T("MaxDevices")
#define IMSCSI_CFG_LOAD_DEVICES_VALUE             _T("LoadDevices")
#define IMSCSI_CFG_DISALLOWED_DRIVE_LETTERS_VALUE _T("DisallowedDriveLetters")
#define IMSCSI_CFG_IMAGE_FILE_PREFIX              _T("FileName")
#define IMSCSI_CFG_SIZE_PREFIX                    _T("Size")
#define IMSCSI_CFG_FLAGS_PREFIX                   _T("Flags")
#define IMSCSI_CFG_OFFSET_PREFIX                  _T("ImageOffset")

#define KEY_NAME_HKEY_MOUNTPOINTS  \
  _T("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MountPoints")
#define KEY_NAME_HKEY_MOUNTPOINTS2  \
  _T("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MountPoints2")

#define IMSCSI_WINVER_MAJOR() (GetVersion() & 0xFF)
#define IMSCSI_WINVER_MINOR() ((GetVersion() & 0xFF00) >> 8)

#define IMSCSI_WINVER() ((IMSCSI_WINVER_MAJOR() << 8) | \
    IMSCSI_WINVER_MINOR())

#if defined(NT4_COMPATIBLE) && defined(_M_IX86)
#define IMSCSI_GTE_WIN2K() (IMSCSI_WINVER_MAJOR() >= 0x05)
#else
#define IMSCSI_GTE_WIN2K() TRUE
#endif

#ifdef _M_IX86
#define IMSCSI_GTE_WINXP() (IMSCSI_WINVER() >= 0x0501)
#else
#define IMSCSI_GTE_WINXP() TRUE
#endif

#define IMSCSI_GTE_SRV2003() (IMSCSI_WINVER() >= 0x0502)

#define IMSCSI_GTE_VISTA() (IMSCSI_WINVER_MAJOR() >= 0x06)

typedef union _DEVICE_NUMBER
{
    struct
    {
        UCHAR   PathId;
        UCHAR   TargetId;
        UCHAR   Lun;
    };

    /// On add/remove this can be set to IMSCSI_AUTO_DEVICE_NUMBER
    /// or IMSCSI_ALL_DEVICES.
    ULONG       LongNumber;

} DEVICE_NUMBER, *PDEVICE_NUMBER;

///
/// Structure used with ImScsiQueryDevice and embedded in
/// SRB_IMSCSI_CREATE_DATA structure used with IOCTL_SCSI_MINIPORT
/// requests.
///
#pragma pack(push, 4)
typedef struct _IMSCSI_DEVICE_CONFIGURATION
{
    /// On create this can be set to IMSCSI_AUTO_DEVICE_NUMBER
    DEVICE_NUMBER   DeviceNumber;

    /// Total size in bytes.
    LARGE_INTEGER   DiskSize;

    /// Bytes per sector
    ULONG           BytesPerSector;

    union
    {
        /// Length of write overlay file name after FileName field, if
        /// Flags field contains write overlay flag.
        USHORT      WriteOverlayFileNameLength;

        /// Padding if none of flag specific fields are in use.
        ULONG       Reserved;
    };

    /// The byte offset in image file where the virtual disk data begins.
    LARGE_INTEGER   ImageOffset;

    /// Creation flags. Type of device and type of connection.
    ULONG           Flags;

    /// Length in bytes of the FileName member.
    USHORT          FileNameLength;

    /// Dynamically-sized member that specifies the image file name.
    WCHAR           FileName[];

} IMSCSI_DEVICE_CONFIGURATION, *PIMSCSI_DEVICE_CONFIGURATION;
#pragma pack(pop)

#ifdef _NTDDSCSIH_

///
/// Structure used with SMP_IMSCSI_CREATE_DEVICE and
/// SMP_IMSCSI_QUERY_DEVICE calls.
///
typedef struct _SRB_IMSCSI_CREATE_DATA
{
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL              SrbIoControl;

    /// Data fields
    IMSCSI_DEVICE_CONFIGURATION Fields;

} SRB_IMSCSI_CREATE_DATA, *PSRB_IMSCSI_CREATE_DATA;

// This is an old structure definition. Only used in some compiler
// compatibility test scenarios to verify that compiler uses same byte offsets
// for each field as a sequential structure would have.
#if 0
///
/// Structure used with SMP_IMSCSI_CREATE_DEVICE and
/// SMP_IMSCSI_QUERY_DEVICE calls.
///
typedef struct _SRB_IMSCSI_CREATE_DATA2
{
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL              SrbIoControl;

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

} SRB_IMSCSI_CREATE_DATA2, *PSRB_IMSCSI_CREATE_DATA2;
#endif

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

    DEVICE_NUMBER   DeviceNumber;

    LARGE_INTEGER   ExtendSize;

} SRB_IMSCSI_EXTEND_DEVICE, *PSRB_IMSCSI_EXTEND_DEVICE;

typedef struct {
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL  SrbIoControl;

    // API compatibility version is returned in SrbIoControl.ReturnCode.
    // SubVersion contains, in newer versions, a revision version that
    // applications can check to see if latest version is loaded.
    ULONG           SubVersion;

} SRB_IMSCSI_QUERY_VERSION, *PSRB_IMSCSI_QUERY_VERSION;

typedef struct {
    /// SRB_IO_CONTROL header
    SRB_IO_CONTROL  SrbIoControl;
} SRB_IMSCSI_CHECK, *PSRB_IMSCSI_CHECK;

#define IMSCSI_FUNCTION_SIGNATURE    "PhDskMnt"

/*
Prepares for sending a device request to an Arsenal Image Mounter
adapter or disk device.
*/
void
__forceinline
ImScsiInitializeSrbIoBlock(PSRB_IO_CONTROL SrbIoControl,
ULONG Size,
ULONG ControlCode,
ULONG Timeout)
{
    SrbIoControl->HeaderLength = sizeof(*SrbIoControl);
    memcpy((char*)SrbIoControl->Signature,
        IMSCSI_FUNCTION_SIGNATURE,
        sizeof(IMSCSI_FUNCTION_SIGNATURE) - 1);
    SrbIoControl->ControlCode = ControlCode;
    SrbIoControl->Length = Size - sizeof(*SrbIoControl);
    SrbIoControl->Timeout = Timeout;
    SrbIoControl->ReturnCode = 0;
}

#endif

///
/// Control codes for IOCTL_SCSI_MINIPORT requests.
///
#define SMP_IMSCSI                      0x83730000
#define SMP_IMSCSI_QUERY_VERSION        ((ULONG) (SMP_IMSCSI | 0x800))
#define SMP_IMSCSI_CREATE_DEVICE        ((ULONG) (SMP_IMSCSI | 0x801))
#define SMP_IMSCSI_QUERY_DEVICE         ((ULONG) (SMP_IMSCSI | 0x802))
#define SMP_IMSCSI_QUERY_ADAPTER        ((ULONG) (SMP_IMSCSI | 0x803))
#define SMP_IMSCSI_CHECK                ((ULONG) (SMP_IMSCSI | 0x804))
#define SMP_IMSCSI_SET_DEVICE_FLAGS     ((ULONG) (SMP_IMSCSI | 0x805))
#define SMP_IMSCSI_REMOVE_DEVICE        ((ULONG) (SMP_IMSCSI | 0x806))
#define SMP_IMSCSI_EXTEND_DEVICE        ((ULONG) (SMP_IMSCSI | 0x807))

#define IMSCSI_API_NO_BROADCAST_NOTIFY  0x00000001
#define IMSCSI_API_FORCE_DISMOUNT       0x00000002

#pragma warning(pop)

///
/// Macro to check if size of supplied SrbIoControl buffer looks ok
///
#define SRB_IO_CONTROL_SIZE_OK(o) ((o)->SrbIoControl.Length >= sizeof(*(o))-sizeof(SRB_IO_CONTROL))

#endif    // _COMMON_H_
