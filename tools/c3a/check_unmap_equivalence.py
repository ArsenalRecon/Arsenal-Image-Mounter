"""Compare actual AIM UNMAP admission/worker code with modeled OS calls."""
import argparse
from pathlib import Path
import subprocess
import tempfile


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
DRIVER = "Unmanaged Source/phdskmnt"
DESIGN_HEAD = "a887e8ddb897e74f7d31e6dc680be37306d0016c"
FUNCTIONS = (
    ("scsi.cpp", "ScsiOpUnmap", "ScsiOpPersistentReserveInOut"),
    ("workerthread.cpp", "ImScsiDispatchUnmapDevice", "ImScsiCreateLU"),
)


def source_functions(revision=None):
    for filename, begin, end in FUNCTIONS:
        path = f"{DRIVER}/{filename}"
        source = (subprocess.check_output(["git", "show", f"{revision}:{path}"],
                                          cwd=ROOT, text=True)
                  if revision else (ROOT / path).read_text(encoding="utf-8"))
        start = source.index(f"VOID\n{begin}(")
        yield source[start:source.index(f"VOID\n{end}(", start)]


def run(compiler, directory, functions, enabled=False):
    (directory / "unmap-under-test.inc").write_text("\n".join(functions), encoding="utf-8")
    binary = directory / "native_unmap.exe"
    args = [compiler]
    if Path(compiler).stem.lower() in ("cl", "clang-cl"):
        args += ["/nologo", "/std:c++14", "/EHsc", f"/I{directory}", f"/Fe{binary}"]
        if enabled:
            args.append("/DAIM_C3A_UNMAP_DIAGNOSTICS=1")
    else:
        args += ["-std=c++14", "-I", str(directory), "-o", str(binary)]
        if enabled:
            args.append("-DAIM_C3A_UNMAP_DIAGNOSTICS=1")
    args.append(str(HERE / "native_unmap_fixture.cpp"))
    subprocess.run(args, cwd=directory, check=True)
    return subprocess.check_output([str(binary)], cwd=directory)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", default="cl",
                        help="C++ compiler in its configured environment")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="aim-unmap-") as temp:
        directory = Path(temp)
        baseline = run(args.compiler, directory, source_functions(DESIGN_HEAD))
        disabled = run(args.compiler, directory, source_functions())
        enabled = run(args.compiler, directory, source_functions(), enabled=True)
    if len(baseline.splitlines()) != 14 or baseline != disabled or baseline != enabled:
        raise SystemExit("observed queue, lower I/O or outgoing SCSI results differ")
    print("PASS: 14 modeled UNMAP scenarios agree across design, default and experimental source")


if __name__ == "__main__":
    main()
