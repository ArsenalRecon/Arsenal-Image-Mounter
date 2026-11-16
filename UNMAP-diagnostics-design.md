# Experimental AIM UNMAP diagnostics for a VhdAccess test unit

This documents the experimental diagnostic provider for the mounted NTFS
discard investigation tracked in
[VhdAccess Rewrite PR #60](https://github.com/LTRData/VhdAccess-Rewrite/pull/60).
The original UNMAP control flow is pinned to AIM source
[`5e0605978b5cefd1dde341046577af446a9b2b86`](https://github.com/ArsenalRecon/Arsenal-Image-Mounter/tree/5e0605978b5cefd1dde341046577af446a9b2b86).

## Evidence gap

The release trace can expose a Storport UNMAP opcode and an IRP identity, and
Kernel-File can pair generic FSCTL events with their completion. The selected
normal Storport schemas omit the UNMAP data-out buffer, and the generic file
events omit the 16-byte ZERO input. The preparation trace in the VhdAccess
workload never reached controlled deletion or retrim. Its only UNMAP opcode
was on another SCSI address. The AIM provider captures the
actual command and worker inputs on the selected test unit. [VhdAccess trace
findings](https://github.com/LTRData/VhdAccess-Rewrite/blob/cb73b9e20a724cf90b6c5d68f4979c4f81dde656/docs/windows-filesystem-discard.md#native-trace-feasibility)

## Build and unit scope

- Compile a small, dedicated TraceLogging provider and its private state only
  when the opt-in build definition `AIM_C3A_UNMAP_DIAGNOSTICS` is present.
  Restrict the first profile to the Windows 10+ x64 Storport build. Generate
  an untracked build header with the run GUID, exact AIM unit number and
  exact canonical VhdAccess test backing name. The diagnostic profile
  requires all three values. The normal build omits the provider, private
  state and hooks. The separate unsigned builder is
  [tools/build-storport-diagnostics.py](tools/build-storport-diagnostics.py).
  It uses the VhdAccess WDK pins and leaves the existing AIM project,
  packaging and signing configuration intact.
- In direct-file LU initialization, before the existing initialized signal,
  compare the unit and existing `ObjectName` against the build
  header. Bind at most one matching synchronous, writable VhdAccess
  `ImageFile` per driver load. Allocate driver-local LU-generation and
  writer-generation IDs for this open and keep the already opened handle
  value private for comparison at dispatch. The bind record identifies
  the selected unit, the existing disk UniqueId, the pinned test route,
  image offset and logical block size. The owner independently binds
  this attach to its actual disk number, adapter, SCSI address and fixture
  file identity under the protected backing directory. The compiled name and
  runtime generation bind the existing open. A different file at that name
  before the initial open must be excluded by owner-side identity checks.
  If worker creation fails after the bind, a new attachment cannot reuse it.
- Preserve writer identity across the queue with the existing LU lifetime.
  A changed handle or reopened attachment cannot inherit the writer
  generation. Record the mismatch as incomplete diagnostic evidence and
  leave the existing I/O path to make its normal decision. No extra file
  open, object reference, control request or rundown wait is needed.
- Write only when the dedicated provider and keyword are enabled. On
  admission, use only fixed-size state, the live SRB bounds and resident
  buffers at a validated IRQL. Use no diagnostic queue, runtime lookup,
  allocation, wait or file operation in the request path. Use normal provider
  registration and unregister only after existing rundown. Mark an unsupported
  observation context as incomplete without touching the request.
- Cap each selected run at 64 UNMAPs and each complete parameter-list
  snapshot at 4,104 bytes, enough for the 8-byte header and 256 descriptors.
  Keep a count of omitted requests and dropped diagnostic records. Record
  original lengths and captured lengths, so an over-budget or partial
  snapshot stays explicit. Keep bound/unbound and diagnostic counters in
  the same capture. Require intact begin/end markers, no missing request
  stages and no ETW loss before making a completeness claim.
- Enable the provider before the selected attach and keep it enabled through
  LU cleanup. The driver emits a provider enable/disable epoch, record
  sequence, omissions and incomplete-request counts. ETW finalization and
  loss counts come from the collector. `RunEnd` delimits diagnostic requests.
  Detach and exclusive backing release require separate positive evidence.

The existing file handle is assigned during LU initialization. AIM can derive
its disk UniqueId from file metadata, so that ID alone cannot distinguish two
opens of the same file. The diagnostic generation supplies that distinction.
[AIM LU initialization](https://github.com/ArsenalRecon/Arsenal-Image-Mounter/blob/5e0605978b5cefd1dde341046577af446a9b2b86/Unmanaged%20Source/phdskmnt/iodisp.cpp#L2200-L2308) ·
[Disk UniqueId](https://github.com/ArsenalRecon/Arsenal-Image-Mounter/blob/5e0605978b5cefd1dde341046577af446a9b2b86/Unmanaged%20Source/phdskmnt/iodisp.cpp#L1030-L1102) ·
[Kernel TraceLogging](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/tracelogging-for-kernel-mode-drivers-and-components) ·
[Kernel ETW payload residency](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/nf-wdm-etwwrite)

## Hook placement and records

All records carry a schema version, the run GUID, the selected LU generation
and the writer generation where applicable. Set a monotonically increasing
request ID at selected UNMAP ingress. Preserve raw value, width and endianness
for each protocol field.

| Record | Placement and contents |
| --- | --- |
| UNMAP admission | In `ScsiOpUnmap`, record the address and request ID, original SRB CDB length and accessible CDB bytes, SRB transfer length, CDB parameter-list allocation length, parameter header lengths, safely captured parameter-list bytes, descriptor count and disposition. Decode each complete descriptor as a big-endian 64-bit LBA and 32-bit block count. Keep capture limits separate from admission logic. For truncated or malformed input retain raw lengths, parse validity and the actual early-return reason. Capture only resident bytes proved within the supplied buffer. |
| UNMAP dispatch | Store the ID and expected writer generation in the existing `MP_WorkRtnParms` before `ImScsiScheduleWorkItem` publishes it. Emit the admission decision before queueing. Propagate the same token into the opcode `SCSIOP_UNMAP` worker and record whether the actual destination still matches the bound writer. Do not touch the work item after handing it off. |
| ZERO begin | Immediately before the existing direct-file `ZwFsControlFile` call, record descriptor ordinal, LBA, block count, writer generation, control code `FSCTL_SET_ZERO_DATA` (`0x000980c8`) and the actual 16 bytes passed to the call. They encode two signed little-endian 64-bit byte offsets, with `BeyondFinalZero` exclusive. The worker's block shift and image offset are recorded at bind time. |
| ZERO result | Immediately after the same call, record descriptor ordinal and raw returned NTSTATUS. For a nonpending return from the existing synchronous writer operation, record the return as terminal. If a return is pending, mark the completion unobserved and neither read an unproved initialized I/O status block nor add a wait. On the existing error exit record the first failed ordinal and the skipped suffix count. Report declared descriptors and attempted descriptors separately. |
| Final response | Record the actual SRB/SCSI status, reported transfer length and valid sense fields at completion, separately from ZERO results. Record synchronous rejections at their existing return points and queued completions before `RequestComplete`. Retain the actual SCSI GOOD response when the current worker reports it after a lower error. |

The first design uses one bounded binary parameter-list event per request and
decodes its descriptors offline. If the CDB length, header, transfer length and
descriptor region disagree, report the disagreement independently of what AIM
decided. The worker uses its original control flow and the exact ZERO bytes
being submitted, regardless of how much input the recorder could capture.
The hooks for `SCSIOP_UNMAP` never run on the all-zero WRITE optimization.
Provisioning flags, error propagation, validation and completion behavior are
outside this diagnostic change. [UNMAP admission](https://github.com/ArsenalRecon/Arsenal-Image-Mounter/blob/5e0605978b5cefd1dde341046577af446a9b2b86/Unmanaged%20Source/phdskmnt/scsi.cpp#L1956-L2088) ·
[Worker dispatch and ZERO](https://github.com/ArsenalRecon/Arsenal-Image-Mounter/blob/5e0605978b5cefd1dde341046577af446a9b2b86/Unmanaged%20Source/phdskmnt/workerthread.cpp#L473-L623) ·
[RequestComplete](https://github.com/ArsenalRecon/Arsenal-Image-Mounter/blob/5e0605978b5cefd1dde341046577af446a9b2b86/Unmanaged%20Source/phdskmnt/workerthread.cpp#L169-L200) ·
[Separate WRITE zero optimization](https://github.com/ArsenalRecon/Arsenal-Image-Mounter/blob/5e0605978b5cefd1dde341046577af446a9b2b86/Unmanaged%20Source/phdskmnt/iodisp.cpp#L912-L952) ·
[I/O status block semantics](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ns-wdm-_io_status_block)

## Upper provenance and qualification

The provider's request identifier starts at AIM admission. A complete attribution
from NTFS requires a proven parent DSM to child SRB relation from the exact
Windows build. The registered ClassPnP WPP provider requires matching decode
metadata and an observed relation. Microsoft's ClassPnP sample leaves the
incoming DSM activity ID unused for this UNMAP work and creates a separate
child IRP. A shared activity value needs positively established parent and
child semantics. Device identity and timestamp proximity do not establish
the relation. If stronger attribution becomes necessary, first evaluate a
separately authorized observation of the DSM dispatch and child-SRB creation
boundary on the target. [Storport activity lookup](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/storport/nf-storport-storportgetactivityidsrb) ·
[ClassPnP WPP sample](https://github.com/microsoft/Windows-driver-samples/blob/d49bb7d72f3651952b2b6e4ad28d14fe2247eb69/storage/class/classpnp/src/utils.c#L3099-L3217) ·
[Child-IRP creation](https://github.com/microsoft/Windows-driver-samples/blob/d49bb7d72f3651952b2b6e4ad28d14fe2247eb69/storage/class/classpnp/src/class.c#L4139-L4246)

A separate outcome-based verdict can establish whether controlled NTFS deletion
or one separately marked optional retrim retires a previously mapped full
VHD block. Verify preserved neighboring data, terminal writer completion,
BAT/bitmap state, read-only reopen, backing release and restoration of the
PR #41 baseline. Report that result independently of AIM UNMAP-to-ZERO
provenance and any stronger NTFS-to-DSM-to-SRB join. Reuse the verified native
128 MiB sequential ETW collector and its independent stop mechanism. The
logging-only calibration enabled zero providers and did not exercise the file
cap under load. Verify the first finalized trace's native payload projection
and loss counts before relying on the decoder for a provenance verdict.
[Owner-runtime plan](https://github.com/LTRData/VhdAccess-Rewrite/blob/cb73b9e20a724cf90b6c5d68f4979c4f81dde656/docs/windows-filesystem-discard.md#focused-owner-runtime-plan)

## Offline validation

1. Confirm the ordinary driver builds with the diagnostic definition absent
   and the diagnostic profile requires an explicit unit/backing/run binding.
   A wrong unit, backing name or repeated attach produces no scoped request.
2. Decode one accepted multi-descriptor SRB and round-trip the raw command
   bytes. Check both endian boundaries, nonzero image offset and the exact
   16-byte lower input. Check unsupported routes and malformed or
   truncated headers separately from any original admission decision.
3. Inject a successful first ZERO and terminal lower failure at a later
   ordinal. Verify the failed ordinal, skipped suffix and original SCSI
   response independently. Repeat with a pending result, empty list, clipped
   capture, lost completion, reused attachment, and interleaved request IDs.
4. Verify that the all-zero WRITE uses its original route and emits no
   UNMAP records. Confirm that budget exhaustion, writer mismatch and provider
   failure omit or mark diagnostics while preserving original behavior.
5. Verify safe payload residency and IRQL at admission, worker and completion.
   Confirm that bounds and provider disablement cannot introduce a wait or
   another I/O. Use an offline decoder corpus for the exact published schema
   before attempting an owner-runtime trace.

The [decoder and host tests](tools/c3a/README.md) cover the published schema
and the actual AIM UNMAP and emitter functions with modeled OS calls. These
tests and the pinned WDK build do not exercise real Storport callbacks or
kernel-to-ETL delivery.
