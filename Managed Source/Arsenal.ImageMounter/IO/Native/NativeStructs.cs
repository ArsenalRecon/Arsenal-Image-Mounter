//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Buffers;
using LTRData.Extensions.Native.Memory;
using LTRData.Extensions.Split;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using IByteCollection = System.Collections.Generic.IReadOnlyCollection<byte>;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator (.NET Framework 4.x compatibility)
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1069 // Enums values should not be duplicated
#pragma warning disable IDE0032 // Use auto property
#pragma warning disable IDE1006 // Naming Styles (follow native struct names instead)
#pragma warning disable IDE0290 // Use primary constructor (cannot be used with unmanaged structs with explicit layout)

namespace Arsenal.ImageMounter.IO.Native;

public readonly struct FileSystemClusterSizes
{
    public FileSystemClusterSizes(int bytesPerSector, int sectorsPerCluster, long numberOfFreeClusters, long totalNumberOfClusters)
    {
        BytesPerSector = bytesPerSector;
        SectorsPerCluster = sectorsPerCluster;
        NumberOfFreeClusters = numberOfFreeClusters;
        TotalNumberOfClusters = totalNumberOfClusters;
    }

    public int BytesPerSector { get; }
    public int SectorsPerCluster { get; }
    public int BytesPerCluster => BytesPerSector * SectorsPerCluster;
    public long NumberOfFreeClusters { get; }
    public long TotalNumberOfClusters { get; }
    public long NumberOfUsedClusters => TotalNumberOfClusters - NumberOfFreeClusters;
}

public readonly struct VOLUME_BITMAP_BUFFER
{
    public long StartingLcn { get; }
    public long BitmapSize { get; }
}

/// <summary>
/// Structure for counted Unicode strings used in NT API calls
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct UNICODE_STRING
{
    /// <summary>
    /// Length in bytes of Unicode string pointed to by Buffer
    /// </summary>
    public ushort Length { get; }

    /// <summary>
    /// Maximum length in bytes of string memory pointed to by Buffer
    /// </summary>
    public ushort MaximumLength { get; }

    /// <summary>
    /// Unicode character buffer in unmanaged memory
    /// </summary>
    public nint Buffer { get; }

    /// <summary>
    /// Returns a <see cref="Span{Char}"/> for the length of the buffer
    /// that is currently in use.
    /// </summary>
    public unsafe Span<char> Span => MemoryMarshal.Cast<byte, char>(new Span<byte>((void*)Buffer, Length));

    /// <summary>
    /// Returns a <see cref="Span{Char}"/> for the complete buffer, including
    /// any currently unused part.
    /// </summary>
    public unsafe Span<char> MaximumSpan => MemoryMarshal.Cast<byte, char>(new Span<byte>((void*)Buffer, MaximumLength));

    /// <summary>
    /// Initialize with pointer to existing unmanaged string
    /// </summary>
    /// <param name="str">Pointer to existing unicode string in managed memory</param>
    /// <param name="byte_count">Length in bytes of string pointed to by <paramref name="str"/></param>
    public UNICODE_STRING(nint str, ushort byte_count)
    {
        Length = byte_count;
        MaximumLength = byte_count;
        Buffer = str;
    }

    /// <summary>
    /// Creates a managed string object from UNICODE_STRING instance.
    /// </summary>
    /// <returns>Managed string</returns>
    public override string ToString()
        => Length == 0 ? string.Empty : Span.ToString();
}

/// <summary>
/// Provides a way to marshal managed strings to UNICODE_STRING in
/// unmanaged memory.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public class UnicodeString
{
    /// <summary>
    /// Length in bytes of Unicode string pointed to by Buffer
    /// </summary>
    public ushort Length { get; }

    /// <summary>
    /// Maximum length in bytes of string memory pointed to by Buffer
    /// </summary>
    public ushort MaximumLength { get; }

    /// <summary>
    /// Unicode character buffer in unmanaged memory
    /// </summary>
    [field: MarshalAs(UnmanagedType.LPWStr)]
    public string? Buffer { get; }

    /// <summary>
    /// Initializes instance with a new managed string
    /// </summary>
    /// <param name="buffer">Managed string</param>
    public UnicodeString(string buffer)
    {
        if (buffer is not null)
        {
            Buffer = buffer;
            MaximumLength = Length = checked((ushort)(buffer.Length << 1));
        }
    }

    /// <summary>
    /// Returns stored managed string
    /// </summary>
    /// <returns>Managed string</returns>
    public override string ToString()
        => Buffer ?? "";

    /// <summary>
    /// Creates a SafeBuffer that marshals this instance to unmanaged memory and keeps
    /// the string pinned until the SafeBuffer instance is disposed.
    /// </summary>
    /// <returns>SafeBuffer instance</returns>
    public NativeStructWrapper<UnicodeString> Pin()
        => new(this);

    /// <summary>
    /// Creates a SafeBuffer that marshals a managed string as UNICODE_STRING in unmanaged
    /// memory and keeps the string pinned until the SafeBuffer instance is disposed.
    /// </summary>
    /// <returns>SafeBuffer instance</returns>
    public static NativeStructWrapper<UnicodeString> Pin(string buffer)
        => new(new(buffer));
}

[Flags]
public enum NtObjectAttributes
{
    Inherit = 0x2,
    Permanent = 0x10,
    Exclusive = 0x20,
    CaseInsensitive = 0x40,
    OpenIf = 0x80,
    OpenLink = 0x100
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct ObjectAttributes
{
    public int Length { get; }

    public nint RootDirectory { get; }

    public nint ObjectName { get; }

    public NtObjectAttributes Attributes { get; }

    public nint SecurityDescriptor { get; }

    public nint SecurityQualityOfService { get; }

    public ObjectAttributes(SafeFileHandle? rootDirectory,
                            SafeBuffer? objectName,
                            NtObjectAttributes objectAttributes,
                            SafeBuffer? securityDescriptor,
                            SafeBuffer? securityQualityOfService)
    {
        Length = Unsafe.SizeOf<ObjectAttributes>();
        RootDirectory = rootDirectory?.DangerousGetHandle() ?? IntPtr.Zero;
        ObjectName = objectName?.DangerousGetHandle() ?? IntPtr.Zero;
        Attributes = objectAttributes;
        SecurityDescriptor = securityDescriptor?.DangerousGetHandle() ?? IntPtr.Zero;
        SecurityQualityOfService = securityQualityOfService?.DangerousGetHandle() ?? IntPtr.Zero;
    }
}

///
/// Structure used with ImScsiQueryDevice and embedded in
/// SRB_IMSCSI_CREATE_DATA structure used with IOCTL_SCSI_MINIPORT
/// requests.
///
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct IMSCSI_DEVICE_CONFIGURATION
{
    public IMSCSI_DEVICE_CONFIGURATION(uint deviceNumber,
                                       long diskSize = 0,
                                       uint bytesPerSector = 0,
                                       ushort writeOverlayFileNameLength = 0,
                                       ushort reserved = 0,
                                       long imageOffset = 0,
                                       int flags = 0,
                                       ushort fileNameLength = 0)
    {
        DeviceNumber = deviceNumber;
        DiskSize = diskSize;
        BytesPerSector = bytesPerSector;
        WriteOverlayFileNameLength = writeOverlayFileNameLength;
        this.reserved = reserved;
        ImageOffset = imageOffset;
        Flags = flags;
        FileNameLength = fileNameLength;
    }

    /// On create this can be set to IMSCSI_AUTO_DEVICE_NUMBER
    public uint DeviceNumber { get; }

    /// Total size in bytes.
    public long DiskSize { get; }

    /// Bytes per sector
    public uint BytesPerSector { get; }

    /// Length of write overlay file name after FileName field, if
    /// Flags field contains write overlay flag.
    public ushort WriteOverlayFileNameLength { get; }

    /// Padding if none of flag specific fields are in use.
    private readonly ushort reserved;

    /// The byte offset in image file where the virtual disk data begins.
    public long ImageOffset { get; }

    /// Creation flags. Type of device and type of connection.
    public int Flags { get; }

    /// Length in bytes of the FileName member.
    public ushort FileNameLength { get; }
}

///
/// Structure used with ImScsiSetDeviceFlags and embedded in
/// SRB_IMSCSI_SET_DEVICE_FLAGS structure used with IOCTL_SCSI_MINIPORT
/// requests.
///
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct IMSCSI_SET_DEVICE_FLAGS
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="deviceNumber"></param>
    /// <param name="flagsToChange"></param>
    /// <param name="flagValues"></param>
    public IMSCSI_SET_DEVICE_FLAGS(uint deviceNumber, uint flagsToChange, uint flagValues)
    {
        DeviceNumber = deviceNumber;
        FlagsToChange = flagsToChange;
        FlagValues = flagValues;
    }

    ///
    public uint DeviceNumber { get; }

    ///
    public uint FlagsToChange { get; }

    ///
    public uint FlagValues { get; }
}

///
/// Structure used with ImScsiExtendSize and embedded in
/// SRB_IMSCSI_EXTEND_SIZE structure used with IOCTL_SCSI_MINIPORT
/// requests.
///
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct IMSCSI_EXTEND_SIZE
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="deviceNumber"></param>
    /// <param name="extendSize"></param>
    public IMSCSI_EXTEND_SIZE(uint deviceNumber, long extendSize)
    {
        DeviceNumber = deviceNumber;
        ExtendSize = extendSize;
    }

    ///
    public uint DeviceNumber { get; }

    ///
    public long ExtendSize { get; }
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly struct REPARSE_DATA_MOUNT_POINT
{
    public REPARSE_DATA_MOUNT_POINT(ushort reparseDataLength,
                                    ushort substituteNameOffset,
                                    ushort substituteNameLength,
                                    ushort printNameOffset,
                                    ushort printNameLength)
    {
        ReparseTag = NativeConstants.IO_REPARSE_TAG_MOUNT_POINT;
        ReparseDataLength = reparseDataLength;
        SubstituteNameOffset = substituteNameOffset;
        SubstituteNameLength = substituteNameLength;
        PrintNameOffset = printNameOffset;
        PrintNameLength = printNameLength;
        Reserved = 0;
    }

    public uint ReparseTag { get; }
    public ushort ReparseDataLength { get; }
    public ushort Reserved { get; }
    public ushort SubstituteNameOffset { get; }
    public ushort SubstituteNameLength { get; }
    public ushort PrintNameOffset { get; }
    public ushort PrintNameLength { get; }
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly struct REPARSE_DATA_SYMLINK
{
    public uint ReparseTag { get; }
    public ushort ReparseDataLength { get; }
    public ushort Reserved { get; }
    public ushort SubstituteNameOffset { get; }
    public ushort SubstituteNameLength { get; }
    public ushort PrintNameOffset { get; }
    public ushort PrintNameLength { get; }
    public SymlinkFlags Flags { get; }
}

[Flags]
public enum SymlinkFlags
{
    FullPath = 0x0,
    Relative = 0x1
}

public enum FsInformationClass : uint
{
    FileFsVolumeInformation = 1U,
    FileFsLabelInformation = 2U,
    FileFsSizeInformation = 3U,
    FileFsDeviceInformation = 4U,
    FileFsAttributeInformation = 5U,
    FileFsControlInformation = 6U,
    FileFsFullSizeInformation = 7U,
    FileFsObjectIdInformation = 8U
}

public enum SystemInformationClass : uint
{
    SystemBasicInformation,  // 0x002C 
    SystemProcessorInformation,  // 0x000C 
    SystemPerformanceInformation,    // 0x0138 
    SystemTimeInformation,   // 0x0020 
    SystemPathInformation,   // Not implemented 
    SystemProcessInformation,    // 0x00C8+ per process 
    SystemCallInformation,   // 0x0018 + (n * 0x0004) 
    SystemConfigurationInformation,  // 0x0018 
    SystemProcessorCounters, // 0x0030 per cpu 
    SystemGlobalFlag,        // 0x0004 (fails If size != 4) 
    SystemCallTimeInformation,   // Not implemented 
    SystemModuleInformation, // 0x0004 + (n * 0x011C) 
    SystemLockInformation,   // 0x0004 + (n * 0x0024) 
    SystemStackTraceInformation, // Not implemented 
    SystemPagedPoolInformation,  // checked build only 
    SystemNonPagedPoolInformation,   // checked build only 
    SystemHandleInformation, // 0x0004 + (n * 0x0010) 
    SystemObjectTypeInformation, // 0x0038+ + (n * 0x0030+) 
    SystemPageFileInformation,   // 0x0018+ per page file 
    SystemVdmInstemulInformation,    // 0x0088 
    SystemVdmBopInformation, // invalid info Class 
    SystemCacheInformation,  // 0x0024 
    SystemPoolTagInformation,    // 0x0004 + (n * 0x001C) 
    SystemInterruptInformation,  // 0x0000 Or 0x0018 per cpu 
    SystemDpcInformation,    // 0x0014 
    SystemFullMemoryInformation, // checked build only 
    SystemLoadDriver,        // 0x0018 Set mode only 
    SystemUnloadDriver,      // 0x0004 Set mode only 
    SystemTimeAdjustmentInformation, // 0x000C 0x0008 writeable 
    SystemSummaryMemoryInformation,  // checked build only 
    SystemNextEventIdInformation,    // checked build only 
    SystemEventIdsInformation,   // checked build only 
    SystemCrashDumpInformation,  // 0x0004 
    SystemExceptionInformation,  // 0x0010 
    SystemCrashDumpStateInformation, // 0x0004 
    SystemDebuggerInformation,   // 0x0002 
    SystemContextSwitchInformation,  // 0x0030 
    SystemRegistryQuotaInformation,  // 0x000C 
    SystemAddDriver,     // 0x0008 Set mode only 
    SystemPrioritySeparationInformation, // 0x0004 Set mode only 
    SystemPlugPlayBusInformation,    // Not implemented 
    SystemDockInformation,   // Not implemented 
    SystemPowerInfo,     // 0x0060 (XP only!) 
    SystemProcessorSpeedInformation, // 0x000C (XP only!) 
    SystemTimeZoneInformation,   // 0x00AC 
    SystemLookasideInformation,  // n * 0x0020 
    SystemSetTimeSlipEvent,
    SystemCreateSession, // Set mode only 
    SystemDeleteSession, // Set mode only 
    SystemInvalidInfoClass1, // invalid info Class 
    SystemRangeStartInformation, // 0x0004 (fails If size != 4) 
    SystemVerifierInformation,
    SystemAddVerifier,
    SystemSessionProcessesInformation,   // checked build only 
    MaxSystemInfoClass
}

public enum ObjectInformationClass : uint
{
    ObjectBasicInformation,     // 0 Y N 
    ObjectNameInformation,      // 1 Y N 
    ObjectTypeInformation,      // 2 Y N 
    ObjectAllTypesInformation,  // 3 Y N 
    ObjectHandleInformation     // 4 Y Y 
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct FILE_FS_FULL_SIZE_INFORMATION
{
    public long TotalAllocationUnits { get; }
    public long CallerAvailableAllocationUnits { get; }
    public long ActualAvailableAllocationUnits { get; }
    public uint SectorsPerAllocationUnit { get; }
    public uint BytesPerSector { get; }

    public long TotalBytes => TotalAllocationUnits * SectorsPerAllocationUnit * BytesPerSector;

    public uint BytesPerAllocationUnit => SectorsPerAllocationUnit * BytesPerSector;

    public override string ToString()
        => TotalBytes.ToString(NumberFormatInfo.InvariantInfo);
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct IoStatusBlock
{
    public nint Status { get; }

    public nint Information { get; }
}

public enum NtCreateDisposition
{
    Supersede = 0x0,
    Open = 0x1,
    Create = 0x2,
    OpenIf = 0x3,
    Overwrite = 0x4,
    OverwriteIf = 0x5
}

[Flags]
public enum NtCreateOptions
{
    DirectoryFile = 0x1,
    WriteThrough = 0x2,
    SequentialOnly = 0x4,
    NoIntermediateBuffering = 0x8,

    SynchronousIoAlert = 0x10,
    SynchronousIoNonAlert = 0x20,
    NonDirectoryFile = 0x40,
    CreateTreeConnection = 0x80,

    CompleteIfOpLocked = 0x100,
    NoEAKnowledge = 0x200,
    OpenForRecovery = 0x400,
    RandomAccess = 0x800,

    DeleteOnClose = 0x1000,
    OpenByFileId = 0x200,
    OpenForBackupIntent = 0x400,
    NoCompression = 0x8000,

    ReserverNoOpFilter = 0x100000,
    OpenReparsePoint = 0x200000,
    OpenNoRecall = 0x400000
}

[Flags]
public enum PARTITION_TYPE : byte
{
    PARTITION_ENTRY_UNUSED = 0x0,      // Entry unused
    PARTITION_FAT_12 = 0x1,      // 12-bit FAT entries
    PARTITION_XENIX_1 = 0x2,      // Xenix
    PARTITION_XENIX_2 = 0x3,      // Xenix
    PARTITION_FAT_16 = 0x4,      // 16-bit FAT entries
    PARTITION_EXTENDED = 0x5,      // Extended partition entry
    PARTITION_HUGE = 0x6,      // Huge partition MS-DOS V4
    PARTITION_IFS = 0x7,      // IFS Partition
    PARTITION_OS2BOOTMGR = 0xA,      // OS/2 Boot Manager/OPUS/Coherent swap
    PARTITION_FAT32 = 0xB,      // FAT32
    PARTITION_FAT32_XINT13 = 0xC,      // FAT32 using extended int13 services
    PARTITION_XINT13 = 0xE,      // Win95 partition using extended int13 services
    PARTITION_XINT13_EXTENDED = 0xF,      // Same as type 5 but uses extended int13 services
    PARTITION_PREP = 0x41,      // PowerPC Reference Platform (PReP) Boot Partition
    PARTITION_LDM = 0x42,      // Logical Disk Manager partition
    PARTITION_UNIX = 0x63,      // Unix
    PARTITION_NTFT = 0x80      // NTFT partition      
}

public enum PARTITION_STYLE : byte
{
    MBR,
    GPT,
    RAW
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct CREATE_DISK_MBR
{
    public CREATE_DISK_MBR(PARTITION_STYLE partitionStyle, uint diskSignature)
    {
        PartitionStyle = partitionStyle;
        DiskSignature = diskSignature;
    }

    [field: MarshalAs(UnmanagedType.I1)]
    public PARTITION_STYLE PartitionStyle { get; }

    public uint DiskSignature { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct CREATE_DISK_GPT
{
    public CREATE_DISK_GPT(PARTITION_STYLE partitionStyle, Guid diskId, int maxPartitionCount)
    {
        PartitionStyle = partitionStyle;
        DiskId = diskId;
        MaxPartitionCount = maxPartitionCount;
    }

    [field: MarshalAs(UnmanagedType.I1)]
    public PARTITION_STYLE PartitionStyle { get; }

    public Guid DiskId { get; }

    public int MaxPartitionCount { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct PARTITION_INFORMATION_EX
{
    public PARTITION_STYLE PartitionStyle { get; }

    public long StartingOffset { get; }

    public long PartitionLength { get; }

    public uint PartitionNumber { get; }

    [field: MarshalAs(UnmanagedType.I1)]
    public bool RewritePartition { get; }

    [field: MarshalAs(UnmanagedType.I1)]
    public bool IsServicePartition { get; }

    private readonly PARTITION_INFORMATION_GPT _part;

    public PARTITION_INFORMATION_GPT GPT => _part;

    public PARTITION_INFORMATION_MBR MBR
        => Unsafe.As<PARTITION_INFORMATION_GPT, PARTITION_INFORMATION_MBR>(ref Unsafe.AsRef(in _part));
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct PARTITION_INFORMATION_MBR
{
    public PARTITION_TYPE PartitionType { get; }

    [field: MarshalAs(UnmanagedType.I1)]
    public bool BootIndicator { get; }

    [field: MarshalAs(UnmanagedType.I1)]
    public bool RecognizedPartition { get; }

    public int HiddenSectors { get; }
}

[Flags]
public enum GptAttributes : ulong
{
    GPT_ATTRIBUTE_PLATFORM_REQUIRED = 0x1L,
    GPT_BASIC_DATA_ATTRIBUTE_NO_DRIVE_LETTER = 0x8000000000000000,
    GPT_BASIC_DATA_ATTRIBUTE_HIDDEN = 0x4000000000000000,
    GPT_BASIC_DATA_ATTRIBUTE_SHADOW_COPY = 0x2000000000000000,
    GPT_BASIC_DATA_ATTRIBUTE_READ_ONLY = 0x1000000000000000
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PARTITION_INFORMATION_GPT
{
    public Guid PartitionType { get; }

    public Guid PartitionId { get; }

    public GptAttributes Attributes { get; }

    private fixed char _name[36];

    public unsafe ReadOnlySpan<char> Name => BufferExtensions.CreateReadOnlySpan(_name[0], 36).ReadNullTerminatedUnicode();
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public readonly struct SP_DEVINFO_DATA
{
    public unsafe SP_DEVINFO_DATA()
    {
        this = default;
        // as per DDK docs on SetupDiEnumDeviceInfo
        Size = sizeof(SP_DEVINFO_DATA);
    }

    public int Size { get; }
    public Guid ClassGuid { get; }
    public uint DevInst { get; }
    public nint Reserved { get; }
}

public enum CmClassRegistryProperty : uint
{
    CM_CRP_UPPERFILTERS = 0x12U
}

public enum CmDevNodeRegistryProperty : uint
{
    CM_DRP_DEVICEDESC = 0x1U,
    CM_DRP_HARDWAREID = 0x2U,
    CM_DRP_COMPATIBLEIDS = 0x3U,
    CM_DRP_SERVICE = 0x5U,
    CM_DRP_CLASS = 0x8U,
    CM_DRP_CLASSGUID = 0x9U,
    CM_DRP_DRIVER = 0xAU,
    CM_DRP_MFG = 0xCU,
    CM_DRP_FRIENDLYNAME = 0xDU,
    CM_DRP_LOCATION_INFORMATION = 0xEU,
    CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME = 0xFU,
    CM_DRP_UPPERFILTERS = 0x12U,
    CM_DRP_LOWERFILTERS = 0x13U
}

[Flags]
public enum ShutdownFlags : uint
{
    HybridShutdown = 0x400000U,
    Logoff = 0x0U,
    PowerOff = 0x8U,
    Reboot = 0x2U,
    RestartApps = 0x40U,
    Shutdown = 0x1U,
    Force = 0x4U,
    ForceIfHung = 0x10U
}

[Flags]
public enum ShutdownReasons : uint
{
    ReasonFlagPlanned = 0x80000000
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct LUID_AND_ATTRIBUTES
{
    public long LUID { get; }
    public int Attributes { get; }

    public LUID_AND_ATTRIBUTES(long LUID, int attributes)
    {
        this.LUID = LUID;
        Attributes = attributes;
    }

    public override string ToString()
        => $"LUID = 0x{LUID:X}, Attributes = 0x{Attributes:X}";
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct SystemHandleTableEntryInformation
{
    public int ProcessId { get; }
    public byte ObjectType { get; }     // OB_TYPE_* (OB_TYPE_TYPE, etc.) 
    public byte Flags { get; }      // HANDLE_FLAG_* (HANDLE_FLAG_INHERIT, etc.) 
    public ushort Handle { get; }
    public nint ObjectPtr { get; }
    public uint GrantedAccess { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct ObjectTypeInformation
{
    public UNICODE_STRING Name { get; }
    public uint ObjectCount { get; }
    public uint HandleCount { get; }
    private readonly uint Reserved11;
    private readonly uint Reserved12;
    private readonly uint Reserved13;
    private readonly uint Reserved14;
    public uint PeakObjectCount { get; }
    public uint PeakHandleCount { get; }
    private readonly uint Reserved21;
    private readonly uint Reserved22;
    private readonly uint Reserved23;
    private readonly uint Reserved24;
    public uint InvalidAttributes { get; }
    public uint GenericRead { get; }
    public uint GenericWrite { get; }
    public uint GenericExecute { get; }
    public uint GenericAll { get; }
    public uint ValidAccess { get; }
    private readonly byte Unknown;
    [MarshalAs(UnmanagedType.I1)]
    private readonly bool MaintainHandleDatabase;
    private readonly ushort Reserved3;
    public int PoolType { get; }
    public uint PagedPoolUsage { get; }
    public uint NonPagedPoolUsage { get; }
}

public enum Win32FileType : int
{
    Unknown = 0x0,
    Disk = 0x1,
    Character = 0x2,
    Pipe = 0x3,
    Remote = 0x8000
}

public enum StdHandle : int
{
    Input = -10,
    Output = -11,
    Error = -12
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct COORD
{
    public short X { get; }
    public short Y { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct SMALL_RECT
{
    public short Left { get; }
    public short Top { get; }
    public short Right { get; }
    public short Bottom { get; }
    public short Width => (short)((short)(Right - Left) + 1);
    public short Height => (short)((short)(Bottom - Top) + 1);
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct CONSOLE_SCREEN_BUFFER_INFO
{
    public COORD Size { get; }
    public COORD CursorPosition { get; }
    public short Attributes { get; }
    public SMALL_RECT Window { get; }
    public COORD MaximumWindowSize { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct SERVICE_STATUS
{
    public int ServiceType { get; }
    public int CurrentState { get; }
    public int ControlsAccepted { get; }
    public int Win32ExitCode { get; }
    public int ServiceSpecificExitCode { get; }
    public int CheckPoint { get; }
    public int WaitHint { get; }
}

[Flags]
public enum DEFINE_DOS_DEVICE_FLAGS : uint
{
    DDD_EXACT_MATCH_ON_REMOVE = 0x4U,
    DDD_NO_BROADCAST_SYSTEM = 0x8U,
    DDD_RAW_TARGET_PATH = 0x1U,
    DDD_REMOVE_DEFINITION = 0x2U
}

public class HGlobalBuffer : SafeBuffer
{
    public HGlobalBuffer(nint numBytes)
        : base(ownsHandle: true)
    {
        var ptr = Marshal.AllocHGlobal(numBytes);
        SetHandle(ptr);
        Initialize((ulong)numBytes);
    }

    public HGlobalBuffer(int numBytes)
        : base(ownsHandle: true)
    {
        var ptr = Marshal.AllocHGlobal(numBytes);
        SetHandle(ptr);
        Initialize((ulong)numBytes);
    }

    public HGlobalBuffer(nint address, ulong numBytes, bool ownsHandle)
        : base(ownsHandle)
    {
        SetHandle(address);
        Initialize(numBytes);
    }

    public void Resize(int newSize)
    {
        if (handle != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(handle);
        }

        handle = Marshal.AllocHGlobal(newSize);
        Initialize((ulong)newSize);
    }

    public void Resize(nint newSize)
    {
        if (handle != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(handle);
        }

        handle = Marshal.AllocHGlobal(newSize);
        Initialize((ulong)newSize);
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            Marshal.FreeHGlobal(handle);
            return true;
        }

        catch
        {
            return false;

        }
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct FindStreamData
{
    public long StreamSize { get; }

    private unsafe fixed char _streamName[296];

    public ReadOnlySpan<char> NamePart => StreamName.TokenEnum(':').ElementAtOrDefault(1);

    public ReadOnlySpan<char> TypePart => StreamName.TokenEnum(':').ElementAtOrDefault(2);

    public unsafe ReadOnlySpan<char> StreamName => BufferExtensions.CreateReadOnlySpan(_streamName[0], 296).ReadNullTerminatedUnicode();
}

/// <summary>
/// Encapsulates a FindVolumeMountPoint handle that is closed by calling FindVolumeMountPointClose () Win32 API.
/// </summary>
public sealed partial class SafeFindHandle : SafeHandleMinusOneIsInvalid
{
#if NET7_0_OR_GREATER
    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FindClose(nint h);
#else
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool FindClose(nint h);
#endif

    /// <summary>
    /// Initiates a new instance with an existing open handle.
    /// </summary>
    /// <param name="open_handle">Existing open handle.</param>
    /// <param name="owns_handle">Indicates whether handle should be closed when this
    /// instance is released.</param>
    public SafeFindHandle(nint open_handle, bool owns_handle)
        : base(owns_handle)
    {

        SetHandle(open_handle);
    }

    /// <summary>
    /// Creates a new empty instance. This constructor is used by native to managed
    /// handle marshaller.
    /// </summary>
    public SafeFindHandle()
        : base(ownsHandle: true)
    {

    }

    /// <summary>
    /// Closes contained handle by calling FindClose() Win32 API.
    /// </summary>
    /// <returns>Return value from FindClose() Win32 API.</returns>
    protected override bool ReleaseHandle() => FindClose(handle);
}

public enum DeviceType
{
    Beep = 0x1,

    CdRom = 0x2,

    CdRomFileSystem = 0x3,

    Controller = 0x4,

    DataLink = 0x5,

    DFS = 0x6,

    Disk = 0x7,

    DiskFileSystem = 0x8,

    FileSystem = 0x9,

    InportPort = 0xa,

    Keyboard = 0xb,

    MailSlot = 0xc,

    MidiIn = 0xd,

    MidiOut = 0xe,

    Mouse = 0xf,

    MultiUncProvider = 0x10,

    NamedPipe = 0x11,

    Network = 0x12,

    NetworkBrowser = 0x13,

    NetworkFileSystem = 0x14,

    Null = 0x15,

    ParallelPort = 0x16,

    PhysicalNetcard = 0x17,

    Printer = 0x18,

    Scanner = 0x19,

    SerialMousePort = 0x1a,

    SerialPort = 0x1b,

    Screen = 0x1c,

    Sound = 0x1d,

    Streams = 0x1e,

    Tape = 0x1f,

    TapeFileSystem = 0x20,

    Transport = 0x21,

    Unknown = 0x22,

    Video = 0x23,

    VirtualDisk = 0x24,

    WaveIn = 0x25,

    WaveOut = 0x26,

    i8042Port = 0x27,

    NetworkRedirector = 0x28,

    Battery = 0x29,

    BusExtender = 0x2a,

    Modem = 0x2b,

    VDM = 0x2c,

    MassStorage = 0x2d,

    SMB = 0x2e,

    KS = 0x2f,

    Changer = 0x30,

    SmartCard = 0x31,

    ACPI = 0x32,

    DVD = 0x33,

    FullscreenVideo = 0x34,

    DfsFileSystem = 0x35,

    DfsVolume = 0x36,

    SerEnum = 0x37,

    TermSrv = 0x38,

    KSec = 0x39,

    FIPS = 0x3a,

    InfiniBand = 0x3b,

    VMBus = 0x3e,

    CryptProvider = 0x3f,

    WPD = 0x40,

    Bluetooth = 0x41,

    MTComposite = 0x42,

    MTTransport = 0x43,

    Biometric = 0x44,

    PMI = 0x45,

    EhStor = 0x46,

    DevApi = 0x47,

    GPIO = 0x48,

    USBEx = 0x49,

    Console = 0x50,

    NFP = 0x51,

    SysEnv = 0x52,

    VirtualBlock = 0x53,

    PointOfService = 0x54
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct STORAGE_DEVICE_NUMBER
{

    public DeviceType DeviceType { get; }

    public uint DeviceNumber { get; }

    public int PartitionNumber { get; }
}

public enum STORAGE_PROPERTY_ID
{
    StorageDeviceProperty = 0,
    StorageAdapterProperty,
    StorageDeviceIdProperty,
    StorageDeviceUniqueIdProperty,
    StorageDeviceWriteCacheProperty,
    StorageMiniportProperty,
    StorageAccessAlignmentProperty,
    StorageDeviceSeekPenaltyProperty,
    StorageDeviceTrimProperty,
    StorageDeviceWriteAggregationProperty
}

public enum STORAGE_QUERY_TYPE
{
    PropertyStandardQuery = 0,          // Retrieves the descriptor
    PropertyExistsQuery,                // Used To test whether the descriptor Is supported
    PropertyMaskQuery,                  // Used To retrieve a mask Of writable fields In the descriptor
    PropertyQueryMaxDefined            // use To validate the value
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct STORAGE_PROPERTY_QUERY
{

    public STORAGE_PROPERTY_ID PropertyId { get; }

    public STORAGE_QUERY_TYPE QueryType { get; }

    private readonly byte _additional;

    public STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID PropertyId, STORAGE_QUERY_TYPE QueryType)
    {
        this.PropertyId = PropertyId;
        this.QueryType = QueryType;
        _additional = 0;
    }
}

[Flags]
public enum DEVICE_DATA_MANAGEMENT_SET_ACTION : uint
{
    DeviceDsmAction_None = 0,
    DeviceDsmAction_Trim = 1,
    DeviceDsmAction_Notification = 2 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_OffloadRead = 3 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_OffloadWrite = 4,
    DeviceDsmAction_Allocation = 5 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_Repair = 6 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_Scrub = 7 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_DrtQuery = 8 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_DrtClear = 9 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_DrtDisable = 10 | DeviceDsmActionFlag_NonDestructive,
    // end_winioctl
    DeviceDsmAction_TieringQuery = 11 | DeviceDsmActionFlag_NonDestructive,
    // begin_winioctl
    DeviceDsmAction_Map = 12 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_RegenerateParity = 13 | DeviceDsmActionFlag_NonDestructive,

    DeviceDsmAction_NvCache_Change_Priority = 14 | DeviceDsmActionFlag_NonDestructive,
    DeviceDsmAction_NvCache_Evict = 15 | DeviceDsmActionFlag_NonDestructive,

    DeviceDsmActionFlag_NonDestructive = 0x80000000
}

//
// input structure for IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES
// 1. Value ofParameterBlockOffset or ParameterBlockLength is 0 indicates that Parameter Block does not exist.
// 2. Value of DataSetRangesOffset or DataSetRangesLength is 0 indicates that DataSetRanges Block does not exist.
//     If DataSetRanges Block exists, it contains contiguous DEVICE_DATA_SET_RANGE structures.
// 3. The total size of buffer should be at least:
//      sizeof (DEVICE_MANAGE_DATA_SET_ATTRIBUTES) + ParameterBlockLength + DataSetRangesLength
//
[StructLayout(LayoutKind.Sequential)]
public readonly struct DEVICE_MANAGE_DATA_SET_ATTRIBUTES
{
    public unsafe DEVICE_MANAGE_DATA_SET_ATTRIBUTES(DEVICE_DATA_MANAGEMENT_SET_ACTION action,
                                                    int flags,
                                                    int parameterBlockOffset,
                                                    int parameterBlockLength,
                                                    int dataSetRangesOffset,
                                                    int dataSetRangesLength)
    {
        Size = sizeof(DEVICE_MANAGE_DATA_SET_ATTRIBUTES);
        Action = action;
        Flags = flags;
        ParameterBlockOffset = parameterBlockOffset;
        ParameterBlockLength = parameterBlockLength;
        DataSetRangesOffset = dataSetRangesOffset;
        DataSetRangesLength = dataSetRangesLength;
    }

    public int Size { get; }                   // Size of structure DEVICE_MANAGE_DATA_SET_ATTRIBUTES
    private DEVICE_DATA_MANAGEMENT_SET_ACTION Action { get; }

    public int Flags { get; }                  // Global flags across all actions

    public int ParameterBlockOffset { get; }   // must be alligned to corresponding structure allignment
    public int ParameterBlockLength { get; }   // 0 means Parameter Block does not exist.

    public int DataSetRangesOffset { get; }    // must be alligned to DEVICE_DATA_SET_RANGE structure allignment.
    public int DataSetRangesLength { get; }    // 0 means DataSetRanges Block does not exist.
}

//
//  Structure used to describe the list of ranges to process
//
[StructLayout(LayoutKind.Sequential)]
public readonly struct DEVICE_DATA_SET_RANGE
{
    public DEVICE_DATA_SET_RANGE(long startingOffset, ulong lengthInBytes)
    {
        StartingOffset = startingOffset;
        LengthInBytes = lengthInBytes;
    }

    public long StartingOffset { get; }        //in bytes,  must allign to sector

    public ulong LengthInBytes { get; }         // multiple of sector size.
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct STORAGE_DESCRIPTOR_HEADER
{

    public uint Version { get; }

    public int Size { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct DEVICE_TRIM_DESCRIPTOR
{

    public STORAGE_DESCRIPTOR_HEADER Header { get; }

    public byte TrimEnabled { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct STORAGE_DEVICE_DESCRIPTOR
{

    public STORAGE_DESCRIPTOR_HEADER Header { get; }

    public byte DeviceType { get; }

    public byte DeviceTypeModifier { get; }

    public byte RemovableMedia { get; }

    public byte CommandQueueing { get; }

    public int VendorIdOffset { get; }

    public int ProductIdOffset { get; }

    public int ProductRevisionOffset { get; }

    public int SerialNumberOffset { get; }

    public byte StorageBusType { get; }

    public int RawPropertiesLength { get; }
}

public readonly struct StorageStandardProperties
{

    public STORAGE_DEVICE_DESCRIPTOR DeviceDescriptor { get; }

    public string? VendorId { get; }
    public string? ProductId { get; }
    public string? ProductRevision { get; }
    public string? SerialNumber { get; }

    public IByteCollection? RawProperties { get; }

    public StorageStandardProperties(ReadOnlySpan<byte> buffer)
        : this()
    {
        DeviceDescriptor = MemoryMarshal.Read<STORAGE_DEVICE_DESCRIPTOR>(buffer);

        if (DeviceDescriptor.ProductIdOffset != 0)
        {
            ProductId = BufferExtensions.ReadNullTerminatedAsciiString(buffer.Slice(DeviceDescriptor.ProductIdOffset));
        }

        if (DeviceDescriptor.VendorIdOffset != 0)
        {
            VendorId = BufferExtensions.ReadNullTerminatedAsciiString(buffer.Slice(DeviceDescriptor.VendorIdOffset));
        }

        if (DeviceDescriptor.SerialNumberOffset != 0)
        {
            SerialNumber = BufferExtensions.ReadNullTerminatedAsciiString(buffer.Slice(DeviceDescriptor.SerialNumberOffset));
        }

        if (DeviceDescriptor.ProductRevisionOffset != 0)
        {
            ProductRevision = BufferExtensions.ReadNullTerminatedAsciiString(buffer.Slice(DeviceDescriptor.ProductRevisionOffset));
        }

        if (DeviceDescriptor.RawPropertiesLength != 0)
        {
            RawProperties = buffer
                .Slice(Unsafe.SizeOf<STORAGE_DEVICE_DESCRIPTOR>(), DeviceDescriptor.RawPropertiesLength)
                .ToArray();

        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct SCSI_ADDRESS : IEquatable<SCSI_ADDRESS>
{
    public int Length { get; }
    public byte PortNumber { get; }
    public byte PathId { get; }
    public byte TargetId { get; }
    public byte Lun { get; }

    public unsafe SCSI_ADDRESS(byte portNumber, uint dWordDeviceNumber)
    {
        Length = sizeof(SCSI_ADDRESS);
        PortNumber = portNumber;
        PathId = (byte)(dWordDeviceNumber & 0xFFL);
        TargetId = (byte)(dWordDeviceNumber >> 8 & 0xFFL);
        Lun = (byte)(dWordDeviceNumber >> 16 & 0xFFL);
    }

    public unsafe SCSI_ADDRESS(uint dWordDeviceNumber)
    {
        Length = sizeof(SCSI_ADDRESS);
        PortNumber = byte.MaxValue;
        PathId = (byte)(dWordDeviceNumber & 0xFFL);
        TargetId = (byte)(dWordDeviceNumber >> 8 & 0xFFL);
        Lun = (byte)(dWordDeviceNumber >> 16 & 0xFFL);
    }

    public uint DWordDeviceNumber
        => PathId | (uint)TargetId << 8 | (uint)Lun << 16;

    public override string ToString()
        => $"Port = {PortNumber}, Path = {PathId}, Target = {TargetId}, Lun = {Lun}";

    public bool Equals(SCSI_ADDRESS other)
        => PortNumber.Equals(other.PortNumber)
        && PathId.Equals(other.PathId)
        && TargetId.Equals(other.TargetId)
        && Lun.Equals(other.Lun);

    public override bool Equals(object? obj)
        => obj is SCSI_ADDRESS aDDRESS && Equals(aDDRESS);

    public override int GetHashCode()
        => PathId | TargetId << 8 | Lun << 16;

    public static bool operator ==(SCSI_ADDRESS first, SCSI_ADDRESS second)
        => first.Equals(second);

    public static bool operator !=(SCSI_ADDRESS first, SCSI_ADDRESS second)
        => !first.Equals(second);

}

[StructLayout(LayoutKind.Sequential)]
public readonly struct DISK_GEOMETRY
{
    public enum MEDIA_TYPE : int
    {
        Unknown = 0x0,
        F5_1Pt2_512 = 0x1,
        F3_1Pt44_512 = 0x2,
        F3_2Pt88_512 = 0x3,
        F3_20Pt8_512 = 0x4,
        F3_720_512 = 0x5,
        F5_360_512 = 0x6,
        F5_320_512 = 0x7,
        F5_320_1024 = 0x8,
        F5_180_512 = 0x9,
        F5_160_512 = 0xA,
        RemovableMedia = 0xB,
        FixedMedia = 0xC,
        F3_120M_512 = 0xD,
        F3_640_512 = 0xE,
        F5_640_512 = 0xF,
        F5_720_512 = 0x10,
        F3_1Pt2_512 = 0x11,
        F3_1Pt23_1024 = 0x12,
        F5_1Pt23_1024 = 0x13,
        F3_128Mb_512 = 0x14,
        F3_230Mb_512 = 0x15,
        F8_256_128 = 0x16,
        F3_200Mb_512 = 0x17,
        F3_240M_512 = 0x18,
        F3_32M_512 = 0x19
    }

    public DISK_GEOMETRY(long cylinders, MEDIA_TYPE mediaType, int tracksPerCylinder, int sectorsPerTrack, int bytesPerSector)
    {
        Cylinders = cylinders;
        MediaType = mediaType;
        TracksPerCylinder = tracksPerCylinder;
        SectorsPerTrack = sectorsPerTrack;
        BytesPerSector = bytesPerSector;
    }

    public long Cylinders { get; }
    public MEDIA_TYPE MediaType { get; }
    public int TracksPerCylinder { get; }
    public int SectorsPerTrack { get; }
    public int BytesPerSector { get; }
}

[StructLayout(LayoutKind.Sequential)]
public struct OSVERSIONINFO
{
    public int OSVersionInfoSize { get; }
    public int MajorVersion { get; }
    public int MinorVersion { get; }
    public int BuildNumber { get; }
    public PlatformID PlatformId { get; }

    private unsafe fixed char csdVersion[128];

    public unsafe ReadOnlySpan<char> CSDVersion
        => BufferExtensions.CreateReadOnlySpan(csdVersion[0], 128).ReadNullTerminatedUnicode();

    public unsafe OSVERSIONINFO()
    {
        OSVersionInfoSize = sizeof(OSVERSIONINFO);
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct OSVERSIONINFOEX
{
    public int OSVersionInfoSize { get; }
    public int MajorVersion { get; }
    public int MinorVersion { get; }
    public int BuildNumber { get; }
    public PlatformID PlatformId { get; }

    private unsafe fixed char csdVersion[128];

    public readonly unsafe ReadOnlySpan<char> CSDVersion
        => BufferExtensions.CreateReadOnlySpan(csdVersion[0], 128).ReadNullTerminatedUnicode();

    public ushort ServicePackMajor { get; }

    public ushort ServicePackMinor { get; }

    public readonly int ServicePack => (ServicePackMajor << 16) | ServicePackMinor;

    public SuiteMask SuiteMask { get; }

    public ProductType ProductType { get; }

    public byte Reserved { get; }

    public unsafe OSVERSIONINFOEX()
    {
        OSVersionInfoSize = sizeof(OSVERSIONINFOEX);
    }

    public readonly OperatingSystem ToOperatingSystem()
        => new(PlatformId,
            new Version(MajorVersion,
                        MinorVersion,
                        BuildNumber,
                        ServicePack));
}

public enum ProductType : byte
{
    VER_NT_WORKSTATION = 0x1,
    VER_NT_DOMAIN_CONTROLLER = 0x2,
    VER_NT_SERVER = 0x3
}

[Flags]
public enum SuiteMask : ushort
{
    VER_SUITE_SMALLBUSINESS = 0x1,
    VER_SUITE_ENTERPRISE = 0x2,
    VER_SUITE_BACKOFFICE = 0x4,
    VER_SUITE_COMMUNICATIONS = 0x8,
    VER_SUITE_TERMINAL = 0x10,
    VER_SUITE_SMALLBUSINESS_RESTRICTED = 0x20,
    VER_SUITE_EMBEDDEDNT = 0x40,
    VER_SUITE_DATACENTER = 0x80,
    VER_SUITE_SINGLEUSERTS = 0x100,
    VER_SUITE_PERSONAL = 0x200,
    VER_SUITE_BLADE = 0x400,
    VER_SUITE_EMBEDDED_RESTRICTED = 0x800,
    VER_SUITE_SECURITY_APPLIANCE = 0x1000,
    VER_SUITE_STORAGE_SERVER = 0x2000,
    VER_SUITE_COMPUTE_SERVER = 0x4000,
    VER_SUITE_WH_SERVER = 0x8000
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct MOUNTMGR_MOUNT_POINT
{
    public int SymbolicLinkNameOffset { get; }
    public ushort SymbolicLinkNameLength { get; }
    public ushort Reserved1 { get; }
    public int UniqueIdOffset { get; }
    public ushort UniqueIdLength { get; }
    public ushort Reserved2 { get; }
    public int DeviceNameOffset { get; }
    public ushort DeviceNameLength { get; }
    public ushort Reserved3 { get; }

    public unsafe MOUNTMGR_MOUNT_POINT(string device_name)
        : this()
    {
        DeviceNameOffset = sizeof(MOUNTMGR_MOUNT_POINT);
        DeviceNameLength = (ushort)(device_name.Length << 1);
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct PARTITION_INFORMATION
{

    public long StartingOffset { get; }
    public long PartitionLength { get; }
    public uint HiddenSectors { get; }
    public uint PartitionNumber { get; }
    public PARTITION_TYPE PartitionType { get; }
    public byte BootIndicator { get; }
    public byte RecognizedPartition { get; }
    public byte RewritePartition { get; }

    /// <summary>
    /// Indicates whether this partition entry represents a Windows NT fault tolerant partition,
    /// such as mirror or stripe set.
    /// </summary>
    /// <value>
    /// Indicates whether this partition entry represents a Windows NT fault tolerant partition,
    /// such as mirror or stripe set.
    /// </value>
    /// <returns>True if this partition entry represents a Windows NT fault tolerant partition,
    /// such as mirror or stripe set. False otherwise.</returns>
    public bool IsFTPartition => PartitionType.HasFlag(PARTITION_TYPE.PARTITION_NTFT);

    /// <summary>
    /// If this partition entry represents a Windows NT fault tolerant partition, such as mirror or stripe,
    /// set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
    /// partitions.
    /// </summary>
    /// <value>
    /// If this partition entry represents a Windows NT fault tolerant partition, such as mirror or stripe,
    /// set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
    /// partitions.
    /// </value>
    /// <returns>If this partition entry represents a Windows NT fault tolerant partition, such as mirror or
    /// stripe, set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
    /// partitions.</returns>
    public PARTITION_TYPE FTPartitionSubType => PartitionType & ~PARTITION_TYPE.PARTITION_NTFT;

    /// <summary>
    /// Indicates whether this partition entry represents a container partition, also known as extended
    /// partition, where an extended partition table can be found in first sector.
    /// </summary>
    /// <value>
    /// Indicates whether this partition entry represents a container partition.
    /// </value>
    /// <returns>True if this partition entry represents a container partition. False otherwise.</returns>
    public bool IsContainerPartition => PartitionType is PARTITION_TYPE.PARTITION_EXTENDED or PARTITION_TYPE.PARTITION_XINT13_EXTENDED;
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct DRIVE_LAYOUT_INFORMATION_EX
{
    public DRIVE_LAYOUT_INFORMATION_EX(PARTITION_STYLE PartitionStyle, int PartitionCount)
    {
        this.PartitionStyle = PartitionStyle;
        this.PartitionCount = PartitionCount;
    }

    public PARTITION_STYLE PartitionStyle { get; }
    public int PartitionCount { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct DRIVE_LAYOUT_INFORMATION_MBR
{
    public DRIVE_LAYOUT_INFORMATION_MBR(uint DiskSignature)
    {
        this.DiskSignature = DiskSignature;
        Checksum = 0;
    }

    public uint DiskSignature { get; }

    public uint Checksum { get; }

    public override int GetHashCode() => DiskSignature.GetHashCode();

    public override string ToString() => DiskSignature.ToString("X8");
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct DRIVE_LAYOUT_INFORMATION_GPT
{
    public Guid DiskId { get; }
    public long StartingUsableOffset { get; }
    public long UsableLength { get; }
    public int MaxPartitionCount { get; }

    public override int GetHashCode() => DiskId.GetHashCode();

    public override string ToString() => DiskId.ToString("b");
}

[Flags]
public enum DiskAttributes : long
{
    None = 0,
    Offline = 1,
    ReadOnly = 2
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct GET_DISK_ATTRIBUTES
{
    public int Version { get; }
    public int Reserved1 { get; }
    public DiskAttributes Attributes { get; }
}

[Flags]
public enum DiskAttributesFlags
{
    None = 0,
    Persistent = 1
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct SET_DISK_ATTRIBUTES
{
    public SET_DISK_ATTRIBUTES(DiskAttributesFlags flags, DiskAttributes attributes, DiskAttributes attributesMask)
    {
        Version = Unsafe.SizeOf<SET_DISK_ATTRIBUTES>();
        Flags = flags;
        Attributes = attributes;
        AttributesMask = attributesMask;
        Reserved = default;
    }

    public int Version { get; }
    public DiskAttributesFlags Flags { get; }
    public DiskAttributes Attributes { get; }
    public DiskAttributes AttributesMask { get; }
    public Guid Reserved { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct DISK_GROW_PARTITION
{
    public DISK_GROW_PARTITION(int partitionNumber, long bytesToGrow)
    {
        PartitionNumber = partitionNumber;
        BytesToGrow = bytesToGrow;
    }

    public int PartitionNumber { get; }
    public long BytesToGrow { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct COMMTIMEOUTS
{
    public COMMTIMEOUTS(uint readIntervalTimeout, uint readTotalTimeoutMultiplier, uint readTotalTimeoutConstant, uint writeTotalTimeoutMultiplier, uint writeTotalTimeoutConstant)
    {
        ReadIntervalTimeout = readIntervalTimeout;
        ReadTotalTimeoutMultiplier = readTotalTimeoutMultiplier;
        ReadTotalTimeoutConstant = readTotalTimeoutConstant;
        WriteTotalTimeoutMultiplier = writeTotalTimeoutMultiplier;
        WriteTotalTimeoutConstant = writeTotalTimeoutConstant;
    }

    public uint ReadIntervalTimeout { get; }
    public uint ReadTotalTimeoutMultiplier { get; }
    public uint ReadTotalTimeoutConstant { get; }
    public uint WriteTotalTimeoutMultiplier { get; }
    public uint WriteTotalTimeoutConstant { get; }
}

public readonly struct SP_CLASSINSTALL_HEADER
{
    [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Native code access")]
    private readonly int _size;

    public uint InstallFunction { get; }

    public unsafe SP_CLASSINSTALL_HEADER(uint installFunction)
    {
        _size = sizeof(SP_CLASSINSTALL_HEADER);
        InstallFunction = installFunction;
    }
}

public readonly struct SP_PROPCHANGE_PARAMS
{
    public SP_PROPCHANGE_PARAMS(SP_CLASSINSTALL_HEADER classInstallHeader, uint stateChange, uint scope, uint hwProfile)
    {
        ClassInstallHeader = classInstallHeader;
        StateChange = stateChange;
        Scope = scope;
        HwProfile = hwProfile;
    }

    public SP_CLASSINSTALL_HEADER ClassInstallHeader { get; }
    public uint StateChange { get; }
    public uint Scope { get; }
    public uint HwProfile { get; }
}

public readonly struct DiskExtent
{
    public uint DiskNumber { get; }
    public long StartingOffset { get; }
    public long ExtentLength { get; }
}

public readonly struct ScsiAddressAndLength : IEquatable<ScsiAddressAndLength>
{

    public SCSI_ADDRESS ScsiAddress { get; }

    public long Length { get; }

    public ScsiAddressAndLength(SCSI_ADDRESS ScsiAddress, long Length)
    {
        this.ScsiAddress = ScsiAddress;
        this.Length = Length;
    }

    public override bool Equals(object? obj)
        => obj is ScsiAddressAndLength length && Equals(length);

    public override int GetHashCode()
        => HashCode.Combine(ScsiAddress, Length);

    public override string ToString()
        => $"{ScsiAddress}, Length = {Length}";

    public bool Equals(ScsiAddressAndLength other)
        => Length.Equals(other.Length) && ScsiAddress.Equals(other.ScsiAddress);

    public static bool operator ==(ScsiAddressAndLength a, ScsiAddressAndLength b)
        => a.Equals(b);

    public static bool operator !=(ScsiAddressAndLength a, ScsiAddressAndLength b)
        => a.Equals(b);
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct SP_DEVICE_INTERFACE_DATA
{
    public unsafe SP_DEVICE_INTERFACE_DATA()
    {
        this = default;
        Size = sizeof(SP_DEVICE_INTERFACE_DATA);
    }

    public int Size { get; }
    public Guid InterfaceClassGuid { get; }
    public uint Flags { get; }
    public nint Reserved { get; }
}

[StructLayout(LayoutKind.Sequential)]
public struct SP_DEVICE_INTERFACE_DETAIL_DATA
{
    public int Size { get; }

    private unsafe fixed char devicePath[32768];

    public unsafe ReadOnlySpan<char> DevicePath => BufferExtensions.CreateReadOnlySpan(devicePath[0], 32768).ReadNullTerminatedUnicode();

    public unsafe SP_DEVICE_INTERFACE_DETAIL_DATA()
    {
        Size = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SP_DEVINFO_LIST_DETAIL_DATA
{
    public const int SP_MAX_MACHINENAME_LENGTH = 263;

    public int Size { get; }

    public Guid ClassGUID { get; }

    public nint RemoteMachineHandle { get; }

    private unsafe fixed char remoteMachineName[SP_MAX_MACHINENAME_LENGTH];

    public unsafe SP_DEVINFO_LIST_DETAIL_DATA()
    {
        Size = sizeof(SP_DEVINFO_LIST_DETAIL_DATA);
    }
}

/// <summary>
/// SRB_IO_CONTROL header, as defined in NTDDDISK.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
[ComVisible(false)]
public readonly struct SRB_IO_CONTROL
{
    public unsafe SRB_IO_CONTROL(ulong signature, uint timeout, uint controlCode, int dataLength)
        : this(sizeof(SRB_IO_CONTROL), signature, timeout, controlCode, 0, dataLength)
    {
    }

    public SRB_IO_CONTROL(int headerLength, ulong signature, uint timeout, uint controlCode, int returnCode, int dataLength)
    {
        HeaderLength = headerLength;
        Signature = signature;
        Timeout = timeout;
        ControlCode = controlCode;
        ReturnCode = returnCode;
        DataLength = dataLength;
    }

    public int HeaderLength { get; }
    public ulong Signature { get; }
    public uint Timeout { get; }
    public uint ControlCode { get; }
    public int ReturnCode { get; }
    public int DataLength { get; }
}

public enum NtFileCreated
{

    Superseded,
    Opened,
    Created,
    Overwritten,
    Exists,
    DoesNotExist

}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Native structure")]
public struct freebsd_statfs
{
    public const int MFSNAMELEN = 16;              /* length of type name including null */
    public const int MNAMELEN = 1024;              /* size of on/from name bufs */
    public const int STATFS_VERSION = 0x20140518;  /* current version number */

    public uint f_version { get; }             /* structure version number */
    public uint f_type { get; }                /* type of filesystem */
    public ulong f_flags { get; }               /* copy of mount exported flags */
    public ulong f_bsize { get; }               /* filesystem fragment size */
    public ulong f_iosize { get; }              /* optimal transfer block size */
    public ulong f_blocks { get; }              /* total data blocks in filesystem */
    public ulong f_bfree { get; }               /* free blocks in filesystem */
    public long f_bavail { get; }              /* free blocks avail to non-superuser */
    public ulong f_files { get; }               /* total file nodes in filesystem */
    public long f_ffree { get; }               /* free nodes avail to non-superuser */
    public ulong f_syncwrites { get; }          /* count of sync writes since mount */
    public ulong f_asyncwrites { get; }         /* count of async writes since mount */
    public ulong f_syncreads { get; }           /* count of sync reads since mount */
    public ulong f_asyncreads { get; }          /* count of async reads since mount */
    public uint f_nvnodelistsize { get; }      /* # of vnodes */
    public uint f_spare0 { get; }              /* unused spare */
    public unsafe fixed ulong f_spare[9];     /* unused spare */
    public uint f_namemax { get; }             /* maximum filename length */
    public int f_owner { get; }              /* user that mounted the filesystem */
    public unsafe fixed int f_fsid[2];               /* filesystem id */
    public unsafe fixed byte f_charspare[80];          /* spare string space */
    public unsafe fixed byte f_fstypename[MFSNAMELEN]; /* filesystem type name */
    public unsafe string FsTypeName
    {
        get
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return MemoryMarshal.CreateReadOnlySpan(ref f_fstypename[0], MFSNAMELEN).ReadNullTerminatedMultiByteString(Encoding.UTF8);
#else
            fixed (byte* ptr = f_fstypename)
            {
                return Encoding.UTF8.GetString(ptr, new ReadOnlySpan<byte>(ptr, MFSNAMELEN).IndexOfTerminator());
            }
#endif
        }
    }

    public unsafe fixed byte f_mntfromname[MNAMELEN];  /* mounted filesystem */
    public unsafe string MntFromName
    {
        get
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return MemoryMarshal.CreateReadOnlySpan(ref f_mntfromname[0], MNAMELEN).ReadNullTerminatedMultiByteString(Encoding.UTF8);
#else
            fixed (byte* ptr = f_mntfromname)
            {
                return Encoding.UTF8.GetString(ptr, new ReadOnlySpan<byte>(ptr, MNAMELEN).IndexOfTerminator());
            }
#endif
        }
    }

    public unsafe fixed byte f_mntonname[MNAMELEN];    /* directory on which mounted */
    public unsafe string MntToName
    {
        get
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return MemoryMarshal.CreateReadOnlySpan(ref f_mntonname[0], MNAMELEN).ReadNullTerminatedMultiByteString(Encoding.UTF8);
#else
            fixed (byte* ptr = f_mntonname)
            {
                return Encoding.UTF8.GetString(ptr, new ReadOnlySpan<byte>(ptr, MNAMELEN).IndexOfTerminator());
            }
#endif
        }
    }
};
