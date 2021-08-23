
/// aimapi.h
/// Declarations for Arsenal Image Mounter public Win32 API in aimapi.dll.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#ifndef AIMAPI_API

#ifdef AIMAPI_EXPORTS
#define AIMAPI_API __declspec(dllexport)
#else
#define AIMAPI_API __declspec(dllimport)
#endif

#ifndef CPP_DEF_ZERO
#ifdef __cplusplus
#define CPP_DEF_ZERO = 0
#else
#define CPP_DEF_ZERO
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

#pragma region Manage behaviour of this API

    /**
    Get behaviour flags for API.
    */
    AIMAPI_API ULONGLONG
        WINAPI
        ImScsiGetAPIFlags();

    /**
    Set behaviour flags for API. Returns previously defined flag field.

    Flags        New flags value to set.
    */
    AIMAPI_API ULONGLONG
        WINAPI
        ImScsiSetAPIFlags(ULONGLONG Flags);

    /**
    Check that the user-mode library and miniport driver version matches for
    an open adapter or device object.

    DeviceHandle Handle to an open virtual disk or SCSI adapter.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiCheckDriverVersion(IN HANDLE DeviceHandle);

    /**
    Retrieves the version numbers of the user-mode API library and the kernel-
    mode driver.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiGetVersion(IN OUT PULONG LibraryVersion OPTIONAL CPP_DEF_ZERO,
        IN OUT PULONG DriverVersion OPTIONAL CPP_DEF_ZERO);

#pragma endregion

#pragma region Manage virtual disks

    /**
    Builds a list of currently existing virtual disks.

    ListLength      Set this parameter to number of DEVICE_NUMBER elements
    that can be store at the location pointed to by DeviceList parameter.
    This parameter must be at least 3 for this function to work
    correctly.

    DeviceList      Pointer to memory location where one DEVICE_NUMBER object
    will be stored for each currently existing virtual device.

    Upon return, NumberOfDevices will contain number of currently
    existing virtual disks. If DeviceList is too small to contain all
    items as indicated by ListLength parameter, number of existing devices will
    be stored at NumberOfDevices location, but not all items will be stored in
    DeviceList.

    If an error occurs, this function returns FALSE and GetLastError
    will return an error code. If successful, the function returns TRUE and
    first element at location pointed to by DeviceList will contain number of
    devices currently on the system, i.e. number of elements following the first
    one in DeviceList.

    If DeviceList buffer is too small, the function returns FALSE and
    GetLastError returns ERROR_MORE_DATA. In that case, number of
    existing devices is still stored at location pointed to by NumberOfDevices
    parameter. That value indicates how large the buffer needs to be to
    successfully store all items.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiGetDeviceList(IN ULONG ListLength,
        IN HANDLE Adapter,
        OUT PDEVICE_NUMBER DeviceList,
        OUT PULONG NumberOfDevices);

    /**
    This function sends an SMP_IMSCSI_QUERY_DEVICE control code to an existing
    device and returns information about the device in an
    IMSCSI_DEVICE_CONFIGURATION structure.

    Config          Pointer to a sufficiently large
    IMSCSI_DEVICE_CONFIGURATION structure to receive all data including the
    image file, if any. When calling this function, set the DeviceNumber
    member to the device number to request information about.

    ConfigSize      The size in bytes of the memory the Config parameter
    points to. The function call will fail if the memory is not
    large enough to hold the entire IMSCSI_DEVICE_CONFIGURATION
    structure.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiQueryDevice(IN HANDLE Adapter,
        IN OUT PIMSCSI_DEVICE_CONFIGURATION Config,
        IN ULONG ConfigSize);

    /**
    This function creates a new virtual disk device.

    hWndStatusText  A handle to a window that can display status message text.
    The function will send WM_SETTEXT messages to this window.
    If this parameter is NULL no WM_SETTEXT messages are sent
    and the function acts non-interactive.

    Adapter         Handle to open Arsenal Image Mounter virtual SCSI adapter.
    If set to INVALID_HANDLE_VALUE, the function automatically uses first
    available SCSI adapter.

    DeviceNumber    In: Device number for device to create. Device number must
    not be in use by an existing virtual disk. For automatic
    allocation of device number, use IMSCSI_AUTO_DEVICE_NUMBER
    constant or specify a NULL pointer.

    Out: If DeviceNumber parameter is not NULL, device number
    for created device is returned in DWORD variable pointed to.

    DiskSize    Size of the new virtual disk, in bytes.

    Size parameter can be zero if the device is backed by
    an image file or a proxy device, but not if it is virtual
    memory only device.

    BytesPerSector  Sector size of the new virtual disk.

    BytesPerSector can be zero, in which case default values will be used
    automatically.

    Flags           Bitwise or-ed combination of one of the IMSCSI_TYPE_xxx
    flags, one of the IMSCSI_DEVICE_TYPE_xxx flags and any
    number of IMSCSI_OPTION_xxx flags. The flags can often be
    left zero and left to the driver to automatically select.
    For example, if a virtual disk size is specified to 1440 KB
    and an image file name is not specified, the driver
    automatically selects IMSCSI_TYPE_VM|IMSCSI_DEVICE_TYPE_FD
    for this parameter.

    FileName        Name of disk image file. In case IMSCSI_TYPE_VM is
    specified in the Flags parameter, this file will be loaded
    into the virtual memory-backed disk when created.

    NativePath      Set to TRUE if the FileName parameter specifies an NT
    native path, such as \??\C:\imagefile.img or FALSE if it
    specifies a Win32/DOS-style path such as C:\imagefile.img.

    MountPoint      Drive letter to assign to the first partition on the new
    virtual disk. It can be specified on the form F: or F:\. It can also
    specify an empty directory on another NTFS volume.

    CreatePartition Set to TRUE to automatically initialize the new virtual
    disk with a partition table and create one partition covering all
    available space. FALSE does not automatically initialize anything on the
    virtual disk.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiCreateDevice(IN HWND hWndStatusText OPTIONAL,
        IN HANDLE Adapter OPTIONAL,
        IN OUT PDEVICE_NUMBER DeviceNumber OPTIONAL,
        IN OUT PLARGE_INTEGER DiskSize OPTIONAL,
        IN OUT LPDWORD BytesPerSector OPTIONAL CPP_DEF_ZERO,
        IN PLARGE_INTEGER ImageOffset OPTIONAL CPP_DEF_ZERO,
        IN OUT LPDWORD Flags OPTIONAL CPP_DEF_ZERO,
        IN LPCWSTR FileName OPTIONAL CPP_DEF_ZERO,
        IN BOOL NativePath CPP_DEF_ZERO,
        IN LPWSTR MountPoint OPTIONAL CPP_DEF_ZERO,
        IN BOOL CreatePartition CPP_DEF_ZERO);

    /**
    This function creates a new virtual disk device.

    hWndStatusText
                    A handle to a window that can display status message text.
                    The function will send WM_SETTEXT messages to this window.
                    If this parameter is NULL no WM_SETTEXT messages are sent
                    and the function acts non-interactive.

    Adapter         
                    Handle to open Arsenal Image Mounter virtual SCSI adapter.
                    If set to INVALID_HANDLE_VALUE, the function automatically
                    uses first available SCSI adapter.

    DeviceNumber    
                    In: Device number for device to create. Device number must
                    not be in use by an existing virtual disk. For automatic
                    allocation of device number, use IMSCSI_AUTO_DEVICE_NUMBER
                    constant or specify a NULL pointer.

                    Out: If DeviceNumber parameter is not NULL, device number
                    for created device is returned in DWORD variable pointed
                    to.

    DiskSize    
                    Size of the new virtual disk, in bytes.

                    Size parameter can be zero if the device is backed by an
                    image file or a proxy device, but not if it is virtual
                    memory only device.

    BytesPerSector
                    Sector size of the new virtual disk.

                    BytesPerSector can be zero, in which case default values
                    will be used automatically.

    Flags           
                    Bitwise or-ed combination of one of the IMSCSI_TYPE_xxx
                    flags, one of the IMSCSI_DEVICE_TYPE_xxx flags and any
                    number of IMSCSI_OPTION_xxx flags. The flags can often be
                    left zero and left to the driver to automatically select.
                    For example, if a virtual disk size is specified to 1440
                    KB and an image file name is not specified, the driver
                    automatically selects IMSCSI_TYPE_VM|IMSCSI_DEVICE_TYPE_FD
                    for this parameter.

    FileName        
                    Name of disk image file. In case IMSCSI_TYPE_VM is
                    specified in the Flags parameter, this file will be loaded
                    into the virtual memory-backed disk when created.

    WriteOverlayFileName
                    Name of write overlay differencing image file to use
                    with IMSCSI_OPTION_WRITE_OVERLAY mode.

    NativePath      
                    Set to TRUE if the FileName parameter specifies an NT
                    native path, such as \??\C:\imagefile.img or FALSE if it
                    specifies a Win32/DOS-style path such as C:\imagefile.img.

    MountPoint      
                    Drive letter to assign to the first partition on the new
                    virtual disk. It can be specified on the form F: or F:\.
                    It can also specify an empty directory on another NTFS
                    volume.

    CreatePartition
                    Set to TRUE to automatically initialize the new virtual
                    disk with a partition table and create one partition
                    covering all available space. FALSE does not automatically
                    initialize anything on the virtual disk.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiCreateDeviceEx(IN HWND hWndStatusText OPTIONAL,
            IN HANDLE Adapter OPTIONAL,
            IN OUT PDEVICE_NUMBER DeviceNumber OPTIONAL,
            IN OUT PLARGE_INTEGER DiskSize OPTIONAL,
            IN OUT LPDWORD BytesPerSector OPTIONAL CPP_DEF_ZERO,
            IN PLARGE_INTEGER ImageOffset OPTIONAL CPP_DEF_ZERO,
            IN OUT LPDWORD Flags OPTIONAL CPP_DEF_ZERO,
            IN LPCWSTR FileName OPTIONAL CPP_DEF_ZERO,
            IN LPCWSTR WriteOverlayFileName OPTIONAL CPP_DEF_ZERO,
            IN BOOL NativePath CPP_DEF_ZERO,
            IN LPWSTR MountPoint OPTIONAL CPP_DEF_ZERO,
            IN BOOL CreatePartition CPP_DEF_ZERO);

    /**
    This function removes (unmounts) an existing virtual disk device.

    hWndStatusText  A handle to a window that can display status message text.
    The function will send WM_SETTEXT messages to this window.
    If this parameter is NULL no WM_SETTEXT messages are sent
    and the function acts non-interactive.

    DeviceNumber    Number of the device to remove.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiRemoveDeviceByNumber(IN HWND hWndStatusText OPTIONAL,
        IN HANDLE Adapter,
        IN DEVICE_NUMBER DeviceNumber);

    /**
    This function removes (unmounts) an existing virtual disk device.

    hWndStatusText  A handle to a window that can display status message text.
    The function will send WM_SETTEXT messages to this window.
    If this parameter is NULL no WM_SETTEXT messages are sent
    and the function acts non-interactive.

    MountPoint      Drive letter of the device to remove. It can be specified
    on the form F: or F:\.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiRemoveDeviceByMountPoint(IN HWND hWndStatusText OPTIONAL,
        IN LPCWSTR MountPoint);

    /**
    This function changes the device characteristics of an existing
    virtual disk device.

    hWndStatusText  A handle to a window that can display status message text.
    The function will send WM_SETTEXT messages to this window.
    If this parameter is NULL no WM_SETTEXT messages are sent
    and the function acts non-interactive.

    DeviceNumber    Number of the device to change.

    FlagsToChange   A bit-field specifying which flags to edit. The flags are
    the same as the option flags in the Flags parameter used
    when a new virtual disk is created. Only flags set in this
    parameter are changed to the corresponding flag value in the
    Flags parameter.

    Flags           New values for the flags specified by the FlagsToChange
    parameter.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiChangeFlags(IN HWND hWndStatusText OPTIONAL,
        IN HANDLE Adapter,
        IN DEVICE_NUMBER DeviceNumber OPTIONAL,
        IN DWORD FlagsToChange CPP_DEF_ZERO,
        IN DWORD Flags CPP_DEF_ZERO);

    /**
    This function extends the size of an existing virtual disk device.

    hWndStatusText  A handle to a window that can display status message text.
    The function will send WM_SETTEXT messages to this window.
    If this parameter is NULL no WM_SETTEXT messages are sent
    and the function acts non-interactive.

    DeviceNumber    Number of the device to extend.

    ExtendSize      A pointer to a LARGE_INTEGER structure that specifies the
    number of bytes to extend the device.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiExtendDevice(IN HWND hWndStatusText OPTIONAL,
        IN HANDLE Adapter,
        IN DEVICE_NUMBER DeviceNumber,
        IN CONST PLARGE_INTEGER ExtendSize);

    /**
    Adds registry settings for creating a virtual disk at system startup (or
    when driver is loaded).

    This function returns TRUE if successful, FALSE otherwise. If FALSE is
    returned, GetLastError could be used to get actual error code.

    Config          Pointer to IMSCSI_DEVICE_CONFIGURATION structure that
    contains device creation settings to save.

    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiSaveRegistrySettings(PIMSCSI_DEVICE_CONFIGURATION Config);

    /**
    Remove registry settings for creating a virtual disk at system startup (or
    when driver is loaded).

    This function returns TRUE if successful, FALSE otherwise. If FALSE is
    returned, GetLastError could be used to get actual error code.

    DeviceNumber    Device number specified in registry settings.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiRemoveRegistrySettings(DEVICE_NUMBER DeviceNumber);

    /**
    Retrieves number of auto-loading devices at system startup, or when driver
    is loaded. This is the value of the LoadDevices registry value for
    imdisk.sys driver.

    This function returns TRUE if successful, FALSE otherwise. If FALSE is
    returned, GetLastError could be used to get actual error code.

    LoadDevicesValue
    Pointer to variable that receives the value.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiGetRegistryAutoLoadDevices(OUT LPDWORD LoadDevicesValue);

    /**
    Checks whether a disk volume has any extents on the disk with specified
    physical disk number.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiVolumeUsesDisk(IN HANDLE Volume,
            IN DWORD DiskNumber);

    /**
    Returns a list of Arsenal Image Mounter DEVICE_NUMBER items that
    correspond to each disk in the set of extents for a given disk volume.

    This function now only returns devices that correspond to devices mounted
    by an Arsenal Image Mounter virtual SCSI adapter.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetDeviceNumbersForVolume(IN HANDLE Volume,
            IN DWORD PortNumber,
            OUT PDEVICE_NUMBER DeviceNumbers,
            IN DWORD NumberOfItems,
            OUT LPDWORD NeededNumberOfItems);

    /**
    Returns a list of Arsenal Image Mounter DEVICE_NUMBER items that
    correspond to each disk in the set of extents for a given disk volume.

    This function now only returns devices that correspond to devices mounted
    by an Arsenal Image Mounter virtual SCSI adapter.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetDeviceNumbersForVolumeEx(IN HANDLE Volume,
            OUT PDEVICE_NUMBER DeviceNumbers,
            OUT LPBYTE PortNumbers,
            IN DWORD NumberOfItems,
            OUT LPDWORD NeededNumberOfItems);

    /**
    Opens the PhysicalDrive disk object that corresponds to a given
    DEVICE_NUMBER connected to specified Arsenal Image Mounter virtual
    SCSI port. Optionally also returns the PhysicalDrive object number
    (disk number) for that disk in the DiskNumber parameter.

    This function now checks whether the specified port number really
    corresponds to an Arsenal Image Mounter virtual SCSI adapter.

    PortNumber can be set to IMSCSI_ANY_PORT_NUMBER. This function does not
    return actual port number of found device. Call
    ImScsiOpenDiskByDeviceNumberEx function if port number is needed.
    */
    AIMAPI_API
        HANDLE
        WINAPI
        ImScsiOpenDiskByDeviceNumber(IN DEVICE_NUMBER DeviceNumber,
            IN DWORD PortNumber,
            OUT LPDWORD DiskNumber OPTIONAL CPP_DEF_ZERO);

    /**
    Opens the PhysicalDrive disk object that corresponds to a given
    DEVICE_NUMBER connected to specified Arsenal Image Mounter virtual
    SCSI port, or any Arsenal Image Mounter SCSI port if the value that
    PortNumber points to specifies IMSCSI_ANY_PORT_NUMBER. If it specifies
    IMSCSI_ANY_PORT_NUMBER, it will upon successful return contain the actual
    port number where the device was found. This function can optionally also
    return the a STORAGE_DEVICE_NUMBER structure with the PhysicalDrive object
    number (disk number) and device type.

    This function only opens devices that belong to an Arsenal Image Mounter
    virtual SCSI adapter.
    */
    AIMAPI_API
        HANDLE
        WINAPI
        ImScsiOpenDiskByDeviceNumberEx(IN DEVICE_NUMBER DeviceNumber,
            IN OUT LPBYTE PortNumber,
            OUT PSTORAGE_DEVICE_NUMBER DiskNumber OPTIONAL CPP_DEF_ZERO);

    /**
    Returns Arsenal Image Mounter DEVICE_NUMBER and SCSI port number
    for an open physical disk ("PhysicalDrive" object).

    This function now checks that the opened disk really belongs to an
    Arsenal Image Mounter virtual SCSI adapter by internally calling
    ImScsiOpenDiskByDeviceNumber.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetDeviceNumberForDisk(HANDLE Device,
            PDEVICE_NUMBER DeviceNumber,
            LPDWORD PortNumber);

    /**
    Returns Arsenal Image Mounter DEVICE_NUMBER and SCSI port number
    for an open physical disk ("PhysicalDrive" object).

    This function now checks that the opened disk really belongs to an
    Arsenal Image Mounter virtual SCSI adapter by internally calling
    ImScsiOpenDiskByDeviceNumber.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetDeviceNumberForDiskEx(HANDLE Device,
            PDEVICE_NUMBER DeviceNumber,
            LPBYTE PortNumber);

#pragma endregion

#pragma region Manage virtual SCSI adapter

    /**
    Finds Arsenal Image Mounter virtual SCSI adapter and opens it.

    Returns handle to open device if successful. If an error occurs,
    INVALID_HANDLE_VALUE is returned and GetLastError returns error code.

    PortNumber      If not NULL, receives SCSI port number of found virtual
    SCSI adapter.
    */
    AIMAPI_API HANDLE
        WINAPI
        ImScsiOpenScsiAdapter(OUT LPBYTE PortNumber OPTIONAL CPP_DEF_ZERO);

    /**
    Opens an Arsenal Image Mounter virtual SCSI adapter with specified SCSI
    port number.

    Returns handle to open device if successful. If an error occurs,
    INVALID_HANDLE_VALUE is returned and GetLastError returns error code.

    PortNumber      SCSI port number of virtual SCSI adapter to open.
    */
    AIMAPI_API HANDLE
        WINAPI
        ImScsiOpenScsiAdapterByScsiPortNumber(IN BYTE PortNumber);

#pragma endregion

#pragma region Manage virtual SCSI adapter

#ifdef _NTDDSCSIH_

    /**
    Returns a SCSI_ADDRESS structure for an open physical disk object.

    This function does not check whether the disk is created by an
    Arsenal Image Mounter virtual SCSI adapter.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetScsiAddressForDisk(IN HANDLE Disk,
            OUT PSCSI_ADDRESS ScsiAddress);

    /**
    Returns SCSI_ADDRESS and STORAGE_DEVICE_NUMBER structures for an open
    physical disk object.

    This function does not check whether the disk is created by an
    Arsenal Image Mounter virtual SCSI adapter.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetScsiAddressForDiskEx(IN HANDLE Disk,
            OUT PSCSI_ADDRESS ScsiAddress,
            OUT PSTORAGE_DEVICE_NUMBER DeviceNumber);

    /**
    Returns a list of SCSI_ADDRESS items that correspond to each disk in the
    set of extents for an open disk volume.

    This function does not check whether the involved disks are created by an
    Arsenal Image Mounter virtual SCSI adapter.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetScsiAddressesForVolume(IN HANDLE Volume,
            OUT PSCSI_ADDRESS ScsiAddresses,
            IN DWORD NumberOfItems,
            OUT LPDWORD NeededNumberOfItems);

    /**
    Returns a list of SCSI_ADDRESS items and disk numbers ("PhysicalDrive"
    numbers) that correspond to each disk in the set of extents for an open
    disk volume.

    This function does not check whether the involved disks are created by an
    Arsenal Image Mounter virtual SCSI adapter.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiGetScsiAddressesForVolumeEx(IN HANDLE Volume,
            OUT PSCSI_ADDRESS ScsiAddresses,
            OUT LPDWORD DiskNumbers,
            IN DWORD NumberOfItems,
            OUT LPDWORD NeededNumberOfItems);

    /**
    Opens the PhysicalDrive disk object that corresponds to a given
    SCSI_ADDRESS. Optionally also returns the PhysicalDrive object number
    (disk number) for that disk in the DiskNumber parameter.

    This function does not check whether the specified SCSI address belongs
    to an Arsenal Image Mounter virtual SCSI adapter.
    */
    AIMAPI_API
        HANDLE
        WINAPI
        ImScsiOpenDiskByScsiAddress(IN SCSI_ADDRESS ScsiAddress,
        OUT LPDWORD DiskNumber OPTIONAL CPP_DEF_ZERO);

    /**
    Sends an SRB_IO_CONTROL to a SCSI miniport or SCSI miniport connected
    device.

    Device          Handle to open device that receives the control.

    ControlCode     The I/O control code to set in SRB_IO_CONTROL header.

    SrbIoControl    Pointer to structure to send. Before sending message,
    certain fields are updated with values of other parameters to this
    function, such as ControlCode and TimeOut. The Length field is set to
    Size parameter minus sizeof(SRB_IO_CONTROL) to indicate amount of data
    after the SRB_IO_CONTROL header that should be sent in this request.

    Size            Total size of structure pointed to by SrbIoControl
    parameter, including the SRB_IO_CONTROL header fields.

    Timeout         Timeout value to set in SRB_IO_CONTROL header.

    ReturnLength    Length of data returned from driver. Indicates how
    many bytes of data are valid in the structure upon return form this
    function.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiDeviceIoControl(HANDLE Device,
        DWORD ControlCode,
        PSRB_IO_CONTROL SrbIoControl,
        DWORD Size,
        DWORD Timeout,
        LPDWORD ReturnLength);

#endif

#pragma endregion

#pragma region Driver setup API

    /**
    This routine installs driver package from a specified directory with
    the driver setup files. Directory needs to contain the same directory tree
    as in official DriverSetup.zip file.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiInstallDriver(IN LPWSTR SetupSource,
        IN HWND OwnerWindow,
        OUT LPBOOL RebootRequired OPTIONAL CPP_DEF_ZERO);

    /**
    This routine removes all device objects created by Arsenal Image Mounter
    driver. This should be called as part of uninstall process before calling
    ImScsiRemoveDriver.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiRemoveDevices(IN HWND OwnerWindow);

    /**
    This routine removes Arsenal Image Mounter driver and related files from
    current system. Always call ImScsiRemoveDevices first to prepare plug-and-
    play system for unloading the driver.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiRemoveDriver(OUT LPBOOL RebootRequired OPTIONAL CPP_DEF_ZERO);

    /**
    Rescans SCSI bus on currently installed Arsenal Image Mounter virtual SCSI
    adapter. This function can be called from applications that cannot detect
    expected newly created devices or that still sees removed devices. It can
    also be called on systems without storport.sys to force scsiport.sys to
    resume forwarding messages down to the Arsenal Image Mounter driver, in
    calls to open the adapter object fails.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiRescanScsiAdapter();

    /**
    Same function as ImScsiRescanScsiAdapter but optionally works in
    asynchronous mode. It returns a handle to an event object that can be
    waited upon to find out when operation is complete. After wait finished,
    the returned event object needs to be closed by calling CloseHandle.
    */
    AIMAPI_API
        HANDLE
        WINAPI
        ImScsiRescanScsiAdapterAsync(BOOL AsyncFlag);

    /**
    Instructs plug-and-play system to re-enumerate devices under the specified
    rootid, or if rootid is NULL, the entire plug-and-play system.
    */
    AIMAPI_API
        DWORD
        WINAPI
        ImScsiScanForHardwareChanges(IN LPWSTR rootid OPTIONAL CPP_DEF_ZERO,
        IN DWORD flags OPTIONAL CPP_DEF_ZERO);

    /**
    Same function as ImScsiScanForHardwareChanges but optionally works in
    asynchronous mode. It returns a handle to an event object that can be
    waited upon to find out when operation is complete. After wait finished,
    the returned event object needs to be closed by calling CloseHandle.
    */
    AIMAPI_API
        HANDLE
        WINAPI
        ImScsiScanForHardwareChangesAsync(BOOL AsyncFlag);

    /**
    Returns the platform identification string such as "Win8.1" that
    corresponds to a subdirectory with driver files for current platform in
    DriverFiles.zip file. Optionally also returns values that indicate if
    current platform supports storport.sys drivers and if current process is
    running in WOW64 subsystem, that is a 32 bit process in 64 bit Windows.
    */
    AIMAPI_API
        LPCWSTR
        WINAPI
        ImScsiGetKernelPlatformCode(
        OUT LPBOOL SupportsStorPort OPTIONAL CPP_DEF_ZERO,
        OUT LPBOOL RunningInWow64 OPTIONAL CPP_DEF_ZERO);

    /**
    Allocates a null character separated list of device ids created by
    specified service. Returns length of this allocated list, in characters.

    The allocated list can be freed by calling LocalFree when no longer
    needed.
    */
    AIMAPI_API
        DWORD
        WINAPI
        ImScsiAllocateDeviceInstanceListForService(IN LPCWSTR service,
        OUT LPWSTR *instances);

#ifdef _M_IX86

    /**
    Calls SetupSetNonInteractiveMode in setupapi.dll, if that function is
    available on the current platform. Otherwise, calls to this function are
    ignored.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiSetupSetNonInteractiveMode(IN BOOL NotInteractiveFlag);

#else

#define ImScsiSetupSetNonInteractiveMode SetupSetNonInteractiveMode

#endif

    /**
    Installs driver on OS versions with storport.sys. Internally used by
    ImScsiInstallDriver. Call ImScsiInstallDriver from applications.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiInstallStorPortDriver(IN LPWSTR SetupSource,
        IN HWND OwnerWindow,
        OUT LPBOOL RebootRequired OPTIONAL CPP_DEF_ZERO);

    /**
    Installs driver on OS versions without storport.sys. Internally used by
    ImScsiInstallDriver. Call ImScsiInstallDriver from applications.
    */
    AIMAPI_API
        BOOL
        WINAPI
        ImScsiInstallScsiPortDriver(IN LPWSTR SetupSource,
        IN HWND OwnerWindow,
        OUT LPBOOL RebootRequired OPTIONAL CPP_DEF_ZERO);

#pragma endregion

#pragma region Misc

    typedef
        BOOL WINAPI fGetVolumePathNamesForVolumeNameW(
        __in   LPCWSTR lpszVolumeName,
        __out  LPWSTR  lpszVolumePathNames,
        __in   DWORD   cchBufferLength,
        __out  PDWORD  lpcchReturnLength);

    typedef fGetVolumePathNamesForVolumeNameW *
        pfGetVolumePathNamesForVolumeNameW;

    typedef
        BOOL WINAPI fGetVolumePathNamesForVolumeNameA(
        __in   LPCWSTR lpszVolumeName,
        __out  LPWSTR  lpszVolumePathNames,
        __in   DWORD   cchBufferLength,
        __out  PDWORD  lpcchReturnLength);

    typedef fGetVolumePathNamesForVolumeNameA *
        pfGetVolumePathNamesForVolumeNameA;

    typedef
        BOOL WINAPI fIsWow64Process(
        __in  HANDLE hProcess,
        __out PBOOL Wow64Process);

    typedef fIsWow64Process *pfIsWow64Process;

    /**
    Returns number of bytes used by a specified null character separated
    string where last string is terminated by double null characters.
    */
    AIMAPI_API
        SIZE_T
        WINAPI
        ImScsiGetMultiStringByteLength(LPCWSTR MultiString);

    /**
    Works like GetVersionEx Win32 API function, but returns true Windows
    version, even in Windows 8.1 and later.

    See documentation for Win32 GetVersionEx for more information about
    parameters and return values.
    */
    AIMAPI_API BOOL
        WINAPI
        ImScsiGetOSVersion(__inout __deref POSVERSIONINFOW lpVersionInformation);

#ifdef _M_IX86

    /**
    Calls the GetVolumePathNamesForVolumeName Win32 API function if that
    function is available on the current platform. Otherwise an internal
    implementation of this routine is called to provide similar functionality.
    */
    AIMAPI_API
        fGetVolumePathNamesForVolumeNameW
        ImScsiGetVolumePathNamesForVolumeName;

#else

#define ImScsiGetVolumePathNamesForVolumeName GetVolumePathNamesForVolumeNameW

#endif

#pragma endregion

#pragma region Debug message tracing

    /**
    Callback function defined by application to receive debug messages from
    functions in aimapi.dll.
    */
    typedef
        VOID
        WINAPI
        fImScsiDebugMessageCallback(LPVOID Context,
        LPWSTR DebugMessage);
    
    typedef fImScsiDebugMessageCallback *pfImScsiDebugMessageCallback;

    /**
    Registers an application provided callback function for receiving debug
    messages from functions in aimapi.dll.
    */
    AIMAPI_API
        VOID
        WINAPI
        ImScsiSetDebugMessageCallback(LPVOID Context,
        pfImScsiDebugMessageCallback DebugMessageCallback);

    /**
    Sends a debug message to function specified in an earlier call to
    ImScsiSetDebugMessageCallback, as well as to attached debuggers, if any.
    
    This function internally calls FormatMessage Win32 API, so FormatString
    parameter must follow specification for that API function.
    */
    AIMAPI_API
        VOID
        CDECL
        ImScsiDebugMessage(LPCWSTR FormatString, ...);

#pragma endregion

#ifdef __cplusplus
}
#endif

#endif

