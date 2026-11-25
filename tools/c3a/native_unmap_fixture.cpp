// Host-only model of the OS interfaces used by the extracted AIM functions.
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <vector>

using UCHAR = uint8_t;
using USHORT = uint16_t;
using ULONG = uint32_t;
using ULONGLONG = uint64_t;
using LONGLONG = int64_t;
using NTSTATUS = int32_t;
using PUSHORT = USHORT*;
using PULONG = ULONG*;
using PULONGLONG = ULONGLONG*;
using VOID = void;
using PKIRQL = int*;

#ifndef __in
#define __in
#endif
#define KdPrint(x)
#define KdPrint2(x)
#define DbgPrint(x)
#define UNREFERENCED_PARAMETER(x) (void)x
#define FIELD_OFFSET(type, field) offsetof(type, field)
#define NT_SUCCESS(x) ((x) >= 0)

constexpr auto MAXLONGLONG = INT64_MAX;
constexpr NTSTATUS STATUS_SUCCESS = 0;
constexpr NTSTATUS STATUS_PENDING = 0x103;
constexpr NTSTATUS STATUS_NOT_SUPPORTED = -1;
constexpr NTSTATUS lowerFailure = -2;
constexpr ULONG FSCTL_SET_ZERO_DATA = 0x980c8;
constexpr int SRB_STATUS_PENDING = 0, SRB_STATUS_SUCCESS = 1, SRB_STATUS_BUSY = 2;
constexpr int SRB_STATUS_ERROR = 3, SRB_STATUS_NO_DEVICE = 4;
constexpr int SRB_STATUS_INVALID_REQUEST = 5, SRB_STATUS_DATA_OVERRUN = 6;
constexpr int SCSI_SENSE_NOT_READY = 1, SCSI_ADSENSE_LUN_NOT_READY = 2;
constexpr int SCSI_SENSEQ_BECOMING_READY = 3, SCSI_SENSE_DATA_PROTECT = 4;
constexpr int SCSI_ADSENSE_WRITE_PROTECT = 5, SCSI_SENSE_HARDWARE_ERROR = 6;
constexpr int SCSI_ADSENSE_NO_SENSE = 0;
constexpr int IMDPROXY_REQ_UNMAP = 1, PagedPool = 0;

USHORT RtlUshortByteSwap(USHORT n) { return static_cast<USHORT>(n << 8 | n >> 8); }
ULONG RtlUlongByteSwap(ULONG n)
{
    return static_cast<ULONG>(RtlUshortByteSwap(static_cast<USHORT>(n))) << 16 |
        RtlUshortByteSwap(static_cast<USHORT>(n >> 16));
}
ULONGLONG RtlUlonglongByteSwap(ULONGLONG n)
{
    return static_cast<ULONGLONG>(RtlUlongByteSwap(static_cast<ULONG>(n))) << 32 |
        RtlUlongByteSwap(static_cast<ULONG>(n >> 32));
}

struct LARGE_INTEGER { LONGLONG QuadPart; };
struct FILE_ZERO_DATA_INFORMATION { LARGE_INTEGER FileOffset, BeyondFinalZero; };
struct DEVICE_DATA_SET_RANGE { LONGLONG StartingOffset; ULONGLONG LengthInBytes; };
struct IO_STATUS_BLOCK { NTSTATUS Status; };
struct HW_HBA_EXT {};
using pHW_HBA_EXT = HW_HBA_EXT*;
struct HW_LU_EXTENSION
{
    int Initialized = 1, StopThread = 0, BlockPower = 9;
    bool ReadOnly = false, SupportsUnmap = true, UseProxy = false;
    ULONG FakeDiskSignature = 1;
    LARGE_INTEGER DiskSize{4096 * 512}, ImageOffset{512};
    void* ImageFile = this;
    int Proxy = 0;
};
using pHW_LU_EXTENSION = HW_LU_EXTENSION*;
using PCDB = UCHAR*;
struct UNMAP { UCHAR Unused[7], AllocationLength[2]; };
using PUNMAP = UNMAP*;
struct Descriptor { alignas(8) UCHAR StartingLba[8]; UCHAR LbaCount[4], Reserved[4]; };
struct UNMAP_LIST_HEADER { UCHAR DataLength[2], BlockDescrDataLength[2], Reserved[4]; Descriptor Descriptors[257]; };
static_assert(sizeof(Descriptor) == 16 && offsetof(UNMAP_LIST_HEADER, Descriptors) == 8,
    "the fixture must match the UNMAP wire format");
using PUNMAP_LIST_HEADER = UNMAP_LIST_HEADER*;
struct SCSI_REQUEST_BLOCK
{
    UCHAR Cdb[16]{};
    void* DataBuffer = nullptr;
    ULONG DataTransferLength = 0;
    int Status = SRB_STATUS_PENDING, ScsiStatus = 0, Sense[3]{};
};
using PSCSI_REQUEST_BLOCK = SCSI_REQUEST_BLOCK*;
enum ResultType { ResultDone, ResultQueued };
using pResultType = ResultType*;

int KeReadStateEvent(int* state) { return *state; }
void ScsiSetError(PSCSI_REQUEST_BLOCK srb, int status)
{
    srb->Status = status;
    srb->DataTransferLength = 0;
}
void ScsiSetCheckCondition(PSCSI_REQUEST_BLOCK srb, int status, int key, int asc, int ascq)
{
    ScsiSetError(srb, status);
    srb->ScsiStatus = 2;
    srb->Sense[0] = key;
    srb->Sense[1] = asc;
    srb->Sense[2] = ascq;
}
void ScsiSetSuccess(PSCSI_REQUEST_BLOCK srb, ULONG length)
{
    srb->Status = SRB_STATUS_SUCCESS;
    srb->ScsiStatus = 0;
    srb->DataTransferLength = length;
}

template<class T, int>
class WPoolMem
{
    std::vector<T> items;
public:
    explicit WPoolMem(std::size_t size) : items(size / sizeof(T)) {}
    explicit operator bool() const { return !items.empty(); }
    T& operator[](int i) { return items[i]; }
};
NTSTATUS ImScsiUnmapOrZeroProxy(int*, int, IO_STATUS_BLOCK*, int*, USHORT, WPoolMem<DEVICE_DATA_SET_RANGE, PagedPool>&)
{
    return STATUS_NOT_SUPPORTED;
}

struct AimUnmapRequest { ULONG Step, Attempted; };
enum class AimUnmapDisposition : ULONG
{
    Queued, Uninitialized, ShuttingDown, ReadOnly, Unsupported, TransferLength,
    DataLength, DescriptorLength, LbaOverflow, DiskBounds, NoWorkItem
};
struct WorkItem { AimUnmapRequest UnmapDiagnostic; };
using pMP_WorkRtnParms = WorkItem*;
WorkItem workItem;
bool allocateWork, queued;
std::vector<NTSTATUS> nativeReturns;
std::vector<FILE_ZERO_DATA_INFORMATION> nativeInputs;

void require(bool success) { if (!success) std::abort(); }
pMP_WorkRtnParms ImScsiCreateWorkItem(pHW_HBA_EXT, pHW_LU_EXTENSION, PSCSI_REQUEST_BLOCK)
{
    return allocateWork ? &workItem : nullptr;
}
void ImScsiScheduleWorkItem(WorkItem* work, PKIRQL)
{
#ifdef AIM_C3A_UNMAP_DIAGNOSTICS
    require(work->UnmapDiagnostic.Step == 2);
#else
    UNREFERENCED_PARAMETER(work);
#endif
    queued = true;
}

NTSTATUS ZwFsControlFile(void*, void*, void*, void*, IO_STATUS_BLOCK*, ULONG code,
    const void* input, ULONG size, void*, ULONG)
{
    require(code == FSCTL_SET_ZERO_DATA && size == sizeof(FILE_ZERO_DATA_INFORMATION));
    const auto ordinal = nativeInputs.size();
    nativeInputs.push_back(*static_cast<const FILE_ZERO_DATA_INFORMATION*>(input));
    return ordinal < nativeReturns.size() ? nativeReturns[ordinal] : STATUS_SUCCESS;
}

AimUnmapRequest AimUnmapBegin(pHW_LU_EXTENSION, PSCSI_REQUEST_BLOCK) { return {1, 0}; }
void AimUnmapAdmission(AimUnmapRequest& request, PSCSI_REQUEST_BLOCK, AimUnmapDisposition, ULONG)
{
    require(request.Step++ == 1);
}
void AimUnmapDispatch(AimUnmapRequest& request, pHW_LU_EXTENSION, ULONG)
{
    require(request.Step++ == 2);
}
FILE_ZERO_DATA_INFORMATION observedInput;
void AimUnmapZeroBegin(AimUnmapRequest& request, pHW_LU_EXTENSION, ULONG ordinal,
    ULONGLONG lba, ULONG blocks, ULONG code, const void* input, ULONG size)
{
    require(request.Step == 3 && request.Attempted == ordinal && nativeInputs.size() == ordinal);
    require(code == FSCTL_SET_ZERO_DATA && size == 16);
    observedInput = *static_cast<const FILE_ZERO_DATA_INFORMATION*>(input);
    require(observedInput.FileOffset.QuadPart == static_cast<LONGLONG>(lba * 512 + 512));
    require(observedInput.BeyondFinalZero.QuadPart == observedInput.FileOffset.QuadPart + blocks * 512);
}
void AimUnmapZeroResult(AimUnmapRequest& request, ULONG ordinal, NTSTATUS status)
{
    require(request.Attempted++ == ordinal && nativeInputs.size() == ordinal + 1);
    require(status == (ordinal < nativeReturns.size() ? nativeReturns[ordinal] : STATUS_SUCCESS));
    require(nativeInputs.back().FileOffset.QuadPart == observedInput.FileOffset.QuadPart);
    require(nativeInputs.back().BeyondFinalZero.QuadPart == observedInput.BeyondFinalZero.QuadPart);
}
void AimUnmapFinish(AimUnmapRequest& request, PSCSI_REQUEST_BLOCK)
{
    require((request.Step == 2 || request.Step == 3) && request.Attempted == nativeInputs.size());
}

#include "unmap-under-test.inc"

int main()
{
    // 3 descriptors unless testing the capture ceiling. The OS model always
    // provides a resident buffer, including for a misdeclared transfer.
    for (int scenario = 0; scenario < 14; ++scenario)
    {
        HW_HBA_EXT hba;
        HW_LU_EXTENSION lu;
        SCSI_REQUEST_BLOCK srb;
        UNMAP_LIST_HEADER list{};
        ResultType result = ResultDone;
        int irql = 0;
        const auto descriptors = scenario == 13 ? 257 : 3;
        const auto length = static_cast<USHORT>(8 + descriptors * 16);
        *reinterpret_cast<PUSHORT>(reinterpret_cast<PUNMAP>(srb.Cdb)->AllocationLength) = RtlUshortByteSwap(length);
        *reinterpret_cast<PUSHORT>(list.DataLength) = RtlUshortByteSwap(length - 2);
        *reinterpret_cast<PUSHORT>(list.BlockDescrDataLength) = RtlUshortByteSwap(length - 8);
        for (int i = 0; i < descriptors; ++i)
        {
            *reinterpret_cast<PULONGLONG>(list.Descriptors[i].StartingLba) = RtlUlonglongByteSwap(i * 4);
            *reinterpret_cast<PULONG>(list.Descriptors[i].LbaCount) = RtlUlongByteSwap(4);
        }
        srb.DataBuffer = &list;
        srb.DataTransferLength = length;
        allocateWork = true;
        queued = false;
        workItem = {};
        nativeInputs.clear();
        nativeReturns.clear();
        switch (scenario)
        {
        case 1: nativeReturns = {STATUS_SUCCESS, lowerFailure}; break;
        case 2: nativeReturns = {STATUS_PENDING}; break;
        case 3: lu.Initialized = 0; break;
        case 4: lu.StopThread = 1; break;
        case 5: lu.ReadOnly = true; break;
        case 6: lu.SupportsUnmap = false; break;
        case 7: --srb.DataTransferLength; break;
        case 8: *reinterpret_cast<PUSHORT>(list.DataLength) = RtlUshortByteSwap(length); break;
        case 9: *reinterpret_cast<PUSHORT>(list.BlockDescrDataLength) = RtlUshortByteSwap(length); break;
        case 10: *reinterpret_cast<PULONGLONG>(list.Descriptors[0].StartingLba) = RtlUlonglongByteSwap(UINT64_MAX); break;
        case 11: lu.DiskSize.QuadPart = 0; break;
        case 12: allocateWork = false; break;
        }
        ScsiOpUnmap(&hba, &lu, &srb, &result, &irql);
        require((result == ResultQueued) == queued && queued == (scenario < 3 || scenario == 13));
        if (queued)
        {
            ImScsiDispatchUnmapDevice(&hba, &lu, &srb
#ifdef AIM_C3A_UNMAP_DIAGNOSTICS
                , workItem.UnmapDiagnostic
#endif
            );
#ifdef AIM_C3A_UNMAP_DIAGNOSTICS
            AimUnmapFinish(workItem.UnmapDiagnostic, &srb);
#endif
            require(srb.Status == SRB_STATUS_SUCCESS && srb.ScsiStatus == 0);
            require(nativeInputs.size() == static_cast<std::size_t>(scenario == 1 ? 2 : descriptors));
        }
        else
        {
            require(nativeInputs.empty());
        }
        std::cout << scenario << ' ' << result << ' ' << srb.Status << ' ' << srb.ScsiStatus
            << ' ' << srb.DataTransferLength << ' ' << srb.Sense[0] << ' ' << srb.Sense[1]
            << ' ' << srb.Sense[2] << ' ' << lu.FakeDiskSignature;
        for (const auto& range : nativeInputs)
            std::cout << ' ' << range.FileOffset.QuadPart << ':' << range.BeyondFinalZero.QuadPart;
        std::cout << '\n';
    }
}
