"""Exercise the real UNMAP event emitter with modeled WDK and ETW operations."""

import argparse
import subprocess
import tempfile
from pathlib import Path


WDK = r"""
#pragma once
#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <cwchar>

using UCHAR = unsigned char;
using USHORT = unsigned short;
using ULONG = unsigned long;
using LONG = long;
using LONG64 = long long;
using ULONGLONG = unsigned long long;
using WCHAR = wchar_t;
using HANDLE = void*;
using NTSTATUS = LONG;
using PVOID = void*;
struct GUID { std::uint32_t first; std::uint16_t second, third; UCHAR rest[8]; };
using LPCGUID = const GUID*;
using PEVENT_FILTER_DESCRIPTOR = void*;
struct SENSE_DATA { UCHAR bytes[18]; };
struct SCSI_REQUEST_BLOCK {
    USHORT Length;
    UCHAR SrbStatus, ScsiStatus, CdbLength, SenseInfoBufferLength;
    ULONG DataTransferLength;
    void* DataBuffer;
    void* SenseInfoBuffer;
    UCHAR Cdb[16];
};
using PSCSI_REQUEST_BLOCK = SCSI_REQUEST_BLOCK*;
#include "unmapdiagnostics.h"
struct UNICODE_STRING { USHORT Length, MaximumLength; const WCHAR* Buffer; };
struct LARGE_INTEGER { LONG64 QuadPart; };
struct _HW_LU_EXTENSION {
    HANDLE ImageFile{};
    UNICODE_STRING ObjectName{};
    bool UseProxy{}, VMDisk{}, AWEAllocDisk{}, SharedImage{}, ReadOnly{};
    HANDLE WriteOverlay{};
    struct { ULONG LongNumber; } DeviceNumber{};
    UCHAR UniqueId[16]{};
    UCHAR BlockPower{9};
    LARGE_INTEGER ImageOffset{512};
    AimUnmapLu UnmapDiagnostic{};
};

constexpr ULONG MAXULONG = 0xffffffffUL;
constexpr int PASSIVE_LEVEL = 0, DISPATCH_LEVEL = 2;
constexpr NTSTATUS STATUS_PENDING = 0x103;
constexpr ULONG FSCTL_SET_ZERO_DATA = 0x980c8;
constexpr ULONG SRB_STATUS_AUTOSENSE_VALID = 0x80;
constexpr int STOR_MAP_NON_READ_WRITE_BUFFERS = 2;
constexpr int STORAGE_MAP_BUFFERS_SETTING = STOR_MAP_NON_READ_WRITE_BUFFERS;
constexpr bool FALSE = false;
inline int irql = PASSIVE_LEVEL;
inline int KeGetCurrentIrql() { return irql; }
inline LARGE_INTEGER KeQueryPerformanceCounter(void*) { return {10000}; }
inline bool NT_SUCCESS(NTSTATUS value) { return value >= 0; }
inline bool RtlEqualUnicodeString(const UNICODE_STRING* a, const UNICODE_STRING* b, bool)
{
    return a->Length == b->Length && memcmp(a->Buffer, b->Buffer, a->Length) == 0;
}
inline LONG InterlockedCompareExchange(volatile LONG* p, LONG value, LONG expected)
{
    LONG old = *p;
    if (old == expected) *p = value;
    return old;
}
inline LONG InterlockedExchange(volatile LONG* p, LONG value)
{
    LONG old = *p;
    *p = value;
    return old;
}
inline LONG64 InterlockedCompareExchange64(volatile LONG64* p, LONG64 value, LONG64 expected)
{
    LONG64 old = *p;
    if (old == expected) *p = value;
    return old;
}
inline LONG InterlockedIncrement(volatile LONG* p) { return ++*p; }
inline LONG64 InterlockedIncrement64(volatile LONG64* p) { return ++*p; }
template<class A, class B> auto min(A a, B b) { return a < b ? a : b; }
#define FIELD_OFFSET(type, member) offsetof(type, member)
#define NTAPI
#define RTL_CONSTANT_STRING(s) {sizeof(s)-sizeof(WCHAR), sizeof(s), s}
"""


ETW = r"""
#pragma once
#include <initializer_list>
#include <string>
#include <vector>

struct Field {
    const char* name;
    ULONGLONG number{};
    std::vector<UCHAR> bytes{};
};
inline Field binary(const char* name, const void* data, size_t length)
{
    auto first = static_cast<const UCHAR*>(data);
    return {name, length, {first, first + length}};
}
struct Event {
    std::string kind;
    std::vector<Field> fields;
    const Field& get(const char* name) const
    {
        for (const auto& field : fields)
            if (strcmp(name, field.name) == 0) return field;
        std::abort();
    }
};
inline std::vector<Event> events;
inline bool enabled;
using Callback = void(*)(LPCGUID, ULONG, UCHAR, ULONGLONG, ULONGLONG,
    PEVENT_FILTER_DESCRIPTOR, PVOID);
inline Callback enableCallback;
inline NTSTATUS TraceLoggingRegisterEx(int, Callback callback, void*)
{
    enableCallback = callback;
    return 0;
}
inline void TraceLoggingUnregister(int) { enableCallback = nullptr; }
inline bool TraceLoggingProviderEnabled(int, UCHAR, ULONGLONG) { return enabled; }
inline void setEnabled(bool value)
{
    enabled = value;
    enableCallback(nullptr, value ? 1 : 0, 4, 1, 0, nullptr, nullptr);
}
inline void writeEvent(const char* kind, std::initializer_list<Field> fields)
{
    events.push_back({kind, fields});
}
#define TRACELOGGING_DEFINE_PROVIDER(symbol, name, guid) constexpr int symbol = 1
#define TraceLoggingLevel(value) Field{"level", value}
#define TraceLoggingKeyword(value) Field{"keyword", value}
#define TraceLoggingUInt64(value, name) Field{name, static_cast<ULONGLONG>(value)}
#define TraceLoggingUInt32(value, name) TraceLoggingUInt64(value, name)
#define TraceLoggingInt64(value, name) TraceLoggingUInt64(value, name)
#define TraceLoggingBoolean(value, name) TraceLoggingUInt64(value, name)
#define TraceLoggingGuid(value, name) binary(name, &(value), sizeof(value))
#define TraceLoggingWideString(value, name) binary(name, value, std::wcslen(value) * sizeof(WCHAR))
#define TraceLoggingBinary(value, length, name) binary(name, value, length)
#define TraceLoggingWrite(provider, kind, ...) writeEvent(kind, {__VA_ARGS__})
"""


CONFIG = r"""
#define AIM_C3A_RUN_GUID {1, 2, 3, {4, 0, 0, 0, 0, 0, 0, 0}}
#define AIM_C3A_UNIT_NUMBER 1UL
#define AIM_C3A_BACKING_PATH L"\\??\\vhdaccess\\??\\D:\\C3a\\owned.vhd"
"""


TEST = r"""
#include <cassert>
#include <iostream>
#include "phdskmnt.h"
#include "aim-c3a-unmap-config.h"
#include "TraceLoggingProvider.h"

int main()
{
    const WCHAR backing[] = AIM_C3A_BACKING_PATH;
    const WCHAR wrong[] = L"wrong";
    HW_LU_EXTENSION lu;
    lu.ImageFile = reinterpret_cast<HANDLE>(8);
    lu.ObjectName = RTL_CONSTANT_STRING(backing);
    lu.DeviceNumber.LongNumber = 2;
    UCHAR payload[8192]{};
    SCSI_REQUEST_BLOCK srb{};
    srb.Length = sizeof(srb);
    srb.CdbLength = 10;
    srb.Cdb[0] = 0x42;
    srb.Cdb[8] = 40;
    srb.DataTransferLength = 40;
    srb.DataBuffer = payload;
    AimUnmapRegister();
    setEnabled(true);
    AimUnmapBind(&lu, true);
    assert(AimUnmapBegin(&lu, &srb).Id == 0 && events.empty());
    lu.DeviceNumber.LongNumber = AIM_C3A_UNIT_NUMBER;
    lu.ObjectName = RTL_CONSTANT_STRING(wrong);
    AimUnmapBind(&lu, true);
    assert(AimUnmapBegin(&lu, &srb).Id == 0);
    lu.ObjectName = RTL_CONSTANT_STRING(backing);
    AimUnmapBind(&lu, false);
    assert(AimUnmapBegin(&lu, &srb).Id == 0);
    AimUnmapBind(&lu, true);
    assert(events.size() == 1 && events[0].kind == "RunBegin");
    const auto generation = events[0].get("LuGeneration").number;
    auto first = AimUnmapBegin(&lu, &srb);
    auto second = AimUnmapBegin(&lu, &srb);
    assert(first.Id == 1 && second.Id == 2 && first.LuGeneration == generation);
    assert(events.back().get("AllocationLength").number == 40);
    assert(events.back().get("Parameters").bytes.size() == 40);

    AimUnmapAdmission(first, &srb, AimUnmapDisposition::Queued, 3);
    AimUnmapAdmission(second, &srb, AimUnmapDisposition::Queued, 1);
    AimUnmapDispatch(first, &lu, 3);
    AimUnmapDispatch(second, &lu, 1);
    const UCHAR zero[16] = {0, 2, 0, 0, 0, 0, 0, 0, 0, 4};
    AimUnmapZeroBegin(first, &lu, 0, 0, 1, FSCTL_SET_ZERO_DATA, zero, sizeof(zero));
    AimUnmapZeroResult(first, 0, 0);
    AimUnmapZeroBegin(first, &lu, 1, 1, 1, FSCTL_SET_ZERO_DATA, zero, sizeof(zero));
    assert(events.back().get("Input").bytes == std::vector<UCHAR>(zero, zero + sizeof(zero)));
    AimUnmapZeroResult(first, 1, static_cast<NTSTATUS>(0xc0000001UL));
    assert(events.back().get("Terminal").number == 1);
    assert(events.back().get("NativeStatus").number == 0xc0000001UL);
    srb.SrbStatus = 1;
    srb.ScsiStatus = 0;
    AimUnmapFinish(first, &srb);
    assert(events.back().get("FailedOrdinal").number == 1);
    assert(events.back().get("Skipped").number == 1);
    assert(events.back().get("ScsiStatus").number == 0);
    assert(events.back().get("Flags").number == 0);

    lu.ImageFile = reinterpret_cast<HANDLE>(12);
    AimUnmapZeroBegin(second, &lu, 0, 0, 1, FSCTL_SET_ZERO_DATA, zero, sizeof(zero));
    assert(events.back().get("Input").bytes.empty());
    AimUnmapZeroResult(second, 0, STATUS_PENDING);
    assert(events.back().get("Terminal").number == 0);
    AimUnmapFinish(second, &srb);
    assert(events.back().get("Flags").number == (4 | 8));
    lu.ImageFile = reinterpret_cast<HANDLE>(8);

    srb.Length = sizeof(srb.Length);
    srb.DataBuffer = reinterpret_cast<void*>(1);
    auto malformed = AimUnmapBegin(&lu, &srb);
    assert(events.back().get("Parameters").bytes.empty());
    assert(events.back().get("Flags").number == 2);
    AimUnmapAdmission(malformed, &srb, AimUnmapDisposition::TransferLength, 0);
    AimUnmapFinish(malformed, &srb);
    srb.Length = sizeof(srb);
    srb.DataBuffer = payload;
    srb.DataTransferLength = sizeof(payload);
    auto capped = AimUnmapBegin(&lu, &srb);
    assert(events.back().get("Parameters").bytes.size() == 4104);
    assert(events.back().get("ParameterLength").number == sizeof(payload));
    AimUnmapAdmission(capped, &srb, AimUnmapDisposition::Queued, 257);
    AimUnmapDispatch(capped, &lu, 257);
    const auto count = events.size();
    AimUnmapZeroBegin(capped, &lu, 256, 256, 1, FSCTL_SET_ZERO_DATA, zero, sizeof(zero));
    AimUnmapZeroResult(capped, 256, 0);
    assert(events.size() == count);
    AimUnmapFinish(capped, &srb);
    assert(events.back().get("Flags").number == (1 | 16));

    srb.DataTransferLength = 40;
    setEnabled(false);
    irql = DISPATCH_LEVEL + 1;
    srb.DataBuffer = reinterpret_cast<void*>(1);
    auto unavailable = AimUnmapBegin(&lu, &srb);
    assert(unavailable.Id == 5 && events.back().kind == "UnmapFinal");
    irql = PASSIVE_LEVEL;
    setEnabled(true);
    AimUnmapAdmission(unavailable, &srb, AimUnmapDisposition::Unsupported, 0);
    AimUnmapFinish(unavailable, &srb);
    assert(events.back().get("Flags").number == 32);
    srb.DataBuffer = payload;
    for (ULONGLONG id = 6; id <= 64; ++id)
    {
        auto request = AimUnmapBegin(&lu, &srb);
        assert(request.Id == id);
        AimUnmapAdmission(request, &srb, AimUnmapDisposition::Unsupported, 0);
        AimUnmapFinish(request, &srb);
    }
    assert(AimUnmapBegin(&lu, &srb).Id == 0);
    HW_LU_EXTENSION secondAttach;
    secondAttach.ImageFile = lu.ImageFile;
    secondAttach.DeviceNumber = lu.DeviceNumber;
    secondAttach.ObjectName = lu.ObjectName;
    AimUnmapBind(&secondAttach, true);
    assert(AimUnmapBegin(&secondAttach, &srb).Id == 0);
    AimUnmapUnbind(&lu);
    assert(events.back().kind == "RunEnd");
    const auto& end = events.back();
    assert(end.get("LuGeneration").number == generation);
    assert(end.get("RequestsSeen").number == 65 && end.get("RequestsFinished").number == 64);
    assert(end.get("IncompleteRequests").number == 4);
    assert(end.get("RequestsOmitted").number == 1 && end.get("AdditionalBinds").number == 1);
    assert(end.get("SkippedRecords").number == 1 && end.get("ProviderEpoch").number == 3);
    assert(end.get("Sequence").number == events.size() + 1);
    AimUnmapUnregister();
    std::cout << "PASS real emitter: binding and generation, interleaving, lower error and "
                 "GOOD, pending, identity loss, malformed SRB bounds, omission and caps.\n";
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vcvars64", type=Path, help="Initialize MSVC when cl.exe is not in PATH")
    args = parser.parse_args()
    source = Path(__file__).resolve().parents[2] / "Unmanaged Source" / "phdskmnt"
    with tempfile.TemporaryDirectory(prefix="aim-c3a-emitter-") as work_name:
        work = Path(work_name)
        (work / "phdskmnt.h").write_text(WDK, encoding="utf-8")
        (work / "TraceLoggingProvider.h").write_text(ETW, encoding="utf-8")
        (work / "aim-c3a-unmap-config.h").write_text(CONFIG, encoding="utf-8")
        (work / "test.cpp").write_text(TEST, encoding="utf-8")
        command = [
            "cl.exe", "/nologo", "/std:c++17", "/EHsc", "/W4",
            "/DAIM_C3A_UNMAP_DIAGNOSTICS", "/DUSE_STORPORT", "/D_AMD64_",
            "/I" + str(work), "/I" + str(source / "inc"),
            str(source / "unmapdiagnostics.cpp"), "test.cpp", "/Fe:emitter-test.exe",
        ]
        if args.vcvars64:
            setup = str(args.vcvars64.resolve()).replace("%", "%%")
            compile_line = subprocess.list2cmdline(command).replace("%", "%%")
            (work / "compile.cmd").write_text(
                '@echo off\ncall "{}" >nul\nif errorlevel 1 exit /b %errorlevel%\n{}\n'.format(
                    setup, compile_line), encoding="ascii")
            command = ["cmd.exe", "/d", "/c", "compile.cmd"]
        subprocess.check_call(command, cwd=str(work))
        subprocess.check_call([str(work / "emitter-test.exe")], cwd=str(work))


if __name__ == "__main__":
    main()
