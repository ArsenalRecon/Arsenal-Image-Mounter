// Host-only interfaces for the extracted Storport UNMAP path.
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <vector>

using UCHAR = uint8_t;
using USHORT = uint16_t;
using ULONG = uint32_t;
using LONGLONG = int64_t;
using ULONGLONG = uint64_t;
using NTSTATUS = int32_t;
using KIRQL = int;
using KPRIORITY = int;
using KLOCK_QUEUE_HANDLE = int;
using KSPIN_LOCK = int;
using PKIRQL = KIRQL*;
using PKSPIN_LOCK = KSPIN_LOCK*;
using PUSHORT = USHORT*;
using PULONG = ULONG*;
using PULONGLONG = ULONGLONG*;
using VOID = void;
using PVOID = void*;
using HANDLE = void*;
using BOOLEAN = bool;
using PUCHAR = UCHAR*;
using PETHREAD = void*;

#ifndef __in
#define __in
#endif
#ifndef __deref
#define __deref
#endif
#ifndef __inout
#define __inout
#endif
#ifndef OPTIONAL
#define OPTIONAL
#endif
#define KdPrint(x)
#define KdPrint2(x)
#define DbgPrint(...)
#define UNREFERENCED_PARAMETER(x) (void)(x)
#define FIELD_OFFSET(type, field) offsetof(type, field)
#define CONTAINING_RECORD(address, type, field) reinterpret_cast<type*>(reinterpret_cast<char*>(address) - offsetof(type, field))
#define NT_SUCCESS(status) ((status) >= 0)
#define USE_STORPORT 1

constexpr LONGLONG MAXLONGLONG = INT64_MAX;
constexpr NTSTATUS STATUS_SUCCESS = 0x00000000, STATUS_PENDING = 0x00000103;
constexpr NTSTATUS STATUS_UNSUCCESSFUL = static_cast<NTSTATUS>(0xc0000001);
constexpr NTSTATUS STATUS_INVALID_DEVICE_REQUEST = static_cast<NTSTATUS>(0xc0000010);
constexpr NTSTATUS STATUS_NOT_SUPPORTED = static_cast<NTSTATUS>(0xc00000bb);
constexpr NTSTATUS STATUS_DEVICE_NOT_READY = static_cast<NTSTATUS>(0xc00000a3);
constexpr NTSTATUS STATUS_MEDIA_WRITE_PROTECTED = static_cast<NTSTATUS>(0xc00000a2);
constexpr NTSTATUS STATUS_CANCELLED = static_cast<NTSTATUS>(0xc0000120);
constexpr NTSTATUS STATUS_IO_DEVICE_ERROR = static_cast<NTSTATUS>(0xc0000185);
constexpr NTSTATUS STATUS_INVALID_BUFFER_SIZE = static_cast<NTSTATUS>(0xc0000206);

constexpr ULONG FSCTL_SET_ZERO_DATA = 0x980c8, SRB_FLAGS_DISABLE_AUTOSENSE = 0x20;
constexpr UCHAR SRB_STATUS_PENDING = 0, SRB_STATUS_SUCCESS = 1, SRB_STATUS_ABORTED = 2;
constexpr UCHAR SRB_STATUS_ERROR = 4, SRB_STATUS_BUSY = 5, SRB_STATUS_INVALID_REQUEST = 6;
constexpr UCHAR SRB_STATUS_NO_DEVICE = 8, SRB_STATUS_DATA_OVERRUN = 0x12;
constexpr UCHAR SRB_STATUS_INTERNAL_ERROR = 0x30, SRB_STATUS_AUTOSENSE_VALID = 0x80;
constexpr UCHAR SCSISTAT_GOOD = 0, SCSISTAT_CHECK_CONDITION = 2;
constexpr UCHAR SCSI_SENSE_NOT_READY = 2, SCSI_SENSE_HARDWARE_ERROR = 4;
constexpr UCHAR SCSI_SENSE_ILLEGAL_REQUEST = 5, SCSI_SENSE_DATA_PROTECT = 7;
constexpr UCHAR SCSI_SENSE_ABORTED_COMMAND = 0x0b, SCSI_SENSE_ERRORCODE_FIXED_CURRENT = 0x70;
constexpr UCHAR SCSI_ADSENSE_LUN_NOT_READY = 0x04, SCSI_SENSEQ_BECOMING_READY = 1;
constexpr UCHAR SCSI_ADSENSE_NO_SENSE = 0, SCSI_ADSENSE_ILLEGAL_COMMAND = 0x20;
constexpr UCHAR SCSI_ADSENSE_ILLEGAL_BLOCK = 0x21, SCSI_ADSENSE_WRITE_PROTECT = 0x27;
constexpr UCHAR SCSI_ADSENSE_INTERNAL_TARGET_FAILURE = 0x44;
constexpr int SRB_FUNCTION_EXECUTE_SCSI = 0, SRB_FUNCTION_IO_CONTROL = 2;
constexpr int SCSIOP_READ = 0x28, SCSIOP_WRITE = 0x2a, SCSIOP_UNMAP = 0x42;
constexpr int SCSIOP_READ16 = 0x88, SCSIOP_WRITE16 = 0x8a;
constexpr int SMP_IMSCSI_CREATE_DEVICE = 1, SMP_IMSCSI_EXTEND_DEVICE = 2;
constexpr int IMDPROXY_REQ_UNMAP = 1, PagedPool = 0, MP_TAG_GENERAL = 1;
constexpr int LOW_REALTIME_PRIORITY = 16, PASSIVE_LEVEL = 0, WaitAny = 1, Executive = 0, KernelMode = 0;
constexpr int RequestComplete = 0;
constexpr bool FALSE = false, TRUE = true;

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
using PLARGE_INTEGER = LARGE_INTEGER*;
struct FILE_ZERO_DATA_INFORMATION { LARGE_INTEGER FileOffset, BeyondFinalZero; };
struct DEVICE_DATA_SET_RANGE { LONGLONG StartingOffset; ULONGLONG LengthInBytes; };
struct IO_STATUS_BLOCK { NTSTATUS Status; uintptr_t Information; };
struct LIST_ENTRY { LIST_ENTRY* next = nullptr; };
using PLIST_ENTRY = LIST_ENTRY*;
using PKEVENT = int*;
struct HW_HBA_EXT {};
using pHW_HBA_EXT = HW_HBA_EXT*;
struct HW_LU_EXTENSION
{
    int Initialized = 1, StopThread = 0, BlockPower = 9, RequestEvent = 0;
    bool ReadOnly = false, SupportsUnmap = true, UseProxy = false, VMDisk = false;
    LIST_ENTRY RequestList;
    KSPIN_LOCK RequestListLock = 0;
    ULONG FakeDiskSignature = 1;
    LARGE_INTEGER DiskSize{4096 * 512}, ImageOffset{512};
    HANDLE ImageFile = this;
    PVOID WorkerThread = nullptr;
    int Proxy = 0;
};
using pHW_LU_EXTENSION = HW_LU_EXTENSION*;
using PCDB = UCHAR*;
struct UNMAP { UCHAR Unused[7], AllocationLength[2], Control; };
using PUNMAP = UNMAP*;
struct Descriptor { alignas(8) UCHAR StartingLba[8]; UCHAR LbaCount[4], Reserved[4]; };
struct UNMAP_LIST_HEADER { UCHAR DataLength[2], BlockDescrDataLength[2], Reserved[4]; Descriptor Descriptors[3]; };
static_assert(sizeof(Descriptor) == 16 && FIELD_OFFSET(UNMAP_LIST_HEADER, Descriptors) == 8, "UNMAP wire layout");
using PUNMAP_LIST_HEADER = UNMAP_LIST_HEADER*;
struct SENSE_DATA
{
    UCHAR ErrorCode : 7, Valid : 1;
    UCHAR SegmentNumber;
    UCHAR SenseKey : 4, Reserved : 1, IncorrectLength : 1, EndOfMedia : 1, FileMark : 1;
    UCHAR Information[4], AdditionalSenseLength, CommandSpecificInformation[4];
    UCHAR AdditionalSenseCode, AdditionalSenseCodeQualifier, FieldReplaceableUnitCode, SenseKeySpecific[3];
};
static_assert(sizeof(SENSE_DATA) == 18, "fixed-format sense layout");
using PSENSE_DATA = SENSE_DATA*;
struct SCSI_REQUEST_BLOCK
{
    alignas(2) UCHAR Cdb[16]{SCSIOP_UNMAP};
    UCHAR CdbLength = 10, Function = SRB_FUNCTION_EXECUTE_SCSI;
    UCHAR SrbStatus = SRB_STATUS_PENDING, ScsiStatus = SCSISTAT_GOOD, SenseInfoBufferLength = 0;
    PVOID DataBuffer = nullptr, SenseInfoBuffer = nullptr;
    ULONG DataTransferLength = 0, SrbFlags = 0;
};
using PSCSI_REQUEST_BLOCK = SCSI_REQUEST_BLOCK*;
struct SRB_IO_CONTROL { ULONG ControlCode; NTSTATUS ReturnCode; };
using PSRB_IO_CONTROL = SRB_IO_CONTROL*;
using PSRB_IMSCSI_EXTEND_DEVICE = PVOID;
enum ResultType { ResultDone, ResultQueued };
using pResultType = ResultType*;
struct MP_WorkRtnParms
{
    LIST_ENTRY RequestListEntry;
    pHW_HBA_EXT pHBAExt;
    pHW_LU_EXTENSION pLUExt;
    PSCSI_REQUEST_BLOCK pSrb;
    PETHREAD pReqThread;
    PKEVENT CallerWaitEvent;
};
using pMP_WorkRtnParms = MP_WorkRtnParms*;
struct { LIST_ENTRY RequestList; KSPIN_LOCK RequestListLock; int RequestEvent, StopWorker; } driverInfo;
auto pMPDrvInfoGlobal = &driverInfo;

struct ZeroResult { NTSTATUS submission, completion; bool alreadyZero = false; };
enum class WorkerEvent { ThreadReleased, CallerSignaled, SrbCompleted, WorkFreed, LuCleaned, ThreadExited };
struct Scenario
{
    HW_HBA_EXT hba;
    HW_LU_EXTENSION lu;
    SCSI_REQUEST_BLOCK srb;
    SENSE_DATA sense;
    UNMAP_LIST_HEADER list{};
    std::vector<ZeroResult> lowerResults;
    std::vector<LONGLONG> expectedLbas{8, 16, 24};
    std::vector<FILE_ZERO_DATA_INFORMATION> calls;
    std::vector<LONGLONG> applied;
    std::vector<WorkerEvent> disposition;
    IO_STATUS_BLOCK* pendingStatus = nullptr;
    const FILE_ZERO_DATA_INFORMATION* pendingInput = nullptr;
    bool allowWork = true, stopDuringWait = false;
    unsigned outstandingWork = 0, waits = 0;
    int callerEvent = 0;
    NTSTATUS waitStatus = STATUS_SUCCESS;
    NTSTATUS proxyStatus = STATUS_SUCCESS;
    unsigned proxyCalls = 0;
    static constexpr USHORT listSize = sizeof(UNMAP_LIST_HEADER);

    Scenario()
    {
        srb.DataBuffer = &list;
        srb.DataTransferLength = listSize;
        srb.SenseInfoBuffer = &sense;
        srb.SenseInfoBufferLength = sizeof(sense);
        std::memset(&sense, 0xa5, sizeof(sense));
        const auto cdbLength = RtlUshortByteSwap(listSize);
        std::memcpy(reinterpret_cast<PUNMAP>(srb.Cdb)->AllocationLength, &cdbLength, sizeof(cdbLength));
        const auto dataLength = RtlUshortByteSwap(listSize - sizeof(list.DataLength));
        std::memcpy(list.DataLength, &dataLength, sizeof(dataLength));
        const auto descriptorLength = RtlUshortByteSwap(sizeof(list.Descriptors));
        std::memcpy(list.BlockDescrDataLength, &descriptorLength, sizeof(descriptorLength));
        for (int index = 0; index < 3; ++index)
        {
            const auto lba = RtlUlonglongByteSwap(8 * (index + 1));
            const auto blocks = RtlUlongByteSwap(4);
            std::memcpy(list.Descriptors[index].StartingLba, &lba, sizeof(lba));
            std::memcpy(list.Descriptors[index].LbaCount, &blocks, sizeof(blocks));
        }
    }
};

Scenario* current;
const char* scenarioName;

void require(bool success, const char* check)
{
    if (!success)
    {
        std::fprintf(stderr, "%s failed: %s\n", scenarioName, check);
        std::exit(EXIT_FAILURE);
    }
}
#define REQUIRE(x) require((x), #x)

void RtlZeroMemory(PVOID address, size_t size) { std::memset(address, 0, size); }
int KeReadStateEvent(PKEVENT event) { return *event; }
PVOID KeGetCurrentThread() { return current; }
void KeSetPriorityThread(PVOID, KPRIORITY) {}
void ImScsiAcquireLock(PKSPIN_LOCK, KLOCK_QUEUE_HANDLE*, KIRQL) {}
void ImScsiReleaseLock(KLOCK_QUEUE_HANDLE*, PKIRQL) {}
PLIST_ENTRY RemoveHeadList(PLIST_ENTRY list)
{
    if (!list->next)
        return list;
    const auto entry = list->next;
    list->next = nullptr;
    return entry;
}
void KeSetEvent(PKEVENT event, KPRIORITY, BOOLEAN)
{
    REQUIRE(event == &current->callerEvent);
    current->disposition.push_back(WorkerEvent::CallerSignaled);
    *event = 1;
}
void ObDereferenceObject(PVOID thread)
{
    REQUIRE(thread == current);
    current->disposition.push_back(WorkerEvent::ThreadReleased);
}
void StorPortNotification(int kind, pHW_HBA_EXT hba, PSCSI_REQUEST_BLOCK srb)
{
    REQUIRE(kind == RequestComplete && hba == &current->hba && srb == &current->srb);
    REQUIRE(current->pendingStatus == nullptr && current->outstandingWork == 1);
    REQUIRE(current->srb.SrbStatus != SRB_STATUS_PENDING);
    current->disposition.push_back(WorkerEvent::SrbCompleted);
    if (!current->lu.StopThread)
        driverInfo.StopWorker = 1;
}
void ExFreePoolWithTag(PVOID data, ULONG tag)
{
    REQUIRE(tag == MP_TAG_GENERAL && current->outstandingWork == 1);
    current->disposition.push_back(WorkerEvent::WorkFreed);
    --current->outstandingWork;
    delete static_cast<pMP_WorkRtnParms>(data);
}
void ImScsiCleanupLU(pHW_LU_EXTENSION lu, PKIRQL)
{
    REQUIRE(lu == &current->lu && current->outstandingWork == 0);
    current->disposition.push_back(WorkerEvent::LuCleaned);
}
void PsTerminateSystemThread(NTSTATUS status)
{
    REQUIRE(status == STATUS_SUCCESS);
    current->disposition.push_back(WorkerEvent::ThreadExited);
}
NTSTATUS KeWaitForSingleObject(PVOID, int, int, BOOLEAN, PLARGE_INTEGER)
{
    REQUIRE(false);
    return STATUS_UNSUCCESSFUL;
}
void KeWaitForMultipleObjects(ULONG, PVOID*, int, int, int, BOOLEAN, PVOID, PVOID) { REQUIRE(false); }
bool ImScsiFillMemoryDisk(pHW_LU_EXTENSION) { REQUIRE(false); return false; }
void ImScsiCreateLU(pHW_HBA_EXT, PSCSI_REQUEST_BLOCK, PETHREAD, PKIRQL) { REQUIRE(false); }
NTSTATUS ImScsiExtendLU(pHW_HBA_EXT, pHW_LU_EXTENSION, PSRB_IMSCSI_EXTEND_DEVICE)
{
    REQUIRE(false);
    return STATUS_UNSUCCESSFUL;
}
void ImScsiDispatchReadWrite(pHW_HBA_EXT, pHW_LU_EXTENSION, PSCSI_REQUEST_BLOCK) { REQUIRE(false); }

pMP_WorkRtnParms ImScsiCreateWorkItem(pHW_HBA_EXT hba, pHW_LU_EXTENSION lu, PSCSI_REQUEST_BLOCK srb)
{
    if (!current->allowWork)
        return nullptr;
    ++current->outstandingWork;
    return new MP_WorkRtnParms{{}, hba, lu, srb, current, &current->callerEvent};
}
void ImScsiScheduleWorkItem(pMP_WorkRtnParms work, PKIRQL)
{
    REQUIRE(work->pLUExt->RequestList.next == nullptr);
    work->pLUExt->RequestList.next = &work->RequestListEntry;
}

template<class T, int>
class WPoolMem
{
    std::vector<T> data;
public:
    explicit WPoolMem(std::size_t size) : data(size / sizeof(T)) {}
    explicit operator bool() const { return !data.empty(); }
    operator T*() { return data.data(); }
    T& operator[](int i) { return data[i]; }
};
NTSTATUS ImScsiUnmapOrZeroProxy(int*, int, IO_STATUS_BLOCK* result, PKEVENT, USHORT count, DEVICE_DATA_SET_RANGE* ranges)
{
    REQUIRE(count == 3 && ranges[0].StartingOffset == 9 * 512 && ranges[0].LengthInBytes == 4 * 512);
    ++current->proxyCalls;
    result->Status = current->proxyStatus;
    result->Information = 0;
    return current->proxyStatus;
}

NTSTATUS ZwFsControlFile(HANDLE file, HANDLE event, PVOID apc, PVOID context, IO_STATUS_BLOCK* status, ULONG code,
    const void* input, ULONG size, void* output, ULONG outputLength)
{
    REQUIRE(file == current->lu.ImageFile && event == nullptr && apc == nullptr && context == nullptr);
    REQUIRE(current->pendingStatus == nullptr && current->outstandingWork == 1);
    REQUIRE(code == FSCTL_SET_ZERO_DATA && size == sizeof(FILE_ZERO_DATA_INFORMATION));
    REQUIRE(output == nullptr && outputLength == 0);
    const auto ordinal = current->calls.size();
    const auto range = static_cast<const FILE_ZERO_DATA_INFORMATION*>(input);
    REQUIRE(ordinal < current->expectedLbas.size());
    REQUIRE(range->FileOffset.QuadPart == (current->expectedLbas[ordinal] + 1) * 512);
    REQUIRE(range->BeyondFinalZero.QuadPart == range->FileOffset.QuadPart + 4 * 512);
    current->calls.push_back(*range);
    const auto result = ordinal < current->lowerResults.size() ? current->lowerResults[ordinal] :
        ZeroResult{STATUS_SUCCESS, STATUS_SUCCESS};
    if (result.submission == STATUS_PENDING)
    {
        current->pendingStatus = status;
        current->pendingInput = range;
        return result.submission;
    }
    // A direct return determines completion. Deliberately disagreeing IOSB
    // values ensure it is read only on the pending path.
    status->Status = result.completion;
    status->Information = 0;
    if (NT_SUCCESS(result.submission) && !result.alreadyZero)
        current->applied.push_back(range->FileOffset.QuadPart);
    return result.submission;
}
NTSTATUS ZwWaitForSingleObject(HANDLE file, BOOLEAN alertable, PLARGE_INTEGER timeout)
{
    REQUIRE(file == current->lu.ImageFile && !alertable && timeout == nullptr);
    REQUIRE(current->pendingStatus != nullptr && current->pendingInput != nullptr);
    REQUIRE(current->disposition.empty() && current->outstandingWork == 1);
    const auto& observed = current->calls.back();
    REQUIRE(std::memcmp(&observed, current->pendingInput, sizeof(observed)) == 0);
    const auto result = current->lowerResults[current->calls.size() - 1];
    current->pendingStatus->Status = result.completion;
    current->pendingStatus->Information = 0;
    if (result.completion != STATUS_PENDING && NT_SUCCESS(result.completion) && !result.alreadyZero)
        current->applied.push_back(observed.FileOffset.QuadPart);
    if (current->stopDuringWait)
        current->lu.StopThread = 1;
    current->pendingStatus = nullptr;
    current->pendingInput = nullptr;
    ++current->waits;
    return current->waitStatus;
}

VOID ImScsiDispatchUnmapDevice(pHW_HBA_EXT, pHW_LU_EXTENSION, PSCSI_REQUEST_BLOCK);
#include "unmap-under-test.inc"

void execute(const char* name, Scenario& scenario, bool expectedQueued)
{
    current = &scenario;
    scenarioName = name;
    driverInfo.StopWorker = 0;
    ResultType result = ResultDone;
    KIRQL irql = PASSIVE_LEVEL;
    ScsiOpUnmap(&scenario.hba, &scenario.lu, &scenario.srb, &result, &irql);
    REQUIRE((result == ResultQueued) == expectedQueued);
    REQUIRE(scenario.calls.empty() && scenario.disposition.empty());
    REQUIRE(scenario.outstandingWork == (expectedQueued ? 1u : 0u));
    if (expectedQueued)
    {
        ImScsiWorkerThread(&scenario.lu);
        const std::vector<WorkerEvent> expected{
            WorkerEvent::ThreadReleased, WorkerEvent::CallerSignaled, WorkerEvent::SrbCompleted,
            WorkerEvent::WorkFreed, WorkerEvent::LuCleaned, WorkerEvent::ThreadExited};
        REQUIRE(scenario.disposition == expected);
        REQUIRE(scenario.callerEvent == 1 && scenario.outstandingWork == 0 && scenario.lu.RequestList.next == nullptr);
    }
    REQUIRE(scenario.srb.DataTransferLength == 0);
}

void verifySuccess(const Scenario& scenario, size_t submitted, size_t applied)
{
    REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_SUCCESS && scenario.srb.ScsiStatus == SCSISTAT_GOOD);
    REQUIRE(scenario.srb.SenseInfoBufferLength == 0);
    REQUIRE(scenario.calls.size() == submitted && scenario.applied.size() == applied);
}

void verifyFailure(const Scenario& scenario, UCHAR status, UCHAR key, UCHAR code, size_t submitted, size_t applied)
{
    REQUIRE(scenario.srb.SrbStatus == (status | SRB_STATUS_AUTOSENSE_VALID));
    REQUIRE(scenario.srb.ScsiStatus == SCSISTAT_CHECK_CONDITION);
    REQUIRE(scenario.srb.SenseInfoBufferLength == sizeof(scenario.sense));
    REQUIRE(scenario.sense.ErrorCode == SCSI_SENSE_ERRORCODE_FIXED_CURRENT && !scenario.sense.Valid);
    REQUIRE(scenario.sense.AdditionalSenseLength == 10);
    REQUIRE(scenario.sense.SenseKey == key && scenario.sense.AdditionalSenseCode == code);
    REQUIRE(scenario.sense.AdditionalSenseCodeQualifier == 0);
    REQUIRE(scenario.calls.size() == submitted && scenario.applied.size() == applied);
}

int main()
{
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_SUCCESS, STATUS_IO_DEVICE_ERROR, true}};
        execute("multiple descriptors including an already-zero range", scenario, true);
        verifySuccess(scenario, 3, 2);
        REQUIRE(scenario.applied.front() == 17 * 512);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_INVALID_BUFFER_SIZE, STATUS_SUCCESS}};
        execute("first lower failure", scenario, true);
        verifyFailure(scenario, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_INTERNAL_TARGET_FAILURE, 1, 0);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_SUCCESS, STATUS_SUCCESS}, {STATUS_MEDIA_WRITE_PROTECTED, STATUS_SUCCESS}};
        execute("successful prefix then write protection", scenario, true);
        verifyFailure(scenario, SRB_STATUS_ERROR, SCSI_SENSE_DATA_PROTECT, SCSI_ADSENSE_WRITE_PROTECT, 2, 1);
        REQUIRE(scenario.applied.front() == 9 * 512);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_PENDING, STATUS_SUCCESS}};
        execute("pending completion success", scenario, true);
        verifySuccess(scenario, 3, 3);
        REQUIRE(scenario.waits == 1);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_PENDING, STATUS_DEVICE_NOT_READY}};
        execute("pending completion failure", scenario, true);
        verifyFailure(scenario, SRB_STATUS_ERROR, SCSI_SENSE_NOT_READY, SCSI_ADSENSE_LUN_NOT_READY, 1, 0);
        REQUIRE(scenario.waits == 1);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_SUCCESS, STATUS_SUCCESS}, {STATUS_PENDING, STATUS_CANCELLED}};
        scenario.stopDuringWait = true;
        execute("pending cancellation during rundown after applied prefix", scenario, true);
        verifyFailure(scenario, SRB_STATUS_ABORTED, SCSI_SENSE_ABORTED_COMMAND, SCSI_ADSENSE_NO_SENSE, 2, 1);
        REQUIRE(scenario.waits == 1 && scenario.lu.StopThread == 1);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_PENDING, STATUS_PENDING}};
        execute("signaled handle with inconsistent pending IOSB", scenario, true);
        verifyFailure(scenario, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_INTERNAL_TARGET_FAILURE, 1, 0);
        REQUIRE(scenario.waits == 1);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_PENDING, STATUS_SUCCESS}};
        scenario.waitStatus = STATUS_UNSUCCESSFUL;
        execute("inconsistent native wait result", scenario, true);
        verifyFailure(scenario, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_INTERNAL_TARGET_FAILURE, 1, 1);
        REQUIRE(scenario.waits == 1);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_INVALID_DEVICE_REQUEST, STATUS_SUCCESS}};
        execute("lower unsupported operation", scenario, true);
        verifyFailure(scenario, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_ILLEGAL_COMMAND, 1, 0);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_IO_DEVICE_ERROR, STATUS_SUCCESS}};
        scenario.srb.SrbFlags = SRB_FLAGS_DISABLE_AUTOSENSE;
        execute("caller disabled autosense", scenario, true);
        REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_ERROR && scenario.srb.ScsiStatus == SCSISTAT_CHECK_CONDITION);
        REQUIRE(reinterpret_cast<const UCHAR*>(&scenario.sense)[0] == 0xa5);
    }
    {
        Scenario scenario;
        scenario.lowerResults = {{STATUS_IO_DEVICE_ERROR, STATUS_SUCCESS}};
        scenario.srb.SenseInfoBufferLength = 2;
        execute("autosense buffer too short", scenario, true);
        REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_ERROR && scenario.srb.ScsiStatus == SCSISTAT_CHECK_CONDITION);
        REQUIRE(reinterpret_cast<const UCHAR*>(&scenario.sense)[2] == 0xa5);
    }
    {
        Scenario scenario;
        std::memset(reinterpret_cast<PUNMAP>(scenario.srb.Cdb)->AllocationLength, 0, 2);
        scenario.srb.DataBuffer = nullptr;
        scenario.srb.DataTransferLength = 0;
        execute("empty parameter list", scenario, false);
        verifySuccess(scenario, 0, 0);
    }
    {
        Scenario scenario;
        scenario.srb.CdbLength = sizeof(UNMAP) - 1;
        execute("short UNMAP CDB", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_INVALID_REQUEST && scenario.calls.empty());
    }
    {
        Scenario scenario;
        scenario.srb.DataBuffer = nullptr;
        execute("missing nonempty parameter list", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_DATA_OVERRUN && scenario.calls.empty());
    }
    {
        Scenario scenario;
        const auto shortLength = RtlUshortByteSwap(2);
        std::memcpy(reinterpret_cast<PUNMAP>(scenario.srb.Cdb)->AllocationLength, &shortLength, sizeof(shortLength));
        scenario.srb.DataTransferLength = 2;
        execute("short parameter header", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_DATA_OVERRUN && scenario.calls.empty());
    }
    {
        Scenario scenario;
        // Only the first two complete descriptors may reach the provider.
        --scenario.list.BlockDescrDataLength[1];
        execute("incomplete trailing descriptor allowed by SBC", scenario, true);
        verifySuccess(scenario, 2, 2);
    }
    for (bool exactTransfer : {false, true})
    {
        Scenario scenario;
        auto cdb = reinterpret_cast<PUNMAP>(scenario.srb.Cdb);
        // One complete descriptor and eight bytes of the second. The header
        // still describes all three and the buffer may have trailing capacity.
        cdb->AllocationLength[1] = 32;
        if (exactTransfer)
            scenario.srb.DataTransferLength = 32;
        execute("CDB bounds the complete descriptor prefix", scenario, true);
        verifySuccess(scenario, 1, 1);
        REQUIRE(scenario.applied.front() == 9 * 512);
    }
    {
        Scenario scenario;
        scenario.list.DataLength[1] = 6;
        scenario.list.BlockDescrDataLength[1] = 0;
        execute("empty descriptor list inside a valid header", scenario, false);
        verifySuccess(scenario, 0, 0);
    }
    {
        Scenario scenario;
        std::memset(scenario.list.Descriptors[1].LbaCount, 0, sizeof(Descriptor::LbaCount));
        scenario.expectedLbas = {8, 24};
        execute("zero-count descriptor between two nonempty descriptors", scenario, true);
        verifySuccess(scenario, 2, 2);
    }
    {
        Scenario scenario;
        scenario.list.BlockDescrDataLength[1] += sizeof(Descriptor);
        execute("descriptor list exceeds transfer", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_DATA_OVERRUN && scenario.calls.empty());
    }
    {
        Scenario scenario;
        // The late invalid range must prevent even the first valid ZERO.
        const auto invalidLba = RtlUlonglongByteSwap(4094);
        std::memcpy(scenario.list.Descriptors[2].StartingLba, &invalidLba, sizeof(invalidLba));
        execute("later descriptor outside disk", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == (SRB_STATUS_ERROR | SRB_STATUS_AUTOSENSE_VALID));
        REQUIRE(scenario.srb.ScsiStatus == SCSISTAT_CHECK_CONDITION && scenario.calls.empty());
        REQUIRE(scenario.sense.SenseKey == SCSI_SENSE_ILLEGAL_REQUEST && scenario.sense.AdditionalSenseCode == SCSI_ADSENSE_ILLEGAL_BLOCK);
    }
    {
        Scenario scenario;
        const auto invalidLba = RtlUlonglongByteSwap(UINT64_MAX);
        std::memcpy(scenario.list.Descriptors[0].StartingLba, &invalidLba, sizeof(invalidLba));
        execute("overflowing LBA range", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == (SRB_STATUS_ERROR | SRB_STATUS_AUTOSENSE_VALID));
        REQUIRE(scenario.srb.ScsiStatus == SCSISTAT_CHECK_CONDITION && scenario.calls.empty());
        REQUIRE(scenario.sense.SenseKey == SCSI_SENSE_ILLEGAL_REQUEST && scenario.sense.AdditionalSenseCode == SCSI_ADSENSE_ILLEGAL_BLOCK);
    }
    {
        Scenario scenario;
        scenario.lu.StopThread = 1;
        execute("admission during rundown", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == SRB_STATUS_NO_DEVICE && scenario.calls.empty());
    }
    {
        Scenario scenario;
        scenario.allowWork = false;
        execute("work item allocation failure", scenario, false);
        REQUIRE(scenario.srb.SrbStatus == (SRB_STATUS_ERROR | SRB_STATUS_AUTOSENSE_VALID));
        REQUIRE(scenario.srb.ScsiStatus == SCSISTAT_CHECK_CONDITION && scenario.calls.empty());
    }
    {
        Scenario scenario;
        scenario.lu.UseProxy = true;
        execute("proxy dispatch unchanged", scenario, true);
        verifySuccess(scenario, 0, 0);
        REQUIRE(scenario.proxyCalls == 1);
    }
    std::puts("PASS: 26 modeled UNMAP cases through actual admission, worker, sense and Storport completion");
}
