import copy
import json
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from decode_unmap import PROVIDER_GUID, decode_capture


RUN = "e4fbbbee-8c77-42a7-9836-aa35449375d2"
BACKING = r"\??\D:\Preview\fixture.vhd"
UNIQUE_ID = "41494d4635ed5ee23112000000000000"
REQUESTS = [(2048, 4097), (7168, 1024)]
SCOPE = {
    "run_id": RUN,
    "unit": 1,
    "backing": BACKING,
    "unique_id": UNIQUE_ID,
    "capture": {
        "finalized": True, "events_lost": 0, "log_buffers_lost": 0,
        "realtime_buffers_lost": 0, "file_header_events_lost": 0,
        "file_header_buffers_lost": 0, "decoder_omissions": 0,
    },
}


def event(kind, request, order, **fields):
    return {
        "ProviderGuid": PROVIDER_GUID,
        "Fields": {
            "Schema": 1, "Kind": kind, "RunId": RUN, "LuGeneration": 11,
            "WriterGeneration": 11, "RequestId": request, "Order": order,
            **fields,
        },
    }


def request(request_id, descriptors=REQUESTS, *, statuses=None, flags=0):
    parameter = (
        struct.pack(">HH4x", 6 + 16 * len(descriptors), 16 * len(descriptors))
        + b"".join(struct.pack(">QI4x", lba, count) for lba, count in descriptors)
    )
    cdb = struct.pack(">B6xH1x", 0x42, len(parameter))
    total = len(descriptors)
    stages = [
        event(3, request_id, 1, CdbLength=len(cdb), CdbCaptured=len(cdb),
              ParameterLength=len(parameter), ParametersCaptured=len(parameter),
              AllocationLength=len(parameter), Cdb=cdb.hex(), Parameters=parameter.hex(), Flags=flags),
        event(4, request_id, 2, Disposition=0, Descriptors=total, Flags=flags),
        event(5, request_id, 3, Descriptors=total, Flags=flags),
    ]
    statuses = [0] * total if statuses is None else statuses
    failure = 0xFFFFFFFF
    attempted = 0
    for ordinal, ((lba, count), status) in enumerate(zip(descriptors, statuses)):
        lower_payload = struct.pack("<qq", lba * 512, (lba + count) * 512)
        stages.append(event(6, request_id, len(stages) + 1, Ordinal=ordinal,
                            Lba=lba, Blocks=count, ControlCode=0x000980C8,
                            InputLength=16, Input=lower_payload.hex()))
        stages.append(event(7, request_id, len(stages) + 1, Ordinal=ordinal,
                            NativeStatus=status, Terminal=status != 0x103))
        attempted += 1
        if status & 0x80000000:
            failure = ordinal
            break
    stages.append(event(8, request_id, len(stages) + 1, Descriptors=total,
                        Attempted=attempted, Skipped=total - attempted, FailedOrdinal=failure,
                        SrbStatus=1, ScsiStatus=0, TransferLength=0, SenseLength=0,
                        SenseCaptured=0, Sense="", Flags=flags))
    return stages


def capture(stages, **counters):
    count = len({row["Fields"]["RequestId"] for row in stages})
    begin = event(1, 0, 0, Unit=1, Backing=BACKING, UniqueId=UNIQUE_ID,
                  BlockPower=9, ImageOffset=0, ProviderEpoch=1)
    ending = event(2, 0, 0, **{
        "RequestsSeen": count, "RequestsOmitted": 0, "RequestsFinished": count,
        "IncompleteRequests": 0, "SkippedRecords": 0, "AdditionalBinds": 0,
        "ProviderEpoch": 1, **counters,
    })
    records = copy.deepcopy([begin, *stages, ending])
    for seq, row in enumerate(records, 1):
        row["Fields"]["Sequence"] = seq
    return records


class DecodeUnmapTests(unittest.TestCase):
    def test_two_exact_descriptors_and_independently_decoded_lower_bytes(self):
        records = capture(request(1))
        original = copy.deepcopy(records)
        reviewed = decode_capture(records, SCOPE)
        self.assertEqual(records, original)
        self.assertTrue(reviewed["evidence_complete"])
        row = reviewed["requests"][0]
        self.assertEqual(row["wire"]["cdb_hex"], "42000000000000002800")
        self.assertEqual(row["wire"]["data_length"], 38)
        self.assertEqual(row["wire"]["descriptor_length"], 32)
        self.assertEqual(row["wire"]["issues"], [])
        zero = row["zero_attempts"]
        self.assertEqual([(part["start"], part["end_exclusive"]) for part in zero],
                         [(1048576, 3146240), (3670016, 4194304)])
        self.assertTrue(all(part["terminal_status"] == 0 for part in zero))
        self.assertEqual(row["response"]["srb_status"], 1)
        self.assertEqual(row["response"]["scsi_status"], 0)

    def test_second_lower_failure_preserved_under_scsi_good_with_skipped_suffix(self):
        stages = request(1, [*REQUESTS, (10000, 16)], statuses=[0, 0xC0000185, 0])
        reviewed = decode_capture(capture(stages), SCOPE)
        self.assertTrue(reviewed["evidence_complete"])
        row = reviewed["requests"][0]
        self.assertEqual(row["zero_attempts"][1]["terminal_status"], 0xC0000185)
        self.assertEqual(row["response"]["skipped"], 1)
        self.assertEqual(row["response"]["failed_ordinal"], 1)
        self.assertEqual(row["response"]["scsi_status"], 0)
        self.assertEqual(len(row["zero_attempts"]), 2)

    def test_pending_is_uncertain_even_if_next_descriptor_and_scsi_good_follow(self):
        stages = request(1, statuses=[0x103, 0], flags=8)
        reviewed = decode_capture(capture(stages, IncompleteRequests=1), SCOPE)
        self.assertFalse(reviewed["evidence_complete"])
        row = reviewed["requests"][0]
        self.assertIn("lower completion uncertain", row["evidence_limits"])
        self.assertEqual(row["zero_attempts"][0]["native_status"], 0x103)
        self.assertIsNone(row["zero_attempts"][0]["terminal_status"])
        self.assertEqual(row["zero_attempts"][1]["terminal_status"], 0)
        self.assertEqual(row["response"]["scsi_status"], 0)

    def test_rejects_pending_claimed_terminal_and_nonpending_uncertain(self):
        for status in (0x103, 0xC0000185):
            with self.subTest(status=status):
                stages = request(1, statuses=[status])
                stages[4]["Fields"]["Terminal"] = status == 0x103
                reviewed = decode_capture(capture(stages), SCOPE)
                self.assertFalse(reviewed["evidence_complete"])
                self.assertIsNone(reviewed["requests"][0]["zero_attempts"][0]["terminal_status"])

    def test_malformed_admission_is_observed_without_inventing_lower_operations(self):
        admitted = request(1)
        entered = admitted[0]["Fields"]
        entered["ParameterLength"] = 8
        entered["ParametersCaptured"] = 8
        entered["Parameters"] = "0006002000000000"
        final = admitted[-1]["Fields"]
        final.update(Descriptors=0, Attempted=0, SrbStatus=0x12, ScsiStatus=2,
                     SenseLength=0, SenseCaptured=0)
        stages = [admitted[0], event(4, 1, 2, Disposition=5, Descriptors=0, Flags=0),
                  event(8, 1, 3, **{k: final[k] for k in (
                      "Descriptors", "Attempted", "Skipped", "FailedOrdinal", "SrbStatus",
                      "ScsiStatus", "TransferLength", "SenseLength", "SenseCaptured", "Sense", "Flags",
                  )})]
        reviewed = decode_capture(capture(stages), SCOPE)
        self.assertTrue(reviewed["evidence_complete"])
        row = reviewed["requests"][0]
        self.assertEqual(row["admission"], "transfer_length")
        self.assertIn("CDB allocation exceeds transfer buffer", row["wire"]["issues"])
        self.assertIn("descriptor region exceeds transfer buffer", row["wire"]["issues"])
        self.assertEqual(row["zero_attempts"], [])
        self.assertEqual(row["response"]["scsi_status"], 2)

    def test_truncation_and_uncaptured_tail_do_not_become_complete(self):
        stages = request(1, flags=1)
        fields = stages[0]["Fields"]
        fields["Parameters"] = fields["Parameters"][:48]
        fields["ParametersCaptured"] = 24
        reviewed = decode_capture(capture(stages, IncompleteRequests=1), SCOPE)
        self.assertFalse(reviewed["evidence_complete"])
        row = reviewed["requests"][0]
        self.assertEqual(len(row["wire"]["descriptors"]), 1)
        self.assertIn("original input is not fully captured", row["evidence_limits"])
        self.assertEqual(row["response"]["scsi_status"], 0)

    def test_interleaved_requests_and_reverse_delivery_use_independent_request_order(self):
        alpha = request(1)
        beta = request(2, [(16384, 64), (32768, 16)])
        records = capture([row for pair in zip(alpha, beta) for row in pair])
        reviewed = decode_capture(list(reversed(records)), SCOPE)
        self.assertTrue(reviewed["evidence_complete"])
        self.assertEqual([row["zero_attempts"][0]["lba"] for row in reviewed["requests"]],
                         [2048, 16384])

    def test_wrong_unit_backing_run_generation_and_disk_id(self):
        original = capture(request(1))
        for key, wrong in (("Unit", 2), ("Backing", BACKING + "x"),
                           ("UniqueId", "ff" * 16), ("RunId", "ff000505-e4bc-4d71-a3fe-d2ca5229b03b")):
            with self.subTest(key=key):
                records = copy.deepcopy(original)
                records[0]["Fields"][key] = wrong
                reviewed = decode_capture(records, SCOPE)
                self.assertFalse(reviewed["evidence_complete"])
                self.assertEqual(reviewed["requests"], [])
        for key in ("LuGeneration", "WriterGeneration"):
            with self.subTest(key=key):
                records = copy.deepcopy(original)
                records[3]["Fields"][key] = 12
                reviewed = decode_capture(records, SCOPE)
                self.assertFalse(reviewed["evidence_complete"])
                self.assertEqual(reviewed["requests"], [])

    def test_identity_mismatch_or_altered_zero_range_blocks_provenance(self):
        for flags in (0, 4):
            with self.subTest(flags=flags):
                stages = request(1, flags=flags)
                stages[3]["Fields"]["Input"] = "00" * 16
                reviewed = decode_capture(capture(stages, IncompleteRequests=int(bool(flags))), SCOPE)
                self.assertFalse(reviewed["evidence_complete"])
                row = reviewed["requests"][0]
                self.assertIn("ZERO byte range disagrees with the bound address calculation", row["evidence_limits"])
                self.assertEqual(row["response"]["scsi_status"], 0)

    def test_missing_duplicate_and_reordered_stage_stay_incomplete(self):
        records = capture(request(1))
        revised = (records[:5] + records[6:], records + [records[4]])
        changed = copy.deepcopy(records)
        changed[4]["Fields"]["Order"], changed[5]["Fields"]["Order"] = 5, 4
        for variant in (*revised, changed):
            self.assertFalse(decode_capture(variant, SCOPE)["evidence_complete"])

    def test_trace_loss_and_budget_exhaustion_are_distinct_from_baseline_scsi_result(self):
        stages = request(1, flags=16)
        records = capture(stages, RequestsSeen=65, RequestsOmitted=64,
                          IncompleteRequests=1, SkippedRecords=2, ProviderEpoch=3)
        scope = copy.deepcopy(SCOPE)
        scope["capture"].update(events_lost=1, log_buffers_lost=2, realtime_buffers_lost=3,
                                file_header_events_lost=4, file_header_buffers_lost=5,
                                decoder_omissions=1, finalized=False)
        reviewed = decode_capture(records, scope)
        self.assertFalse(reviewed["evidence_complete"])
        self.assertIn("nonzero events_lost", reviewed["evidence_limits"])
        self.assertIn("nonzero provider RequestsOmitted", reviewed["evidence_limits"])
        self.assertIn("provider enable state changed during the selected run", reviewed["evidence_limits"])
        self.assertIn("descriptor budget exceeded", reviewed["requests"][0]["evidence_limits"])
        self.assertEqual(reviewed["requests"][0]["response"]["scsi_status"], 0)

    def test_ended_without_capture_scope_or_unbind_never_claims_complete(self):
        for records in ([], capture(request(1))[:-1]):
            self.assertFalse(decode_capture(records, SCOPE)["evidence_complete"])
        scope = copy.deepcopy(SCOPE)
        del scope["capture"]["events_lost"]
        with self.assertRaises(KeyError):
            decode_capture(capture([]), scope)

    def test_cli_and_duplicate_input_keys(self):
        with tempfile.TemporaryDirectory() as folder:
            parent = Path(folder)
            events = parent / "records.ndjson"
            scope = parent / "scope.json"
            scope.write_text(json.dumps(SCOPE), encoding="utf-8")
            events.write_text("\n".join(map(json.dumps, capture(request(1)))), encoding="utf-8")
            args = [sys.executable, str(Path(__file__).with_name("decode_unmap.py")),
                    "--scope", str(scope), str(events)]
            result = subprocess.run(args, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertTrue(json.loads(result.stdout)["evidence_complete"])
            events.write_text('{"Fields": {}, "Fields": {}}', encoding="utf-8")
            result = subprocess.run(args, capture_output=True, text=True)
            self.assertEqual(result.returncode, 2, result.stdout)
            self.assertIn("duplicate JSON field", result.stderr)


if __name__ == "__main__":
    unittest.main()
