from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("project-netwasm-candidate-version.py")
SPEC = importlib.util.spec_from_file_location("candidate_projection", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ProjectNetWasmCandidateVersionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        props = self.root / "packaging/Directory.Build.props"
        props.parent.mkdir(parents=True)
        props.write_text(
            "<Project><PropertyGroup><NetWasmPackageVersion>0.4.0</NetWasmPackageVersion>"
            "</PropertyGroup></Project>\n",
            encoding="utf-8",
        )
        for relative_path in MODULE.GLOBAL_JSON_PATHS:
            path = self.root / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                json.dumps({"msbuild-sdks": {"NetWasm.Sdk": "0.4.0"}}, indent=2) + "\n",
                encoding="utf-8",
            )

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_projects_all_candidate_inputs_and_writes_receipt(self) -> None:
        receipt_path = self.root / "receipt.json"

        receipt = MODULE.project(
            self.root,
            "0.4.0-local.20260921.17",
            receipt_path,
        )

        self.assertEqual("0.4.0", receipt["sourceVersion"])
        self.assertEqual(3, len(receipt["changedFiles"]))
        self.assertIn(
            "<NetWasmPackageVersion>0.4.0-local.20260921.17</NetWasmPackageVersion>",
            (self.root / MODULE.PROPS_PATH).read_text(encoding="utf-8"),
        )
        for relative_path in MODULE.GLOBAL_JSON_PATHS:
            document = json.loads((self.root / relative_path).read_text(encoding="utf-8"))
            self.assertEqual(
                "0.4.0-local.20260921.17",
                document["msbuild-sdks"]["NetWasm.Sdk"],
            )
        self.assertEqual(receipt, json.loads(receipt_path.read_text(encoding="utf-8")))

    def test_rejects_disagreeing_sdk_version(self) -> None:
        path = self.root / MODULE.GLOBAL_JSON_PATHS[1]
        path.write_text(
            json.dumps({"msbuild-sdks": {"NetWasm.Sdk": "0.3.0"}}, indent=2) + "\n",
            encoding="utf-8",
        )

        with self.assertRaisesRegex(ValueError, "expected '0.4.0'"):
            MODULE.project(self.root, "0.4.0-local.1", self.root / "receipt.json")

    def test_accepts_release_version_already_projected_to_candidate(self) -> None:
        props = self.root / MODULE.PROPS_PATH
        props.write_text(
            "<Project><PropertyGroup>"
            "<NetWasmPackageVersion>0.4.0-ci.123.1</NetWasmPackageVersion>"
            "</PropertyGroup></Project>\n",
            encoding="utf-8",
        )

        receipt = MODULE.project(
            self.root,
            "0.4.0-ci.123.1",
            self.root / "receipt.json",
        )

        self.assertEqual("0.4.0", receipt["sourceVersion"])
        self.assertNotIn(MODULE.PROPS_PATH.as_posix(), receipt["changedFiles"])
        for relative_path in MODULE.GLOBAL_JSON_PATHS:
            document = json.loads((self.root / relative_path).read_text(encoding="utf-8"))
            self.assertEqual(
                "0.4.0-ci.123.1",
                document["msbuild-sdks"]["NetWasm.Sdk"],
            )

    def test_rejects_noncanonical_candidate(self) -> None:
        with self.assertRaisesRegex(ValueError, "Invalid NetWasm candidate version"):
            MODULE.project(self.root, "0.4", self.root / "receipt.json")


if __name__ == "__main__":
    unittest.main()
