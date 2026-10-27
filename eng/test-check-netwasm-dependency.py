from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("check-netwasm-dependency.py")
SPEC = importlib.util.spec_from_file_location("netwasm_dependency_check", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class CheckNetWasmDependencyTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        props = self.root / MODULE.PROPS_PATH
        props.parent.mkdir(parents=True)
        props.write_text(
            "<Project><PropertyGroup><NetWasmPackageVersion>0.4.0</NetWasmPackageVersion>"
            "</PropertyGroup></Project>\n",
            encoding="utf-8",
        )
        dependencies = self.root / MODULE.DEPENDENCIES_PATH
        dependencies.parent.mkdir(parents=True, exist_ok=True)
        dependencies.write_text(
            json.dumps({"schemaVersion": 1, "netwasm": "0.4.0"}, indent=2) + "\n",
            encoding="utf-8",
        )
        for relative_path in MODULE.GLOBAL_JSON_PATHS:
            path = self.root / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                json.dumps({"msbuild-sdks": {"NetWasm.Sdk": "0.4.0"}}, indent=2) + "\n",
                encoding="utf-8",
            )
        self.write_manifest({"NetWasm.Sdk": "0.4.0", "NetWasm.Testing.VSTest": "0.4.0"})

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_manifest(self, versions: dict[str, str]) -> None:
        (self.root / MODULE.RELEASE_MANIFEST_PATH).write_text(
            json.dumps({"dependencyVersions": versions}, indent=2) + "\n",
            encoding="utf-8",
        )

    def test_accepts_pins_that_agree(self) -> None:
        self.assertEqual("0.4.0", MODULE.check(self.root))

    def test_rejects_disagreeing_props_version(self) -> None:
        (self.root / MODULE.PROPS_PATH).write_text(
            "<Project><PropertyGroup><NetWasmPackageVersion>0.3.0</NetWasmPackageVersion>"
            "</PropertyGroup></Project>\n",
            encoding="utf-8",
        )

        with self.assertRaisesRegex(ValueError, "Directory.Build.props selects '0.3.0'"):
            MODULE.check(self.root)

    def test_rejects_disagreeing_sdk_version(self) -> None:
        path = self.root / MODULE.GLOBAL_JSON_PATHS[1]
        path.write_text(
            json.dumps({"msbuild-sdks": {"NetWasm.Sdk": "0.3.0"}}, indent=2) + "\n",
            encoding="utf-8",
        )

        with self.assertRaisesRegex(ValueError, "global.json selects '0.3.0', expected '0.4.0'"):
            MODULE.check(self.root)

    def test_rejects_disagreeing_manifest_version(self) -> None:
        self.write_manifest({"NetWasm.Sdk": "0.4.0", "NetWasm.Testing.VSTest": "0.3.0"})

        with self.assertRaisesRegex(ValueError, "NetWasm.Testing.VSTest selects '0.3.0'"):
            MODULE.check(self.root)

    def test_rejects_noncanonical_dependency(self) -> None:
        (self.root / MODULE.DEPENDENCIES_PATH).write_text(
            json.dumps({"schemaVersion": 1, "netwasm": "0.4"}) + "\n",
            encoding="utf-8",
        )

        with self.assertRaisesRegex(ValueError, "Invalid dependency manifest"):
            MODULE.check(self.root)


if __name__ == "__main__":
    unittest.main()
