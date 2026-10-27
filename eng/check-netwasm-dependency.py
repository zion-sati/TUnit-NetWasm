#!/usr/bin/env python3

"""Check that every NetWasm pin in the source tree names the version in eng/dependencies.json."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


VERSION_PATTERN = re.compile(
    r"^[0-9]+\.[0-9]+\.[0-9]+"
    r"(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$"
)
DEPENDENCIES_PATH = Path("eng/dependencies.json")
PROPS_PATH = Path("packaging/Directory.Build.props")
GLOBAL_JSON_PATHS = (
    Path("packaging/global.json"),
    Path("packaging/NetWasm.TUnit.Templates/content/NetWasmTUnitTests/global.json"),
)
RELEASE_MANIFEST_PATH = Path("eng/release-manifest.json")


def read_dependencies(root: Path) -> dict[str, object]:
    value = json.loads((root / DEPENDENCIES_PATH).read_text(encoding="utf-8"))
    if (
        not isinstance(value, dict)
        or value.get("schemaVersion") != 1
        or set(value) != {"schemaVersion", "netwasm"}
        or not isinstance(value.get("netwasm"), str)
        or VERSION_PATTERN.fullmatch(str(value["netwasm"])) is None
    ):
        raise ValueError(f"Invalid dependency manifest: {DEPENDENCIES_PATH}")
    return value


def require_version(value: object, description: str, expected: str) -> None:
    if value != expected:
        raise ValueError(f"{description} selects {value!r}, expected {expected!r}")


def check(source_root: Path) -> str:
    source_root = source_root.resolve()
    expected = str(read_dependencies(source_root)["netwasm"])

    props = (source_root / PROPS_PATH).read_text(encoding="utf-8")
    match = re.search(
        r"<NetWasmPackageVersion>([^<]+)</NetWasmPackageVersion>", props
    )
    if match is None:
        raise ValueError(f"Unable to read NetWasmPackageVersion from {PROPS_PATH}")
    require_version(match.group(1), str(PROPS_PATH), expected)

    for relative_path in GLOBAL_JSON_PATHS:
        document = json.loads((source_root / relative_path).read_text(encoding="utf-8"))
        require_version(
            document.get("msbuild-sdks", {}).get("NetWasm.Sdk"),
            str(relative_path),
            expected,
        )

    manifest = json.loads(
        (source_root / RELEASE_MANIFEST_PATH).read_text(encoding="utf-8")
    )
    versions = manifest.get("dependencyVersions")
    if not isinstance(versions, dict):
        raise ValueError("Release manifest has no dependencyVersions object.")
    for package in ("NetWasm.Sdk", "NetWasm.Testing.VSTest"):
        require_version(versions.get(package), f"{RELEASE_MANIFEST_PATH}:{package}", expected)
    return expected


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, required=True)
    arguments = parser.parse_args()
    version = check(arguments.source_root)
    print(f"Every NetWasm pin names {version}, as eng/dependencies.json does.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
