#pragma once

#ifdef AIM_C3A_UNMAP_DIAGNOSTICS

typedef struct _HW_LU_EXTENSION HW_LU_EXTENSION;

// Published with the initialized LU. The handle is borrowed for comparison
// only. No diagnostic reference is acquired or transferred to a work item.
struct AimUnmapLu
{
    HANDLE ImageFileAtBind;
    ULONGLONG LuGeneration;
    ULONGLONG WriterGeneration;
    volatile LONG Published;
};

struct AimUnmapRequest
{
    ULONGLONG Id;
    ULONGLONG LuGeneration;
    ULONGLONG WriterGeneration;
    ULONG Order;
    ULONG Flags;
    ULONG Descriptors;
    ULONG Attempted;
    ULONG FailedOrdinal;
};

enum class AimUnmapDisposition : ULONG
{
    Queued,
    Uninitialized,
    ShuttingDown,
    ReadOnly,
    Unsupported,
    TransferLength,
    DataLength,
    DescriptorLength,
    LbaOverflow,
    DiskBounds,
    NoWorkItem
};

void AimUnmapRegister();
void AimUnmapUnregister();
void AimUnmapBind(HW_LU_EXTENSION* lu, bool synchronousFile);
void AimUnmapUnbind(HW_LU_EXTENSION* lu);
AimUnmapRequest AimUnmapBegin(HW_LU_EXTENSION* lu, PSCSI_REQUEST_BLOCK srb);
void AimUnmapAdmission(AimUnmapRequest& request, PSCSI_REQUEST_BLOCK srb,
    AimUnmapDisposition disposition, ULONG descriptors);
void AimUnmapDispatch(AimUnmapRequest& request, HW_LU_EXTENSION* lu, ULONG descriptors);
void AimUnmapZeroBegin(AimUnmapRequest& request, HW_LU_EXTENSION* lu,
    ULONG ordinal, ULONGLONG lba, ULONG blocks, ULONG controlCode,
    const void* input, ULONG inputLength);
void AimUnmapZeroResult(AimUnmapRequest& request, ULONG ordinal, NTSTATUS nativeStatus);
void AimUnmapFinish(AimUnmapRequest& request, PSCSI_REQUEST_BLOCK srb);

#endif
