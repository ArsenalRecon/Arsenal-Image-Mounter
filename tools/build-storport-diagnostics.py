"""Build unsigned x64 Storport phdskmnt with the VhdAccess pinned WDK tools."""
import argparse
import io
import json
from pathlib import Path
import re
import subprocess
import sys
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ("phdskmnt", "scsi", "utils", "iodisp", "workerthread",
           "srbioctl", "proxy", "debug")
PACKAGES = ("microsoft.windows.sdk.cpp", "microsoft.windows.sdk.cpp.x64",
            "microsoft.windows.wdk.x64")


def build_config(path, run_id, unit, backing):
    # The route is an NT object name and must be exactly the name AIM opens.
    prefix = "\\Device\\VhdAccess\\Writable\\??\\"
    if (not backing.startswith(prefix) or
            not re.fullmatch(r"[A-Z]:\\[^<>:\"/|?*]+", backing[len(prefix):], re.ASCII) or
            any(part in ("", ".", "..") or part.endswith((" ", "."))
                for part in backing[len(prefix) + 3:].split("\\")) or
            len(backing) > 240 or any(not 32 <= ord(c) < 127 for c in backing) or
            not 0 <= unit < 0xFFFFFF or run_id.int == 0):
        raise ValueError("require nonzero run ID, exact AIM unit and canonical local VhdAccess route")
    data1, data2, data3 = run_id.fields[:3]
    tails = ", ".join(f"0x{b:02x}" for b in run_id.bytes[8:])
    path.write_text(
        f"#define AIM_C3A_RUN_GUID {{ 0x{data1:08x}, 0x{data2:04x}, 0x{data3:04x}, {{ {tails} }} }}\n"
        f"#define AIM_C3A_UNIT_NUMBER 0x{unit:06x}UL\n"
        f"#define AIM_C3A_BACKING_PATH L{json.dumps(backing)}\n", encoding="ascii")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, help="exact AIM commit")
    parser.add_argument("--vhdaccess", type=Path, required=True,
                        help="VhdAccess Rewrite checkout with the pinned WDK builder")
    parser.add_argument("--package-cache", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--run-id", type=uuid.UUID)
    parser.add_argument("--unit", type=lambda value: int(value, 0))
    parser.add_argument("--backing", help=r"exact kernel route, e.g. \Device\VhdAccess\Writable\??\D:\C3a\fixture.vhd")
    args = parser.parse_args()
    if not re.fullmatch("[0-9a-f]{40}", args.source):
        parser.error("--source must be a full commit ID")
    binding = (args.run_id, args.unit, args.backing)
    diagnostic = all(value is not None for value in binding)
    if not diagnostic and any(value is not None for value in binding):
        parser.error("--run-id, --unit and --backing must be supplied together")

    wdk_tools = args.vhdaccess.resolve() / "tools"
    sys.path[:0] = [str(wdk_tools / "wdk"), str(wdk_tools / "driver")]
    import build_gate as gate
    from pe_identity import pe_identity, pdb_identity

    source = subprocess.check_output(
        ["git", "-C", str(ROOT), "rev-parse", args.source + "^{commit}"], text=True).strip()
    gate.require(source == args.source, "source must identify the exact selected commit")
    output = args.output.resolve()
    output.mkdir(parents=True)
    generated = output / "aim-c3a-unmap-config.h"
    if diagnostic:
        build_config(generated, *binding)
    snapshot = output / "source"
    source_dir = snapshot / "Unmanaged Source" / "phdskmnt"
    archive = subprocess.check_output(
        ["git", "-C", str(ROOT), "archive", "--format=zip", source, "Unmanaged Source/phdskmnt"])
    with zipfile.ZipFile(io.BytesIO(archive)) as files:
        files.extractall(snapshot)

    toolset, binaries, environment, versions = gate.discover("x64")
    build = gate.Build(output, binaries, environment)
    package_receipts = {}
    cache = args.package_cache.resolve()
    cache.mkdir(parents=True, exist_ok=True)
    for name in PACKAGES:
        package = gate.package(cache, name)
        gate.extract(package, output / "packages" / name, name)
        package_receipts[name] = {"version": gate.MANIFEST["package_version"], "sha256": gate.sha(package)}

    kit = gate.MANIFEST["kit_directory"]
    sdk = output / "packages" / PACKAGES[0] / "c"
    wdk = output / "packages" / PACKAGES[2] / "c"
    includes = [wdk / "Include" / kit / "km/crt", toolset / "include",
                wdk / "Include" / kit / "km", sdk / "Include" / kit / "shared"]
    library_directory = wdk / "Lib" / kit / "km/x64"
    libraries = [library_directory / name for name in
                 ("storport.lib", "hal.lib", "ntoskrnl.lib", "wdm.lib", "bufferoverflowfastfailk.lib")]
    resource_compiler = sdk / "bin" / kit / "x64/rc.exe"
    gate.require(all(path.exists() for path in includes + libraries + [resource_compiler]),
                 "missing pinned WDK or SDK input")
    objects_directory = output / "obj"
    objects_directory.mkdir()
    package_directory = output / "unsigned"
    package_directory.mkdir()

    flags = ["/nologo", "/c", "/TP", "/std:c++14", "/kernel", "/W4", "/WX", "/GS", "/Zl",
             "/O2", "/GL-", "/Gy", "/Zi", "/GR-", "/external:W0",
             # The baseline wkmem.hpp declares three legacy default-int operator! methods.
             "/wd4430",
             "/D_WIN64", "/D_AMD64_", "/D_WIN32_WINNT=0x0A00", "/DNTDDI_VERSION=0x0A000008",
             "/D_NT_TARGET_VERSION=0x0A00", "/DUSE_STORPORT=1", "/DMP_DrvInfo_Inline",
             "/I" + str(source_dir / "inc")]
    if diagnostic:
        flags += ["/DAIM_C3A_UNMAP_DIAGNOSTICS=1", "/I" + str(output)]
    flags += ["/external:I" + str(path) for path in includes]
    objects = []
    for name in SOURCES + (("unmapdiagnostics",) if diagnostic else ()):
        item = source_dir / (name + ".cpp")
        obj = objects_directory / (name + ".obj")
        dependency = objects_directory / (name + ".json")
        build.run("compile_" + name, binaries / "cl.exe", flags +
                  ["/Fo" + str(obj), "/Fd" + str(objects_directory / "compiler.pdb"),
                   "/sourceDependencies", dependency, item])
        resolved = json.loads(dependency.read_text(encoding="utf-8-sig"))["Data"]
        gate.require(Path(resolved["Source"]) == item, "compiler source path differs from the snapshot")
        allowed_includes = [source_dir, *includes]
        for included in map(Path, resolved["Includes"]):
            gate.require((diagnostic and included == generated) or
                         any(included.is_relative_to(root) for root in allowed_includes),
                         "compiler used an unpinned header: " + str(included))
        objects.append(obj)

    resource = objects_directory / "phdskmnt.res"
    build.run("resource", resource_compiler,
              ["/nologo", "/x", "/i", sdk / "Include" / kit / "um",
               "/i", sdk / "Include" / kit / "shared", "/i", source_dir, "/d", "_WIN32",
               "/fo", resource, source_dir / "phdskmnt.rc"])

    image = package_directory / "phdskmnt.sys"
    pdb = package_directory / "phdskmnt.pdb"
    map_file = output / "phdskmnt.map"
    build.run("link", binaries / "link.exe",
              ["/NOLOGO", "/WX", "/DRIVER:WDM", "/SUBSYSTEM:NATIVE,10.00", "/KERNEL",
               "/ENTRY:GsDriverEntry", "/NODEFAULTLIB", "/INCREMENTAL:NO", "/LTCG:OFF",
               "/OPT:REF", "/OPT:ICF", "/MANIFEST:NO", "/RELEASE", "/DEBUG:FULL",
               "/MACHINE:X64", "/INCLUDE:_load_config_used", "/OUT:" + str(image),
               "/PDB:" + str(pdb), "/MAP:" + str(map_file), *objects, resource, *libraries])
    build.run("image", binaries / "dumpbin.exe", ["/nologo", "/headers", "/imports", "/loadconfig", image])
    allowed_imports = {}
    for module, library in zip(("storport.sys", "hal.dll", "ntoskrnl.exe"), libraries):
        members = build.run(module + "_exports", binaries / "dumpbin.exe",
                            ["/nologo", "/linkermember:1", library])
        allowed_imports[module] = set(re.findall(r"\b__imp_([A-Za-z_][A-Za-z_0-9]*)", members))
        gate.require(allowed_imports[module], "missing pinned import inventory for " + module)
    image_bytes = image.read_bytes()
    audit = gate.pe(image_bytes, gate.MACHINES["x64"], allowed_imports)
    provider_name = b"ArsenalRecon.AIM.C3A.Unmap"
    map_text = map_file.read_text(encoding="utf-8")
    gate.require((provider_name in image_bytes) == diagnostic and
                 ("unmapdiagnostics.obj" in map_text) == diagnostic,
                 "linked diagnostic provider does not match selected profile")
    report = pe_identity(image_bytes)
    del report["noncertificate_bytes"]
    symbols = pdb_identity(pdb.read_bytes())
    gate.require(not report["certificate_bytes"] and report["codeview"] == symbols,
                 "expected unsigned PE with exactly matching PDB")
    gate.write_json(output / "build-manifest.json", {
        "source_commit": source,
        "source_files": {path.relative_to(snapshot).as_posix(): gate.sha(path)
                         for path in sorted(snapshot.rglob("*")) if path.is_file()},
        "profile": "c3a-unmap-diagnostic" if diagnostic else "ordinary",
        "architecture": "x64",
        "run_id": str(args.run_id) if diagnostic else None,
        "unit": args.unit,
        "backing": args.backing,
        "configuration_header_sha256": gate.sha(generated) if diagnostic else None,
        "packages": package_receipts,
        "toolchain": versions,
        "tools": {str(path): gate.sha(path) for path in
                  [Path(__file__), Path(gate.__file__), wdk_tools / "wdk/gate.json",
                   wdk_tools / "wdk/audit.py", wdk_tools / "driver/pe_identity.py",
                   *(binaries / name for name in ("cl.exe", "link.exe", "dumpbin.exe")),
                   resource_compiler]},
        "sys": {"bytes": image.stat().st_size, **report},
        "pdb": {"bytes": pdb.stat().st_size, "sha256": gate.sha(pdb), **symbols},
        "imports": audit["imports"],
        "runtime_qualified": False,
    })
    print(output / "build-manifest.json")


if __name__ == "__main__":
    main()
