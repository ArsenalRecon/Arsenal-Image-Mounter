"""Run the miniport UNMAP admission, worker and completion code with modeled OS calls."""

import argparse
from pathlib import Path
import subprocess
import tempfile


HERE = Path(__file__).resolve().parent
DRIVER = HERE.parents[1] / "Unmanaged Source" / "phdskmnt"


def extract(text, start, end):
    offset = text.index(start)
    return text[offset:text.index(end, offset)]


def source_under_test():
    header = (DRIVER / "inc/phdskmnt.h").read_text(encoding="utf-8-sig")
    scsi = (DRIVER / "scsi.cpp").read_text(encoding="utf-8-sig")
    worker = (DRIVER / "workerthread.cpp").read_text(encoding="utf-8-sig")
    return "\n".join((
        extract(header, "#define ScsiSetSuccess(", "#ifdef USE_SCSIPORT"),
        extract(scsi, "VOID\nScsiSetCheckCondition(", "UCHAR\nScsiResetLun("),
        extract(scsi, "VOID\nScsiOpUnmap(", "VOID\nScsiOpPersistentReserveInOut("),
        extract(worker, "VOID\nImScsiDispatchWork(", "VOID\nImScsiCreateLU("),
        extract(worker, "VOID\nImScsiWorkerThread(", "VOID\nImScsiDispatchReadWrite("),
    ))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", default="cl", help="C++ compiler in its configured environment")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="aim-unmap-test-") as temporary:
        root = Path(temporary)
        (root / "unmap-under-test.inc").write_text(source_under_test(), encoding="utf-8")
        binary = root / "test-unmap.exe"
        if Path(args.compiler).stem.lower() in ("cl", "clang-cl"):
            flags = ["/nologo", "/std:c++14", "/EHsc", f"/I{root}", f"/Fe{binary}"]
        else:
            flags = ["-std=c++14", "-I", str(root), "-o", str(binary)]
        subprocess.run([args.compiler, *flags, str(HERE / "unmap_fixture.cpp")], cwd=root, check=True)
        subprocess.run([str(binary)], cwd=root, check=True)


if __name__ == "__main__":
    main()
