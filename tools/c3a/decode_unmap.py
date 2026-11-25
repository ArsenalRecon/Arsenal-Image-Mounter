#!/usr/bin/env python3
"""Independently replay the bytes and evidence from the AIM C3a UNMAP provider."""

import argparse
import json
import re
import struct
import sys
import uuid
from pathlib import Path


FSCTL_SET_ZERO_DATA = 0x000980C8
STATUS_PENDING = 0x103
PROVIDER_GUID = "7dd9310b-1d36-4568-adc2-e6c006d7669d"
MAX_PARAMETER_BYTES = 4104
MAX_REQUESTS = 64
MAX_CAPTURE_BYTES = 32 * 1024 * 1024

LOSS_FIELDS = (
    "events_lost", "log_buffers_lost", "realtime_buffers_lost",
    "file_header_events_lost", "file_header_buffers_lost", "decoder_omissions",
)

_HEX_BYTES = re.compile(r"(?:[0-9a-fA-F]{2})*\Z")
_INTEGER = re.compile(r"(?:-?(?:0|[1-9][0-9]*)|0[xX][0-9a-fA-F]+)\Z")


def integer(value, bit_width, name, *, signed=False):
    """Read numeric JSON or native XML text, rejecting bool and truncation."""
    if isinstance(value, str) and _INTEGER.fullmatch(value):
        value = int(value, 16 if value.lower().startswith("0x") else 10)
    lower = -(1 << (bit_width - 1)) if signed else 0
    if type(value) is not int or value < lower or value >= 1 << bit_width:
        raise ValueError(f"{name}: invalid {bit_width}-bit integer")
    return value & ((1 << bit_width) - 1)


def byte_string(value, maximum, name):
    """Accept hex string payloads from a lossless trace event projection."""
    if isinstance(value, str):
        value = value[2:] if value.startswith(("0x", "0X")) else value
        if len(value) <= 2 * maximum and _HEX_BYTES.fullmatch(value):
            return bytes.fromhex(value)
    raise ValueError(f"{name}: invalid hex payload or byte bound")


def guid(value, name):
    try:
        result = uuid.UUID(str(value))
    except ValueError as error:
        raise ValueError(f"{name}: invalid GUID") from error
    if not result.int:
        raise ValueError(f"{name}: zero GUID")
    return str(result)


def flg(value, name):
    if type(value) is bool:
        return value
    if value in ("true", "True"):
        return True
    if value in ("false", "False"):
        return False
    val = integer(value, 8, name)
    if val not in (0, 1):
        raise ValueError(f"{name}: invalid boolean")
    return bool(val)


def ntstatus(value):
    return integer(value, 32, "NTSTATUS", signed=True)


def unmap_wire(cdb, data, cdb_length, available_length):
    """Decode the SCSI fields without using AIM's admission verdict."""
    issues = []
    descriptors = []
    allocation = None
    data_length = None
    descriptor_length = None
    if cdb_length != 10:
        issues.append("unexpected UNMAP CDB length")
    if len(cdb) >= 9:
        if cdb[0] != 0x42:
            issues.append("unexpected SCSI opcode")
        allocation = int.from_bytes(cdb[7:9], "big")
        if allocation > available_length:
            issues.append("CDB allocation exceeds transfer buffer")
    if len(data) >= 2:
        data_length = int.from_bytes(data[0:2], "big")
        if data_length + 2 > available_length:
            issues.append("header data length exceeds transfer buffer")
    if len(data) >= 4:
        descriptor_length = int.from_bytes(data[2:4], "big")
        if descriptor_length % 16:
            issues.append("descriptor length has a partial descriptor")
        if descriptor_length + 8 > available_length:
            issues.append("descriptor region exceeds transfer buffer")
        if data_length is not None and data_length < descriptor_length + 6:
            issues.append("descriptor region extends past declared list data")
    if available_length < 8 and (allocation or available_length):
        issues.append("parameter header is incomplete")
    if descriptor_length is not None and len(data) >= 8:
        count = min(descriptor_length, len(data) - 8) // 16
        for index in range(count):
            lba, blocks = struct.unpack_from(">QI", data, 8 + index * 16)
            descriptors.append({"ordinal": index, "lba": lba, "blocks": blocks})
    return {
        "allocation_length": allocation,
        "data_length": data_length,
        "descriptor_length": descriptor_length,
        "descriptor_count": None if descriptor_length is None else descriptor_length // 16,
        "descriptors": descriptors,
        "issues": issues,
    }


def zero_wire(buffer, lba, blocks, block_power, image_offset):
    if len(buffer) != 16:
        raise ValueError("ZERO input must contain exactly 16 bytes")
    start, end = struct.unpack("<qq", buffer)
    expected_start = (lba << block_power) + image_offset
    expected_end = expected_start + (blocks << block_power)
    return {
        "start": start,
        "end_exclusive": end,
        "matches_descriptor": (start, end) == (expected_start, expected_end),
    }


KIND = {
    1: "RunBegin", 2: "RunEnd", 3: "UnmapInput", 4: "UnmapAdmission",
    5: "UnmapDispatch", 6: "ZeroBegin", 7: "ZeroResult", 8: "UnmapFinal",
}
DISPOSITIONS = (
    "queued", "uninitialized", "shutting_down", "read_only", "unsupported",
    "transfer_length", "data_length", "descriptor_length", "lba_overflow",
    "disk_bounds", "no_work_item",
)
FLAGS = {
    0x01: "truncated raw payload",
    0x02: "unavailable raw payload",
    0x04: "writer identity mismatch",
    0x08: "lower completion uncertain",
    0x10: "descriptor budget exceeded",
    0x20: "diagnostic record omitted",
    0x40: "unexpected ZERO input",
    0x80: "incomplete sense bytes",
}
NO_ORDINAL = 0xFFFFFFFF
MAX_DESCRIPTORS = 256
MAX_EVENTS = 2 + MAX_REQUESTS * (4 + 2 * MAX_DESCRIPTORS)


def u32(fields, name):
    return integer(fields[name], 32, name)


def u64(fields, name):
    return integer(fields[name], 64, name)


def fields_with_flags(fields, concerns):
    flags = u32(fields, "Flags")
    for bit, meaning in FLAGS.items():
        if flags & bit:
            concerns.add(meaning)
    if flags & ~sum(FLAGS):
        concerns.add("unrecognized diagnostic flags")


def decode_input(fields, concerns):
    cdb = byte_string(fields["Cdb"], 16, "CDB")
    raw = byte_string(fields["Parameters"], MAX_PARAMETER_BYTES, "parameters")
    cdb_length = u32(fields, "CdbLength")
    parameter_length = u32(fields, "ParameterLength")
    if (
        len(cdb) != u32(fields, "CdbCaptured") or len(cdb) > cdb_length
        or len(raw) != u32(fields, "ParametersCaptured") or len(raw) > parameter_length
    ):
        concerns.add("captured lengths do not match the raw buffers")
    if cdb_length != len(cdb) or parameter_length != len(raw):
        concerns.add("original input is not fully captured")
    wire = unmap_wire(cdb, raw, cdb_length, parameter_length)
    allocation = u32(fields, "AllocationLength")
    if (None if allocation == NO_ORDINAL else allocation) != wire["allocation_length"]:
        concerns.add("CDB allocation disagrees with the reported field")
    wire.update({
        "cdb_original_length": cdb_length,
        "parameter_original_length": parameter_length,
        "cdb_hex": cdb.hex(),
        "parameters_hex": raw.hex(),
    })
    return wire


def decode_final(fields, concerns):
    sense = byte_string(fields["Sense"], 252, "sense")
    length = u32(fields, "SenseLength")
    captured = u32(fields, "SenseCaptured")
    if captured != len(sense) or captured > length:
        concerns.add("captured sense length disagrees with raw sense")
    ordinal = u32(fields, "FailedOrdinal")
    return {
        "descriptors": u32(fields, "Descriptors"),
        "attempted": u32(fields, "Attempted"),
        "skipped": u32(fields, "Skipped"),
        "failed_ordinal": None if ordinal == NO_ORDINAL else ordinal,
        "srb_status": integer(fields["SrbStatus"], 8, "SrbStatus"),
        "scsi_status": integer(fields["ScsiStatus"], 8, "ScsiStatus"),
        "transfer_length": u32(fields, "TransferLength"),
        "sense_length": length,
        "sense_hex": sense.hex(),
    }


def decode_attempts(stages, wire, bound, concerns):
    """Pair only a stage-complete, ordinal-matching begin/result from one request."""
    operations = []
    current = None
    for kind, fields in stages:
        if kind == "ZeroBegin":
            if current is not None:
                concerns.add("ZERO result missing before another ZERO begins")
            ordinal = u32(fields, "Ordinal")
            lba = u64(fields, "Lba")
            blocks = u32(fields, "Blocks")
            control_code = u32(fields, "ControlCode")
            data = byte_string(fields["Input"], 16, "ZERO input")
            if ordinal >= MAX_DESCRIPTORS or ordinal != len(operations):
                concerns.add("ZERO attempt ordinal is not a bounded descriptor prefix")
            if (
                wire is None or ordinal >= len(wire["descriptors"])
                or wire["descriptors"][ordinal] != {"ordinal": ordinal, "lba": lba, "blocks": blocks}
            ):
                concerns.add("ZERO does not match the captured UNMAP descriptor")
            if control_code != FSCTL_SET_ZERO_DATA or len(data) != u32(fields, "InputLength"):
                concerns.add("lower input code or length is inconsistent")
            current = {"ordinal": ordinal, "lba": lba, "blocks": blocks,
                       "control_code": control_code, "input_hex": data.hex(),
                       "native_status": None, "terminal_status": None}
            if len(data) == 16:
                current.update(zero_wire(data, lba, blocks, bound["block_power"], bound["image_offset"]))
                if not current["matches_descriptor"]:
                    concerns.add("ZERO byte range disagrees with the bound address calculation")
            else:
                concerns.add("ZERO input is not exactly 16 bytes")
            operations.append(current)
        else:
            ordinal = u32(fields, "Ordinal")
            native = ntstatus(fields["NativeStatus"])
            terminal = flg(fields["Terminal"], "Terminal")
            if current is None or current["ordinal"] != ordinal:
                concerns.add("ZERO result lacks a matching active ordinal")
                continue
            current["native_status"] = native
            if terminal and native != STATUS_PENDING:
                current["terminal_status"] = native
            else:
                concerns.add("lower completion uncertain")
                if terminal and native == STATUS_PENDING:
                    concerns.add("pending native status cannot be terminal")
            current = None
    if current is not None:
        concerns.add("final ZERO result missing")
    return operations


def decode_request(identifier, events, bound):
    concerns = set()
    events.sort(key=lambda item: item["order"])
    order = [item["order"] for item in events]
    sequences = [item["sequence"] for item in events]
    if order != list(range(1, len(order) + 1)):
        concerns.add("request-local event order missing or repeated")
    if any(left >= right for left, right in zip(sequences, sequences[1:])):
        concerns.add("request-local emitted sequence is out of order")
    stages = [(item["event"], item["fields"]) for item in events]
    for kind, fields in stages:
        if kind in ("UnmapInput", "UnmapAdmission", "UnmapDispatch", "UnmapFinal"):
            fields_with_flags(fields, concerns)
    wire = None
    disposition = None
    count = None
    worker_descriptors = None
    final = None
    kinds = [kind for kind, _ in stages]
    if kinds[:2] != ["UnmapInput", "UnmapAdmission"] or kinds[-1:] != ["UnmapFinal"]:
        concerns.add("missing admission or final request stages")
    singleton_kinds = ("UnmapInput", "UnmapAdmission", "UnmapDispatch", "UnmapFinal")
    for kind in singleton_kinds:
        if kinds.count(kind) > 1:
            concerns.add(f"repeated {kind} stage")
    for kind, fields in stages:
        if kind == "UnmapInput" and wire is None:
            wire = decode_input(fields, concerns)
        elif kind == "UnmapAdmission" and disposition is None:
            reason = u32(fields, "Disposition")
            if reason >= len(DISPOSITIONS):
                raise ValueError(f"unknown admission disposition {reason}")
            disposition = DISPOSITIONS[reason]
            count = u32(fields, "Descriptors")
        elif kind == "UnmapDispatch" and worker_descriptors is None:
            worker_descriptors = u32(fields, "Descriptors")
        elif kind == "UnmapFinal" and final is None:
            final = decode_final(fields, concerns)
    zero_stages = [stage for stage in stages if stage[0] in ("ZeroBegin", "ZeroResult")]
    lower = decode_attempts(zero_stages, wire, bound, concerns)
    if disposition == "queued":
        if wire and wire["descriptor_count"] != count:
            concerns.add("wire and admission descriptor counts disagree")
        if worker_descriptors is not None and worker_descriptors != count:
            concerns.add("admission and worker descriptor counts disagree")
        if zero_stages and kinds[2:3] != ["UnmapDispatch"]:
            concerns.add("ZERO activity lacks preceding worker dispatch identity")
        if kinds[2:3] == ["UnmapDispatch"] and any(kind not in ("ZeroBegin", "ZeroResult") for kind in kinds[3:-1]):
            concerns.add("unexpected stages between dispatch and response")
    elif worker_descriptors is not None or lower:
        concerns.add("worker activity follows an unqueued admission")
    failures = [
        row["ordinal"] for row in lower
        if row["native_status"] is not None and row["native_status"] & 0x80000000
    ]
    if final is not None:
        if count is not None and final["descriptors"] != count:
            concerns.add("final descriptor count differs from admission")
        if final["attempted"] != len(lower):
            concerns.add("final attempted count disagrees with recorded ZERO starts")
        if failures != ([] if final["failed_ordinal"] is None else [final["failed_ordinal"]]):
            concerns.add("native failure disagrees with reported failed ordinal")
        if final["attempted"] + final["skipped"] != final["descriptors"]:
            concerns.add("final attempted and skipped counts do not cover the descriptors")
        if disposition == "queued" and worker_descriptors is not None:
            if final["descriptors"] != worker_descriptors:
                concerns.add("final descriptor count differs from dispatch")
            if failures and final["attempted"] != failures[0] + 1:
                concerns.add("later descriptors appear attempted after terminal lower failure")
    return {
        "request_id": identifier,
        "event_sequence": sequences,
        "evidence_complete": not concerns,
        "evidence_limits": sorted(concerns),
        "wire": wire,
        "admission": disposition,
        "admission_descriptors": count,
        "worker_descriptors": worker_descriptors,
        "zero_attempts": lower,
        "response": final,
    }


def _selection(scope):
    run = guid(scope["run_id"], "expected run")
    unit = integer(scope["unit"], 32, "expected unit")
    path = scope["backing"]
    if type(path) is not str or not path or "\0" in path or len(path) > 1024:
        raise ValueError("expected exact backing path is invalid")
    unique_id = byte_string(scope["unique_id"], 16, "expected disk UniqueId")
    if len(unique_id) != 16 or not any(unique_id):
        raise ValueError("expected disk UniqueId is invalid")
    return run, unit, path, unique_id


def decode_capture(records, scope):
    """Check one bounded record projection using an independently pinned scope."""
    selected_run, selected_unit, backing, selected_unique_id = _selection(scope)
    concerns = set()
    seen = set()
    bindings = []
    endings = []
    requests = {}
    sequences = []
    for record_index, record in enumerate(records):
        if record_index >= MAX_EVENTS:
            raise ValueError("event count exceeds the diagnostic scope")
        if guid(record["ProviderGuid"], "provider GUID") != PROVIDER_GUID:
            raise ValueError("unexpected event provider")
        fields = record["Fields"]
        if u32(fields, "Schema") != 1:
            raise ValueError("unsupported event schema")
        kind = KIND.get(u32(fields, "Kind"))
        if kind is None:
            raise ValueError("unknown event kind")
        if record.get("EventName", kind) != kind:
            raise ValueError("event name and wire kind differ")
        if guid(fields["RunId"], "run ID") != selected_run:
            concerns.add("unexpected run identity")
            continue
        lu = u64(fields, "LuGeneration")
        writer = u64(fields, "WriterGeneration")
        request = u64(fields, "RequestId")
        sequence = u64(fields, "Sequence")
        order = u32(fields, "Order")
        if not lu or not writer or not sequence:
            concerns.add("missing generation or event sequence")
        if sequence in seen:
            concerns.add("duplicate emitted event sequence")
        seen.add(sequence)
        sequences.append(sequence)
        event = {
            "event": kind, "fields": fields, "order": order, "sequence": sequence,
            "generation": (lu, writer),
        }
        if kind in ("RunBegin", "RunEnd"):
            if request or order:
                concerns.add("request identity assigned to a run record")
            (bindings if kind == "RunBegin" else endings).append(event)
        else:
            if not 1 <= request <= MAX_REQUESTS or not order:
                concerns.add("out-of-scope request identity or missing request order")
            if request not in requests and len(requests) >= MAX_REQUESTS:
                raise ValueError("request count exceeds the run budget")
            requests.setdefault(request, []).append(event)

    # Native loss is separate from the provider's own counters. Both must be known.
    loss = scope["capture"]
    if not flg(loss["finalized"], "capture finalized"):
        concerns.add("ETL was not independently finalized")
    for name in LOSS_FIELDS:
        if integer(loss[name], 64, name):
            concerns.add(f"nonzero {name}")

    if len(bindings) != 1 or len(endings) != 1:
        concerns.add("trace lacks one unambiguous run begin and end")
    binding = None
    summary = None
    audit = []
    if len(bindings) == 1:
        start = bindings[0]
        fields = start["fields"]
        unit = u32(fields, "Unit")
        unique_id = byte_string(fields["UniqueId"], 16, "disk UniqueId")
        power = u32(fields, "BlockPower")
        offset = integer(fields["ImageOffset"], 64, "ImageOffset", signed=True)
        if offset >= 1 << 63:
            offset -= 1 << 64
        binding = {
            "unit": unit, "backing": fields["Backing"], "unique_id_hex": unique_id.hex(),
            "block_power": power, "image_offset": offset, "generation": start["generation"],
            "provider_epoch": u32(fields, "ProviderEpoch"),
        }
        identity_matches = (unit, fields["Backing"], unique_id) == (
            selected_unit, backing, selected_unique_id,
        )
        if not identity_matches:
            concerns.add("unit, disk ID or exact backing identity differs")
        if power >= 63 or len(unique_id) != 16 or not any(unique_id):
            concerns.add("invalid stored disk block size or identity")
        elif identity_matches:
            for identifier, trace in sorted(requests.items()):
                if any(event["generation"] != start["generation"] for event in trace):
                    concerns.add("request carries a different attach or writer generation")
                    continue
                audit.append(decode_request(identifier, trace, binding))
        if any(item["sequence"] <= start["sequence"] for trace in requests.values() for item in trace):
            concerns.add("request record precedes its binding")
    if len(endings) == 1:
        ending = endings[0]
        fields = ending["fields"]
        summary = {
            name: u32(fields, name) for name in (
                "RequestsSeen", "RequestsOmitted", "RequestsFinished",
                "IncompleteRequests", "SkippedRecords", "AdditionalBinds",
            )
        }
        if binding and ending["generation"] != binding["generation"]:
            concerns.add("run ended on a different attach or writer generation")
        if binding and u32(fields, "ProviderEpoch") != binding["provider_epoch"]:
            concerns.add("provider enable state changed during the selected run")
        if any(seq > ending["sequence"] for seq in sequences):
            concerns.add("trace extends past the run end")
        if binding and (bindings[0]["sequence"] != 1 or min(sequences) != 1
                        or len(seen) != ending["sequence"]):
            concerns.add("emitted sequence has missing records")
        if summary["RequestsSeen"] != len(requests) + summary["RequestsOmitted"]:
            concerns.add("run request total disagrees with observed request IDs")
        if set(requests) != set(range(1, len(requests) + 1)):
            concerns.add("observed request IDs are not the retained capture prefix")
        if summary["RequestsFinished"] != sum(item["response"] is not None for item in audit):
            concerns.add("run terminal total disagrees with observed responses")
        for name in ("RequestsOmitted", "IncompleteRequests", "SkippedRecords", "AdditionalBinds"):
            if summary[name]:
                concerns.add(f"nonzero provider {name}")
    if any(not row["evidence_complete"] for row in audit):
        concerns.add("one or more requests have incomplete evidence")
    return {
        "scope": "AIM UNMAP to ZERO only",
        "run_id": selected_run,
        "evidence_complete": not concerns,
        "evidence_limits": sorted(concerns),
        "binding": binding,
        "provider_counters": summary,
        "requests": audit,
    }


def unique_fields(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def read_projection(path):
    with path.open("rb") as source:
        consumed = 0
        while line := source.readline(128 * 1024 + 1):
            consumed += len(line)
            if consumed > MAX_CAPTURE_BYTES or len(line) > 128 * 1024:
                raise ValueError("event projection exceeds the replay size bound")
            yield json.loads(line, object_pairs_hook=unique_fields)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scope", type=Path, required=True, help="independent unit and capture receipt JSON")
    parser.add_argument("events", type=Path, help="lossless normalized provider event NDJSON")
    args = parser.parse_args()
    try:
        with args.scope.open("rb") as source:
            data = source.read(8193)
        if len(data) > 8192:
            raise ValueError("scope receipt exceeds the replay size bound")
        scope = json.loads(data, object_pairs_hook=unique_fields)
        result = decode_capture(read_projection(args.events), scope)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"UNMAP evidence input error: {error}", file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2))
    return 0 if result["evidence_complete"] else 1


if __name__ == "__main__":
    sys.exit(main())
