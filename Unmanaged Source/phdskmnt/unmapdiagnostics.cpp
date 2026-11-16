#ifdef AIM_C3A_UNMAP_DIAGNOSTICS

#include "phdskmnt.h"
#include "aim-c3a-unmap-config.h"
#include <TraceLoggingProvider.h>

#if !defined(_AMD64_) || !defined(USE_STORPORT)
#error AIM C3a UNMAP diagnostics require the x64 Storport build.
#endif

static_assert(STORAGE_MAP_BUFFERS_SETTING == STOR_MAP_NON_READ_WRITE_BUFFERS,
    "The UNMAP recorder relies on the Storport data-buffer mapping contract.");

TRACELOGGING_DEFINE_PROVIDER(
    AimUnmapProvider,
    "ArsenalRecon.AIM.C3A.Unmap",
    (0x7dd9310b, 0x1d36, 0x4568, 0xad, 0xc2, 0xe6, 0xc0, 0x06, 0xd7, 0x66, 0x9d));

namespace
{
    constexpr ULONG MaxRequests = 64;
    constexpr ULONG MaxDescriptors = 256;
    constexpr ULONG MaxParameters = 8 + 16 * MaxDescriptors;
    constexpr ULONG MaxSense = sizeof(SENSE_DATA);
    constexpr ULONG ZeroInputLength = 16;
    constexpr ULONG Missing = MAXULONG;
    const ULONG SrbHeaderLength = FIELD_OFFSET(SCSI_REQUEST_BLOCK, Cdb);
    constexpr ULONGLONG DiagnosticKeyword = 1;
    constexpr UCHAR DiagnosticLevel = 4; // Informational.
    // ETW enable-callback control codes as used by the WDK provider wrapper.
    constexpr ULONG DisableProvider = 0;
    constexpr ULONG EnableProvider = 1;

    constexpr ULONG RawTruncated = 1;
    constexpr ULONG RawUnavailable = 2;
    constexpr ULONG WriterMismatch = 4;
    constexpr ULONG CompletionUnknown = 8;
    constexpr ULONG DescriptorBudget = 16;
    constexpr ULONG RecordOmitted = 32;
    constexpr ULONG UnexpectedZeroInput = 64;
    constexpr ULONG SenseIncomplete = 128;

    const GUID RunId = AIM_C3A_RUN_GUID;
    const WCHAR BackingName[] = AIM_C3A_BACKING_PATH;
    const UCHAR EmptyBuffer = 0;

    volatile LONG BindingClaimed;
    volatile LONG RunBegan;
    volatile LONG RunEnded;
    volatile LONG ProviderEpoch;
    volatile LONG AdditionalBinds;
    volatile LONG RequestsFinished;
    volatile LONG IncompleteRequests;
    volatile LONG SkippedRecords;
    volatile LONG64 RequestsSeen;
    // Sequence 1 is reserved for RunBegin even when requests enter on
    // separate CPUs before the thread that emits the marker is scheduled.
    volatile LONG64 Sequence = 1;

    ULONG ReadCounter(volatile LONG& counter)
    {
        return static_cast<ULONG>(InterlockedCompareExchange(&counter, 0, 0));
    }

    ULONGLONG ReadCounter(volatile LONG64& counter)
    {
        return static_cast<ULONGLONG>(InterlockedCompareExchange64(&counter, 0, 0));
    }

    void NTAPI ProviderChanged(LPCGUID, ULONG code, UCHAR, ULONGLONG,
        ULONGLONG, PEVENT_FILTER_DESCRIPTOR, PVOID)
    {
        if (code == EnableProvider || code == DisableProvider)
        {
            InterlockedIncrement(&ProviderEpoch);
        }
    }

    bool CaptureAvailable()
    {
        return KeGetCurrentIrql() <= DISPATCH_LEVEL &&
            TraceLoggingProviderEnabled(AimUnmapProvider, DiagnosticLevel, DiagnosticKeyword);
    }

    bool SameWriter(const AimUnmapRequest& request, const HW_LU_EXTENSION* lu)
    {
        const auto& writer = lu->UnmapDiagnostic;
        return request.LuGeneration == writer.LuGeneration &&
            request.WriterGeneration == writer.WriterGeneration &&
            writer.ImageFileAtBind != nullptr && writer.ImageFileAtBind == lu->ImageFile &&
            !lu->UseProxy && !lu->VMDisk && !lu->AWEAllocDisk && !lu->SharedImage &&
            !lu->ReadOnly && lu->WriteOverlay == nullptr;
    }

    ULONGLONG NextRecord(AimUnmapRequest& request)
    {
        const auto sequence = static_cast<ULONGLONG>(InterlockedIncrement64(&Sequence));
        ++request.Order;
        if (!CaptureAvailable())
        {
            request.Flags |= RecordOmitted;
            InterlockedIncrement(&SkippedRecords);
            return 0;
        }
        return sequence;
    }

    // Event packing at DISPATCH_LEVEL uses only resident scalars, static
    // metadata and Storport-owned, mapped buffers within the live SRB bounds.
    #define AIM_UNMAP_EVENT(kind, lu, writer, request, order, sequence) \
        TraceLoggingLevel(DiagnosticLevel), \
        TraceLoggingKeyword(DiagnosticKeyword), \
        TraceLoggingUInt32(1, "Schema"), \
        TraceLoggingUInt32(kind, "Kind"), \
        TraceLoggingGuid(RunId, "RunId"), \
        TraceLoggingUInt64(lu, "LuGeneration"), \
        TraceLoggingUInt64(writer, "WriterGeneration"), \
        TraceLoggingUInt64(request, "RequestId"), \
        TraceLoggingUInt32(order, "Order"), \
        TraceLoggingUInt64(sequence, "Sequence")

    #define AIM_UNMAP_REQUEST_EVENT(kind, request, sequence) \
        AIM_UNMAP_EVENT(kind, (request).LuGeneration, (request).WriterGeneration, \
            (request).Id, (request).Order, sequence)

    void StartRun(const HW_LU_EXTENSION* lu)
    {
        if (!CaptureAvailable() || InterlockedCompareExchange(&RunBegan, 1, 0) != 0)
        {
            return;
        }

        const auto& writer = lu->UnmapDiagnostic;
        TraceLoggingWrite(AimUnmapProvider, "RunBegin",
            AIM_UNMAP_EVENT(1, writer.LuGeneration, writer.WriterGeneration, 0, 0, 1),
            TraceLoggingUInt32(lu->DeviceNumber.LongNumber, "Unit"),
            TraceLoggingWideString(BackingName, "Backing"),
            TraceLoggingBinary(lu->UniqueId, sizeof(lu->UniqueId), "UniqueId"),
            TraceLoggingUInt32(lu->BlockPower, "BlockPower"),
            TraceLoggingInt64(lu->ImageOffset.QuadPart, "ImageOffset"),
            TraceLoggingUInt32(ReadCounter(ProviderEpoch), "ProviderEpoch"));
    }
}

void AimUnmapRegister()
{
    // Provider failures affect evidence only and cannot fail driver load.
    (void)TraceLoggingRegisterEx(AimUnmapProvider, ProviderChanged, nullptr);
}

void AimUnmapUnregister()
{
    TraceLoggingUnregister(AimUnmapProvider);
}

void AimUnmapBind(HW_LU_EXTENSION* lu, bool synchronousFile)
{
    if (KeGetCurrentIrql() != PASSIVE_LEVEL ||
        lu->DeviceNumber.LongNumber != AIM_C3A_UNIT_NUMBER ||
        !synchronousFile || lu->ImageFile == nullptr || lu->ObjectName.Buffer == nullptr ||
        lu->UseProxy || lu->VMDisk || lu->AWEAllocDisk || lu->SharedImage ||
        lu->ReadOnly || lu->WriteOverlay != nullptr)
    {
        return;
    }

    const UNICODE_STRING approvedName = RTL_CONSTANT_STRING(BackingName);
    if (!RtlEqualUnicodeString(&lu->ObjectName, &approvedName, FALSE))
    {
        return;
    }
    if (InterlockedCompareExchange(&BindingClaimed, 1, 0) != 0)
    {
        InterlockedIncrement(&AdditionalBinds);
        return;
    }

    // The scope can be claimed once per driver load. The generation ids
    // disclose no addresses and cannot transfer to a later open at this unit.
    auto& writer = lu->UnmapDiagnostic;
    writer.LuGeneration = static_cast<ULONGLONG>(KeQueryPerformanceCounter(nullptr).QuadPart);
    writer.WriterGeneration = writer.LuGeneration;
    writer.ImageFileAtBind = lu->ImageFile;
    StartRun(lu);
    // The existing LU list can expose the LU before Initialized is signaled.
    // Publish the diagnostic fields together, including to rejected requests.
    InterlockedExchange(&writer.Published, 1);
}

void AimUnmapUnbind(HW_LU_EXTENSION* lu)
{
    auto& writer = lu->UnmapDiagnostic;
    if (ReadCounter(writer.Published) == 0 || ReadCounter(RunBegan) == 0 ||
        InterlockedCompareExchange(&RunEnded, 1, 0) != 0)
    {
        return;
    }

    const auto sequence = static_cast<ULONGLONG>(InterlockedIncrement64(&Sequence));
    if (!CaptureAvailable())
    {
        return;
    }

    const auto seen = ReadCounter(RequestsSeen);
    const auto captured = static_cast<ULONGLONG>(min(seen, MaxRequests));
    const auto finished = ReadCounter(RequestsFinished);
    const auto live = captured > finished ? captured - finished : 0;
    TraceLoggingWrite(AimUnmapProvider, "RunEnd",
        AIM_UNMAP_EVENT(2, writer.LuGeneration, writer.WriterGeneration, 0, 0, sequence),
        TraceLoggingUInt32(static_cast<ULONG>(min(seen, MAXULONG)), "RequestsSeen"),
        TraceLoggingUInt32(static_cast<ULONG>(min(seen - captured, MAXULONG)), "RequestsOmitted"),
        TraceLoggingUInt32(finished, "RequestsFinished"),
        TraceLoggingUInt32(ReadCounter(IncompleteRequests) + static_cast<ULONG>(live), "IncompleteRequests"),
        TraceLoggingUInt32(ReadCounter(SkippedRecords), "SkippedRecords"),
        TraceLoggingUInt32(ReadCounter(AdditionalBinds), "AdditionalBinds"),
        TraceLoggingUInt32(ReadCounter(ProviderEpoch), "ProviderEpoch"));
}

AimUnmapRequest AimUnmapBegin(HW_LU_EXTENSION* lu, PSCSI_REQUEST_BLOCK srb)
{
    AimUnmapRequest request{};
    auto& writer = lu->UnmapDiagnostic;
    if (ReadCounter(writer.Published) == 0)
    {
        return request;
    }

    const auto id = static_cast<ULONGLONG>(InterlockedIncrement64(&RequestsSeen));
    if (id > MaxRequests)
    {
        return request;
    }

    request.Id = id;
    request.LuGeneration = writer.LuGeneration;
    request.WriterGeneration = writer.WriterGeneration;
    request.FailedOrdinal = Missing;
    if (ReadCounter(RunEnded) != 0 || !SameWriter(request, lu))
    {
        request.Flags |= WriterMismatch;
    }

    StartRun(lu);
    const auto sequence = NextRecord(request);
    if (sequence == 0)
    {
        return request;
    }

    const bool hasHeader = srb->Length >= SrbHeaderLength;
    const ULONG cdbAvailable = hasHeader ? srb->Length - SrbHeaderLength : 0;
    const ULONG cdbLength = hasHeader ? srb->CdbLength : 0;
    const ULONG cdbCaptured = min(cdbLength, min(cdbAvailable, static_cast<ULONG>(sizeof(srb->Cdb))));
    const ULONG parameterLength = hasHeader ? srb->DataTransferLength : 0;
    const bool mapped = hasHeader && srb->DataBuffer != nullptr;
    const ULONG parametersCaptured = mapped ? min(parameterLength, MaxParameters) : 0;
    const ULONG allocationLength = cdbCaptured >= 9 ?
        (static_cast<ULONG>(srb->Cdb[7]) << 8) | srb->Cdb[8] : Missing;
    const auto parameters = mapped ? static_cast<const UCHAR*>(srb->DataBuffer) : &EmptyBuffer;
    if (cdbCaptured < cdbLength || parametersCaptured < parameterLength)
    {
        request.Flags |= RawTruncated;
    }
    if (!hasHeader || (!mapped && parameterLength != 0) || cdbAvailable == 0)
    {
        request.Flags |= RawUnavailable;
    }

    TraceLoggingWrite(AimUnmapProvider, "UnmapInput",
        AIM_UNMAP_REQUEST_EVENT(3, request, sequence),
        TraceLoggingUInt32(cdbLength, "CdbLength"),
        TraceLoggingUInt32(cdbCaptured, "CdbCaptured"),
        TraceLoggingUInt32(parameterLength, "ParameterLength"),
        TraceLoggingUInt32(parametersCaptured, "ParametersCaptured"),
        TraceLoggingUInt32(allocationLength, "AllocationLength"),
        TraceLoggingUInt32(request.Flags, "Flags"),
        TraceLoggingBinary(cdbCaptured != 0 ? srb->Cdb : &EmptyBuffer, cdbCaptured, "Cdb"),
        TraceLoggingBinary(parameters, parametersCaptured, "Parameters"));
    return request;
}

void AimUnmapAdmission(AimUnmapRequest& request, PSCSI_REQUEST_BLOCK,
    AimUnmapDisposition disposition, ULONG descriptors)
{
    if (request.Id == 0)
    {
        return;
    }
    request.Descriptors = descriptors;
    if (descriptors > MaxDescriptors)
    {
        request.Flags |= DescriptorBudget;
    }
    const auto sequence = NextRecord(request);
    if (sequence != 0)
    {
        TraceLoggingWrite(AimUnmapProvider, "UnmapAdmission",
            AIM_UNMAP_REQUEST_EVENT(4, request, sequence),
            TraceLoggingUInt32(static_cast<ULONG>(disposition), "Disposition"),
            TraceLoggingUInt32(descriptors, "Descriptors"),
            TraceLoggingUInt32(request.Flags, "Flags"));
    }
}

void AimUnmapDispatch(AimUnmapRequest& request, HW_LU_EXTENSION* lu, ULONG descriptors)
{
    if (request.Id == 0)
    {
        return;
    }
    if (!SameWriter(request, lu))
    {
        request.Flags |= WriterMismatch;
    }
    if (descriptors > MaxDescriptors)
    {
        request.Flags |= DescriptorBudget;
    }
    const auto sequence = NextRecord(request);
    if (sequence != 0)
    {
        TraceLoggingWrite(AimUnmapProvider, "UnmapDispatch",
            AIM_UNMAP_REQUEST_EVENT(5, request, sequence),
            TraceLoggingUInt32(descriptors, "Descriptors"),
            TraceLoggingUInt32(request.Flags, "Flags"));
    }
}

void AimUnmapZeroBegin(AimUnmapRequest& request, HW_LU_EXTENSION* lu,
    ULONG ordinal, ULONGLONG lba, ULONG blocks, ULONG controlCode,
    const void* input, ULONG inputLength)
{
    if (request.Id == 0)
    {
        return;
    }
    ++request.Attempted;
    if (!SameWriter(request, lu))
    {
        request.Flags |= WriterMismatch;
    }
    if (ordinal >= MaxDescriptors)
    {
        request.Flags |= DescriptorBudget;
        return;
    }
    const bool expectedInput = controlCode == FSCTL_SET_ZERO_DATA &&
        input != nullptr && inputLength == ZeroInputLength;
    if (!expectedInput)
    {
        request.Flags |= UnexpectedZeroInput;
    }
    const bool captureInput = expectedInput && !(request.Flags & WriterMismatch);
    const auto sequence = NextRecord(request);
    if (sequence != 0)
    {
        TraceLoggingWrite(AimUnmapProvider, "ZeroBegin",
            AIM_UNMAP_REQUEST_EVENT(6, request, sequence),
            TraceLoggingUInt32(ordinal, "Ordinal"),
            TraceLoggingUInt64(lba, "Lba"),
            TraceLoggingUInt32(blocks, "Blocks"),
            TraceLoggingUInt32(controlCode, "ControlCode"),
            TraceLoggingUInt32(inputLength, "InputLength"),
            TraceLoggingBinary(captureInput ? input : &EmptyBuffer,
                captureInput ? ZeroInputLength : 0, "Input"));
    }
}

void AimUnmapZeroResult(AimUnmapRequest& request, ULONG ordinal, NTSTATUS nativeStatus)
{
    if (request.Id == 0)
    {
        return;
    }
    const bool terminal = nativeStatus != STATUS_PENDING && !(request.Flags & WriterMismatch);
    if (!terminal)
    {
        request.Flags |= CompletionUnknown;
    }
    if (!NT_SUCCESS(nativeStatus) && request.FailedOrdinal == Missing)
    {
        request.FailedOrdinal = ordinal;
    }
    if (ordinal >= MaxDescriptors)
    {
        return;
    }
    const auto sequence = NextRecord(request);
    if (sequence != 0)
    {
        TraceLoggingWrite(AimUnmapProvider, "ZeroResult",
            AIM_UNMAP_REQUEST_EVENT(7, request, sequence),
            TraceLoggingUInt32(ordinal, "Ordinal"),
            TraceLoggingUInt32(static_cast<ULONG>(nativeStatus), "NativeStatus"),
            TraceLoggingBoolean(terminal, "Terminal"));
    }
}

void AimUnmapFinish(AimUnmapRequest& request, PSCSI_REQUEST_BLOCK srb)
{
    if (request.Id == 0)
    {
        return;
    }

    const auto sequence = NextRecord(request);
    if (sequence != 0)
    {
        const bool hasHeader = srb->Length >= SrbHeaderLength;
        const ULONG srbStatus = hasHeader ? srb->SrbStatus : Missing;
        const ULONG scsiStatus = hasHeader ? srb->ScsiStatus : Missing;
        const ULONG transferLength = hasHeader ? srb->DataTransferLength : Missing;
        const bool senseValid = hasHeader && (srbStatus & SRB_STATUS_AUTOSENSE_VALID) != 0;
        const ULONG senseLength = senseValid ? srb->SenseInfoBufferLength : 0;
        const ULONG senseCaptured = senseValid && srb->SenseInfoBuffer != nullptr ?
            min(senseLength, MaxSense) : 0;
        if (senseValid && senseCaptured != MaxSense)
        {
            request.Flags |= SenseIncomplete;
        }
        if (!hasHeader)
        {
            request.Flags |= RawUnavailable;
        }

        const ULONG skipped = request.Descriptors > request.Attempted ?
            request.Descriptors - request.Attempted : 0;
        TraceLoggingWrite(AimUnmapProvider, "UnmapFinal",
            AIM_UNMAP_REQUEST_EVENT(8, request, sequence),
            TraceLoggingUInt32(request.Descriptors, "Descriptors"),
            TraceLoggingUInt32(request.Attempted, "Attempted"),
            TraceLoggingUInt32(skipped, "Skipped"),
            TraceLoggingUInt32(request.FailedOrdinal, "FailedOrdinal"),
            TraceLoggingUInt32(srbStatus, "SrbStatus"),
            TraceLoggingUInt32(scsiStatus, "ScsiStatus"),
            TraceLoggingUInt32(transferLength, "TransferLength"),
            TraceLoggingUInt32(senseLength, "SenseLength"),
            TraceLoggingUInt32(senseCaptured, "SenseCaptured"),
            TraceLoggingBinary(senseCaptured != 0 ? srb->SenseInfoBuffer : &EmptyBuffer,
                senseCaptured, "Sense"),
            TraceLoggingUInt32(request.Flags, "Flags"));
    }
    if (request.Flags != 0)
    {
        InterlockedIncrement(&IncompleteRequests);
    }
    InterlockedIncrement(&RequestsFinished);
}

#endif
