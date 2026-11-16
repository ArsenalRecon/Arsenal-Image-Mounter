# AIM C3a UNMAP evidence replay

Replay a finalized trace of the compile-time diagnostic provider with Python
3.9 or later. The decoder reads files and does not issue Windows or driver
operations.

```powershell
python tools/c3a/decode_unmap.py --scope scope.json selected-events.ndjson
python -B -m unittest discover -s tools/c3a -p 'test_*.py' -v
```

The input uses the C3a Windows collector's selected-event NDJSON shape. Select
all events from provider GUID `7dd9310b-1d36-4568-adc2-e6c006d7669d`. The
TraceLogging payload's `Kind` distinguishes events, so an event ID filter
would be inappropriate. Preserve the original ETL, native event XML, timing
metadata, and loss and finalization receipts alongside the projection.

Each input row contains `ProviderGuid` and the named native payload in
`Fields`. Optional `EventName` is checked against `Kind`. Numeric and
Boolean fields may use native XML text. Binary fields use contiguous hex
without case restrictions and may have an optional `0x` prefix. Preserve empty
binary fields as empty strings. Projection errors, missing field decoding and
rows skipped by bounds count as omissions. The first real capture must
validate the native TraceLogging field projection, including empty and
nonempty binary fields. The included tests replay modeled records.

Fill `scope.json` from the owner run configuration, independent actual
attachment inventory, and trace receipts:

```json
{
  "run_id": "e4fbbbee-8c77-42a7-9836-aa35449375d2",
  "unit": 1,
  "backing": "\\??\\D:\\Preview\\fixture.vhd",
  "unique_id": "41494d4635ed5ee23112000000000000",
  "capture": {
    "finalized": true,
    "events_lost": 0,
    "log_buffers_lost": 0,
    "realtime_buffers_lost": 0,
    "file_header_events_lost": 0,
    "file_header_buffers_lost": 0,
    "decoder_omissions": 0
  }
}
```

Use the exact packed AIM unit and the independently recorded 16-byte AIM disk
identifier. When AIM generates an identifier prefixed with `AIMF`, the next
four and eight bytes encode the volume serial and internal file index in
little-endian order. Verify the fixture and its identity during attachment.
The scope values above are examples. Use actual, known loss counters from the
session and ETL header. Keep the diagnostic provider enabled through the
observed AIM detach to capture the ending record.

The decoder independently reads SCSI big-endian descriptors and little-endian
ZERO byte ranges, checks the bound sector size and image offset, and accounts
for request and stream order, incomplete payloads and trace loss. Lower native
return, established terminal status and actual outgoing SRB/SCSI response are
reported separately. An observed malformed request or lower failure can have
complete evidence. Exit code 0 denotes complete evidence for the selected
scope, 1 denotes incomplete evidence, and 2 denotes invalid input. A complete
trace still requires independent integrity and workload verification. The
evidence scope ends at the AIM UNMAP-to-ZERO boundary.

## Offline checks

From an x64 Visual Studio developer shell, run the decoder and the comparison
of the original and instrumented AIM admission and worker functions.

```powershell
python -B -m unittest discover -s tools/c3a -p test_decode_unmap.py
python -B tools/c3a/check_unmap_equivalence.py
```

The emitter test compiles the actual recorder with modeled WDK and ETW APIs.
Pass the installed x64 developer environment script to initialize MSVC.

```powershell
python -B tools/c3a/test_native_emitter.py --vcvars64 $VcVars64
```

Use the pinned WDK build manifests for compiler and link results. A runtime
capture is still needed to verify that Windows projects all of the provider's
native binary fields into the decoder input.
