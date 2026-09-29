#!/usr/bin/env python3

"""Project one NetWasm dependency version into a disposable source tree."""

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


def require_version(value: object, description: str, allowed: set[str]) -> str:
    if not isinstance(value, str) or VERSION_PATTERN.fullmatch(value) is None:
        raise ValueError(f"{description} selects invalid version {value!r}")
    if value not in allowed:
        raise ValueError(f"{description} selects {value!r}, expected one of {sorted(allowed)!r}")
    return value


def validate_projection(source_root: Path) -> None:
    source_root = source_root.resolve()
    expected = str(read_dependencies(source_root)["netwasm"])
    allowed = {expected}

    props = (source_root / PROPS_PATH).read_text(encoding="utf-8")
    match = re.search(
        r"<NetWasmPackageVersion>([^<]+)</NetWasmPackageVersion>", props
    )
    if match is None:
        raise ValueError(f"Unable to read NetWasmPackageVersion from {PROPS_PATH}")
    require_version(match.group(1), str(PROPS_PATH), allowed)

    for relative_path in GLOBAL_JSON_PATHS:
        document = json.loads((source_root / relative_path).read_text(encoding="utf-8"))
        require_version(
            document.get("msbuild-sdks", {}).get("NetWasm.Sdk"),
            str(relative_path),
            allowed,
        )

    manifest = json.loads(
        (source_root / RELEASE_MANIFEST_PATH).read_text(encoding="utf-8")
    )
    versions = manifest.get("dependencyVersions")
    if not isinstance(versions, dict):
        raise ValueError("Release manifest has no dependencyVersions object.")
    for package in ("NetWasm.Sdk", "NetWasm.Testing.VSTest"):
        require_version(versions.get(package), f"{RELEASE_MANIFEST_PATH}:{package}", allowed)


def project(source_root: Path, candidate: str, receipt_path: Path) -> dict[str, object]:
    if not VERSION_PATTERN.fullmatch(candidate):
        raise ValueError(f"Invalid NetWasm candidate version: {candidate}")

    source_root = source_root.resolve()
    dependencies = read_dependencies(source_root)
    source_version = str(dependencies["netwasm"])
    allowed = {source_version, candidate}
    changed_files: list[str] = []

    dependencies["netwasm"] = candidate
    dependency_path = source_root / DEPENDENCIES_PATH
    if source_version != candidate:
        dependency_path.write_text(json.dumps(dependencies, indent=2) + "\n", encoding="utf-8")
        changed_files.append(DEPENDENCIES_PATH.as_posix())

    props_path = source_root / PROPS_PATH
    props = props_path.read_text(encoding="utf-8")
    match = re.search(
        r"(<NetWasmPackageVersion>)([^<]+)(</NetWasmPackageVersion>)", props
    )
    if match is None:
        raise ValueError(f"Unable to read NetWasmPackageVersion from {PROPS_PATH}")
    configured = require_version(match.group(2), str(PROPS_PATH), allowed)
    if configured != candidate:
        props_path.write_text(
            props[: match.start(2)] + candidate + props[match.end(2) :],
            encoding="utf-8",
        )
        changed_files.append(PROPS_PATH.as_posix())

    for relative_path in GLOBAL_JSON_PATHS:
        path = source_root / relative_path
        document = json.loads(path.read_text(encoding="utf-8"))
        sdk_version = document.get("msbuild-sdks", {}).get("NetWasm.Sdk")
        require_version(sdk_version, str(relative_path), allowed)
        if sdk_version != candidate:
            document["msbuild-sdks"]["NetWasm.Sdk"] = candidate
            path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
            changed_files.append(relative_path.as_posix())

    manifest_path = source_root / RELEASE_MANIFEST_PATH
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    versions = manifest.get("dependencyVersions")
    if not isinstance(versions, dict):
        raise ValueError("Release manifest has no dependencyVersions object.")
    for package in ("NetWasm.Sdk", "NetWasm.Testing.VSTest"):
        require_version(versions.get(package), f"{RELEASE_MANIFEST_PATH}:{package}", allowed)
    if any(versions[package] != candidate for package in ("NetWasm.Sdk", "NetWasm.Testing.VSTest")):
        versions["NetWasm.Sdk"] = candidate
        versions["NetWasm.Testing.VSTest"] = candidate
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        changed_files.append(RELEASE_MANIFEST_PATH.as_posix())

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
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--version")
    parser.add_argument("--receipt", type=Path)
    arguments = parser.parse_args()
    if arguments.check:
        if arguments.version is not None or arguments.receipt is not None:
            parser.error("--check cannot be combined with --version or --receipt")
        validate_projection(arguments.source_root)
        print("NetWasm dependency projections match eng/dependencies.json.")
        return 0
    if arguments.version is None or arguments.receipt is None:
        parser.error("--version and --receipt are required unless --check is used")
    receipt = project(arguments.source_root, arguments.version, arguments.receipt)
    print(
        f"Projected NetWasm {receipt['sourceVersion']} to "
        f"{receipt['candidateVersion']} in {len(receipt['changedFiles'])} files."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
