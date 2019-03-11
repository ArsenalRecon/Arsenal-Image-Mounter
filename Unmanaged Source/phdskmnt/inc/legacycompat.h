#pragma once

#pragma warning(push)
#pragma warning(disable: 4200)

#ifndef CD_SECS
#define CD_SECS                             0x3C
#endif

#ifndef CD_FRAMES
#define CD_FRAMES                           0x4B
#endif

#ifndef CD_MSF_OFFSET
#define CD_MSF_OFFSET                       150
#endif

#ifndef SCSI_ADSENSE_RESOURCE_FAILURE
#define SCSI_ADSENSE_RESOURCE_FAILURE       0x55
#endif

#ifndef SCSI_SENSEQ_NOT_REACHABLE
#define SCSI_SENSEQ_NOT_REACHABLE           0x02
#endif

#ifndef STATUS_DEVICE_FEATURE_NOT_SUPPORTED
#define STATUS_DEVICE_FEATURE_NOT_SUPPORTED ((NTSTATUS)0xC0000463L)
#endif

// We can support SCSIOP_UNMAP and friends even on older Windows versions, so
// include definitions from newer version storport.h headers. Of course,
// older than Windows 8 will never send SCSIOP_UNMAP with default drivers, but
// users could be using upper filter drivers/filesystem drivers that support
// TRIM/UNMAP operations anyway.

#if (_NT_TARGET_VERSION < 0x601) || defined(_IA64_)

#define VPD_THIRD_PARTY_COPY               0x8F
#define VPD_BLOCK_LIMITS                   0xB0
#define VPD_BLOCK_DEVICE_CHARACTERISTICS   0xB1
#define VPD_LOGICAL_BLOCK_PROVISIONING     0xB2

#pragma pack(push, read_capacity16, 1)
typedef struct _READ_CAPACITY16_DATA {
    LARGE_INTEGER LogicalBlockAddress;
    ULONG BytesPerBlock;
    UCHAR ProtectionEnable : 1;
    UCHAR ProtectionType : 3;
    UCHAR Reserved : 4;
    UCHAR LogicalPerPhysicalExponent : 4;
    UCHAR Reserved1 : 4;
    UCHAR LowestAlignedBlock_MSB : 6;
    UCHAR LBPRZ : 1;
    UCHAR LBPME : 1;
    UCHAR LowestAlignedBlock_LSB;
    UCHAR Reserved3[16];
} READ_CAPACITY16_DATA, *PREAD_CAPACITY16_DATA;
#pragma pack(pop, read_capacity16)

//
// Structure related to 0x42 - SCSIOP_UNMAP
//

#pragma pack(push, unmap, 1)
typedef struct _UNMAP_BLOCK_DESCRIPTOR {
    UCHAR StartingLba[8];
    UCHAR LbaCount[4];
    UCHAR Reserved[4];
} UNMAP_BLOCK_DESCRIPTOR, *PUNMAP_BLOCK_DESCRIPTOR;

typedef struct _UNMAP_LIST_HEADER {
    UCHAR DataLength[2];
    UCHAR BlockDescrDataLength[2];
    UCHAR Reserved[4];
#if !defined(__midl)
    UNMAP_BLOCK_DESCRIPTOR Descriptors[0];
#endif
} UNMAP_LIST_HEADER, *PUNMAP_LIST_HEADER;
#pragma pack(pop, unmap)

//
// VPD Page 0xB0, Block Limits
//
typedef struct _VPD_BLOCK_LIMITS_PAGE {
    UCHAR DeviceType : 5;
    UCHAR DeviceTypeQualifier : 3;
    UCHAR PageCode;                 // 0xB0
    UCHAR PageLength[2];            // 0x3C if device supports logical block provisioning, otherwise the value may be 0x10.

    union {
        struct {
            UCHAR Reserved0;

            UCHAR MaximumCompareAndWriteLength;
            UCHAR OptimalTransferLengthGranularity[2];
            UCHAR MaximumTransferLength[4];
            UCHAR OptimalTransferLength[4];
            UCHAR MaxPrefetchXDReadXDWriteTransferLength[4];
            UCHAR MaximumUnmapLBACount[4];
            UCHAR MaximumUnmapBlockDescriptorCount[4];
            UCHAR OptimalUnmapGranularity[4];
            union {
                struct {
                    UCHAR UnmapGranularityAlignmentByte3 : 7;
                    UCHAR UGAValid : 1;
                    UCHAR UnmapGranularityAlignmentByte2;
                    UCHAR UnmapGranularityAlignmentByte1;
                    UCHAR UnmapGranularityAlignmentByte0;
                };
                UCHAR UnmapGranularityAlignment[4];
            };
            UCHAR Reserved1[28];
        };
#if !defined(__midl)
        UCHAR Descriptors[0];
#endif
    };
} VPD_BLOCK_LIMITS_PAGE, *PVPD_BLOCK_LIMITS_PAGE;

//
// VPD Page 0xB1, Block Device Characteristics
//
typedef struct _VPD_BLOCK_DEVICE_CHARACTERISTICS_PAGE {
    UCHAR DeviceType : 5;
    UCHAR DeviceTypeQualifier : 3;
    UCHAR PageCode;                 // 0xB1
    UCHAR Reserved0;
    UCHAR PageLength;               // 0x3C

    UCHAR MediumRotationRateMsb;
    UCHAR MediumRotationRateLsb;
    UCHAR MediumProductType;
    UCHAR NominalFormFactor : 4;
    UCHAR Reserved2 : 4;
    UCHAR Reserved3[56];
} VPD_BLOCK_DEVICE_CHARACTERISTICS_PAGE, *PVPD_BLOCK_DEVICE_CHARACTERISTICS_PAGE;

//
// VPD Page 0xB2, Logical Block Provisioning
//

#define PROVISIONING_TYPE_UNKNOWN       0x0
#define PROVISIONING_TYPE_RESOURCE      0x1
#define PROVISIONING_TYPE_THIN          0x2

typedef struct _VPD_LOGICAL_BLOCK_PROVISIONING_PAGE {
    UCHAR DeviceType : 5;
    UCHAR DeviceTypeQualifier : 3;
    UCHAR PageCode;                 // 0xB2
    UCHAR PageLength[2];

    UCHAR ThresholdExponent;

    UCHAR DP : 1;
    UCHAR ANC_SUP : 1;
    UCHAR LBPRZ : 1;
    UCHAR Reserved0 : 2;
    UCHAR LBPWS10 : 1;
    UCHAR LBPWS : 1;
    UCHAR LBPU : 1;

    UCHAR ProvisioningType : 3;
    UCHAR Reserved1 : 5;

    UCHAR Reserved2;
#if !defined(__midl)
    UCHAR ProvisioningGroupDescr[0];
#endif
} VPD_LOGICAL_BLOCK_PROVISIONING_PAGE, *PVPD_LOGICAL_BLOCK_PROVISIONING_PAGE;

//
// Block Device UNMAP CDB
//

typedef struct _UNMAP {
    UCHAR OperationCode;    // 0x42 - SCSIOP_UNMAP
    UCHAR Anchor : 1;
    UCHAR Reserved1 : 7;
    UCHAR Reserved2[4];
    UCHAR GroupNumber : 5;
    UCHAR Reserved3 : 3;
    UCHAR AllocationLength[2];
    UCHAR Control;
} UNMAP, *PUNMAP;

#if _NT_TARGET_VERSION < 0x0501

//
//  Structure used to describe the list of ranges to process
//

typedef struct _DEVICE_DATA_SET_RANGE {
    LONGLONG    StartingOffset;        //in bytes,  must align to sector
    ULONGLONG   LengthInBytes;         // multiple of sector size.
} DEVICE_DATA_SET_RANGE, *PDEVICE_DATA_SET_RANGE;

#endif

#else

typedef _CDB::_UNMAP UNMAP, *PUNMAP;

#endif

NTSTATUS
ImScsiUnmapOrZeroProxy(
    __in __deref PPROXY_CONNECTION Proxy,
    __in ULONGLONG RequestCode,
    __out __deref PIO_STATUS_BLOCK IoStatusBlock,
    __in __deref PKEVENT CancelEvent,
    __in ULONG Items,
    __in __deref PDEVICE_DATA_SET_RANGE Ranges);

#pragma warning(pop)
