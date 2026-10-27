#!/usr/bin/env python3

from __future__ import annotations

import importlib.util
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT_PATH = Path(__file__).parent / "project-release-version.py"
SPEC = importlib.util.spec_from_file_location("project_release_version", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ProjectReleaseVersionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        subprocess.run(["git", "init", "--quiet", str(self.root)], check=True)
        subprocess.run(["git", "-C", str(self.root), "config", "user.name", "Test"], check=True)
        subprocess.run(
            ["git", "-C", str(self.root), "config", "user.email", "test@example.invalid"],
            check=True,
        )
        (self.root / "eng").mkdir()
        (self.root / "packaging/NetWasm.TUnit.Templates/content/NetWasmTUnitTests").mkdir(parents=True)
        (self.root / "eng/NetWasm.ReleaseVersion.txt").write_text("0.1.0\n")
        (self.root / "packaging/Directory.Build.props").write_text(
            "<Project><PropertyGroup>"
            "<NetWasmTUnitPackageVersion>0.1.0</NetWasmTUnitPackageVersion>"
            "<NetWasmPackageVersion>0.2.0</NetWasmPackageVersion>"
            "<Protocol>M11.2.3</Protocol><Assembly>1.0.0.0</Assembly>"
            "</PropertyGroup></Project>\n"
        )
        (self.root / "packaging/NetWasm.TUnit.Templates/content/NetWasmTUnitTests/NetWasmTUnitTests.csproj").write_text(
            '<Project><ItemGroup><PackageReference Include="NetWasm.TUnit" '
            'Version="0.0.1" /></ItemGroup></Project>\n'
        )
        (self.root / "packaging/global.json").write_text('{"msbuild-sdks":{"NetWasm.Sdk":"0.2.0"}}\n')
        (self.root / "docs.txt").write_text("Historical release 0.1.0\n")
        subprocess.run(["git", "-C", str(self.root), "add", "."], check=True)
        subprocess.run(["git", "-C", str(self.root), "commit", "--quiet", "-m", "fixture"], check=True)

    def project(self, version: str) -> dict[str, object]:
        return MODULE.project_version(self.root, version, self.root.parent / "receipt.json")

    def test_projects_only_package_version_inputs(self) -> None:
        receipt = self.project("0.2.0-preview.1")

        self.assertEqual("0.2.0-preview.1\n", (self.root / "eng/NetWasm.ReleaseVersion.txt").read_text())
        props = (self.root / "packaging/Directory.Build.props").read_text()
        self.assertIn(
            "<NetWasmTUnitPackageVersion>0.2.0-preview.1</NetWasmTUnitPackageVersion>",
            props,
        )
        self.assertIn("<NetWasmPackageVersion>0.2.0</NetWasmPackageVersion>", props)
        template = self.root / "packaging/NetWasm.TUnit.Templates/content/NetWasmTUnitTests/NetWasmTUnitTests.csproj"
        self.assertIn('Version="0.2.0-preview.1"', template.read_text())
        self.assertIn('"NetWasm.Sdk":"0.2.0"', (self.root / "packaging/global.json").read_text())
        self.assertEqual("Historical release 0.1.0\n", (self.root / "docs.txt").read_text())
        self.assertEqual(3, receipt["replacementCount"])

    def test_preserves_larger_dotted_numeric_tokens(self) -> None:
        self.project("0.2.0")

        props = (self.root / "packaging/Directory.Build.props").read_text()
        self.assertIn("<Protocol>M11.2.3</Protocol>", props)
        self.assertIn("<Assembly>1.0.0.0</Assembly>", props)
        self.assertIn("<NetWasmPackageVersion>0.2.0</NetWasmPackageVersion>", props)

    def test_rejects_invalid_version(self) -> None:
        with self.assertRaisesRegex(ValueError, "Invalid release version"):
            self.project("not-a-version")

    def test_rejects_dirty_worktree_and_names_the_change(self) -> None:
        (self.root / "docs.txt").write_text("Edited\n")

        with self.assertRaisesRegex(ValueError, "clean Git worktree(.|\n)*M docs.txt"):
            self.project("0.2.0")

    def test_git_diagnostics_on_stderr_do_not_make_a_clean_worktree_dirty(self) -> None:
        # GIT_TRACE writes trace lines to stderr for every git command.
        with mock.patch.dict(os.environ, {"GIT_TRACE": "1"}):
            receipt = self.project("0.2.0")

        self.assertEqual(3, receipt["replacementCount"])
        self.assertRegex(str(receipt["repositoryCommit"]), "^[0-9a-f]{40}$")


if __name__ == "__main__":
    unittest.main()
