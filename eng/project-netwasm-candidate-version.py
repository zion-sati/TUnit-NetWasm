#!/usr/bin/env python3

"""Project one immutable NetWasm candidate version into a disposable source tree."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


VERSION_PATTERN = re.compile(
    r"^[0-9]+\.[0-9]+\.[0-9]+"
    r"(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$"
)
PROPS_PATH = Path("packaging/Directory.Build.props")
GLOBAL_JSON_PATHS = (
    Path("packaging/global.json"),
    Path("packaging/NetWasm.TUnit.Templates/content/NetWasmTUnitTests/global.json"),
)


def project(source_root: Path, candidate: str, receipt_path: Path) -> dict[str, object]:
    if not VERSION_PATTERN.fullmatch(candidate):
        raise ValueError(f"Invalid NetWasm candidate version: {candidate}")

    source_root = source_root.resolve()
    props_path = source_root / PROPS_PATH
    props = props_path.read_text(encoding="utf-8")
    match = re.search(
        r"(<NetWasmPackageVersion>)([^<]+)(</NetWasmPackageVersion>)",
        props,
    )
    if match is None or not VERSION_PATTERN.fullmatch(match.group(2)):
        raise ValueError(f"Unable to read NetWasmPackageVersion from {PROPS_PATH}")
    configured_version = match.group(2)
    changed_files: list[str] = []
    if configured_version != candidate:
        props_path.write_text(
            props[: match.start(2)] + candidate + props[match.end(2) :],
            encoding="utf-8",
        )
        changed_files.append(PROPS_PATH.as_posix())

    source_version: str | None = None
    for relative_path in GLOBAL_JSON_PATHS:
        path = source_root / relative_path
        document = json.loads(path.read_text(encoding="utf-8"))
        sdk_version = document.get("msbuild-sdks", {}).get("NetWasm.Sdk")
        if not isinstance(sdk_version, str) or not VERSION_PATTERN.fullmatch(sdk_version):
            raise ValueError(
                f"{relative_path} selects invalid NetWasm.Sdk version {sdk_version!r}"
            )
        if source_version is None:
            source_version = sdk_version
        elif sdk_version != source_version:
            raise ValueError(
                f"{relative_path} selects {sdk_version!r}, expected {source_version!r}"
            )
        if sdk_version != candidate:
            document["msbuild-sdks"]["NetWasm.Sdk"] = candidate
            path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
            changed_files.append(relative_path.as_posix())

    assert source_version is not None

    receipt = {
        "schemaVersion": 1,
        "sourceVersion": source_version,
        "candidateVersion": candidate,
        "changedFiles": changed_files,
    }
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    arguments = parser.parse_args()
    receipt = project(arguments.source_root, arguments.version, arguments.receipt)
    print(
        f"Projected NetWasm {receipt['sourceVersion']} to "
        f"{receipt['candidateVersion']} in {len(receipt['changedFiles'])} files."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
