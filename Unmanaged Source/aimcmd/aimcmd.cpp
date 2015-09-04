
/// aimcmd.cpp
/// Command line access to Arsenal Image Mounter features.
/// 
/// Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objbase.h>
#include <shellapi.h>
#include <devioctl.h>
#include <ntddscsi.h>
#include <ntdddisk.h>
#include <WinIoCtl.h>

#include "..\phdskmnt\inc\common.h"
#include "..\aimapi\aimapi.h"

#include <stdio.h>
#include <stdlib.h>

#include "..\aimapi\winstrct.hpp"

#include "..\phdskmnt\inc\ntumapi.h"
#include "..\phdskmnt\inc\phdskmntver.h"

#include "aimcmd.h"

#include <imdisk.h>
#include <imdproxy.h>

#pragma comment(lib, "imdisk.lib")
#pragma comment(lib, "ntdll.lib")

enum
{
    IMSCSI_CLI_SUCCESS = 0,
    IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND = 1,
    IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE = 2,
    IMSCSI_CLI_ERROR_CREATE_DEVICE = 3,
    IMSCSI_CLI_ERROR_DRIVER_NOT_INSTALLED = 4,
    IMSCSI_CLI_ERROR_DRIVER_WRONG_VERSION = 5,
    IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE = 6,
    IMSCSI_CLI_ERROR_SERVICE_INACCESSIBLE = 7,
    IMSCSI_CLI_ERROR_FORMAT = 8,
    IMSCSI_CLI_ERROR_BAD_MOUNT_POINT = 9,
    IMSCSI_CLI_ERROR_BAD_SYNTAX = 10,
    IMSCSI_CLI_ERROR_NOT_ENOUGH_MEMORY = 11,
    IMSCSI_CLI_ERROR_PARTITION_NOT_FOUND = 12,
    IMSCSI_CLI_ERROR_WRONG_SYNTAX = 13,
    IMSCSI_CLI_NO_FREE_DRIVE_LETTERS = 14,
    IMSCSI_CLI_ERROR_FATAL = -1
};

//#define DbgOemPrintF(x) ImScsiOemPrintF x
#define DbgOemPrintF(x)

/// Macros for "human readable" file sizes.
#define _1KB  (1ui64<<10)
#define _1MB  (1ui64<<20)
#define _1GB  (1ui64<<30)
#define _1TB  (1ui64<<40)

#define _B(n)  ((double)(n))
#define _KB(n) ((double)(n)/_1KB)
#define _MB(n) ((double)(n)/_1MB)
#define _GB(n) ((double)(n)/_1GB)
#define _TB(n) ((double)(n)/_1TB)

#define _h(n) ((n)>=_1TB ? _TB(n) : (n)>=_1GB ? _GB(n) :	\
	       (n)>=_1MB ? _MB(n) : (n)>=_1KB ? _KB(n) : (n))
#define _p(n) ((n)>=_1TB ? "TB" : (n)>=_1GB ? "GB" :			\
	       (n)>=_1MB ? "MB" : (n)>=_1KB ? "KB": (n)==1 ? "byte" : "bytes")

#pragma warning(disable: 6255)
#pragma warning(disable: 28719)
#pragma warning(disable: 28159)

void __declspec(noreturn)
ImScsiSyntaxHelp()
{
    int rc = fputs
        ("Control program for the Arsenal Image Mounter.\r\n"
        "For version information, license, copyrights and credits, type aimcmd --version\r\n"
        "\n"
        "Setup syntax:\r\n"
        "aimcmd --install setup_directory\r\n"
        "        Installs Arsenal Image Mounter driver from a directory with setup\r\n"
        "        files.\r\n"
        "\n"
        "aimcmd --uninstall\r\n"
        "        Uninstalls Arsenal Image Mounter driver.\r\n"
        "\n"
        "aimcmd --rescan\r\n"
        "        Rescans SCSI bus on installed adapter.\r\n"
        "\n"
        "Manage virtual disks:\r\n"
        "aimcmd -a -t type [-n] [-o opt1[,opt2 ...]] [-f|-F file] [-s size] [-b offset]\r\n"
        "       [-S sectorsize] [-u devicenumber] [-m mountpoint]\r\n"
        "       [-p \"format-parameters\"] [-P]\r\n"
        "aimcmd -d|-D [-u devicenumber | -m mountpoint] [-P]\r\n"
        "aimcmd -R -u unit\r\n"
        "aimcmd -l [-u devicenumber | -m mountpoint]\r\n"
        "aimcmd -e [-s size] [-o opt1[,opt2 ...]] [-u devicenumber | -m mountpoint]\r\n"
        "\n"
        "-a      Attach a virtual disk. This will configure and attach a virtual disk\r\n"
        "        with the parameters specified and attach it to the system.\r\n"
        "\n"
        "-d      Detach a virtual disk from the system and release all resources.\r\n"
        "        Use -D to force removal even if the device is in use.\r\n"
        "\n"
        "-R      Emergency removal of hung virtual disks. Should only be used as a last\r\n"
        "        resort when a virtual disk has some kind of problem that makes it\r\n"
        "        impossible to detach it in a safe way. This could happen for example\r\n"
        "        for proxy-type virtual disks sometimes when proxy communication fails.\r\n"
        "        Note that this does not attempt to dismount filesystem or lock the\r\n"
        "        volume in any way so there is a potential risk of data loss. Use with\r\n"
        "        caution!\r\n"
        "\n"
        "-e      Edit an existing virtual disk.\r\n"
        "\n"
        "        Along with the -s parameter extends the size of an existing virtual\r\n"
        "        disk.\r\n"
        "\n"
        "        Along with the -o parameter changes media characteristics for an\r\n"
        "        existing virtual disk. Options that can be changed on existing virtual\r\n"
        "        disks are those specifying wether or not the media of the virtual disk\r\n"
        "        should be writable and/or removable.\r\n"
        "\n"
        "-t type\r\n"
        "        Select the backingstore for the virtual disk.\r\n"
        "\n"
        "vm      Storage for this type of virtual disk is allocated from virtual memory\r\n"
        "        in the system process. If a file is specified with -f that file is\r\n"
        "        is loaded into the memory allocated for the disk image.\r\n"
        "\n"
        "file    A file specified with -f file becomes the backingstore for this\r\n"
        "        virtual disk.\r\n"
        "\n"
        "proxy   The actual backingstore for this type of virtual disk is controlled by\r\n"
        "        a storage server accessed by the driver on this machine by\r\n"
        "        sending storage I/O requests through a named pipe specified with -f.\r\n"
        "\n"
        "-f file or -F file\r\n"
        "        Filename to use as backingstore for the file type virtual disk, to\r\n"
        "        initialize a vm type virtual disk or name of a named pipe for I/O\r\n"
        "        client/server communication for proxy type virtual disks. For proxy\r\n"
        "        type virtual disks \"file\" may be a COM port or a remote server\r\n"
        "        address if the -o options includes \"ip\" or \"comm\".\r\n"
        "\n"
        "        Instead of using -f to specify 'DOS-style' paths, such as\r\n"
        "        C:\\dir\\image.bin or \\\\server\\share\\image.bin, you can use -F to\r\n"
        "        specify 'NT-style' native paths, such as\r\n"
        "        \\Device\\Harddisk0\\Partition1\\image.bin. This makes it possible to\r\n"
        "        specify files on disks or communication devices that currently have no\r\n"
        "        drive letters assigned.\r\n"
        "\n"
        "-l      List configured devices. If given with -u or -m, display details about\r\n"
        "        that particular device.\r\n"
        "\n"
        "-n      When printing listing devices, print only the unit number without other\r\n"
        "        information.\r\n"
        "\n"
        "-s size\r\n"
        "        Size of the virtual disk. Size is number of bytes unless suffixed with\r\n"
        "        a b, k, m, g, t, K, M, G or T which denotes number of 512-byte blocks,\r\n"
        "        thousand bytes, million bytes, billion bytes, trillion bytes,\r\n"
        "        kilobytes, megabytes, gigabytes and terabytes respectively. The suffix\r\n"
        "        can also be % to indicate percentage of free physical memory which\r\n"
        "        could be useful when creating vm type virtual disks. It is optional to\r\n"
        "        specify a size unless the file to use for a file type virtual disk does\r\n"
        "        not already exist or when a vm type virtual disk is created without\r\n"
        "        specifying an initialization image file using the -f or -F. If size is\r\n"
        "        specified when creating a file type virtual disk, the size of the file\r\n"
        "        used as backingstore for the virtual disk is adjusted to the new size\r\n"
        "        specified with this size option.\r\n"
        "\n"
        "        The size can be a negative value to indicate the size of free physical\r\n"
        "        memory minus this size. If you e.g. type -400M the size of the virtual\r\n"
        "        disk will be the amount of free physical memory minus 400 MB.\r\n"
        "\n"
        "-b offset\r\n"
        "        Specifies an offset in an image file where the virtual disk begins. All\r\n"
        "        offsets of I/O operations on the virtual disk will be relative to this\r\n"
        "        offset. This parameter is particularily useful when mounting a specific\r\n"
        "        partition in an image file that contains an image of a complete hard\r\n"
        "        disk, not just one partition. This parameter has no effect when\r\n"
        "        creating a blank vm type virtual disk. When creating a vm type virtual\r\n"
        "        disk with a pre-load image file specified with -f or -F parameters, the\r\n"
        "        -b parameter specifies an offset in the image file where the image to\r\n"
        "        be loaded into the vm type virtual disk begins.\r\n"
        "\n"
        "        Specify auto as offset to automatically select offset for a few known\r\n"
        "        non-raw disk image file formats. Currently auto-selection is supported\r\n"
        "        for Nero .nrg and Microsoft .sdi image files.\r\n"
        "\n"
        "-S sectorsize\r\n"
        "        Sectorsize to use for the virtual disk device. Default value is 512\r\n"
        "        bytes except for CD-ROM/DVD-ROM style devices where 2048 bytes is used\r\n"
        "        by default.\r\n"
        "\n"
        "-p \"format-parameters\"\r\n"
        "        If -p is specified the 'format' command is invoked to create a\r\n"
        "        filesystem when the new virtual disk has been created.\r\n"
        "        \"format-parameters\" must be a parameter string enclosed within\r\n"
        "        double-quotes. The string is added to the command line that starts\r\n"
        "        'format'. You usually specify something like \"/fs:ntfs /q /y\", that\r\n"
        "        is, create an NTFS filesystem with quick formatting and without user\r\n"
        "        interaction.\r\n"
        "\n"
        "-o option\r\n"
        "        Set or reset options.\r\n"
        "\n"
        "ro      Creates a read-only virtual disk. For vm type virtual disks, this\r\n"
        "        option can only be used if the -f option is also specified.\r\n"
        "\n"
        "rw      Specifies that the virtual disk should be read/writable. This is the\r\n"
        "        default setting. It can be used with the -e parameter to set an\r\n"
        "        existing read-only virtual disk writable.\r\n"
        "\n"
        "fksig   If this flag is set, the driver will report a random fake disk\r\n"
        "        signature to Windows in case device is read-only, existing disk\r\n"
        "        signature is zero and master boot record has otherwise apparently valid\r\n"
        "        data.\r\n"
        "\n"
        "sparse  Sets NTFS sparse attribute on image file. This has no effect on proxy\r\n"
        "        or vm type virtual disks.\r\n"
        "\n"
        "rem     Specifies that the device should be created with removable media\r\n"
        "        characteristics. This changes the device properties returned by the\r\n"
        "        driver to the system. For example, this changes how some filesystems\r\n"
        "        cache write operations.\r\n"
        "\n"
        "fix     Specifies that the media characteristics of the virtual disk should be\r\n"
        "        fixed media, as opposed to removable media specified with the rem\r\n"
        "        option. Fixed media is the default setting. The fix option can be used\r\n"
        "        with the -e parameter to set an existing removable virtual disk as\r\n"
        "        fixed.\r\n"
        "\n"
        "saved   Clears the 'image modified' flag from an existing virtual disk. This\r\n"
        "        flag is set by the driver when an image is modified and is displayed\r\n"
        "        in the -l output for a virtual disk. The 'saved' option is only valid\r\n"
        "        with the -e parameter.\r\n"
        "\n"
        "        Note that virtual floppy or CD/DVD-ROM drives are always read-only and\r\n"
        "        removable devices and that cannot be changed.\r\n"
        "\n"
        "cd      Creates a virtual CD-ROM/DVD-ROM.\r\n"
        "\n"
        "fd      Creates a virtual floppy disk.\r\n"
        "\n"
        "        NOTE: cd and fd options are currently not supported by the driver.\r\n"
        "\n"
        "hd      Creates a virtual hard disk. This is the default.\r\n"
        "\n"
        "raw     Creates a device object with \"controller\" device type. The system will\r\n"
        "        not attempt to use such devices as a storage device, but it could be\r\n"
        "        useful in combination with third-party drivers that can provide further\r\n"
        "        device objects using this virtual disk device as a backing store.\r\n"
        "\n"
        "ip      Can only be used with proxy-type virtual disks. With this option, the\r\n"
        "        user-mode service component is initialized to connect to a\r\n"
        "        storage server using TCP/IP. With this option, the -f switch specifies\r\n"
        "        the remote host optionally followed by a colon and a port number to\r\n"
        "        connect to.\r\n"
        "\n"
        "comm    Can only be used with proxy-type virtual disks. With this option, the\r\n"
        "        user-mode service component is initialized to connect to a\r\n"
        "        storage server through a COM port. With this option, the -f switch\r\n"
        "        specifies the COM port to connect to, optionally followed by a colon,\r\n"
        "        a space, and then a device settings string with the same syntax as the\r\n"
        "        MODE command.\r\n"
        "\n"
        "shm     Can only be used with proxy-type virtual disks. With this option, the\r\n"
        "        driver communicates with a storage server on the same computer using\r\n"
        "        shared memory block to transfer I/O data.\r\n"
        "\n"
        "awe     Can only be used with file-type virtual disks. With this option, the\r\n"
        "        driver copies contents of image file to physical memory. No changes are\r\n"
        "        written to image file. If this option is used in combination with  no\r\n"
        "        image file name, a physical memory block will be used without loading\r\n"
        "        an image file onto it. In that case, -s parameter is needed to specify\r\n"
        "        size of memory block. This option requires awealloc driver, which\r\n"
        "        requires Windows 2000 or later.\r\n"
        "\n"
        "bswap   Instructs driver to swap each pair of bytes read from or written to\r\n"
        "        image file. Useful when examining images from some embedded systems\r\n"
        "        and similar where data is stored in reverse byte order.\r\n"
        "\n"
        "        NOTE: This option is currently not supported by the driver.\r\n"
        "\n"
        "par     Parallel I/O. Valid for file-type virtual disks. With this flag set,\r\n"
        "        driver sends read and write requests for the virtual disk directly down\r\n"
        "        to the driver that handles the image file, within the SCSIOP dispatch\r\n"
        "        routine. This flag is intended for developers who provide their own\r\n"
        "        driver that handles image file requests. Such driver need to handle\r\n"
        "        requests at DISPATCH_LEVEL at any time, otherwise system crashes are\r\n"
        "        very likely to happen. *Never* use this flag when mounting image files!\r\n"
        "        Use it *only* with special purpose drivers that can meet all neeed\r\n"
        "        requirements!\r\n"
        "\n"
        "-u devicenumber\r\n"
        "        Six hexadecimal digits indicating SCSI path, target and lun numbers\r\n"
        "        for a device. Format: LLTTPP. Along with -a, request a specific device\r\n"
        "        number for the new device instead of automatic allocation. Along with\r\n"
        "        -d or -l specifies the unit number of the virtual disk to remove or\r\n"
        "        query.\r\n"
        "\n"
        "-m mountpoint\r\n"
        "        Specifies a drive letter or mount point for the new virtual disk, the\r\n"
        "        virtual disk to query or the virtual disk to remove. When creating a\r\n"
        "        new virtual disk you can specify #: as mountpoint in which case the\r\n"
        "        first unused drive letter is automatically used.\r\n"
        "\n"
        "        Note that even if you don't specify -m, Windows normally assigns drive\r\n"
        "        letters to new volumes anyway. This behaviour can be changed using the\r\n"
        "        MOUNTVOL command line tool.\r\n"
        "\n"
        "-P      Persistent. Along with -a, saves registry settings for re-creating the\r\n"
        "        same virtual disk automatically when driver is loaded, which usually\r\n"
        "        occurs during system startup. Along with -d or -D, existing such\r\n"
        "        settings for the removed virtual disk are also removed from registry.\r\n"
        "        There are some limitations to what settings could be saved in this way.\r\n"
        "        Only features directly implemented in the kernel level driver are\r\n"
        "        saved, so for example the -p switch to format a virtual disk will not\r\n"
        "        be saved.\r\n"
        "\n"
        "        NOTE: Registry settings for auto-loading devices are currently not\r\n"
        "        supported by the driver, so this switch has currently no effect.\r\n",
        stderr);

    if (rc > 0)
        exit(IMSCSI_CLI_ERROR_WRONG_SYNTAX);
    else
        exit(IMSCSI_CLI_ERROR_FATAL);
}

// Prints out a FormatMessage style parameterized message to specified stream.
BOOL
ImScsiOemPrintF(FILE *Stream, LPCSTR Message, ...)
{
    va_list param_list;
    LPSTR lpBuf = NULL;

    va_start(param_list, Message);

    if (!FormatMessageA(78 |
        FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_FROM_STRING, Message, 0, 0,
        (LPSTR)&lpBuf, 0, &param_list))
        return FALSE;

    CharToOemA(lpBuf, lpBuf);
    fprintf(Stream, "%s\n", lpBuf);
    LocalFree(lpBuf);
    return TRUE;
}

inline char
NextWaitChar(char *chr)
{
    switch (*chr)
    {
    case '\\':
        *chr = '|';
        break;
    case '|':
        *chr = '/';
        break;
    case '/':
        *chr = '-';
        break;
    default:
        *chr = '\\';
        break;
    }

    return *chr;
}

// Writes out to console a message followed by system error message
// corresponding to current "last error" code from Win32 API.
void
PrintLastError(LPCWSTR Prefix)
{
    LPSTR MsgBuf;

    if (!FormatMessageA(FORMAT_MESSAGE_MAX_WIDTH_MASK |
        FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_FROM_SYSTEM |
        FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL, GetLastError(), 0, (LPSTR)&MsgBuf, 0, NULL))
        MsgBuf = NULL;

    if (Prefix != NULL)
    {
        ImScsiOemPrintF(stderr, "%1!ws! %2", Prefix, MsgBuf);
    }
    else
    {
        ImScsiOemPrintF(stderr, "%1", MsgBuf);
    }

    if (MsgBuf != NULL)
        LocalFree(MsgBuf);
}

LPVOID
ImScsiCliAssertNotNull(LPVOID Ptr)
{
    if (Ptr == NULL)
        RaiseException(STATUS_NO_MEMORY,
        EXCEPTION_NONCONTINUABLE,
        0,
        NULL);

    return Ptr;
}

VOID
WINAPI
DebugMessageCallback(LPVOID, LPWSTR Message)
{
    ImScsiOemPrintF(stderr, "%1!ws!%t%t", Message);
}

// Checks current driver version for compatibility with this library and
// returns TRUE if found compatible, FALSE otherwise. Device parameter is
// handle to miniport adapter or a device connected to it.
BOOL
ImScsiCliCheckDriverVersion(HANDLE Device)
{
    if (!ImScsiCheckDriverVersion(Device))
    {
        if (GetLastError() == ERROR_REVISION_MISMATCH)
        {
            fputs("Wrong version of Arsenal Image Mounter.\r\n", stderr);
            return FALSE;
        }
        else
        {
            ImScsiOemPrintF(stderr,
                "Device is not an Arsenal Image Mounter virtual disk.");

            return FALSE;
        }
    }

    return TRUE;
}

// Creates a new virtual disk device.
int
ImScsiCliCreateDevice(PDEVICE_NUMBER DeviceNumber,
PLARGE_INTEGER DiskGeometry,
ULONG BytesPerSector,
PLARGE_INTEGER ImageOffset,
DWORD Flags,
LPCWSTR FileName,
BOOL NativePath,
BOOL NumericPrint,
BOOL SaveSettings,
LPWSTR MountPoint,
LPWSTR FormatOptions)
{
    HANDLE driver;
    DWORD dw;
    BYTE port_number;

    driver = ImScsiOpenScsiAdapter(&port_number);

    if (driver == INVALID_HANDLE_VALUE)
    {
        if (GetLastError() == ERROR_FILE_NOT_FOUND)
        {
            fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
            return IMSCSI_CLI_ERROR_DRIVER_NOT_INSTALLED;
        }
        else
        {
            PrintLastError(L"Error controlling the Arsenal Image Mounter driver:");
            return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
        }
    }

    if (!ImScsiCliCheckDriverVersion(driver))
    {
        CloseHandle(driver);
        return IMSCSI_CLI_ERROR_DRIVER_WRONG_VERSION;
    }

    // Physical memory allocation requires the AWEAlloc driver.
    if (((IMSCSI_TYPE(Flags) == IMSCSI_TYPE_FILE) |
        (IMSCSI_TYPE(Flags) == 0)) &
        (IMSCSI_FILE_TYPE(Flags) == IMSCSI_FILE_TYPE_AWEALLOC))
    {
        HANDLE awealloc;
        UNICODE_STRING file_name;

        RtlInitUnicodeString(&file_name, AWEALLOC_DEVICE_NAME);

        for (;;)
        {
            awealloc = ImDiskOpenDeviceByName(&file_name,
                GENERIC_READ | GENERIC_WRITE);

            if (awealloc != INVALID_HANDLE_VALUE)
            {
                NtClose(awealloc);
                break;
            }

            if (GetLastError() != ERROR_FILE_NOT_FOUND)
                break;

            if (ImDiskStartService(AWEALLOC_DRIVER_NAME))
            {
                puts("AWEAlloc driver was loaded into the kernel.");
                continue;
            }

            switch (GetLastError())
            {
            case ERROR_SERVICE_DOES_NOT_EXIST:
                fputs("The AWEAlloc driver is not installed.\r\n"
                    "Please install ImDisk Virtual Disk Driver.\r\n", stderr);
                break;

            case ERROR_PATH_NOT_FOUND:
            case ERROR_FILE_NOT_FOUND:
                fputs("Cannot load AWEAlloc driver.\r\n"
                    "Please install ImDisk Virtual Disk Driver.\r\n", stderr);
                break;

            case ERROR_SERVICE_DISABLED:
                fputs("The AWEAlloc driver is disabled.\r\n", stderr);
                break;

            default:
                PrintLastError(L"Error loading AWEAlloc driver:");
            }

            CloseHandle(driver);
            return IMSCSI_CLI_ERROR_SERVICE_INACCESSIBLE;
        }
    }
    // Proxy reconnection types requires the user mode service.
    else if ((IMSCSI_TYPE(Flags) == IMSCSI_TYPE_PROXY) &
        ((IMSCSI_PROXY_TYPE(Flags) == IMSCSI_PROXY_TYPE_TCP) |
        (IMSCSI_PROXY_TYPE(Flags) == IMSCSI_PROXY_TYPE_COMM)))
    {
        if (!WaitNamedPipe(IMDPROXY_SVC_PIPE_DOSDEV_NAME, 0))
            if (GetLastError() == ERROR_FILE_NOT_FOUND)
                if (ImDiskStartService(IMDPROXY_SVC))
                {
                    while (!WaitNamedPipe(IMDPROXY_SVC_PIPE_DOSDEV_NAME, 0))
                        if (GetLastError() == ERROR_FILE_NOT_FOUND)
                            Sleep(200);
                        else
                            break;

                    puts
                        ("The Arsenal Image Mounter Helper Service was started.");
                }
                else
                {
                    switch (GetLastError())
                    {
                    case ERROR_SERVICE_DOES_NOT_EXIST:
                        fputs("The ImDisk Virtual Disk Driver Helper Service is not \r\n"
                            "installed.\r\n"
                            "Please install ImDisk Virtual Disk Driver.\r\n", stderr);
                        break;

                    case ERROR_PATH_NOT_FOUND:
                    case ERROR_FILE_NOT_FOUND:
                        fputs("Cannot ImDisk Virtual Disk Driver Helper \r\n"
                            "Service.\r\n"
                            "Please install ImDisk Virtual Disk Driver.\r\n", stderr);
                        break;

                    case ERROR_SERVICE_DISABLED:
                        fputs("The ImDisk Virtual Disk Driver Helper Service is \r\n"
                            "disabled.\r\n", stderr);
                        break;

                    default:
                        PrintLastError
                            (L"Error starting ImDisk Virtual Disk Driver Helper \r\n"
                            L"Service:");
                    }

                    CloseHandle(driver);
                    return IMSCSI_CLI_ERROR_SERVICE_INACCESSIBLE;
                }
    }

    UNICODE_STRING file_name;
    if (FileName == NULL)
        RtlInitUnicodeString(&file_name, NULL);
    else if (NativePath)
    {
        if (!RtlCreateUnicodeString(&file_name, FileName))
        {
            CloseHandle(driver);
            fputs("Memory allocation error.\r\n", stderr);
            return IMSCSI_CLI_ERROR_FATAL;
        }
    }
    else if ((IMSCSI_TYPE(Flags) == IMSCSI_TYPE_PROXY) &
        (IMSCSI_PROXY_TYPE(Flags) == IMSCSI_PROXY_TYPE_SHM))
    {
        LPWSTR namespace_prefix;
        HANDLE h = CreateFile(L"\\\\?\\Global", 0, FILE_SHARE_READ, NULL,
            OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);

        if ((h == INVALID_HANDLE_VALUE) &
            (GetLastError() == ERROR_FILE_NOT_FOUND))
            namespace_prefix = L"\\BaseNamedObjects\\";
        else
            namespace_prefix = L"\\BaseNamedObjects\\Global\\";

        if (h != INVALID_HANDLE_VALUE)
            CloseHandle(h);

        WHeapMem<WCHAR> prefixed_name(
            ((wcslen(namespace_prefix) + wcslen(FileName)) << 1) + 1,
            HEAP_GENERATE_EXCEPTIONS);

        wcscpy(prefixed_name, namespace_prefix);
        wcscat(prefixed_name, FileName);

        if (!RtlCreateUnicodeString(&file_name, prefixed_name))
        {
            CloseHandle(driver);
            fputs("Memory allocation error.\r\n", stderr);
            return IMSCSI_CLI_ERROR_FATAL;
        }
    }
    else
    {
        if (!RtlDosPathNameToNtPathName_U(FileName, &file_name, NULL, NULL))
        {
            CloseHandle(driver);
            fputs("Memory allocation error.\r\n", stderr);
            return IMSCSI_CLI_ERROR_FATAL;
        }
    }

    WHeapMem<SRB_IMSCSI_CREATE_DATA> create_data(
        sizeof(SRB_IMSCSI_CREATE_DATA) + file_name.Length,
        HEAP_GENERATE_EXCEPTIONS | HEAP_ZERO_MEMORY);

    puts("Creating device...");

    create_data->Fields.DeviceNumber = *DeviceNumber;
    create_data->Fields.DiskSize = *DiskGeometry;
    create_data->Fields.BytesPerSector = BytesPerSector;
    create_data->Fields.ImageOffset = *ImageOffset;
    create_data->Fields.Flags = Flags;
    create_data->Fields.FileNameLength = file_name.Length;

    if (file_name.Length != 0)
    {
        memcpy(&create_data->Fields.FileName, file_name.Buffer,
            file_name.Length);
        RtlFreeUnicodeString(&file_name);
    }

    if (!ImScsiDeviceIoControl(driver,
        SMP_IMSCSI_CREATE_DEVICE,
        &create_data->SrbIoControl,
        (DWORD)create_data.GetSize(),
        0, &dw))
    {
        NtClose(driver);

        PrintLastError(L"Error creating virtual disk:");
        return IMSCSI_CLI_ERROR_CREATE_DEVICE;
    }

    NtClose(driver);

    *DeviceNumber = create_data->Fields.DeviceNumber;

    if (NumericPrint)
        printf("%u\n", *DeviceNumber);
    else
    {
        ImScsiOemPrintF(stdout,
            "Created device %1!.6X! -> %2!ws!",
            *DeviceNumber,
            FileName == NULL ? L"Image in memory" : FileName);
    }

    if (SaveSettings)
    {
        puts("Saving registry settings...");

        if (!ImScsiSaveRegistrySettings(&create_data->Fields))
            PrintLastError(L"Registry edit failed");
    }

    if ((IMSCSI_DEVICE_TYPE(create_data->Fields.Flags) != 0) &&
        (IMSCSI_DEVICE_TYPE(create_data->Fields.Flags) !=
        IMSCSI_DEVICE_TYPE_HD))
    {
        puts("Non-harddisk device successfully attached.");

        return IMSCSI_CLI_SUCCESS;
    }

    WHeapMem<WCHAR> dosdevs(UNICODE_STRING_MAX_BYTES,
        HEAP_GENERATE_EXCEPTIONS);

    HANDLE disk = INVALID_HANDLE_VALUE;
    DWORD disk_number = ULONG_MAX;

    char wait_char = 0;

    Sleep(400);

    for (;;)
    {
        disk =
            ImScsiOpenDiskByDeviceNumber(create_data->Fields.DeviceNumber,
            port_number, &disk_number);

        if (disk != INVALID_HANDLE_VALUE)
        {
            break;
        }

        printf("Disk not attached yet, waiting... %c\r",
            NextWaitChar(&wait_char));

        HANDLE event = ImScsiRescanScsiAdapterAsync(TRUE);

        if (event == NULL)
        {
            Sleep(600);
        }
        else
        {
            while (WaitForSingleObject(event, 200) == WAIT_TIMEOUT)
            {
                printf("Disk not attached yet, waiting... %c\r",
                    NextWaitChar(&wait_char));
            }

            CloseHandle(event);
        }
    }

    CloseHandle(disk);

    WMem<WCHAR> dev_path(ImDiskAllocPrintF(L"\\\\?\\PhysicalDrive%1!u!",
        disk_number));

    if (dev_path == NULL)
    {
        return IMSCSI_CLI_ERROR_NOT_ENOUGH_MEMORY;
    }

    disk = CreateFile(dev_path, GENERIC_READ | GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);

    if (disk == INVALID_HANDLE_VALUE)
    {
        WErrMsg errmsg;

        ImScsiOemPrintF(stderr, "Error reopening for writing '%1!ws!': %2!ws!",
            dev_path, (LPCWSTR)errmsg);

        return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
    }

    SET_DISK_ATTRIBUTES disk_attributes = { sizeof(disk_attributes) };
    disk_attributes.AttributesMask = DISK_ATTRIBUTE_OFFLINE;
    if (!IMSCSI_READONLY(create_data->Fields.Flags))
    {
        disk_attributes.AttributesMask |= DISK_ATTRIBUTE_READ_ONLY;
    }

    if ((!DeviceIoControl(disk, IOCTL_DISK_SET_DISK_ATTRIBUTES,
        &disk_attributes, sizeof(disk_attributes),
        NULL, 0,
        &dw, NULL)) &&
        (GetLastError() != ERROR_INVALID_FUNCTION))
    {
        PrintLastError(L"Cannot set disk in writable online mode:");
    }

    if (((FormatOptions != NULL) ||
        (create_data->Fields.FileNameLength == 0)) &&
        !IMSCSI_READONLY(create_data->Fields.Flags))
    {
        puts("Creating partition...");

        ULONG rand_seed = GetTickCount();

        for (;;)
        {
            DRIVE_LAYOUT_INFORMATION drive_layout = { 0 };
            drive_layout.Signature = RtlRandom(&rand_seed);
            drive_layout.PartitionCount = 1;
            drive_layout.PartitionEntry[0].StartingOffset.QuadPart = 1048576;
            drive_layout.PartitionEntry[0].PartitionLength.QuadPart =
                create_data->Fields.DiskSize.QuadPart -
                drive_layout.PartitionEntry[0].StartingOffset.QuadPart;
            drive_layout.PartitionEntry[0].PartitionNumber = 1;
            drive_layout.PartitionEntry[0].PartitionType = PARTITION_IFS;
            drive_layout.PartitionEntry[0].BootIndicator = TRUE;
            drive_layout.PartitionEntry[0].RecognizedPartition = TRUE;
            drive_layout.PartitionEntry[0].RewritePartition = TRUE;

            if (DeviceIoControl(disk, IOCTL_DISK_SET_DRIVE_LAYOUT, &drive_layout,
                sizeof(drive_layout), NULL, 0, &dw, NULL))
            {
                break;
            }

            if (IMSCSI_READONLY(create_data->Fields.Flags) ||
                (GetLastError() != ERROR_WRITE_PROTECT))
            {
                PrintLastError(L"Error creating partition:");

                CloseHandle(disk);

                return IMSCSI_CLI_ERROR_FORMAT;
            }

            printf("Disk not yet ready, waiting... %wc\r",
                NextWaitChar(&wait_char));

            SET_DISK_ATTRIBUTES disk_attributes = { sizeof(disk_attributes) };
            disk_attributes.AttributesMask =
                DISK_ATTRIBUTE_OFFLINE | DISK_ATTRIBUTE_READ_ONLY;

            if (!DeviceIoControl(disk, IOCTL_DISK_SET_DISK_ATTRIBUTES,
                &disk_attributes, sizeof(disk_attributes),
                NULL, 0,
                &dw, NULL))
            {
                Sleep(400);
            }
            else
            {
                Sleep(0);
            }
        }

        puts("Successfully created partition.");
    }

    if ((!DeviceIoControl(disk, IOCTL_DISK_UPDATE_PROPERTIES,
        NULL, 0, NULL, 0, &dw, NULL)) &&
        (GetLastError() != ERROR_INVALID_FUNCTION))
    {
        PrintLastError(L"Error updating disk properties:");
    }

    CloseHandle(disk);

    disk = INVALID_HANDLE_VALUE;

    int return_code = IMSCSI_CLI_SUCCESS;

    WCHAR vol_name[50];

    WHeapMem<WCHAR> vol_mnt(UNICODE_STRING_MAX_BYTES,
        HEAP_GENERATE_EXCEPTIONS);

    DWORD start_time = GetTickCount();

    bool format_done = false;
    int volumes_found = 0;

    for (;;)
    {
        HANDLE volume = FindFirstVolume(vol_name, _countof(vol_name));
        if (volume == INVALID_HANDLE_VALUE)
        {
            PrintLastError(L"Error enumerating disk volumes:");

            return IMSCSI_CLI_ERROR_FORMAT;
        }

        do
        {
            vol_name[48] = 0;

            HANDLE vol_handle = CreateFile(vol_name, 0,
                FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0,
                NULL);

            if (vol_handle == INVALID_HANDLE_VALUE)
            {
                continue;
            }

            if (!ImScsiVolumeUsesDisk(vol_handle, disk_number))
            {
                CloseHandle(vol_handle);
                continue;
            }

            CloseHandle(vol_handle);

            ImScsiOemPrintF(stdout, "Attached disk volume %1!ws!",
                (LPCWSTR)vol_name);

            ++volumes_found;

            vol_handle = CreateFile(vol_name, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0,
                NULL);

            if (vol_handle == INVALID_HANDLE_VALUE)
            {
                PrintLastError(L"Error opening volume in read/write mode:");
            }
            else
            {
                if (!DeviceIoControl(vol_handle,
                    IOCTL_VOLUME_ONLINE, NULL, 0, NULL, 0, &dw, NULL))
                {
                    PrintLastError(L"Error setting volume in online mode:");
                }

                CloseHandle(vol_handle);
            }

            if (FormatOptions != NULL)
            {
                format_done = true;

                puts("Formatting disk volume...");

                WMem<WCHAR> format_cmd(ImDiskAllocPrintF(
                    L"format.com %1!ws! %2!ws!",
                    (LPCWSTR)vol_name, FormatOptions));

                if (!format_cmd)
                {
                    return IMSCSI_CLI_ERROR_NOT_ENOUGH_MEMORY;
                }

                STARTUPINFO startup_info = { sizeof(startup_info) };
                PROCESS_INFORMATION process_info;
                if (CreateProcess(NULL, format_cmd, NULL, NULL, TRUE, 0, NULL,
                    NULL, &startup_info, &process_info))
                {
                    CloseHandle(process_info.hThread);
                    WaitForSingleObject(process_info.hProcess, INFINITE);

                    DWORD exit_code;
                    if (GetExitCodeProcess(process_info.hProcess, &exit_code))
                    {
                        if (exit_code == 0)
                        {
                            puts("Format successful.");
                            return_code = IMSCSI_CLI_SUCCESS;
                        }
                        else
                        {
                            puts("Format failed.");
                            return_code = IMSCSI_CLI_ERROR_FORMAT;
                        }
                    }
                    else
                    {
                        PrintLastError(L"FORMAT.COM process error:");
                        return_code = IMSCSI_CLI_ERROR_FORMAT;
                    }

                    CloseHandle(process_info.hProcess);
                }
                else
                {
                    PrintLastError(L"Cannot launch FORMAT.COM:");
                    return_code = IMSCSI_CLI_ERROR_FORMAT;
                }
            }

            wcscat(vol_name + 48, L"\\");

            if (!ImScsiGetVolumePathNamesForVolumeName(vol_name,
                vol_mnt, (DWORD)vol_mnt.Count(), &dw))
            {
                PrintLastError(L"Error enumerating mount points");
                continue;
            }

            WMem<WCHAR> mount_point_buffer;

            if ((MountPoint != NULL) && (MountPoint[0] != 0) &&
                (MountPoint[wcslen(MountPoint) - 1] != L'\\'))
            {
                mount_point_buffer =
                    ImDiskAllocPrintF(L"%1\\", MountPoint);

                if (mount_point_buffer)
                {
                    MountPoint = mount_point_buffer;
                }
            }

            bool mount_point_found = false;
            size_t length;
            for (LPWSTR mnt = vol_mnt;
                (length = wcslen(mnt)) != 0;
                mnt += length + 1)
            {
                if ((length == 3) &&
                    (MountPoint != NULL) && (MountPoint[0] != 0) &&
                    (wcscmp(MountPoint + 1, L":\\") == 0) &&
                    (MountPoint[0] != L'#') &&
                    (wcscmp(mnt + 1, L":\\") == 0) &&
                    (wcsicmp(MountPoint, mnt) != 0))
                {
                    if (!DeleteVolumeMountPoint(mnt))
                    {
                        WErrMsg errmsg;

                        ImScsiOemPrintF(stderr,
                            "Error removing old mount point '%1!ws!': %2!ws!",
                            mnt, (LPCWSTR)errmsg);
                    }
                }
                else
                {
                    mount_point_found = true;

                    ImScsiOemPrintF(stdout, "  Mounted at %1!ws!", mnt);
                }
            }

            if ((MountPoint != NULL) && (MountPoint[0] != 0) &&
                ((wcscmp(MountPoint, L"#:\\") != 0) || !mount_point_found))
            {
                if (wcscmp(MountPoint, L"#:\\") == 0)
                {
                    MountPoint[0] = ImDiskFindFreeDriveLetter();
                    if (MountPoint[0] == 0)
                    {
                        fputs("All drive letters are in use.\r\n", stderr);
                        return IMSCSI_CLI_NO_FREE_DRIVE_LETTERS;
                    }
                }

                if (!SetVolumeMountPoint(MountPoint, vol_name))
                {
                    WErrMsg errmsg;

                    ImScsiOemPrintF(stderr,
                        "Error setting volume '%1!ws!' mount point to '%2!ws!':",
                        vol_name, MountPoint, (LPCWSTR)errmsg);
                }
                else
                {
                    ImScsiOemPrintF(stdout,
                        "Created new volume mount point at %1!ws!",
                        MountPoint);
                }

                MountPoint = NULL;
            }

        } while (FindNextVolume(volume, vol_name, _countof(vol_name)));

        FindVolumeClose(volume);

        if (format_done || (volumes_found > 0))
        {
            break;
        }
        
        if (((FormatOptions == NULL) &&
            (GetTickCount() - start_time) > 3000))
        {
            puts("No volumes attached. Disk could be offline or not partitioned.");
            break;
        }

        printf("Volume not yet attached, waiting... %c\r",
            NextWaitChar(&wait_char));

        Sleep(400);
    }

    return return_code;
}

// Removes an existing virtual disk device. ForeDismount can be set to TRUE to
// continue with dismount even if there are open handles to files or similar on
// the virtual disk. EmergencyRemove can be set to TRUE to have the device
// immediately removed, regardless of whether device handler loop in driver is
// responsive or hung, or whether or not there are any handles open to the
// device. Use this as a last resort to remove for example proxy backed
// devices with hung proxy connections and similar.
int
ImScsiCliRemoveDevice(DEVICE_NUMBER DeviceNumber,
LPCWSTR MountPoint,
BOOL ForceDismount,
BOOL EmergencyRemove,
BOOL RemoveSettings)
{
    if (ForceDismount)
    {
        ImScsiSetAPIFlags(IMSCSI_API_FORCE_DISMOUNT);
    }

    if (MountPoint == NULL)
    {
        BYTE port_number;
        HANDLE adapter = ImScsiOpenScsiAdapter(&port_number);
        if (adapter == INVALID_HANDLE_VALUE)
        {
            if (GetLastError() == ERROR_FILE_NOT_FOUND)
            {
                fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
            }
            else
            {
                PrintLastError();
            }
            return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
        }

        if (DeviceNumber.LongNumber == IMSCSI_ALL_DEVICES)
        {
            puts("Removing all devices...");
        }
        else
        {
            printf("Removing device %.6X...\n", DeviceNumber.LongNumber);
        }

        if ((DeviceNumber.LongNumber == IMSCSI_ALL_DEVICES) ||
            EmergencyRemove)
        {
            if (!ImScsiRemoveDeviceByNumber(NULL, adapter, DeviceNumber))
            {
                if (GetLastError() == ERROR_FILE_NOT_FOUND)
                {
                    if (DeviceNumber.LongNumber == IMSCSI_ALL_DEVICES)
                    {
                        fputs("No devices found.\r\n", stderr);
                    }
                    else
                    {
                        fprintf(stderr, "No device with number %.6X.\r\n",
                            DeviceNumber.LongNumber);
                    }

                    return IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND;
                }
                else
                {
                    PrintLastError();
                    return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
                }
            }

            puts("Done.");

            return IMSCSI_CLI_SUCCESS;
        }

        if (RemoveSettings)
        {
            printf("Removing registry settings for device %.6X...\n",
                DeviceNumber);

            if (!ImScsiRemoveRegistrySettings(DeviceNumber))
                PrintLastError(L"Registry edit failed");
        }

        if (!ImScsiRemoveDeviceByNumber(NULL, adapter, DeviceNumber))
        {
            PrintLastError();
            return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
        }

        return IMSCSI_CLI_SUCCESS;
    }

    HANDLE device = ImDiskOpenDeviceByMountPoint(MountPoint,
        FILE_READ_ATTRIBUTES);

    if (device == INVALID_HANDLE_VALUE)
    {
        switch (GetLastError())
        {
        case ERROR_INVALID_PARAMETER:
            fputs("This version of Windows only supports drive letters as \r\n"
                "mount points.\r\n"
                "Windows 2000 or higher is required to support \r\n"
                "subdirectory mount points.\r\n",
                stderr);
            return IMSCSI_CLI_ERROR_BAD_MOUNT_POINT;

        case ERROR_INVALID_FUNCTION:
            fputs("Mount points are only supported on NTFS volumes.\r\n",
                stderr);
            return IMSCSI_CLI_ERROR_BAD_MOUNT_POINT;

        case ERROR_NOT_A_REPARSE_POINT:
        case ERROR_DIRECTORY:
        case ERROR_DIR_NOT_EMPTY:
            ImScsiOemPrintF(stderr, "Not a mount point: '%1!ws!'",
                MountPoint);
            return IMSCSI_CLI_ERROR_BAD_MOUNT_POINT;

        default:
            PrintLastError(MountPoint);
            return IMSCSI_CLI_ERROR_BAD_MOUNT_POINT;
        }
    }

    SCSI_ADDRESS addresses[8];
    DWORD number_of_disks = 1;
    if ((!ImScsiGetScsiAddressForDisk(device, addresses)) &&
        (!ImScsiGetScsiAddressesForVolume(device, addresses,
        _countof(addresses), &number_of_disks)))
    {
        PrintLastError(MountPoint);
        CloseHandle(device);
        return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
    }

    for (DWORD i = 0; i < number_of_disks; i++)
    {
        HANDLE adapter = ImScsiOpenScsiAdapterByScsiPortNumber(
            addresses[i].PortNumber);

        if (adapter == INVALID_HANDLE_VALUE)
        {
            PrintLastError();
            CloseHandle(device);
            return IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND;
        }

        DeviceNumber.PathId = addresses[i].PathId;
        DeviceNumber.TargetId = addresses[i].TargetId;
        DeviceNumber.Lun = addresses[i].Lun;

        if (RemoveSettings)
        {
            printf("Removing registry settings for device %.6X...\n",
                DeviceNumber);

            if (!ImScsiRemoveRegistrySettings(DeviceNumber))
                PrintLastError(L"Registry edit failed");
        }

        CloseHandle(adapter);
    }

    if (!ImScsiRemoveDeviceByMountPoint(NULL, MountPoint))
    {
        return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
    }

    return IMSCSI_CLI_SUCCESS;
}

// Prints information about an existing virtual disk device, identified by
// either a device number or mount point.
int
ImScsiCliQueryStatusDevice(DEVICE_NUMBER DeviceNumber,
LPWSTR MountPoint)
{
    WHeapMem<IMSCSI_DEVICE_CONFIGURATION> config(
        UNICODE_STRING_MAX_BYTES,
        HEAP_GENERATE_EXCEPTIONS | HEAP_ZERO_MEMORY);

    SCSI_ADDRESS addresses[8];
    DWORD number_of_disks = 1;

    if (MountPoint != NULL)
    {
        HANDLE device = ImDiskOpenDeviceByMountPoint(MountPoint,
            GENERIC_READ | GENERIC_WRITE);

        if (device == INVALID_HANDLE_VALUE)
        {
            PrintLastError(L"Error opening device:");
            return IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND;
        }

        if ((!ImScsiGetScsiAddressForDisk(device, addresses)) &&
            (!ImScsiGetScsiAddressesForVolume(device, addresses,
            _countof(addresses), &number_of_disks)))
        {
            NtClose(device);

            if (GetLastError() == ERROR_INVALID_FUNCTION)
            {
                puts("Not an Arsenal Image Mounter device.");
                return IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND;
            }
            else
            {
                PrintLastError(L"Error querying device:");
                return IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND;
            }
        }

        NtClose(device);
    }
    else
    {
        HANDLE adapter = ImScsiOpenScsiAdapter(&addresses[0].PortNumber);
        if (adapter == INVALID_HANDLE_VALUE)
        {
            if (GetLastError() == ERROR_FILE_NOT_FOUND)
            {
                fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
                return IMSCSI_CLI_ERROR_DRIVER_NOT_INSTALLED;
            }
            else
            {
                PrintLastError();
                return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
            }
        }

        CloseHandle(adapter);

        addresses[0].PathId = DeviceNumber.PathId;
        addresses[0].TargetId = DeviceNumber.TargetId;
        addresses[0].Lun = DeviceNumber.Lun;
    }

    for (DWORD i = 0; i < number_of_disks; i++)
    {
        DeviceNumber.PathId = addresses[i].PathId;
        DeviceNumber.TargetId = addresses[i].TargetId;
        DeviceNumber.Lun = addresses[i].Lun;

        printf("SCSI port number %i device number %.6X\n",
            (int)addresses[i].PortNumber, DeviceNumber.LongNumber);

        HANDLE adapter = ImScsiOpenScsiAdapterByScsiPortNumber(
            addresses[0].PortNumber);

        if (adapter == INVALID_HANDLE_VALUE)
        {
            if (GetLastError() == ERROR_FILE_NOT_FOUND)
            {
                fprintf(stderr, "Not an Arsenal Image Mounter device.\r\n");
            }
            else
            {
                PrintLastError();
            }
            continue;
        }

        config->DeviceNumber = DeviceNumber;

        if (!ImScsiQueryDevice(adapter, config, (DWORD)config.GetSize()))
        {
            if (GetLastError() == ERROR_FILE_NOT_FOUND)
            {
                fputs("No such device.\r\n", stderr);
                return IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND;
            }
            else
            {
                PrintLastError();
                return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
            }
        }

        if (config->FileNameLength != 0)
        {
            ImScsiOemPrintF(stdout,
                "Image file: %1!.*ws!",
                (int)(config->FileNameLength /
                sizeof(*config->FileName)),
                config->FileName);
        }
        else
            puts("No image file.");

        if (config->ImageOffset.QuadPart > 0)
            printf("Image file offset: %I64i bytes\n",
            config->ImageOffset.QuadPart);

        printf("Size: %I64i bytes (%.4g %s)",
            config->DiskSize.QuadPart,
            _h(config->DiskSize.QuadPart),
            _p(config->DiskSize.QuadPart));

        printf("%s%s%s%s%s.\n",
            IMSCSI_READONLY(config->Flags) ?
            ", ReadOnly" : "",
            IMSCSI_REMOVABLE(config->Flags) ?
            ", Removable" : "",
            IMSCSI_TYPE(config->Flags) == IMSCSI_TYPE_VM ?
            ", Virtual Memory" :
            IMSCSI_TYPE(config->Flags) == IMSCSI_TYPE_PROXY ?
            ", Proxy" :
            IMSCSI_FILE_TYPE(config->Flags) == IMSCSI_FILE_TYPE_AWEALLOC ?
            ", Physical Memory" :
            IMSCSI_FILE_TYPE(config->Flags) == IMSCSI_FILE_TYPE_PARALLEL_IO ?
            ", Parallel I/O Image File" :
            ", Queued I/O Image File",
            IMSCSI_DEVICE_TYPE(config->Flags) ==
            IMSCSI_DEVICE_TYPE_CD ? ", CD-ROM" :
            IMSCSI_DEVICE_TYPE(config->Flags) ==
            IMSCSI_DEVICE_TYPE_RAW ? ", RAW" :
            IMSCSI_DEVICE_TYPE(config->Flags) ==
            IMSCSI_DEVICE_TYPE_FD ? ", Floppy" : ", HDD",
            config->Flags & IMSCSI_IMAGE_MODIFIED ? ", Modified" : "");

        flushall();

        // Now enumerate disk volumes

        BYTE port_number;
        HANDLE device = ImScsiOpenScsiAdapter(&port_number);
        if (device == INVALID_HANDLE_VALUE)
        {
            PrintLastError(L"Error opening virtual SCSI adapter:");
            return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
        }

        CloseHandle(device);

        DWORD disk_number;

        device = ImScsiOpenDiskByDeviceNumber(DeviceNumber, port_number,
            &disk_number);

        if (device == INVALID_HANDLE_VALUE)
        {
            PrintLastError(L"Cannot find any associated PhysicalDrive object:");
            return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
        }

        CloseHandle(device);

        WCHAR vol_name[50];

        device = FindFirstVolume(vol_name, _countof(vol_name));

        if (device == INVALID_HANDLE_VALUE)
        {
            PrintLastError(L"Error enumerating disk volumes:");
            return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
        }

        DWORD dw;

        WHeapMem<WCHAR> mnt_name(UNICODE_STRING_MAX_BYTES,
            HEAP_GENERATE_EXCEPTIONS);

        do
        {
            SCSI_ADDRESS address;

            vol_name[48] = 0;

            HANDLE vol_handle = CreateFile(vol_name, 0,
                FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0,
                NULL);

            wcscat(vol_name + 48, L"\\");

            if (vol_handle == INVALID_HANDLE_VALUE)
            {
                break;
            }

            if (!DeviceIoControl(vol_handle,
                IOCTL_SCSI_GET_ADDRESS,
                NULL, 0,
                &address, sizeof(address),
                &dw, NULL))
            {
                CloseHandle(vol_handle);
                continue;
            }

            if ((address.PortNumber != port_number) ||
                (address.PathId != DeviceNumber.PathId) ||
                (address.TargetId != DeviceNumber.TargetId) ||
                (address.Lun != DeviceNumber.Lun))
            {
                CloseHandle(vol_handle);
                continue;
            }

            STORAGE_DEVICE_NUMBER device_number;
            if (!DeviceIoControl(vol_handle,
                IOCTL_STORAGE_GET_DEVICE_NUMBER,
                NULL, 0,
                &device_number, sizeof(device_number),
                &dw, NULL))
            {
                CloseHandle(vol_handle);
                continue;
            }

            if ((device_number.DeviceNumber != disk_number) ||
                (device_number.DeviceType != FILE_DEVICE_DISK) ||
                (((LONG)device_number.PartitionNumber) <= 0))
            {
                CloseHandle(vol_handle);
                continue;
            }

            CloseHandle(vol_handle);

            ImScsiOemPrintF(stdout, "Contains volume %1!ws!", vol_name);

            if (!ImScsiGetVolumePathNamesForVolumeName(vol_name, mnt_name,
                (DWORD)mnt_name.Count(), &dw))
            {
                PrintLastError(L"Error enumerating mount points:");
                continue;
            }

            for (LPWSTR mnt = mnt_name;
                *mnt != 0;
                mnt += wcslen(mnt) + 1)
            {
                ImScsiOemPrintF(stdout, "  Mounted at %1!ws!", mnt);
            }

        } while (FindNextVolume(device, vol_name, _countof(vol_name)));

        FindVolumeClose(device);
    }

    return IMSCSI_CLI_SUCCESS;
}

// Prints a list of current virtual disk devices. If NumericPrint is TRUE a
// simple number list is printed, otherwise each device object name with path
// is printed.
int
ImScsiCliQueryStatusDriver(BOOL NumericPrint)
{
    HANDLE adapter = ImScsiOpenScsiAdapter();

    if (adapter == INVALID_HANDLE_VALUE)
    {
        if (GetLastError() == ERROR_FILE_NOT_FOUND)
        {
            fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
            return IMSCSI_CLI_ERROR_DRIVER_NOT_INSTALLED;
        }
        else
        {
            fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
            return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
        }
    }

    ULONG required = 0;
    if ((!ImScsiGetDeviceList(0, adapter, NULL, &required)) &&
        (GetLastError() != ERROR_MORE_DATA))
    {
        NtClose(adapter);
        if (GetLastError() == ERROR_FILE_NOT_FOUND)
        {
            fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
            return IMSCSI_CLI_ERROR_DRIVER_NOT_INSTALLED;
        }
        else
        {
            PrintLastError(L"Cannot control the Arsenal Image Mounter:");
            return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
        }
    }

    if (required == 0)
    {
        NtClose(adapter);

        if (!NumericPrint)
            puts("No virtual disks.");

        return IMSCSI_CLI_ERROR_DEVICE_NOT_FOUND;
    }

    WHeapMem<DEVICE_NUMBER> device_list(required * sizeof(DEVICE_NUMBER),
        HEAP_GENERATE_EXCEPTIONS);

    if (!ImScsiGetDeviceList(required, adapter, device_list, &required))
    {
        NtClose(adapter);

        PrintLastError(L"Cannot control the Arsenal Image Mounter:");
        return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
    }

    NtClose(adapter);

    for (ULONG counter = 0; counter < required; counter++)
    {
        if (NumericPrint)
        {
            printf("%.6X\n",
                device_list[counter].LongNumber);
            continue;
        }

        printf("Device number %.6X\n",
            device_list[counter].LongNumber);

        ImScsiCliQueryStatusDevice(device_list[counter], NULL);

        puts("");
    }

    if (!NumericPrint)
    {
        printf("%u device%s found.\n", required, required == 1 ? "" : "s");
    }

    return IMSCSI_CLI_SUCCESS;
}

// Changes flags for an existing virtual disk. FlagsToChange specifies which
// flag bits to change,
// (0=not touch, 1=set to corresponding bit value in Flags parameter).
int
ImScsiCliChangeFlags(DEVICE_NUMBER DeviceNumber,
DWORD FlagsToChange, DWORD Flags)
{
    HANDLE adapter = ImScsiOpenScsiAdapter();

    if (adapter == INVALID_HANDLE_VALUE)
    {
        if (GetLastError() == ERROR_FILE_NOT_FOUND)
        {
            fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
            return IMSCSI_CLI_ERROR_DRIVER_NOT_INSTALLED;
        }
        else
        {
            fprintf(stderr, "Arsenal Image Mounter not installed.\r\n");
            return IMSCSI_CLI_ERROR_DRIVER_INACCESSIBLE;
        }
    }

    if (!ImScsiChangeFlags(NULL, adapter, DeviceNumber, FlagsToChange,
        Flags))
    {
        NtClose(adapter);
        PrintLastError();
        return IMSCSI_CLI_ERROR_DEVICE_INACCESSIBLE;
    }

    NtClose(adapter);

    return 0;
}

// Extends an existing virtual disk, identified by either device number or
// mount point.
int
ImScsiCliExtendDevice(DEVICE_NUMBER DeviceNumber,
LARGE_INTEGER ExtendSize)
{
    DeviceNumber;
    ExtendSize;

    fprintf(stderr, "Not implemented.\n");

    return IMSCSI_CLI_ERROR_FATAL;
}

// Entry function. Translates command line switches and parameters and calls
// corresponding functions to carry out actual tasks.
int
__cdecl
wmain(int argc, LPWSTR argv[])
{
    // Catch debug messages from API
    ImScsiSetDebugMessageCallback(NULL, DebugMessageCallback);

    if ((argc == 2) && (_wcsicmp(argv[1], L"--version") == 0))
    {
        puts(
            "Control program for the Arsenal Image Mounter.\r\n"
            "\n"
            "Version " PHDSKMNT_RC_VERSION_STR " - (Compiled " __DATE__ ")\r\n"
            "\n"
            "Copyright (C) 2012-2015 Arsenal Recon.\r\n"
            "\n"
            "\n"
            "http://www.ArsenalRecon.com\r\n"
            "\n"
            "Arsenal Image Mounter including its kernel driver, API library,\r\n"
            "MountTool and Control program (\"the Software\")\r\n"
            "are provided \"AS IS\" and \"WITH ALL FAULTS,\" without warranty\r\n"
            "of any kind, including without limitation the warranties of\r\n"
            "merchantability, fitness for a particular purpose and\r\n"
            "non - infringement.Arsenal makes no warranty that the Software\r\n"
            "is free of defects or is suitable for any particular purpose.\r\n"
            "In no event shall Arsenal be responsible for loss or damages\r\n"
            "arising from the installation or use of the Software, including\r\n"
            "but not limited to any indirect, punitive, special, incidental\r\n"
            "or consequential damages of any character including, without\r\n"
            "limitation, damages for loss of goodwill, work stoppage,\r\n"
            "computer failure or malfunction, or any and all other\r\n"
            "commercial damages or losses.The entire risk as to the\r\n"
            "quality and performance of the Software is borne by you.Should\r\n"
            "the Software prove defective, you and not Arsenal assume the\r\n"
            "entire cost of any service and repair.\r\n"
            "\n"
            "Arsenal Consulting, Inc. (d/b/a Arsenal Recon) retains the copyright to the\r\n"
            "Arsenal Image Mounter source code being made available under terms of the\r\n"
            "Affero General Public License v3.\r\n"
            "(http://www.fsf.org/licensing/licenses/agpl-3.0.html). This source code may\r\n"
            "be used in projects that are licensed so as to be compatible with AGPL v3.\r\n"
            "\n"
            "Contributors to Arsenal Image Mounter must sign the Arsenal Contributor\r\n"
            "Agreement(\"ACA\").The ACA gives Arsenal and the contributor joint\r\n"
            "copyright interests in the code.\r\n"
            "\n"
            "If your project is not licensed under an AGPL v3 compatible license,\r\n"
            "contact us directly regarding alternative licensing.");

        DWORD library_version;
        DWORD driver_version;

        auto rc = ImScsiGetVersion(&library_version, &driver_version);

        printf("\nCurrently installed Arsenal Image Mounter:\n"
            "Library version: %u.%u.%u.%u\n",
            library_version >> 24,
            (library_version >> 16) & 0xff,
            (library_version >> 8) & 0xff,
            library_version & 0xff);

        if (rc)
        {
            printf("Driver version: %u.%u.%u.%u\n",
                driver_version >> 24,
                (driver_version >> 16) & 0xff,
                (driver_version >> 8) & 0xff,
                driver_version & 0xff);
        }
        else
        {
            PrintLastError(L"Error checking driver version:");
        }

        rc = ImDiskGetVersion(&library_version, NULL);

        printf("Using library functions from ImDisk Virtual Disk Driver version: %u.%u.%u\n",
            library_version >> 8,
            (library_version >> 4) & 0xf,
            library_version & 0xf);

        return 0;
    }

    if ((argc >= 2) &&
        ((_wcsicmp(argv[1], L"--install") == 0) ||
        (_wcsicmp(argv[1], L"--uninstall") == 0) ||
        (_wcsicmp(argv[1], L"--rescan") == 0)))
    {
        return wmainSetup(argc - 1, argv + 1);
    }

    enum
    {
        OP_MODE_NONE,
        OP_MODE_CREATE,
        OP_MODE_REMOVE,
        OP_MODE_QUERY,
        OP_MODE_EDIT
    } op_mode = OP_MODE_NONE;
    DWORD flags = 0;
    BOOL native_path = FALSE;
    BOOL numeric_print = FALSE;
    BOOL force_dismount = FALSE;
    BOOL emergency_remove = FALSE;
    LPWSTR file_name = NULL;
    LPWSTR format_options = NULL;
    BOOL save_settings = FALSE;
    DEVICE_NUMBER device_number;
    device_number.LongNumber = IMSCSI_AUTO_DEVICE_NUMBER;
    LPWSTR mount_point = NULL;
    LARGE_INTEGER disk_geometry = { 0 };
    ULONG bytes_per_sector = 0;
    LARGE_INTEGER image_offset = { 0 };
    BOOL auto_find_offset = FALSE;
    DWORD flags_to_change = 0;
    int ret = 0;

    // Argument parse loop
    while (argc-- > 1)
    {
        argv++;

        if ((wcslen(argv[0]) == 2) && (argv[0][0] == L'-'))
        {
            switch (argv[0][1])
            {
            case L'a':
                if (op_mode != OP_MODE_NONE)
                    ImScsiSyntaxHelp();

                op_mode = OP_MODE_CREATE;
                break;

            case L'd':
            case L'D':
            case L'R':
                if (op_mode != OP_MODE_NONE)
                    ImScsiSyntaxHelp();

                op_mode = OP_MODE_REMOVE;

                if (argv[0][1] == L'D')
                    force_dismount = TRUE;

                if (argv[0][1] == L'R')
                {
                    force_dismount = TRUE;
                    emergency_remove = TRUE;
                }

                break;

            case L'l':
                if (op_mode != OP_MODE_NONE)
                    ImScsiSyntaxHelp();

                op_mode = OP_MODE_QUERY;
                break;

            case L'e':
                if (op_mode != OP_MODE_NONE)
                    ImScsiSyntaxHelp();

                op_mode = OP_MODE_EDIT;
                break;

            case L't':
                if ((op_mode != OP_MODE_CREATE) |
                    (argc < 2) |
                    (IMSCSI_TYPE(flags) != 0))
                    ImScsiSyntaxHelp();

                if (wcscmp(argv[1], L"file") == 0)
                    flags |= IMSCSI_TYPE_FILE;
                else if (wcscmp(argv[1], L"vm") == 0)
                    flags |= IMSCSI_TYPE_VM;
                else if (wcscmp(argv[1], L"proxy") == 0)
                    flags |= IMSCSI_TYPE_PROXY;
                else
                    ImScsiSyntaxHelp();

                argc--;
                argv++;
                break;

            case L'n':
                numeric_print = TRUE;
                break;

            case L'o':
                if (((op_mode != OP_MODE_CREATE) & (op_mode != OP_MODE_EDIT)) |
                    (argc < 2))
                    ImScsiSyntaxHelp();

                {
                    LPWSTR opt;

                    for (opt = wcstok(argv[1], L",");
                        opt != NULL;
                        opt = wcstok(NULL, L","))
                        if (wcscmp(opt, L"ro") == 0)
                        {
                            if (IMSCSI_READONLY(flags_to_change))
                                ImScsiSyntaxHelp();

                            flags_to_change |= IMSCSI_OPTION_RO;
                            flags |= IMSCSI_OPTION_RO;
                        }
                        else if (wcscmp(opt, L"rw") == 0)
                        {
                            if (IMSCSI_READONLY(flags_to_change))
                                ImScsiSyntaxHelp();

                            flags_to_change |= IMSCSI_OPTION_RO;
                            flags &= ~IMSCSI_OPTION_RO;
                        }
                        else if (wcscmp(opt, L"fksig") == 0)
                        {
                            flags_to_change |= IMSCSI_FAKE_DISK_SIG_IF_ZERO;
                            flags |= IMSCSI_FAKE_DISK_SIG_IF_ZERO;
                        }
                        else if (wcscmp(opt, L"sparse") == 0)
                        {
                            flags_to_change |= IMSCSI_OPTION_SPARSE_FILE;
                            flags |= IMSCSI_OPTION_SPARSE_FILE;
                        }
                        else if (wcscmp(opt, L"rem") == 0)
                        {
                            if (IMSCSI_REMOVABLE(flags_to_change))
                                ImScsiSyntaxHelp();

                            flags_to_change |= IMSCSI_OPTION_REMOVABLE;
                            flags |= IMSCSI_OPTION_REMOVABLE;
                        }
                        else if (wcscmp(opt, L"fix") == 0)
                        {
                            if (IMSCSI_REMOVABLE(flags_to_change))
                                ImScsiSyntaxHelp();

                            flags_to_change |= IMSCSI_OPTION_REMOVABLE;
                            flags &= ~IMSCSI_OPTION_REMOVABLE;
                        }
                        else if (wcscmp(opt, L"saved") == 0)
                        {
                            if (op_mode != OP_MODE_EDIT)
                                ImScsiSyntaxHelp();

                            flags_to_change |= IMSCSI_IMAGE_MODIFIED;
                            flags &= ~IMSCSI_IMAGE_MODIFIED;
                        }
                        // None of the other options are valid with the -e parameter.
                        else if (op_mode != OP_MODE_CREATE)
                            ImScsiSyntaxHelp();
                        else if (wcscmp(opt, L"ip") == 0)
                        {
                            if ((IMSCSI_TYPE(flags) != IMSCSI_TYPE_PROXY) |
                                (IMSCSI_PROXY_TYPE(flags) != IMSCSI_PROXY_TYPE_DIRECT))
                                ImScsiSyntaxHelp();

                            native_path = TRUE;
                            flags |= IMSCSI_PROXY_TYPE_TCP;
                        }
                        else if (wcscmp(opt, L"comm") == 0)
                        {
                            if ((IMSCSI_TYPE(flags) != IMSCSI_TYPE_PROXY) |
                                (IMSCSI_PROXY_TYPE(flags) != IMSCSI_PROXY_TYPE_DIRECT))
                                ImScsiSyntaxHelp();

                            native_path = TRUE;
                            flags |= IMSCSI_PROXY_TYPE_COMM;
                        }
                        else if (wcscmp(opt, L"shm") == 0)
                        {
                            if ((IMSCSI_TYPE(flags) != IMSCSI_TYPE_PROXY) |
                                (IMSCSI_PROXY_TYPE(flags) != IMSCSI_PROXY_TYPE_DIRECT))
                                ImScsiSyntaxHelp();

                            flags |= IMSCSI_PROXY_TYPE_SHM;
                        }
                        else if (wcscmp(opt, L"awe") == 0)
                        {
                            if (((IMSCSI_TYPE(flags) != IMSCSI_TYPE_FILE) &
                                (IMSCSI_TYPE(flags) != 0)) |
                                (IMSCSI_FILE_TYPE(flags) != 0))
                                ImScsiSyntaxHelp();

                            flags |= IMSCSI_TYPE_FILE | IMSCSI_FILE_TYPE_AWEALLOC;
                        }
                        else if (wcscmp(opt, L"par") == 0)
                        {
                            if (((IMSCSI_TYPE(flags) != IMSCSI_TYPE_FILE) &
                                (IMSCSI_TYPE(flags) != 0)) |
                                (IMSCSI_FILE_TYPE(flags) != 0))
                                ImScsiSyntaxHelp();

                            flags |= IMSCSI_TYPE_FILE | IMSCSI_FILE_TYPE_PARALLEL_IO;
                        }
                        else if (wcscmp(opt, L"bswap") == 0)
                        {
                            flags |= IMSCSI_OPTION_BYTE_SWAP;
                        }
                        else if (IMSCSI_DEVICE_TYPE(flags) != 0)
                            ImScsiSyntaxHelp();
                        else if (wcscmp(opt, L"hd") == 0)
                            flags |= IMSCSI_DEVICE_TYPE_HD;
                        else if (wcscmp(opt, L"fd") == 0)
                            flags |= IMSCSI_DEVICE_TYPE_FD;
                        else if (wcscmp(opt, L"cd") == 0)
                            flags |= IMSCSI_DEVICE_TYPE_CD;
                        else if (wcscmp(opt, L"raw") == 0)
                            flags |= IMSCSI_DEVICE_TYPE_RAW;
                        else
                            ImScsiSyntaxHelp();
                }

                argc--;
                argv++;
                break;

            case L'f':
            case L'F':
                if ((op_mode != OP_MODE_CREATE) |
                    (argc < 2) |
                    (file_name != NULL))
                    ImScsiSyntaxHelp();

                if (argv[0][1] == L'F')
                    native_path = TRUE;

                file_name = argv[1];

                argc--;
                argv++;
                break;

            case L's':
                if (((op_mode != OP_MODE_CREATE) & (op_mode != OP_MODE_EDIT)) |
                    (argc < 2) |
                    (disk_geometry.QuadPart != 0))
                    ImScsiSyntaxHelp();

                {
                    WCHAR suffix = 0;

                    (void)swscanf(argv[1], L"%I64i%c",
                        &disk_geometry, &suffix);

                    switch (suffix)
                    {
                    case 0:
                        break;
                    case '%':
                        if ((disk_geometry.QuadPart <= 0) |
                            (disk_geometry.QuadPart >= 100))
                            ImScsiSyntaxHelp();

                        {
                            MEMORYSTATUS memstat;
#pragma warning(suppress: 28159)
                            GlobalMemoryStatus(&memstat);
                            disk_geometry.QuadPart =
                                disk_geometry.QuadPart *
                                memstat.dwAvailPhys / 100;
                        }

                        break;
                    case 'T':
                        disk_geometry.QuadPart <<= 10;
                    case 'G':
                        disk_geometry.QuadPart <<= 10;
                    case 'M':
                        disk_geometry.QuadPart <<= 10;
                    case 'K':
                        disk_geometry.QuadPart <<= 10;
                        break;
                    case 'b':
                        disk_geometry.QuadPart <<= 9;
                        break;
                    case 't':
                        disk_geometry.QuadPart *= 1000;
                    case 'g':
                        disk_geometry.QuadPart *= 1000;
                    case 'm':
                        disk_geometry.QuadPart *= 1000;
                    case 'k':
                        disk_geometry.QuadPart *= 1000;
                        break;
                    default:
                        fprintf(stderr, "Unsupported size suffix: '%wc'\n",
                            suffix);
                        return IMSCSI_CLI_ERROR_BAD_SYNTAX;
                    }

                    if (disk_geometry.QuadPart < 0)
                    {
                        MEMORYSTATUS memstat;
#pragma warning(suppress: 28159)
                        GlobalMemoryStatus(&memstat);
                        disk_geometry.QuadPart =
                            memstat.dwAvailPhys +
                            disk_geometry.QuadPart;

                        if (disk_geometry.QuadPart < 0)
                        {
                            fprintf(stderr,
                                "Not enough memory, there is currently \r\n"
                                "%.4g %s free physical memory.\n",
                                _h(memstat.dwAvailPhys),
                                _p(memstat.dwAvailPhys));

                            return IMSCSI_CLI_ERROR_NOT_ENOUGH_MEMORY;
                        }
                    }
                }

                argc--;
                argv++;
                break;

            case L'S':
                if ((op_mode != OP_MODE_CREATE) |
                    (argc < 2) |
                    (bytes_per_sector != 0))
                    ImScsiSyntaxHelp();

                if (!iswdigit(argv[1][0]))
                    ImScsiSyntaxHelp();

                bytes_per_sector = wcstoul(argv[1], NULL, 0);

                argc--;
                argv++;
                break;

            case L'b':
                if ((op_mode != OP_MODE_CREATE) |
                    (argc < 2) |
                    (image_offset.QuadPart != 0) |
                    (auto_find_offset != FALSE))
                    ImScsiSyntaxHelp();

                if (wcscmp(argv[1], L"auto") == 0)
                    auto_find_offset = TRUE;
                else
                {
                    WCHAR suffix = 0;

                    (void)swscanf(argv[1], L"%I64u%c",
                        &image_offset, &suffix);

                    switch (suffix)
                    {
                    case 0:
                        break;
                    case 'T':
                        image_offset.QuadPart <<= 10;
                    case 'G':
                        image_offset.QuadPart <<= 10;
                    case 'M':
                        image_offset.QuadPart <<= 10;
                    case 'K':
                        image_offset.QuadPart <<= 10;
                        break;
                    case 'b':
                        image_offset.QuadPart <<= 9;
                        break;
                    case 't':
                        image_offset.QuadPart *= 1000;
                    case 'g':
                        image_offset.QuadPart *= 1000;
                    case 'm':
                        image_offset.QuadPart *= 1000;
                    case 'k':
                        image_offset.QuadPart *= 1000;
                    default:
                        fprintf(stderr, "Unsupported size suffix: '%wc'\n",
                            suffix);
                        return IMSCSI_CLI_ERROR_BAD_SYNTAX;
                    }
                }

                argc--;
                argv++;
                break;

            case L'p':
                if ((op_mode != OP_MODE_CREATE) |
                    (argc < 2) |
                    (format_options != NULL))
                    ImScsiSyntaxHelp();

                format_options = argv[1];

                argc--;
                argv++;
                break;

            case L'P':
                if ((op_mode != OP_MODE_CREATE) &
                    (op_mode != OP_MODE_REMOVE))
                    ImScsiSyntaxHelp();

                save_settings = TRUE;

                break;

            case L'u':
                if ((argc < 2) |
                    (device_number.LongNumber != IMSCSI_AUTO_DEVICE_NUMBER))
                    ImScsiSyntaxHelp();

                LPWSTR endptr;
                device_number.LongNumber = wcstoul(argv[1], &endptr, 16);
                if (*endptr != 0)
                    ImScsiSyntaxHelp();

                argc--;
                argv++;
                break;

            case L'm':
                if ((argc < 2) ||
                    (mount_point != NULL))
                    ImScsiSyntaxHelp();

                mount_point = argv[1];

                argc--;
                argv++;
                break;

            default:
                ImScsiSyntaxHelp();
            }
        }
        else
        {
            ImScsiSyntaxHelp();
        }
    }

    // Switch block for operation switch found on command line.
    switch (op_mode)
    {
    case OP_MODE_CREATE:
    {
        if (auto_find_offset)
            if (file_name == NULL)
                ImScsiSyntaxHelp();
            else
                ImDiskGetOffsetByFileExt(file_name, &image_offset);

        ret = ImScsiCliCreateDevice(&device_number,
            &disk_geometry,
            bytes_per_sector,
            &image_offset,
            flags,
            file_name,
            native_path,
            numeric_print,
            save_settings,
            mount_point,
            format_options);

        if (ret != 0)
            return ret;

        puts("Done.");

        return 0;
    }

    case OP_MODE_REMOVE:
        return ImScsiCliRemoveDevice(device_number, mount_point, force_dismount,
            save_settings, emergency_remove);

    case OP_MODE_QUERY:
        if ((device_number.LongNumber == IMSCSI_AUTO_DEVICE_NUMBER) &
            (mount_point == NULL))
            return !ImScsiCliQueryStatusDriver(numeric_print);

        return ImScsiCliQueryStatusDevice(device_number, mount_point);

    case OP_MODE_EDIT:
        if ((device_number.LongNumber == IMSCSI_AUTO_DEVICE_NUMBER) &
            (mount_point == NULL))
            ImScsiSyntaxHelp();

        if (flags_to_change != 0)
            ret = ImScsiCliChangeFlags(device_number, flags_to_change,
            flags);

        if (disk_geometry.QuadPart > 0)
        {
            if (mount_point != NULL)
                ImScsiSyntaxHelp();

            ret = ImScsiCliExtendDevice(device_number,
                disk_geometry);
        }

        return ret;
    }

    ImScsiSyntaxHelp();
}

#ifndef _DEBUG

LONG
WINAPI
ExceptionFilter(LPEXCEPTION_POINTERS ExceptionInfo)
{
    LPSTR MsgBuf = NULL;

    if (FormatMessageA(FORMAT_MESSAGE_MAX_WIDTH_MASK |
        FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_FROM_HMODULE |
        FORMAT_MESSAGE_IGNORE_INSERTS,
        GetModuleHandle(L"ntdll.dll"),
        ExceptionInfo->ExceptionRecord->ExceptionCode, 0,
        (LPSTR)&MsgBuf, 0, NULL))
    {
        CharToOemA(MsgBuf, MsgBuf);
    }
    else
    {
        MsgBuf = NULL;
    }

    if (MsgBuf != NULL)
    {
        fprintf(stderr, "\n"
            "%s\n", MsgBuf);

        LocalFree(MsgBuf);
    }

    fprintf(stderr,
        "\n"
        "Fatal error - unhandled exception.\n"
        "\n"
        "Exception 0x%X at address 0x%p\n",
        ExceptionInfo->ExceptionRecord->ExceptionCode,
        ExceptionInfo->ExceptionRecord->ExceptionAddress);

    for (DWORD i = 0;
        i < ExceptionInfo->ExceptionRecord->NumberParameters;
        i++)
    {
        fprintf(stderr,
            "Parameter %u: 0x%p\n",
            i + 1,
            ExceptionInfo->ExceptionRecord->ExceptionInformation[i]);
    }

    flushall();
    ExitProcess((UINT)-1);
}

// We have our own EXE entry to be less dependent of
// specific MSVCRT code that may not be available in older Windows versions.
// It also saves some EXE file size.
extern "C"
__declspec(noreturn)
void
__cdecl
wmainCRTStartup()
{
    SetUnhandledExceptionFilter(ExceptionFilter);

    int argc = 0;
    LPWSTR *argv = CommandLineToArgvW(GetCommandLine(), &argc);

    if (argv == NULL)
    {
        MessageBoxA(NULL,
            "This program requires Windows NT/2000/XP.",
            "Arsenal Image Mounter",
            MB_ICONSTOP);

        ExitProcess((UINT)-1);
    }

    exit(wmain(argc, argv));
}

#endif
